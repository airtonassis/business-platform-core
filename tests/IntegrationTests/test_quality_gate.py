"""Only tooling under test: no product suite, SQL, SSH, IdP or external service."""
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('quality_gate', ROOT / 'scripts/quality_gate.py')
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class QualityGateTests(unittest.TestCase):
    def evidence(self, root):
        leaf = root / 'proof.json'; leaf.write_text('{"passed":true}', encoding='utf-8')
        summary = root / 'summary.json'
        summary.write_text(json.dumps({'image': 'synthetic', 'evidence': [{'name': leaf.name, 'sha256': gate.digest(leaf)}]}), encoding='utf-8')
        return summary, gate.digest(summary)

    def test_integrity_reuses_history_without_claiming_execution(self):
        with tempfile.TemporaryDirectory() as d:
            summary, anchor = self.evidence(Path(d))
            result = gate.verify(summary, anchor)
            self.assertEqual('REUSED_VALID_EVIDENCE', result['status'])
            self.assertNotIn('PASS_EXECUTED', result.values())

    def test_tampering_and_missing_evidence_are_distinct(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d); summary, anchor = self.evidence(root)
            (root/'proof.json').write_text('changed', encoding='utf-8')
            self.assertEqual('FAIL', gate.verify(summary, anchor)['status'])
            (root/'proof.json').unlink()
            self.assertEqual('BLOCKED', gate.verify(summary, anchor)['status'])

    def test_anchor_is_not_self_asserted(self):
        with tempfile.TemporaryDirectory() as d:
            summary, _ = self.evidence(Path(d))
            self.assertEqual('SummaryHashMismatch', gate.verify(summary, '0'*64)['reason'])

    def test_evidence_cannot_escape_directory(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'summary.json'
            p.write_text(json.dumps({'evidence':[{'name':'../secret','sha256':'0'*64}]}),encoding='utf-8')
            self.assertEqual('FAIL', gate.verify(p,gate.digest(p))['status'])

    def test_untracked_inputs_and_deletions_are_visible_but_secrets_not_read(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); subprocess.run(['git','init','--quiet'],cwd=root,check=True)
            (root/'src').mkdir(); source=root/'src/Real.cs';source.write_text('class Real {}')
            (root/'src/.env').write_text('DO_NOT_READ=synthetic')
            before=gate.inputs(root)
            self.assertIn('src/Real.cs',before);self.assertNotIn('src/.env',before)
            source.unlink();self.assertNotIn('src/Real.cs',gate.inputs(root))

    def test_tooling_selection_does_not_rerun_historical_dotnet_suite(self):
        result=gate.select(['scripts/quality_gate.py','.agents/skills/quality-gates/SKILL.md'])
        self.assertEqual(1,len(result['commands']))
        self.assertIn('test_quality_gate.py',result['commands'][0]['argv'])
        self.assertTrue(all(c['status']=='NOT_RUN' for c in result['commands']))

    def test_security_changes_and_acceptance_require_full_product_regression(self):
        for changed, acceptance in [(['src/Business.Platform.Core.Api/Authentication/Handler.cs'],False),([],True)]:
            result=gate.select(changed,acceptance)
            self.assertIn('Business.Platform.Core.sln',result['commands'][0]['argv'])
            self.assertNotIn('--filter',result['commands'][0]['argv'])
            self.assertTrue(result['manualChecks'])

    def test_platform_slice_keeps_http_mfa_and_architecture_coverage(self):
        result=gate.select(['src/Business.Platform.Core.Application/Platform/PlatformOperations.cs'])
        self.assertIn('FullyQualifiedName~Platform|FullyQualifiedName~Architecture',result['commands'][0]['argv'])
        self.assertTrue(result['manualChecks'])

    def test_outputs_never_overwrite_evidence(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'evidence.json';gate.write(p,{'status':'PASS_EXECUTED'})
            with self.assertRaises(FileExistsError):gate.write(p,{'status':'FAIL'})

    def test_g3_only_change_does_not_schedule_product_or_real_protocols(self):
        result=gate.select(['tests/TechnicalValidation/ADR0009.G3/stage2/Core/Protocol.cs'])
        self.assertEqual(2,len(result['commands']))
        self.assertFalse(any('Business.Platform.Core.sln' in c['argv'] for c in result['commands']))
        self.assertFalse(result['manualChecks'])


if __name__=='__main__':unittest.main()
