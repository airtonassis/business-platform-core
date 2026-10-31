"""Derive only the runner in ignored obj; G1 sources/evidence are read-only inputs."""
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parent
G1 = ROOT.parent / 'ADR0009.G1'
CALLS = ('IntegrityProofs', 'SecurityProofs', 'AdditionalProofs', 'RemainingSqlProofs',
         'AuditCorrectionProofs', 'P1ClosureProofs', 'G1ClosingProofs', 'G2Proofs')
IDS = {'Q07','Q08','Q11','Q12','V01','V02','V03','V04','V05','V06','V07','V08',
       'F01','F02','F03','F04','F05','F06','F09','H01'}

def generate():
    source = (G1/'runtime/Program.cs').read_text(encoding='utf-8-sig')
    all_proofs = re.findall(r'await Proof\("([A-Z]\d+)","([^"]+)"', source)
    # Static Owner negative originally traced under K09; no concurrency is invoked.
    source = source.replace('Proof("K09","two-active-Owners-direct-owner-revocation-denied"',
                            'Proof("V07","two-active-Owners-direct-owner-revocation-denied"')
    allowed = {(i,v) for i,v in all_proofs if i in IDS and not v.startswith('G1-')}
    allowed |= {('V07','two-active-Owners-direct-owner-revocation-denied'),
                ('V06','G1-event-coverage-multiple-updates-and-immediate-check'),
                ('F05','G1-installed-T-SELF-REC-BOOT-boundaries')}
    extension = (ROOT/'runtime/G2Proofs.cs').read_text(encoding='utf-8')
    allowed |= set(re.findall(r'await Proof\("([A-Z]\d+)","([^"]+)"', extension))
    assert all(i in IDS for i,v in allowed)
    source = source.replace('args.Length != 1 || args[0] != "--execute-disposable" && args[0] != "--execute-g1-closing"',
                            'args.Length is <1 or >2 || args[0] != "--execute-g2" || (args.Length==2 && args[1]!="--remaining" && args[1]!="--pool-baseline-only")')
    source = source.replace('new ValidationRun(args[0]=="--execute-g1-closing")', 'new ValidationRun(false,args.Length==2,args.Length==2 && args[1]=="--pool-baseline-only")')
    source = source.replace('sealed class ValidationRun(bool closing=false)', 'sealed partial class ValidationRun(bool closing=false,bool remaining=false,bool poolOnly=false)')
    source = source.replace('readonly string package = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));',
        'readonly string package = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../ADR0009.G1"));')
    source = source.replace('Path.Combine(package, "evidence", "runs", run)', 'Path.Combine(package, "../ADR0009.G2/evidence/runs", run)')
    source = source.replace('bool created, schemaInstalled, stoppedForP0;', 'bool created, schemaInstalled, stoppedForP0, networkCreated;')
    source = source.replace('var binding=container.', 'stage="inspect-disposable-port"; await Write("network-observed.json",container.GetProperty("NetworkSettings").Clone());\n            var binding=container.')
    source = source.replace('exceptionType=ex.GetType().Name,sqlstate=', 'exceptionType=ex.GetType().Name,stack=ex.StackTrace,sqlstate=')
    source = source.replace('await Docker(["run","--detach"', 'await Docker(["network","create","--label","bpc.adr0009.run="+run,name+"-net"]); networkCreated=true;\n            await Docker(["run","--network",name+"-net","--detach"')
    source = source.replace('port=int.Parse(binding.', '''Require(container.GetProperty("NetworkSettings").GetProperty("Networks").EnumerateObject().Single().Name==name+"-net","Dedicated network only.");
            var net=JsonDocument.Parse(await Docker(["network","inspect",name+"-net"])).RootElement[0];
            Require(net.GetProperty("Containers").EnumerateObject().Count()==1 && net.GetProperty("Labels").GetProperty("bpc.adr0009.run").GetString()==run,"Dedicated run-owned network with exactly one container.");
            await Write("isolation-verified.json",new {run,name,database,image=Image,network=name+"-net",internalNetwork=net.GetProperty("Internal").GetBoolean(),mounts=container.GetProperty("Mounts").Clone(),binding=binding.Clone(),externalTarget=false,credentials="ephemeral random memory only"});
            port=int.Parse(binding.''')
    source = source.replace('NpgsqlConnection.ClearAllPools();', 'await File.WriteAllTextAsync(Path.Combine(output,"postgres.log"),await Docker(["logs",name]));\n                    NpgsqlConnection.ClearAllPools();')
    source = source.replace('// Preserve NOT_EXECUTED:', '''if(networkCreated){
                var networkLabel=(await Docker(["network","inspect",name+"-net","--format","{{index .Labels \\"bpc.adr0009.run\\"}}"])) .Trim();
                Require(networkLabel==run,"Network cleanup label");
                await Docker(["network","rm",name+"-net"]);
            }
            await Write("cleanup.json",new{run,cleanup,networkRemoved=networkCreated,remaining=(await Docker(["ps","-a","--filter","label=bpc.adr0009.run="+run,"--format","{{.ID}}"])).Trim()});
            // Preserve NOT_EXECUTED:''')
    source = source.replace('Directory.CreateDirectory(output);', '''Directory.CreateDirectory(output);
        await Write("selection.json", G2Selected());
        await Write("g2-source-hashes.json",new[]{"prepare_g2.py","runtime/G2Proofs.cs","runtime/G2.Validation.csproj","runtime/obj/G2.Program.g.cs","selection.json"}.ToDictionary(x=>x,x=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(package,"../ADR0009.G2",x))))));
        await Write("g1-before.json", Directory.GetFiles(package,"*",SearchOption.AllDirectories).Where(x=>!x.Contains("/bin/")&&!x.Contains("/obj/")&&!x.Contains("\\\\bin\\\\")&&!x.Contains("\\\\obj\\\\")).ToDictionary(x=>Path.GetRelativePath(package,x),x=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x)))));''')
    source = source.replace('if(closing){await G1ClosingProofs();}', 'if(closing){throw new SafetyFailure("G1 mode forbidden in G2");}')
    source = source.replace('            await ConcurrencyProofs();\n', '')
    source = source.replace('            await P1ClosureProofs();', '            await P1ClosureProofs();\n            await G1ClosingProofs();\n            await G2Proofs();')
    old = next(line for line in source.splitlines() if 'if(!closing && !new[]' in line)
    checks = ' || '.join('(caseId=='+json.dumps(i)+' && variant=='+json.dumps(v)+')' for i,v in sorted(allowed))
    source = source.replace(old, '        if(remaining && !RemainingVariant(variant))return;\n        if(!('+checks+'))return;')
    source = source.replace('await Write("results.json",new', 'await Write("coverage.json",new {gate="G2_NOT_HUMAN_APPROVED",selected=G2Selected(),executed=proofs,cleanup,stoppedForP0});\n            await Write("results.json",new')
    source = '#nullable enable\n' + source
    source = source.replace('await action();proofs.Add', 'await G2Snapshot("before"); await action(); await G2Snapshot("after");proofs.Add')
    source = source.replace('static async Task Context(', 'async Task Context(')
    source = source.replace('("i",identity.ToString()),("a",account?.ToString()??""),("o",org?.ToString()??""));',
        '("i",identity.ToString()),("a",account?.ToString()??""),("o",org?.ToString()??""));\n        observations.Add(new {proof=activeProof,pid=c.ProcessID,role=c.UserName,identity,account,org,context="SET LOCAL within transaction"});')
    source = re.sub(r'await (\w+)\.(CommitAsync|RollbackAsync)\(([^;]*)\);',
        lambda m: '{ '+m.group(0)+' timeline.Add(new {proof=activeProof,transaction="'+m.group(1)+'",eventName="'+m.group(2)+' completed"}); }',source)
    source = source.replace('bool originOk=true;', '''bool originOk=true;
            var diagnoses=new Dictionary<string,string>{{"P1001","Integrity.ContextInvalid"},{"P1003","Integrity.AccountMembership"},{"P1004","Integrity.OwnerMissing"},{"P1005","Integrity.Immutable"},{"P2002","Account.Ineligible"}};
            if(diagnoses.TryGetValue(state,out var diagnosis))originOk=e.MessageText==diagnosis && e.Routine=="exec_stmt_raise";
            if(state=="22P02")originOk=e.MessageText.StartsWith("invalid input syntax for type uuid:",StringComparison.Ordinal);
            if(state=="23503")originOk=e.TableName=="memberships"&&e.ConstraintName=="memberships_organization_id_account_id_fkey";''')
    (ROOT/'runtime/obj').mkdir(parents=True,exist_ok=True)
    (ROOT/'runtime/obj/G2.Program.g.cs').write_text(source,encoding='utf-8')
    selected = [{'id':i,'variant':v,'gate':'G2','reason':'Deterministic SQL/driver validation; no concurrent writer protocol'} for i,v in sorted(allowed)]
    selection = {'selected':selected,'excluded':[{'id':i,'variant':v,'result':'DEFERRED' if i.startswith('K') or i in {'V09','V11','V12','V13'} else 'NOT RUN','reason':'G3 concurrency/failure protocol' if i.startswith('K') or i in {'V09','V11','V12','V13'} else 'Approved G1 surface reused; not a G2 variant'} for i,v in all_proofs if (i,v) not in allowed and not(i=='K09' and v=='two-active-Owners-direct-owner-revocation-denied')],
        'deferred':['Application/API/OIDC/session/recovery protocols: G4','V11 structural: G3'],
        'trace':'Static two-Owner negative reuses original K09 variant as V07 G2 regression; no V11/write skew execution',
        'sourceSha256':hashlib.sha256((G1/'runtime/Program.cs').read_bytes()).hexdigest()}
    (ROOT/'selection.json').write_text(json.dumps(selection,indent=2)+'\n',encoding='utf-8')
    return source,selection

if __name__ == '__main__':
    source,selection=generate()
    print(json.dumps({'selected':len(selection['selected']),'excluded':len(selection['excluded']),'g1Modified':False}))
