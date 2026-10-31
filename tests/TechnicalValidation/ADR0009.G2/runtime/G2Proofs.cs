using System.Text.Json;
using Npgsql;

sealed partial class ValidationRun
{
    object G2Selected()
    {
        var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(package,"../ADR0009.G2/selection.json"))).RootElement;
        return new {selected=manifest.GetProperty("selected").EnumerateArray().Where(x=>!remaining||RemainingVariant(x.GetProperty("variant").GetString()!)).Select(x=>x.Clone()).ToArray(),
            exclusions=manifest.GetProperty("excluded").Clone(),reusedRun=remaining?(poolOnly?"f31a6617ed85":"e76986da515a"):null,
            reason=remaining?(poolOnly?"Reuse 30 passing subproofs; execute only persistent session baseline reset":"Reuse 27 passing subproofs; execute corrected probes"):"Full deterministic G2 SQL selection",gate="G2_NOT_HUMAN_APPROVED"};
    }
    bool RemainingVariant(string variant)=>poolOnly?variant=="G2-session-baseline-reset":variant is "G2-technical-context-components-and-rollback" or "G2-savepoint-restores-previous-context" or "G2-native-keys-and-checks";

    async Task G2Snapshot(string phase)
    {
        await using var observer=await Open("postgres");
        var tables=new[]{"identity.identity_consumptions","identity.enterprise_user_accounts","tenancy.organizations","tenancy.memberships","tenancy.invitations","audit.events","operation.idempotency_records"};
        var state=new Dictionary<string,object?>();
        foreach(var table in tables) state[table]=await Scalar(observer,"SELECT count(*) FROM "+table);
        state["demo"]=await Scalar(observer,"SELECT coalesce(json_agg(x ORDER BY id)::text,'[]') FROM (SELECT id,organization_id,title,version FROM sandbox.demo_records) x");
        observations.Add(new {proof=activeProof,phase,observerRole="postgres read-only observation, not runtime authorization",pid=observer.ProcessID,state});
    }

    async Task G2Proofs()
    {
        await Proof("Q12","G2-session-baseline-reset",async()=>{
            await using var data=NpgsqlDataSource.Create(Connection("core_tenant",credentials["core_tenant"],true));
            int pid;
            await using(var c=await data.OpenConnectionAsync()){
                pid=c.ProcessID;
                await Command(c,"SELECT set_config('app.identity_id',@i,false),set_config('app.account_id',@a,false),set_config('app.organization_id',@o,false)",("i",ownerA.ToString()),("a",accountA.ToString()),("o",orgA.ToString()));
                Require((Guid)(await Scalar(c,"SELECT id FROM sandbox.demo_records"))! ==demoA,"Synthetic persistent baseline exposes only exact A under trusted SQL fixture.");
            }
            await using(var c=await data.OpenConnectionAsync()){
                Require(c.ProcessID==pid,"Same physical backend reused after session baseline injection.");
                Require(Convert.ToBoolean(await Scalar(c,"SELECT authz.ctx('app.identity_id') IS NULL AND authz.ctx('app.account_id') IS NULL AND authz.ctx('app.organization_id') IS NULL")),"Npgsql reset clears all persisted context before first business read.");
                Require(Convert.ToInt64(await Scalar(c,"SELECT count(*) FROM sandbox.demo_records"))==0,"No tenant data after reset and before explicit transaction context.");
                await using var t=await c.BeginTransactionAsync();await Context(c,ownerB,accountB,orgB);
                Require((Guid)(await Scalar(c,"SELECT id FROM sandbox.demo_records"))! ==demoB,"Next transaction sees exact B only.");await t.RollbackAsync();
                observations.Add(new{proof=activeProof,role="core_tenant",pid,baseline="SET SESSION synthetic A; no external authentication claim",reset="Npgsql default reset enabled; same PID, empty context then B",transaction="B rolled back"});
            }
        });
        await Proof("V02","G2-native-keys-and-checks",async()=>{
            await using var observer=await Open("postgres");
            var before=await Scalar(observer,"SELECT count(*) FROM identity.enterprise_user_accounts");
            var tests=new[]{
                ("INSERT INTO tenancy.organizations(id,name,status) VALUES(@org,'duplicate','Active')","23505","organizations_pkey","organizations"),
                ("INSERT INTO tenancy.organizations(id,name,status) VALUES(@id,'invalid','Unknown')","23514","organizations_status_check","organizations"),
                ("INSERT INTO tenancy.memberships(id,organization_id,account_id,role,status) VALUES(@id,@org,@account,'Unknown','Active')","23514","memberships_role_check","memberships"),
                ("INSERT INTO identity.enterprise_user_accounts(id,identity_id,organization_id,status) VALUES(@id,@identity,@org,'Active')","23505","enterprise_user_accounts_identity_id_key","enterprise_user_accounts"),
                ("INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id) VALUES(@identity,@org,@id)","23505","identity_consumptions_pkey","identity_consumptions")};
            foreach(var (sql,state,constraint,table) in tests){
                var id=Guid.NewGuid();var contextOrg=constraint=="organizations_status_check"?id:orgA;
                await using var c=await Open("g1_probe");await using var t=await c.BeginTransactionAsync();await IntegrityContext(c,contextOrg);
                try{await Scalar(c,sql,("org",orgA),("id",id),("identity",ownerA),("account",accountA));throw new SafetyFailure("Native constraint did not reject");}
                catch(PostgresException e){
                    observations.Add(new{proof=activeProof,role="g1_probe",pid=c.ProcessID,organization=contextOrg,operation=sql,expected=state,observed=e.SqlState,e.ConstraintName,e.TableName,e.Routine,e.MessageText});
                    Require(e.SqlState==state&&e.ConstraintName==constraint&&e.TableName==table,"Native diagnosis matches exact SQLSTATE, constraint and table.");
                }
                await t.RollbackAsync();
            }
            Require(Equals(before,await Scalar(observer,"SELECT count(*) FROM identity.enterprise_user_accounts")),"No duplicate account persisted after native rejections.");
        });
        await Proof("F02","G2-physical-model-catalog",async()=>{
            await using var o=await Open("postgres");
            const string sql="SELECT json_build_object('tables',(SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN('identity','tenancy','sandbox','audit','operation') AND relkind='r'),'constraints',(SELECT json_agg(x) FROM (SELECT conrelid::regclass::text AS relation,conname,contype,condeferrable,condeferred,pg_get_constraintdef(oid) AS definition FROM pg_constraint WHERE connamespace IN(SELECT oid FROM pg_namespace WHERE nspname IN('identity','tenancy','sandbox','audit','operation')) ORDER BY conrelid::regclass::text,conname)x),'columns',(SELECT json_agg(x) FROM(SELECT table_schema,table_name,column_name,is_nullable,data_type FROM information_schema.columns WHERE table_schema IN('identity','tenancy','sandbox','audit','operation') ORDER BY table_schema,table_name,ordinal_position)x),'indexes',(SELECT json_agg(x) FROM(SELECT schemaname,tablename,indexname,indexdef FROM pg_indexes WHERE schemaname IN('identity','tenancy','sandbox','audit','operation') ORDER BY indexname)x))::text";
            var model=JsonDocument.Parse((string)(await Scalar(o,sql))!).RootElement.Clone();await Write("E-DB-model.json",model);
            Require(model.GetProperty("tables").GetInt32()==12,"Exactly 12 expected physical tables.");
            foreach(var kind in new[]{"p","f","u","c"})Require(model.GetProperty("constraints").EnumerateArray().Any(x=>x.GetProperty("contype").GetString()==kind),"Catalog has constraint kind "+kind);
            Require(Convert.ToInt64(await Scalar(o,"SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN('identity','tenancy','sandbox','audit','operation') AND relkind='r' AND NOT EXISTS(SELECT FROM pg_constraint k WHERE k.conrelid=c.oid AND contype='p')"))==0,"Every business table has primary key.");
            foreach(var table in new[]{"identity.enterprise_user_accounts","tenancy.memberships","tenancy.organizations","tenancy.invitations","sandbox.demo_records"}){
                Require(Convert.ToBoolean(await Scalar(o,"SELECT attnotnull FROM pg_attribute WHERE attrelid=@t::regclass AND attname='id'",("t",table))),"NOT NULL id: "+table);
                if(table!="tenancy.organizations")Require(Convert.ToBoolean(await Scalar(o,"SELECT attnotnull FROM pg_attribute WHERE attrelid=@t::regclass AND attname='organization_id'",("t",table))),"NOT NULL organization: "+table);
            }
        });
        await Proof("Q07","G2-each-context-field-read-write-denials",async()=>{
            foreach(var field in new[]{"identity_id","account_id","organization_id"})foreach(var mode in new[]{"empty","malformed","mixed"}){
                await using var c=await Open("core_tenant");await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);
                var value=mode=="empty"?"":mode=="malformed"?"invalid-uuid":(field=="identity_id"?ownerB:field=="account_id"?accountB:orgB).ToString();
                await Command(c,"SELECT set_config(@k,@v,true)",("k","app."+field),("v",value));
                observations.Add(new {proof=activeProof,role="core_tenant",pid=c.ProcessID,field,mode,context="A except specified field; synthetic identifiers only",expected=mode=="malformed"?"22P02":"0 rows and Access.Denied"});
                if(mode=="malformed")await Expect("22P02",()=>Scalar(c,"SELECT count(*) FROM sandbox.demo_records"));
                else {
                    Require(Convert.ToInt64(await Scalar(c,"SELECT count(*) FROM sandbox.demo_records"))==0,"Incomplete or mismatched context cannot read.");
                    const string sql="SELECT operation.update_demo(@id,1,'forbidden')";
                    await ExpectExact(c,sql,"42501","Access.Denied","core_tenant",()=>Scalar(c,sql,("id",demoA)));
                }
                await tx.RollbackAsync();
            }
            await using var o=await Open("postgres");Require(Convert.ToString(await Scalar(o,"SELECT title FROM sandbox.demo_records WHERE id=@i",("i",demoA)))=="A-canary","Denied mutations leave A unchanged.");
        });
        await Proof("Q08","G2-cross-tenant-target-write-and-own-positive",async()=>{
            await using var o=await Open("postgres");var before=Convert.ToInt64(await Scalar(o,"SELECT count(*) FROM audit.events"));
            await using(var c=await Open("core_tenant")){
                await using(var t=await c.BeginTransactionAsync()){
                    await Context(c,ownerA,accountA,orgA);
                    const string sql="SELECT operation.update_demo(@id,1,'cross-write')";
                    await ExpectExact(c,sql,"P2003","Resource.Unavailable","core_tenant",()=>Scalar(c,sql,("id",demoB)));await t.RollbackAsync();
                }
                await using(var t=await c.BeginTransactionAsync()){
                    await Context(c,ownerA,accountA,orgA);await Scalar(c,"SELECT operation.update_demo(@id,1,'own-write')",("id",demoA));await t.CommitAsync();
                }
            }
            Require(Convert.ToString(await Scalar(o,"SELECT title||':'||version FROM sandbox.demo_records WHERE id=@i",("i",demoA)))=="own-write:2","Own mutation committed with version.");
            Require(Convert.ToString(await Scalar(o,"SELECT title||':'||version FROM sandbox.demo_records WHERE id=@i",("i",demoB)))=="B-canary:1","B unchanged after cross-write attempt.");
            Require(Convert.ToInt64(await Scalar(o,"SELECT count(*) FROM audit.events"))==before+1,"Exactly own success audited.");
        });
        await Proof("V08","G2-technical-context-components-and-rollback",async()=>{
            await using var o=await Open("postgres");var before=await Scalar(o,"SELECT version FROM tenancy.organizations WHERE id=@o",("o",orgA));
            foreach(var key in new[]{"app.integrity_org","app.integrity_tx","app.operation_id"}){
                await using var c=await Open("g1_probe");await using var t=await c.BeginTransactionAsync();await IntegrityContext(c,orgA);
                Require((Guid)(await Scalar(c,"UPDATE tenancy.organizations SET version=version+1 WHERE id=@o RETURNING id",("o",orgA)))! ==orgA,"Probe actually updated anchor before stripping validator context.");
                await Command(c,"SELECT set_config(@k,'',true)",("k",key));
                try{await t.CommitAsync();throw new SafetyFailure("Invalid integrity context unexpectedly committed");}
                catch(PostgresException e){Require(e.SqlState=="P1001"&&e.MessageText=="Integrity.ContextInvalid"&&e.Routine=="exec_stmt_raise","Exact deferred validator context rejection: "+key);observations.Add(new{proof=activeProof,key,role="g1_probe",pid=c.ProcessID,sqlstate=e.SqlState,message=e.MessageText,transaction="COMMIT rejected; server rollback"});}
            }
            Require(Equals(before,await Scalar(o,"SELECT version FROM tenancy.organizations WHERE id=@o",("o",orgA))),"All failed COMMITs leave organization version unchanged.");
        });
        await Proof("Q12","G2-savepoint-restores-previous-context",async()=>{
            await using var c=await Open("core_tenant");await using var t=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);await t.SaveAsync("A");
            await Context(c,ownerB,accountB,orgB);Require(Convert.ToString(await Scalar(c,"SELECT title FROM sandbox.demo_records"))=="B-canary","B context set within savepoint.");
            await t.RollbackAsync("A");Require((Guid)(await Scalar(c,"SELECT id FROM sandbox.demo_records"))! ==demoA,"Rollback restores exact A record, not B, independent of earlier write fixture.");await t.CommitAsync();
            Require(Convert.ToInt64(await Scalar(c,"SELECT count(*) FROM sandbox.demo_records"))==0,"COMMIT clears restored context too.");
        });
    }
}
