"""Synthetic fixtures only; no protocol execution or external services."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

MODULE = Path(__file__).with_name('offline.py')


class OfflineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        assert MODULE.exists(), 'Offline implementation is missing'
        spec = importlib.util.spec_from_file_location('g3_offline', MODULE)
        cls.api = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.api)

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / 'source.md').write_text('synthetic source', encoding='utf-8')
        self.manifest = {'formatVersion': 1, 'sources': {'source.md': self.hash('source.md')},
                         'decisions': {'D4': 'PENDING'}, 'exclusions': [], 'variants': [self.variant('K01/a')]}

    def hash(self, name):
        return hashlib.sha256((self.root / name).read_bytes()).hexdigest()

    def variant(self, name):
        return dict(id=name, protocol='K01', relatedK=['K01'], contracts=['C04'],
                    description='Synthetic fixture', preconditions=['synthetic actors'], dependencies=[],
                    executionRequirements=['D4'], oracle='Exactly one committed effect; observer verifies loser rollback',
                    evidenceTypes=['E-TRACE', 'E-DB'], planningState='PLANNED', blockers=[],
                    references=['source.md'])

    def invalid(self, obj, code):
        with self.assertRaisesRegex(self.api.ValidationError, code):
            self.api.validate_manifest(obj, self.root)

    def test_valid_and_deterministic_selection(self):
        self.manifest['variants'].append(self.variant('K01/b'))
        self.api.validate_manifest(self.manifest, self.root)
        a = self.api.select(self.manifest, ['K01/b', 'K01/a'])
        self.assertEqual(a, self.api.select(self.manifest, ['K01/a', 'K01/b']))
        self.assertFalse(a['executionAuthorized'])
        self.assertEqual(['K01/a'], self.api.select(self.manifest, ['K01/a'])['variants'])
        self.assertEqual(['D4'], a['pendingDecisions'])

    def test_unknown_and_duplicate_selection(self):
        for ids, code in [(['unknown'], 'UNKNOWN_ID'), (['K01/a', 'K01/a'], 'DUPLICATE_ID')]:
            with self.assertRaisesRegex(self.api.ValidationError, code):
                self.api.select(self.manifest, ids)

    def test_manifest_negatives(self):
        changes = [
            ('id', '', 'INVALID_MANIFEST'), ('protocol', 'Q99', 'CLASSIFICATION'),
            ('contracts', ['C99'], 'CLASSIFICATION'), ('oracle', '', 'ORACLE'),
            ('evidenceTypes', ['E-DB'], 'EVIDENCE_TYPES'), ('references', ['missing'], 'REFERENCE'),
            ('dependencies', ['missing'], 'DEPENDENCY'), ('planningState', 'PASS', 'CLASSIFICATION'),
            ('executionRequirements', ['D99'], 'DEPENDENCY')]
        for field, value, code in changes:
            with self.subTest(field=field):
                m = copy.deepcopy(self.manifest); m['variants'][0][field] = value
                self.invalid(m, code)

    def test_duplicate_cycle_and_missing_selected_dependency(self):
        m = copy.deepcopy(self.manifest); m['variants'] *= 2
        self.invalid(m, 'DUPLICATE_ID')
        m = copy.deepcopy(self.manifest); m['variants'].append(self.variant('K01/b'))
        m['variants'][0]['dependencies'] = ['K01/b']
        self.api.validate_manifest(m, self.root)
        with self.assertRaisesRegex(self.api.ValidationError, 'DEPENDENCY'):
            self.api.select(m, ['K01/a'])
        m['variants'][1]['dependencies'] = ['K01/a']; self.invalid(m, 'CYCLE')

    def test_blocked_and_v11_cannot_be_planned(self):
        v = self.manifest['variants'][0]; v['planningState'] = 'BLOCKED'; v['blockers'] = ['No approved probe']
        self.api.validate_manifest(self.manifest, self.root)
        with self.assertRaisesRegex(self.api.ValidationError, 'BLOCKED'):
            self.api.select(self.manifest, [v['id']])
        v['protocol'] = 'V11'; v['planningState'] = 'PLANNED'; v['blockers'] = []
        self.invalid(self.manifest, 'CLASSIFICATION')

    def test_strict_json_jsonl(self):
        for text, lines in [('{', False), ('{"a":1,"a":2}', False), ('{"a":NaN}', False), ('{}\n{', True)]:
            p = self.root / 'bad.json'; p.write_text(text, encoding='utf-8')
            with self.assertRaisesRegex(self.api.ValidationError, 'JSON'):
                self.api.read_json(p, lines=lines)
        p.write_text('{}\n{}\n', encoding='utf-8')
        self.assertEqual([{}, {}], self.api.read_json(p, lines=True))

    def bundle(self):
        (self.root / 'events.jsonl').write_text('{"sequence":1,"session":"s","transaction":"t","barrier":"B1","event":"commit"}\n', encoding='utf-8')
        (self.root / 'state.json').write_text('{"before":[],"after":[],"observer":"o"}', encoding='utf-8')
        b = dict(kind='fixture', runId='synthetic-run', variantId='K01/a', formatVersion=1,
                 sources=copy.deepcopy(self.manifest['sources']), environment={'synthetic': True},
                 configuration={'synthetic': True}, actors=[{'id':'a','mode':'fixture','sessionUser':'fixture','currentUser':'fixture'}],
                 sessions=[{'id':'s','actor':'a','backendPid':1}, {'id':'o','actor':'a','backendPid':2}],
                 transactions=[{'id':'t','session':'s','outcome':'COMMITTED'}],
                 diagnostics=[{'transaction':'t','sqlstate':'00000','message':'synthetic control','constraint':None}],
                 assertions=[{'id':'synthetic','passed':True,'evidence':['state.json','events.jsonl']}],
                 result='PASS', reason='Synthetic contract test only', cleanup={'confirmed':True,'details':'temporary fixture'},
                 evidence={'E-TRACE':['events.jsonl'],'E-DB':['state.json']},
                 hashes={p:self.hash(p) for p in ['events.jsonl','state.json']})
        return b

    def test_valid_fixture_never_accepted_as_execution(self):
        b = self.bundle()
        self.api.validate_evidence(b, self.manifest, self.root, allow_fixture=True)
        with self.assertRaisesRegex(self.api.ValidationError, 'FIXTURE'):
            self.api.validate_evidence(b, self.manifest, self.root)

    def test_relabelled_fixture_is_not_execution(self):
        b=self.bundle(); b['kind']='execution'
        for allow in [False,True]:
            with self.subTest(allow_fixture=allow):
                with self.assertRaisesRegex(self.api.ValidationError,'PROVENANCE'):
                    self.api.validate_evidence(b,self.manifest,self.root,allow_fixture=allow)

    def execution_contract_fixture(self):
        # Fabricated metadata for contract tests ONLY; never a real gate result.
        b=self.bundle(); b['kind']='execution'
        b['environment']={'synthetic':False,'id':'temporary-contract-environment'}
        b['configuration']={'synthetic':False}
        b['actors'][0]['mode']='runtime-login'
        b['provenance']={'origin':'observed-run','runId':b['runId'],
                         'variantId':b['variantId'],'environmentId':b['environment']['id'],
                         'runner':'synthetic contract-test metadata','runnerVersion':'test-only'}
        return b

    def test_execution_requires_approved_dependencies_without_mutation(self):
        b=self.execution_contract_fixture()
        self.manifest['decisions']['STAGE2']='PENDING'
        self.manifest['variants'][0]['executionRequirements'].append('STAGE2')
        before=copy.deepcopy(self.manifest)
        with self.assertRaisesRegex(self.api.ValidationError,'AUTHORIZATION'):
            self.api.validate_evidence(b,self.manifest,self.root)
        self.assertEqual(before,self.manifest)

    def test_execution_provenance_and_markers_must_be_coherent(self):
        self.manifest['decisions']['D4']='APPROVED'  # Temporary fixture only.
        original=self.execution_contract_fixture()
        changes=[('environment',{'synthetic':True,'id':'temporary-contract-environment'}),
                 ('environment',{'id':'temporary-contract-environment'}),
                 ('configuration',{'synthetic':True}),('provenance',{}),
                 ('provenance',dict(original['provenance'],runId='other-run')),
                 ('provenance',dict(original['provenance'],variantId='other-variant')),
                 ('provenance',dict(original['provenance'],environmentId='other-environment'))]
        for key,value in changes:
            with self.subTest(field=key,value=value):
                b=copy.deepcopy(original); b[key]=value
                with self.assertRaisesRegex(self.api.ValidationError,'PROVENANCE'):
                    self.api.validate_evidence(b,self.manifest,self.root)
        b=copy.deepcopy(original); b['actors'][0]['mode']='fixture'
        with self.assertRaisesRegex(self.api.ValidationError,'PROVENANCE'):
            self.api.validate_evidence(b,self.manifest,self.root)

    def test_structurally_coherent_metadata_does_not_approve_gate(self):
        self.manifest['decisions']['D4']='APPROVED'  # Temporary fixture only.
        b=self.execution_contract_fixture(); before=copy.deepcopy(self.manifest)
        self.assertTrue(self.api.validate_evidence(b,self.manifest,self.root))
        self.assertEqual(before,self.manifest)

    def test_evidence_negatives(self):
        original = self.bundle()
        cases = [('result','UNKNOWN','RESULT'), ('assertions',[],'INCOMPLETE'),
                 ('hashes',{},'HASH'), ('sources',{},'SOURCE'), ('cleanup',{},'INCOMPLETE'),
                 ('transactions',[],'INCOMPLETE'), ('sessions',[],'INCOMPLETE')]
        for key, value, code in cases:
            with self.subTest(key=key):
                b=copy.deepcopy(original); b[key]=value
                with self.assertRaisesRegex(self.api.ValidationError, code):
                    self.api.validate_evidence(b,self.manifest,self.root,allow_fixture=True)
        b=copy.deepcopy(original); b['assertions'][0]['passed']=False
        with self.assertRaisesRegex(self.api.ValidationError,'RESULT'):
            self.api.validate_evidence(b,self.manifest,self.root,allow_fixture=True)

    def test_event_order_observer_and_hash_inventory(self):
        b=self.bundle()
        path=self.root/'events.jsonl'
        path.write_text('{"sequence":2,"session":"s","transaction":"t","barrier":"B1","event":"commit"}\n',encoding='utf-8')
        b['hashes']['events.jsonl']=self.hash('events.jsonl')
        with self.assertRaisesRegex(self.api.ValidationError,'INCOMPLETE'):
            self.api.validate_evidence(b,self.manifest,self.root,allow_fixture=True)
        b=self.bundle(); path=self.root/'state.json'
        path.write_text('{"before":[],"after":[],"observer":"s"}',encoding='utf-8')
        b['hashes']['state.json']=self.hash('state.json')
        with self.assertRaisesRegex(self.api.ValidationError,'INCOMPLETE'):
            self.api.validate_evidence(b,self.manifest,self.root,allow_fixture=True)
        b=self.bundle(); b['hashes']['unlisted.json']='0'*64
        with self.assertRaisesRegex(self.api.ValidationError,'HASH'):
            self.api.validate_evidence(b,self.manifest,self.root,allow_fixture=True)

    def test_unknown_commit_and_blocked_result(self):
        b=self.bundle(); b['transactions'][0]['outcome']='UNKNOWN'
        with self.assertRaisesRegex(self.api.ValidationError,'RESULT'):
            self.api.validate_evidence(b,self.manifest,self.root,allow_fixture=True)
        b=self.bundle(); self.manifest['variants'][0].update(planningState='BLOCKED',blockers=['pending'])
        with self.assertRaisesRegex(self.api.ValidationError,'RESULT'):
            self.api.validate_evidence(b,self.manifest,self.root,allow_fixture=True)

    def test_cli_codes_and_read_only_commands(self):
        import contextlib
        import io
        p=self.root/'manifest.json'; p.write_text(json.dumps(self.manifest),encoding='utf-8')
        before={x.name:x.read_bytes() for x in self.root.iterdir()}
        prefix=['--root',str(self.root),'--manifest',str(p)]
        with contextlib.redirect_stdout(io.StringIO()),contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(0,self.api.main(prefix+['validate']))
            self.assertEqual(0,self.api.main(prefix+['list']))
            self.assertEqual(0,self.api.main(prefix+['select','K01/a']))
            self.assertEqual(1,self.api.main(prefix+['select','unknown']))
        self.assertEqual(before,{x.name:x.read_bytes() for x in self.root.iterdir()})

    def test_paths_hashes_missing_files_and_history_unchanged(self):
        b=self.bundle(); before={p.name:p.read_bytes() for p in self.root.iterdir()}
        for path in ['../outside','C:\\secret','/absolute','a/../../b']:
            with self.assertRaisesRegex(self.api.ValidationError,'PATH'):
                self.api.safe_path(self.root,path)
        b['hashes']['state.json']='0'*64
        with self.assertRaisesRegex(self.api.ValidationError,'HASH'):
            self.api.validate_evidence(b,self.manifest,self.root,allow_fixture=True)
        self.assertEqual(before,{p.name:p.read_bytes() for p in self.root.iterdir()})
        (self.root/'state.json').unlink()
        with self.assertRaisesRegex(self.api.ValidationError,'REFERENCE'):
            self.api.validate_evidence(self.bundle_without_recreate(b),self.manifest,self.root,allow_fixture=True)

    def bundle_without_recreate(self,b):
        b['hashes']['state.json']=hashlib.sha256(b'{"before":[],"after":[],"observer":"o"}').hexdigest()
        return b


if __name__ == '__main__':
    unittest.main()
