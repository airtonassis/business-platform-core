"""Offline verification; does not start a database or change approved G1 evidence."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT=Path(__file__).resolve().parent
G1=ROOT.parent/'ADR0009.G1'
RUN=sys.argv[1] if len(sys.argv)>1 else 'f31a6617ed85'
assert re.fullmatch(r'[a-f0-9]{12}',RUN)
folder=ROOT/'evidence/runs'/RUN
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()

lock=read(ROOT.parent/'ADR0009.G1.CI/originals.sha256.json')['files']
assert len(lock)==272
assert all(digest(G1/p)==h.lower() for p,h in lock.items()),'G1 byte integrity violation'
selection=read(folder/'selection.json')
result=read(folder/'results.json')
expected={(x['id'],x['variant']) for x in selection['selected']}
actual={(x['caseId'],x['variant']):x for x in result['subproofs']}
assert set(actual)==expected and len(expected) in {30,31}
assert all(x['status']=='PASS' for x in actual.values())
pool=ROOT/'evidence/runs/6909108391b4' if len(expected)==30 else None
combined=set(actual)
if pool:
    pool_result=read(pool/'results.json')
    assert len(pool_result['subproofs'])==1 and pool_result['subproofs'][0]['status']=='PASS'
    assert pool_result['subproofs'][0]['variant']=='G2-session-baseline-reset'
    combined.add(('Q12','G2-session-baseline-reset'))
assert combined=={(x['id'],x['variant']) for x in read(ROOT/'selection.json')['selected']}
assert not result['stoppedForP0'] and result['schemaInstalled']
assert all(not i.startswith('K') and i not in {'V09','V11','V12','V13','V14'} for i,v in actual)
if not (folder/'security.comparison.json').exists():
    comparator=G1/'harness/compare_observed.py'
    source=comparator.read_text(encoding='utf-8-sig').replace('package = Path(__file__).resolve().parent.parent','package = Path('+repr(str(ROOT))+')')
    arguments=sys.argv[:]
    try:
        sys.argv=[str(comparator),RUN]
        exec(compile(source,str(comparator),'exec'),{'__file__':str(comparator),'__name__':'__main__'})
    finally:sys.argv=arguments
comparison=read(folder/'security.comparison.json')
assert sum(x['effectiveColumnCount'] for x in comparison['ownerGroups'])==178
assert all(x['effectiveTableCount']==0 and x['exactGrantComparison']=='MATCH_TEST_ALLOWLIST_NOT_HUMAN_APPROVAL' for x in comparison['ownerGroups'])
for key in ['publicExecute','unexpectedRuntimeExecuteAgainstImplementedPorts','unsafeRoleAttributes','memberships','rlsOrOwnershipViolations','missingExpectedTables','functionModeMismatches']:
    assert not comparison[key],(key,comparison[key])
assert all(not value for x in comparison['missingOrExcessExactGrants'] for key,value in x.items() if key!='owner')
assert all(not x['mismatches'] for x in comparison['ownerAttributeMismatches'])
approved=G1/'evidence/runs/ece79aa930df'
normalize=lambda p:sorted((x['signature'],x['owner'],x['definition']) for x in read(p/'graph.json'))
assert normalize(folder)==normalize(approved),'Function body/owner drift from approved G1'
if pool:assert normalize(pool)==normalize(approved),'Pool run function body/owner drift'
tables=lambda p:sorted(x['name'] for x in (json.loads(s) for s in (p/'catalog.jsonl').read_text().splitlines()) if x['kind']=='table')
assert tables(folder)==tables(approved)
for kind in ['policy','trigger','constraint','index']:
    select=lambda p:[x for x in map(json.loads,(p/'catalog.jsonl').read_text().splitlines()) if x['kind']==kind]
    assert select(folder)==select(approved),kind+' drift from approved G1'
assert comparison['additionalObservedTables']==read(approved/'security.comparison.json')['additionalObservedTables']==['identity.identity_links']
assert digest(folder/'preflight.expected.json')==digest(approved/'preflight.expected.json')
assert read(folder/'cleanup.json')['remaining']==''
for run in (ROOT/'evidence/runs').iterdir():
    c=read(run/'cleanup.json');assert c['cleanup']=='REMOVED_RUN_CONTAINER_AND_TMPFS' and c['networkRemoved'] and c['remaining']==''
remaining=subprocess.check_output(['docker','ps','-a','--filter','label=bpc.adr0009.run','--format','{{.ID}}']).decode().strip()
networks=subprocess.check_output(['docker','network','ls','--filter','label=bpc.adr0009.run','--format','{{.ID}}']).decode().strip()
assert not remaining and not networks,'Run resources remain'
before=read(ROOT/'preflight.json')['dockerResourcesBefore']
after=subprocess.check_output(['docker','ps','-a','--format','{{.ID}} {{.Names}}']).decode().splitlines()
assert set(before)==set(after),'Preexisting container inventory changed'
bad=[]
for path in ROOT.rglob('*'):
    if not path.is_file() or any(x in path.parts for x in ('bin','obj','__pycache__')):continue
    if path.suffix.lower() in {'.json','.jsonl'}:
        if path.suffix=='.json':read(path)
        else:
            for line in path.read_text(encoding='utf-8-sig').splitlines():json.loads(line)
    text=path.read_text(encoding='utf-8-sig')
    if re.search(r'-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----|(?:gh[pousr]_[A-Za-z0-9]{30,})|AKIA[0-9A-Z]{16}',text):bad.append(str(path))
assert not bad,bad
manifest=ROOT/'evidence.sha256.json'
if manifest.exists():
    assert all(digest(ROOT/p)==h for p,h in read(manifest)['files'].items()),'G2 evidence/source hash mismatch'
print(json.dumps({'runs':[RUN]+([pool.name] if pool else []),'selectedPass':31,'failed':0,'g1FilesIntact':272,'columnPrivileges':178,'functionDefinitionsMatchApprovedG1':11,'tableSetMatchesApprovedG1':12,'knownManifestOmission':'identity.identity_links already present in approved G1; no new drift','cleanup':'all run containers and networks absent; preexisting inventory unchanged','jsonAndSecretChecks':'PASS; pattern scan is bounded, not a proof of absence of every possible secret'}))
