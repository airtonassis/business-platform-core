"""Read-only G3 planning and evidence-contract validation. Standard library only."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sys


class ValidationError(ValueError):
    pass


def require(condition, code, detail):
    if not condition:
        raise ValidationError(f'{code}: {detail}')


def safe_path(root, raw):
    require(isinstance(raw, str) and raw, 'PATH', 'empty or non-string path')
    p = PurePosixPath(raw.replace('\\', '/'))
    require(not p.is_absolute() and ':' not in raw and '..' not in p.parts,
            'PATH', f'unsafe path {raw!r}')
    target = (root / str(p)).resolve()
    require(target.is_relative_to(root.resolve()), 'PATH', 'path escapes root')
    return target


def read_json(path, lines=False):
    def pairs(items):
        result = {}
        for k, v in items:
            require(k not in result, 'JSON', f'duplicate key {k}')
            result[k] = v
        return result
    def parse(text):
        return json.loads(text, object_pairs_hook=pairs,
                          parse_constant=lambda value: require(False, 'JSON', 'non-finite number'))
    try:
        text = path.read_text(encoding='utf-8-sig')
        return [parse(line) for line in text.splitlines()] if lines else parse(text)
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ValidationError(f'JSON: {path.name}: {type(exc).__name__}') from exc


def check_hashes(entries, root):
    require(isinstance(entries, dict) and entries, 'HASH', 'empty hash manifest')
    resolved = set()
    for name, expected in sorted(entries.items()):
        p = safe_path(root, name)
        require(p not in resolved, 'HASH', 'aliased duplicate path')
        resolved.add(p)
        require(p.is_file(), 'REFERENCE', f'missing file {name}')
        require(isinstance(expected, str) and re.fullmatch('[0-9a-f]{64}', expected), 'HASH', f'invalid digest {name}')
        require(hashlib.sha256(p.read_bytes()).hexdigest() == expected, 'HASH', f'mismatch {name}')


PROTOCOLS = {'Q12', *[f'V{i:02}' for i in range(8, 15)], *[f'K{i:02}' for i in range(1, 11)]}
EVIDENCE_TYPES = {'E-TRACE', 'E-DB', 'E-LOCK', 'E-POOL', 'E-ACL', 'E-REVIEW'}


def validate_manifest(m, root):
    require(isinstance(m, dict) and m.get('formatVersion') == 1, 'INVALID_MANIFEST', 'formatVersion must be 1')
    check_hashes(m.get('sources'), root)
    require(isinstance(m.get('decisions'), dict), 'INVALID_MANIFEST', 'decisions required')
    require(all(s in {'PENDING', 'APPROVED'} for s in m['decisions'].values()), 'CLASSIFICATION', 'decision status')
    variants = m.get('variants')
    require(isinstance(variants, list) and variants, 'INVALID_MANIFEST', 'variants required')
    by_id = {}
    for v in variants:
        require(isinstance(v, dict), 'INVALID_MANIFEST', 'variant object required')
        ident = v.get('id')
        require(isinstance(ident, str) and re.fullmatch(r'[A-Z][0-9]{2}/[a-z0-9-]+', ident), 'INVALID_MANIFEST', 'variant ID')
        require(ident not in by_id, 'DUPLICATE_ID', ident)
        by_id[ident] = v
        require(v.get('protocol') in PROTOCOLS and ident.split('/')[0] == v['protocol'], 'CLASSIFICATION', ident)
        for field in ['relatedK', 'contracts', 'dependencies', 'executionRequirements', 'references', 'blockers', 'preconditions']:
            require(isinstance(v.get(field), list) and all(isinstance(x, str) and x for x in v[field]), 'INVALID_MANIFEST', f'{ident}: {field}')
            require(len(v[field]) == len(set(v[field])), 'DUPLICATE_ID', f'{ident}: {field}')
        require(v['relatedK'] and set(v['relatedK']) <= {f'K{i:02}' for i in range(1,11)}, 'CLASSIFICATION', ident)
        require(v['contracts'] and set(v['contracts']) <= {f'C{i:02}' for i in range(1,11)}, 'CLASSIFICATION', ident)
        require(v.get('planningState') in {'PLANNED', 'BLOCKED'}, 'CLASSIFICATION', ident)
        require(bool(v['blockers']) == (v['planningState'] == 'BLOCKED'), 'CLASSIFICATION', f'{ident}: blockers/state')
        require(v['protocol'] != 'V11' or v['planningState'] == 'BLOCKED', 'CLASSIFICATION', 'V11 has no approved reachable probe')
        require(isinstance(v.get('oracle'), str) and v['oracle'].strip(), 'ORACLE', ident)
        require(isinstance(v.get('description'), str) and v['description'].strip() and v['preconditions'], 'INVALID_MANIFEST', ident)
        types = v.get('evidenceTypes')
        require(isinstance(types,list) and {'E-TRACE','E-DB'} <= set(types) <= EVIDENCE_TYPES and len(types)==len(set(types)), 'EVIDENCE_TYPES', ident)
        require(v['references'] and set(v['references']) <= set(m['sources']), 'REFERENCE', ident)
        require(set(v['executionRequirements']) <= set(m['decisions']), 'DEPENDENCY', ident)
    visited, active = set(), set()
    def visit(ident):
        require(ident in by_id, 'DEPENDENCY', ident)
        require(ident not in active, 'CYCLE', ident)
        if ident in visited:
            return
        active.add(ident)
        for dep in sorted(by_id[ident]['dependencies']):
            visit(dep)
        active.remove(ident); visited.add(ident)
    for ident in sorted(by_id):
        visit(ident)
    require(isinstance(m.get('exclusions'),list), 'INVALID_MANIFEST', 'exclusions required')
    for item in m['exclusions']:
        require(isinstance(item,dict) and item.get('gate')=='G4' and item.get('reason') and item.get('protocol') in PROTOCOLS,
                'CLASSIFICATION','G4 exclusion')
    return by_id


def select(m, ids):
    require(ids, 'UNKNOWN_ID', 'explicit selection required')
    require(len(ids)==len(set(ids)), 'DUPLICATE_ID', 'selection')
    by_id={v['id']:v for v in m['variants']}
    for ident in sorted(ids):
        require(ident in by_id, 'UNKNOWN_ID', ident)
        v=by_id[ident]
        require(v['planningState'] != 'BLOCKED', 'BLOCKED', ident)
        require(set(v['dependencies']) <= set(ids), 'DEPENDENCY', f'{ident}: select dependencies explicitly')
    return {'kind':'selection-plan','executionAuthorized':False,'variants':sorted(ids),
            'pendingDecisions':sorted({d for ident in ids for d in by_id[ident]['executionRequirements'] if m['decisions'][d]=='PENDING'})}


def validate_evidence(b, m, root, allow_fixture=False):
    require(isinstance(b,dict), 'INCOMPLETE', 'bundle object required')
    require(b.get('kind')=='execution' or (allow_fixture and b.get('kind')=='fixture'), 'FIXTURE', 'not an execution record')
    require(b.get('formatVersion')==1 and isinstance(b.get('runId'),str) and b['runId'], 'INCOMPLETE', 'identity/version')
    by_id={v['id']:v for v in m['variants']}
    require(b.get('variantId') in by_id, 'UNKNOWN_ID', 'evidence variant')
    v=by_id[b['variantId']]
    require(b.get('result') in {'PASS','FAIL','BLOCKED'}, 'RESULT', 'unknown result')
    require(v['planningState']!='BLOCKED' or b['result']=='BLOCKED', 'RESULT', 'blocked variant cannot have executed outcome')
    require(b.get('sources')==m['sources'], 'SOURCE', 'source hashes must match validated manifest')
    for key in ['environment','configuration','actors','sessions','transactions','diagnostics','assertions','cleanup','reason','evidence']:
        require(b.get(key), 'INCOMPLETE', key)
    for key in ['actors','sessions','transactions','diagnostics','assertions']:
        require(isinstance(b[key],list), 'INCOMPLETE', key)
    require(isinstance(b['environment'],dict) and isinstance(b['configuration'],dict), 'INCOMPLETE','environment/configuration')
    require(isinstance(b['cleanup'],dict) and isinstance(b['cleanup'].get('confirmed'),bool) and b['cleanup'].get('details'), 'INCOMPLETE','cleanup')
    def index(items, fields, name):
        result={}
        for item in items:
            require(isinstance(item,dict) and all(item.get(f) is not None for f in fields), 'INCOMPLETE',name)
            require(item['id'] not in result,'DUPLICATE_ID',name)
            result[item['id']]=item
        return result
    actors=index(b['actors'],['id','mode','sessionUser','currentUser'],'actors')
    require(all(a['mode'] in {'fixture','set-role','security-definer','runtime-login'} for a in actors.values()),'CLASSIFICATION','actor mode')
    if b['kind']=='fixture':
        require(b['environment'].get('synthetic') is True,
                'PROVENANCE', 'contract fixtures must explicitly identify a synthetic environment')
    else:
        # Metadata consistency is not independent attestation that a run occurred.
        require(b['environment'].get('synthetic') is False
                and b['configuration'].get('synthetic') is False
                and all(a['mode']!='fixture' for a in actors.values()),
                'PROVENANCE', 'execution cannot contain synthetic environment/configuration or fixture actors')
        provenance=b.get('provenance')
        require(isinstance(provenance,dict)
                and provenance.get('origin')=='observed-run'
                and provenance.get('runId')==b['runId']
                and provenance.get('variantId')==b['variantId']
                and isinstance(b['environment'].get('id'),str) and b['environment']['id'].strip()
                and provenance.get('environmentId')==b['environment']['id']
                and all(isinstance(provenance.get(k),str) and provenance[k].strip()
                        for k in ['runner','runnerVersion']),
                'PROVENANCE', 'execution requires matching run/variant/environment and runner provenance')
        require(all(m['decisions'].get(d)=='APPROVED' for d in v['executionRequirements']),
                'AUTHORIZATION', 'execution prerequisites are not approved in the supplied manifest')
    sessions=index(b['sessions'],['id','actor','backendPid'],'sessions')
    require(all(s['actor'] in actors and type(s['backendPid']) is int and s['backendPid']>0 for s in sessions.values()),'REFERENCE','session actor/PID')
    txs=index(b['transactions'],['id','session','outcome'],'transactions')
    require(all(t['session'] in sessions and t['outcome'] in {'COMMITTED','ROLLED_BACK','UNKNOWN','NOT_STARTED'} for t in txs.values()),'RESULT','transaction outcome')
    for d in b['diagnostics']:
        require(isinstance(d,dict) and d.get('transaction') in txs and re.fullmatch('[0-9A-Z]{5}',str(d.get('sqlstate',''))) and d.get('message') and 'constraint' in d,'INCOMPLETE','diagnostic')
    evidence=b['evidence']; hashes=b.get('hashes')
    require(isinstance(evidence,dict) and set(v['evidenceTypes']) <= set(evidence) <= EVIDENCE_TYPES,'EVIDENCE_TYPES','missing type')
    require(all(isinstance(paths,list) and paths and all(isinstance(p,str) for p in paths) for paths in evidence.values()),'INCOMPLETE','evidence paths')
    paths={p for group in evidence.values() for p in group}
    require(isinstance(hashes,dict) and set(hashes)==paths,'HASH','exact complete evidence inventory required')
    check_hashes(hashes,root)
    for a in b['assertions']:
        require(isinstance(a,dict) and a.get('id') and type(a.get('passed')) is bool and a.get('evidence') and set(a['evidence']) <= paths,'INCOMPLETE','assertion evidence')
    if b['result']=='PASS':
        require(all(a['passed'] for a in b['assertions']) and b['cleanup']['confirmed'] and all(t['outcome'] not in {'UNKNOWN','NOT_STARTED'} for t in txs.values()),'RESULT','PASS contradicts assertions/cleanup/outcomes')
    if b['result']=='FAIL':
        require(any(not a['passed'] for a in b['assertions']),'RESULT','FAIL requires failed assertion')
    events=[]; states=[]
    for p in sorted(paths):
        path=safe_path(root,p)
        data=read_json(path,lines=path.suffix=='.jsonl')
        if p in evidence['E-TRACE']:
            require(isinstance(data,list) and data,'INCOMPLETE','ordered JSONL trace required')
            events.extend(data)
        if p in evidence['E-DB']:
            require(isinstance(data,dict) and 'before' in data and 'after' in data and data.get('observer') in sessions,'INCOMPLETE','state before/after/observer')
            states.append(data)
    require([e.get('sequence') for e in events]==list(range(1,len(events)+1)),'INCOMPLETE','ordered event sequence')
    for e in events:
        require(e.get('session') in sessions and e.get('transaction') in txs and txs[e['transaction']]['session']==e['session'] and e.get('barrier') and e.get('event'),'REFERENCE','event session/transaction/barrier')
    writer_sessions={t['session'] for t in txs.values()}
    require(all(s['observer'] not in writer_sessions for s in states),'INCOMPLETE','independent observer session required')
    return True


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[3])
    parser.add_argument('--manifest',type=Path,default=Path(__file__).with_name('variants.json'))
    sub=parser.add_subparsers(dest='command',required=True)
    sub.add_parser('validate'); sub.add_parser('list')
    choose=sub.add_parser('select'); choose.add_argument('ids',nargs='+')
    evidence=sub.add_parser('evidence'); evidence.add_argument('bundle',type=Path)
    try:
        args=parser.parse_args(argv)
        m=read_json(args.manifest); validate_manifest(m,args.root)
        if args.command=='select':
            result=select(m,args.ids)
        elif args.command=='list':
            result=[{'id':v['id'],'planningState':v['planningState'],'blockers':v['blockers']} for v in sorted(m['variants'],key=lambda x:x['id'])]
        elif args.command=='evidence':
            validate_evidence(read_json(args.bundle),m,args.bundle.parent)
            result={'contractValid':True,'gateApproved':False}
        else:
            result={'manifestValid':True,'variantCount':len(m['variants']),'executionAuthorized':False}
        print(json.dumps(result,ensure_ascii=True,sort_keys=True,indent=2)); return 0
    except ValidationError as exc:
        print(str(exc),file=sys.stderr); return 1
    except (KeyError,TypeError,AttributeError,ValueError) as exc:
        print(f'INVALID_STRUCTURE: {type(exc).__name__}',file=sys.stderr); return 1


if __name__=='__main__':
    sys.exit(main())
