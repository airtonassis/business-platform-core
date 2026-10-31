using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;

if (args.Length != 1 || args[0] != "--execute-disposable" && args[0] != "--execute-g1-closing")
    throw new InvalidOperationException("Explicit disposable execution switch required; no external database target accepted.");
await new ValidationRun(args[0]=="--execute-g1-closing").Execute();

sealed class ValidationRun(bool closing=false)
{
    const string Image = "sha256:79bd7c99e923138f136f8009d6bffa66e21e9d4fda5c0c561b00fc9c90cfe537";
    readonly string run = Guid.NewGuid().ToString("N")[..12];
    readonly string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    readonly Dictionary<string, string> credentials = new();
    readonly List<object> proofs = new();
    readonly List<object> timeline = new();
    readonly List<object> observations = new();
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Exception,(string Sql,string Session,string Effective)> errorContexts = new();
    string activeProof = "preflight";
    readonly Dictionary<string, CaseResult> results = new();
    readonly string package = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    readonly Guid platform = Guid.NewGuid(), ownerA = Guid.NewGuid(), ownerB = Guid.NewGuid();
    readonly Guid orgA = Guid.NewGuid(), orgB = Guid.NewGuid();
    readonly string referenceA = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    readonly string referenceB = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    Guid accountA, accountB, memberA, demoA, demoB;
    string name = "", database = "", output = "", adminConnection = "";
    int port;
    bool created, schemaInstalled, stoppedForP0;
    string cleanup = "NOT_CREATED", stage = "preflight";
    public async Task Execute()
    {
        output = Path.Combine(package, "evidence", "runs", run);
        Directory.CreateDirectory(output);
        var frozenExpected=await File.ReadAllTextAsync(Path.Combine(package,"manifests","security.expected.json"));
        await Write("preflight.expected.json",JsonDocument.Parse(frozenExpected).RootElement.Clone());
        await Write("expectation-freeze.json",new {run,capturedBeforeDatabaseCreationUtc=DateTimeOffset.UtcNow,
            sha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(frozenExpected))),
            origin="Manual capabilities frozen before this run; no observed catalog input"});
        await Write("source-hashes.json",Directory.GetFiles(Path.Combine(package,"runtime"),"*.*")
            .Where(x=>x.EndsWith(".sql",StringComparison.Ordinal)||x.EndsWith(".cs",StringComparison.Ordinal)||x.EndsWith(".csproj",StringComparison.Ordinal)||x.EndsWith(".json",StringComparison.Ordinal))
            .ToDictionary(x=>Path.GetFileName(x)!,x=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x)))));
        var source = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(package,"proofs","scenarios.json")));
        foreach(var item in source.RootElement.GetProperty("cases").EnumerateArray())
            results.Add(item.GetProperty("id").GetString()!,new CaseResult("NOT_EXECUTED","Not started."));
        name = "bpc-adr0009-g1-" + run;
        database = "bpc_adr0009_g1_" + run;
        try
        {
            var image = await Docker(["image","inspect",Image,"--format","{{.Id}} {{.Os}}/{{.Architecture}}"]);
            Require(image.Trim() == Image+" linux/amd64","Pinned local Linux image identity.");
            await Write("isolation-before.json", new {run, name, database, image=Image, host="127.0.0.1",
                storage="new tmpfs, no host or named volume", authorization="User instruction: minimal implementation and disposable execution",
                credentials="random in memory; never serialized", targetInputAccepted=false});
            await Docker(["run","--detach","--rm","--name",name,"--label","bpc.adr0009.run="+run,
                "--publish","127.0.0.1::5432","--tmpfs","/var/lib/postgresql/data:rw,nosuid,size=256m",
                "--tmpfs","/var/run/postgresql:rw,nosuid,size=16m","--tmpfs","/tmp:rw,nosuid,size=16m",
                "--env","POSTGRES_PASSWORD","--env","POSTGRES_DB="+database,Image,
                "-c","track_functions=all","-c","log_statement=none","-c","log_min_error_statement=panic","-c","deadlock_timeout=200ms"],
                new Dictionary<string,string>{{"POSTGRES_PASSWORD",password}});
            created=true; cleanup="CREATED_DISPOSABLE";
            var inspection = JsonDocument.Parse(await Docker(["inspect",name]));
            var container=inspection.RootElement[0];
            Require(container.GetProperty("Config").GetProperty("Labels").GetProperty("bpc.adr0009.run").GetString()==run,"Run label.");
            Require(container.GetProperty("Image").GetString()==Image,"Container image.");
            var binding=container.GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("5432/tcp")[0];
            Require(binding.GetProperty("HostIp").GetString()=="127.0.0.1","Loopback only.");
            foreach(var mount in container.GetProperty("Mounts").EnumerateArray())
                Require(mount.GetProperty("Type").GetString()=="tmpfs","No persistent/host mounts.");
            port=int.Parse(binding.GetProperty("HostPort").GetString()!,System.Globalization.CultureInfo.InvariantCulture);
            adminConnection=Connection("postgres",password,false);
            var ready=Stopwatch.StartNew();
            while(true)
            {
                try { await using var c=await Open("postgres"); await Scalar(c,"SELECT 1"); break; }
                catch(NpgsqlException) when(ready.Elapsed<TimeSpan.FromSeconds(30)){ await Task.Delay(100); }
            }
            await using(var admin=await Open("postgres"))
            {
                await RequireTarget(admin);
                await Command(admin,$"COMMENT ON DATABASE {database} IS 'bpc-adr0009-g1:disposable'");
                var version=await Scalar(admin,"SELECT version()");
                await Write("environment.json",new {run,database,host="127.0.0.1",port,image=Image,
                    postgres=version?.ToString(),dotnet=Environment.Version.ToString(),npgsql=typeof(NpgsqlConnection).Assembly.GetName().Version?.ToString(),
                    operationalChoices=new {statementTimeoutMs=8000,lockTimeoutMs=4000,concurrencyDeadlineMs=10000,
                        deadlockTimeoutMs=200,isolation="READ COMMITTED",automaticRetries=0,disposableOnly=true}});
                foreach(var file in new[]{"001-test-only.sql","002-test-ports.sql","003-test-replace.sql","004-candidate-grants.sql"}) {
                    stage=file;
                    await Command(admin,await File.ReadAllTextAsync(Path.Combine(package,"runtime",file)));
                }
                foreach(var user in new[]{"core_identity","core_tenant","core_platform","g1_probe"})
                {
                    credentials[user]=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                    // Fixed identifiers and in-memory random hex only. Never log SQL or credentials.
                    await Command(admin,$"ALTER ROLE {user} PASSWORD '{credentials[user]}'");
                    await Command(admin,$"GRANT CONNECT ON DATABASE {database} TO {user}");
                }
                schemaInstalled=true;stage="fixtures-and-positive-bootstrap";
                await Command(admin,$"REVOKE ALL ON DATABASE {database} FROM PUBLIC");
                foreach(var id in new[]{platform,ownerA,ownerB}) await Identity(admin,id);
                await Reference(admin,ownerA,referenceA);
                await Reference(admin,ownerB,referenceB);
            }
            await Bootstrap(ownerA,orgA,referenceA);
            await Bootstrap(ownerB,orgB,referenceB);
            await using(var a=await Open("postgres"))
            {
                accountA=await Id(a,"SELECT id FROM identity.enterprise_user_accounts WHERE identity_id=@i",("i",ownerA));
                accountB=await Id(a,"SELECT id FROM identity.enterprise_user_accounts WHERE identity_id=@i",("i",ownerB));
                memberA=await Id(a,"SELECT id FROM tenancy.memberships WHERE account_id=@i",("i",accountA));
                demoA=Guid.NewGuid();demoB=Guid.NewGuid();
                await Command(a,"INSERT INTO sandbox.demo_records(id,organization_id,title) VALUES(@a,@oa,'A-canary'),(@b,@ob,'B-canary')",
                    ("a",demoA),("oa",orgA),("b",demoB),("ob",orgB));
            }
            stage="catalog-and-proofs";
            await Catalog();
            if(closing){await G1ClosingProofs();}
            else {
            await Proof("Q08","tenant-sql-isolation",async()=>{
                await using var c=await Open("core_tenant");await using var tx=await c.BeginTransactionAsync();
                await Context(c,ownerA,accountA,orgA);
                Require(Convert.ToInt64(await Scalar(c,"SELECT count(*) FROM sandbox.demo_records"))==1,"Only one own row.");
                Require(Convert.ToString(await Scalar(c,"SELECT title FROM sandbox.demo_records"))=="A-canary","No B canary.");
                await tx.RollbackAsync();
            });
            await Proof("Q07","missing-mismatched-malformed-context",async()=>{
                await using var c=await Open("core_tenant");
                await using(var tx=await c.BeginTransactionAsync()){
                    Require(Convert.ToInt64(await Scalar(c,"SELECT count(*) FROM sandbox.demo_records"))==0,"Absent context denies.");
                    await Context(c,ownerA,accountB,orgB);
                    Require(Convert.ToInt64(await Scalar(c,"SELECT count(*) FROM sandbox.demo_records"))==0,"Mismatched tuple denies.");
                    await tx.RollbackAsync();
                }
                await using(var tx=await c.BeginTransactionAsync()){
                    await Command(c,"SELECT set_config('app.organization_id','invalid-uuid',true)");
                    await Expect("22P02",()=>Scalar(c,"SELECT count(*) FROM sandbox.demo_records"));
                    await tx.RollbackAsync();
                }
            });
            await Proof("Q11","npgsql-pool-commit-rollback-exception",async()=>{
                await using var data=NpgsqlDataSource.Create(Connection("core_tenant",credentials["core_tenant"],true));
                int prior=0;
                foreach(var mode in new[]{"commit","rollback","exception"}){
                    await using(var c=await data.OpenConnectionAsync()){
                        int pid=Convert.ToInt32(await Scalar(c,"SELECT pg_backend_pid()"));if(prior!=0)Require(pid==prior,"Same physical PID.");prior=pid;
                        await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);
                        Require(Convert.ToInt64(await Scalar(c,"SELECT count(*) FROM sandbox.demo_records"))==1,"A visible.");
                        if(mode=="commit")await tx.CommitAsync();
                        else {if(mode=="exception")await Expect("22012",()=>Scalar(c,"SELECT 1/0"));await tx.RollbackAsync();}
                    }
                    await using(var c=await data.OpenConnectionAsync()){
                        Require(Convert.ToInt32(await Scalar(c,"SELECT pg_backend_pid()"))==prior,"Reused backend.");
                        Require(Convert.ToInt64(await Scalar(c,"SELECT count(*) FROM sandbox.demo_records"))==0,"No context residue.");
                        await using var tx=await c.BeginTransactionAsync();await Context(c,ownerB,accountB,orgB);
                        Require(Convert.ToString(await Scalar(c,"SELECT title FROM sandbox.demo_records"))=="B-canary","B only.");await tx.RollbackAsync();
                    }
                }
                timeline.Add(new {proof="pool",pid=prior,modes=new[]{"commit","rollback","exception"}});
            });
            await Proof("Q12","savepoint-context-rollback",async()=>{
                await using var c=await Open("core_tenant");await using var tx=await c.BeginTransactionAsync();
                await tx.SaveAsync("before_context");await Context(c,ownerA,accountA,orgA);
                await tx.RollbackAsync("before_context");
                Require(Convert.ToInt64(await Scalar(c,"SELECT count(*) FROM sandbox.demo_records"))==0,"Savepoint removes local context.");
                await tx.RollbackAsync();
            });
            await Proof("Q10","compromised-credential-characterization-only",async()=>{
                await using var c=await Open("core_tenant");await using var tx=await c.BeginTransactionAsync();await Context(c,ownerB,accountB,orgB);
                Require(Convert.ToString(await Scalar(c,"SELECT title FROM sandbox.demo_records"))=="B-canary","D1 residual characterized, not OIDC proof.");
                await tx.RollbackAsync();
            },characterization:true);
            await IntegrityProofs();
            await SecurityProofs();
            await ConcurrencyProofs();
            await AdditionalProofs();
            await RemainingSqlProofs();
            await AuditCorrectionProofs();
            await P1ClosureProofs();
            }
        }
        catch(Exception ex)
        {
            await Write("stop.json",new {category=stoppedForP0?"P0_STOP":"SETUP_OR_OPERATIONAL_BLOCK",
                stage,exceptionType=ex.GetType().Name,sqlstate=(ex as PostgresException)?.SqlState,
                functionContext=ex is PostgresException diagnostic ? diagnostic.Where?.Split('\n').LastOrDefault():null,
                setupMessage=ex is PostgresException pe && (!schemaInstalled || pe.SqlState is "42703" or "42883" or "42501") ? pe.MessageText : null,
                position=!schemaInstalled && ex is PostgresException pp ? pp.Position : 0,
                detail=ex is SafetyFailure ? ex.Message : "Failure recorded without SQL, payload or credential; inspect stage and sanitized proof evidence."});
        }
        finally
        {
            if(created){
                try {
                    var label=(await Docker(["inspect",name,"--format","{{index .Config.Labels \"bpc.adr0009.run\"}}"])).Trim();
                    Require(label==run && name=="bpc-adr0009-g1-"+run,"Cleanup identity must match this run.");
                    NpgsqlConnection.ClearAllPools();
                    await Docker(["rm","--force",name]);cleanup="REMOVED_RUN_CONTAINER_AND_TMPFS";
                    var remaining=await Docker(["ps","-a","--filter","label=bpc.adr0009.run="+run,"--format","{{.ID}}"]);
                    Require(string.IsNullOrWhiteSpace(remaining),"No run container remains.");
                }catch {cleanup="CLEANUP_FAILED_MANUAL_ATTENTION"; }
            }
            // Preserve NOT_EXECUTED: an unstarted scenario is not a failed or executed proof.
            await Write("results.json",new {run,gate="BLOCKED_PENDING_HUMAN_REVIEW",cleanup,schemaInstalled,stoppedForP0,
                counts=results.Values.GroupBy(x=>x.Status).ToDictionary(g=>g.Key,g=>g.Count()),cases=results,subproofs=proofs,timeline});
            await Write("assertions.json",observations);
            Console.WriteLine(JsonSerializer.Serialize(new {run,output,cleanup,stoppedForP0,counts=results.Values.GroupBy(x=>x.Status).ToDictionary(g=>g.Key,g=>g.Count())}));
        }
    }

    async Task IntegrityProofs()
    {
        await Proof("V01","active-account-without-member-commit",async()=>{
            var ident=Guid.NewGuid();var aid=Guid.NewGuid();
            await using(var a=await Open("postgres"))await Identity(a,ident);
            await using(var c=await Open("g1_probe")){
                await using var tx=await c.BeginTransactionAsync();await IntegrityContext(c,orgA);
                await Scalar(c,"SELECT id FROM tenancy.organizations WHERE id=@o FOR UPDATE",("o",orgA));
                await Command(c,"INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id) VALUES(@i,@o,@a)",("i",ident),("o",orgA),("a",aid));
                await Command(c,"INSERT INTO identity.enterprise_user_accounts(id,identity_id,organization_id,status) VALUES(@a,@i,@o,'Active')",("a",aid),("i",ident),("o",orgA));
                await Expect("P1003",async()=>{await tx.CommitAsync();return null;});
            }
            await using var observer=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM identity.identity_consumptions WHERE identity_id=@i",("i",ident)))==0,"No consumed identity persisted.");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM identity.enterprise_user_accounts WHERE id=@a",("a",aid)))==0,"No partial account.");
        });
        await Proof("V05","organization-without-owner-at-commit",async()=>{
            var org=Guid.NewGuid();
            await using(var c=await Open("g1_probe")){
                await using var tx=await c.BeginTransactionAsync();await IntegrityContext(c,org);
                await Command(c,"INSERT INTO tenancy.organizations(id,name,status) VALUES(@o,'ownerless','Active')",("o",org));
                await Expect("P1004",async()=>{await tx.CommitAsync();return null;});
            }
            await using var a=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(a,"SELECT count(*) FROM tenancy.organizations WHERE id=@o",("o",org)))==0,"No orphan org.");
        });
        await Proof("V08","missing-integrity-context-rejects",async()=>{
            await using var c=await Open("g1_probe");await using var tx=await c.BeginTransactionAsync();
            await Command(c,"SELECT set_config('app.integrity_org',@o,true)",("o",orgA.ToString()));
            await Scalar(c,"SELECT id FROM tenancy.organizations WHERE id=@o FOR UPDATE",("o",orgA));
            await Command(c,"UPDATE tenancy.organizations SET version=version+1 WHERE id=@o",("o",orgA));
            await Expect("P1001",async()=>{await tx.CommitAsync();return null;});
        });
        await Proof("V04","partial-revocation-rejected",async()=>{
            await using var c=await Open("g1_probe");await using var tx=await c.BeginTransactionAsync();await IntegrityContext(c,orgA);
            await Scalar(c,"SELECT id FROM tenancy.organizations WHERE id=@o FOR UPDATE",("o",orgA));
            await Command(c,"UPDATE identity.enterprise_user_accounts SET status='Revoked' WHERE id=@a",("a",accountA));
            await Expect("P1003",async()=>{await tx.CommitAsync();return null;});
        });
        await Proof("V07","immutable-org-and-historical-consumption",async()=>{
            await using var a=await Open("postgres");
            await Expect("P1005",()=>Scalar(a,"UPDATE identity.enterprise_user_accounts SET organization_id=@b WHERE id=@a RETURNING id",("b",orgB),("a",accountA)));
            await Expect("P1005",()=>Scalar(a,"DELETE FROM identity.identity_consumptions WHERE identity_id=@i RETURNING identity_id",("i",ownerA)));
        });
    }
    async Task SecurityProofs()
    {
        await Proof("F01","runtime-direct-dml-denied",async()=>{
            foreach(var role in new[]{"core_tenant","core_identity","core_platform"}){
                await using var c=await Open(role);
                await Expect("42501",()=>Scalar(c,"INSERT INTO sandbox.demo_records(id,organization_id,title) VALUES(@id,@o,'forbidden') RETURNING id",("id",Guid.NewGuid()),("o",orgA)));
                if(role!="core_tenant")await Expect("42501",()=>Scalar(c,"SELECT count(*) FROM sandbox.demo_records"));
            }
        });
        await Proof("F04","no-set-role-or-owner-escalation",async()=>{
            foreach(var user in new[]{"core_tenant","core_identity","core_platform"}){
                await using var c=await Open(user);
                foreach(var owner in new[]{"exec_identity","exec_tenant","exec_bootstrap","exec_context","exec_integrity","ddl_owner","core_migrator"})
                    await Expect("42501",()=>Scalar(c,"SET ROLE "+owner));
            }
        });
        await Proof("F03","public-execute-and-wrong-port-denied",async()=>{
            await using var a=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(a,@"SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace,
                LATERAL aclexplode(coalesce(p.proacl,acldefault('f',p.proowner))) acl
                WHERE n.nspname IN('operation','authz') AND acl.grantee=0 AND acl.privilege_type='EXECUTE'"))==0,"No PUBLIC execute.");
            await using var c=await Open("core_identity");
            await Expect("42501",()=>Scalar(c,"SELECT operation.bootstrap(@i,@o,'forbidden','key','wrong')",("i",ownerA),("o",Guid.NewGuid())));
        });
        await Proof("F02","owners-and-runtime-not-bypass",async()=>{
            await using var c=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(c,@"SELECT count(*) FROM pg_roles WHERE rolname IN
            ('core_identity','core_tenant','core_platform','exec_identity','exec_tenant','exec_bootstrap','exec_context','exec_integrity')
            AND (rolsuper OR rolbypassrls OR rolcreaterole OR rolcreatedb)"))==0,"No unsafe role attributes.");
            Require(Convert.ToInt64(await Scalar(c,@"SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname IN('identity','tenancy','sandbox','audit','operation') AND c.relkind='r'
                AND (NOT relrowsecurity OR NOT relforcerowsecurity OR pg_get_userbyid(relowner)<>'ddl_owner')"))==0,"All tables forced and segregated.");
        });
    }
    async Task ConcurrencyProofs()
    {
        await Proof("K01","accept-A-B-two-physical-connections",async()=>{
            foreach(var aFirst in new[]{true,false}){
            var recipient=Guid.NewGuid();var invA=Guid.NewGuid();var invB=Guid.NewGuid();
            var codeA=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var codeB=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await using(var a=await Open("postgres")){
                await Identity(a,recipient);await Invite(a,invA,recipient,orgA,codeA);await Invite(a,invB,recipient,orgB,codeB);
            }
            await using var one=await Open("core_identity");await using var two=await Open("core_identity");await using var observer=await Open("postgres");
            await using var t1=await one.BeginTransactionAsync();await using var t2=await two.BeginTransactionAsync();
            await Context(one,recipient,null,null);await Context(two,recipient,null,null);
            int pid1=one.ProcessID,pid2=two.ProcessID;Require(pid1!=pid2,"Distinct physical connections.");
            await Scalar(one,"SELECT operation.accept_invitation(@id,@code,1)",("id",aFirst?invA:invB),("code",aFirst?codeA:codeB));
            var pending=Scalar(two,"SELECT operation.accept_invitation(@id,@code,1)",("id",aFirst?invB:invA),("code",aFirst?codeB:codeA));
            await WaitBlocked(observer,pid2,pid1);
            timeline.Add(new {proof="K01",pid1,pid2,eventName="D2 blocked by D1 identity; barrier observed"});
            try{await t1.CommitAsync();}catch{try{await pending;}catch{}await t2.RollbackAsync();throw;}await Expect("P2002",()=>pending);await t2.RollbackAsync();
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM identity.enterprise_user_accounts WHERE identity_id=@i",("i",recipient)))==1,"One account.");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM tenancy.invitations WHERE recipient_identity_id=@i AND status='Accepted'",("i",recipient)))==1,"One accepted.");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM identity.enterprise_user_accounts WHERE identity_id=@i AND organization_id=@o",("i",recipient),("o",aFirst?orgA:orgB)))==1,"Winner organization matches committed invitation, both orders.");
            timeline.Add(new {proof="K01",aFirst,eventName="both organization orderings covered"});
            }
        });
        await Proof("K04","same-version-two-writers",async()=>{
            await using var one=await Open("core_tenant");await using var two=await Open("core_tenant");await using var observer=await Open("postgres");
            await using var t1=await one.BeginTransactionAsync();await using var t2=await two.BeginTransactionAsync();
            await Context(one,ownerA,accountA,orgA);await Context(two,ownerA,accountA,orgA);
            await Scalar(one,"SELECT operation.update_demo(@id,1,'winner')",("id",demoA));
            var pending=Scalar(two,"SELECT operation.update_demo(@id,1,'loser')",("id",demoA));
            await WaitBlocked(observer,two.ProcessID,one.ProcessID);
            timeline.Add(new {proof="K04",pid1=one.ProcessID,pid2=two.ProcessID,eventName="wait observed before release"});
            await t1.CommitAsync();await Expect("P2007",()=>pending);await t2.RollbackAsync();
            Require(Convert.ToInt64(await Scalar(observer,"SELECT version FROM sandbox.demo_records WHERE id=@id",("id",demoA)))==2,"Only one update.");
        });
        await Proof("K08","audit-failure-rolls-back-demo",async()=>{
            await using var admin=await Open("postgres");
            await Command(admin,"ALTER TABLE audit.events ADD CONSTRAINT g1_fault_audit CHECK(false) NOT VALID");
            try {
                await using var c=await Open("core_tenant");await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);
                await Expect("23514",()=>Scalar(c,"SELECT operation.update_demo(@id,2,'not-committed')",("id",demoA)));
                await tx.RollbackAsync();
                Require(Convert.ToInt64(await Scalar(admin,"SELECT version FROM sandbox.demo_records WHERE id=@id",("id",demoA)))==2,"Demo rollback with failed event.");
            }finally{await Command(admin,"ALTER TABLE audit.events DROP CONSTRAINT g1_fault_audit");}
        });
        await Proof("K07","confirmed-commit-lost-response-bootstrap-replay",async()=>{
            var who=Guid.NewGuid();var org=Guid.NewGuid();var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await using(var a=await Open("postgres")){await Identity(a,who);await Reference(a,who,secret);}
            await Bootstrap(who,org,secret); // The confirmed response is intentionally discarded.
            await Bootstrap(who,org,secret);
            await using var observer=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM identity.enterprise_user_accounts WHERE identity_id=@i",("i",who)))==1,"No duplicate after replay.");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM audit.events WHERE organization_id=@o",("o",org)))==1,"No repeated success event.");
        });
        await Proof("V13","real-deadlock-in-dedicated-probe",async()=>{
            var first=orgA.CompareTo(orgB)<0?orgA:orgB;var second=first==orgA?orgB:orgA;
            await using var one=await Open("g1_probe");await using var two=await Open("g1_probe");await using var o=await Open("postgres");
            await using var t1=await one.BeginTransactionAsync();await using var t2=await two.BeginTransactionAsync();
            await IntegrityContext(one,first);await IntegrityContext(two,second);
            await Scalar(one,"SELECT id FROM tenancy.organizations WHERE id=@o FOR UPDATE",("o",first));
            await Scalar(two,"SELECT id FROM tenancy.organizations WHERE id=@o FOR UPDATE",("o",second));
            await Command(one,"SELECT set_config('app.integrity_org',@o,true)",("o",second.ToString()));
            var p1=Scalar(one,"SELECT id FROM tenancy.organizations WHERE id=@o FOR UPDATE",("o",second));
            await WaitBlocked(o,one.ProcessID,two.ProcessID);
            await Command(two,"SELECT set_config('app.integrity_org',@o,true)",("o",first.ToString()));
            var p2=Scalar(two,"SELECT id FROM tenancy.organizations WHERE id=@o FOR UPDATE",("o",first));
            static async Task<string> Outcome(Task<object?> task) {
                try {await task;return "OK";}catch(PostgresException e){return e.SqlState;}
            }
            var outcomes=await Task.WhenAll(Outcome(p1),Outcome(p2)).WaitAsync(TimeSpan.FromSeconds(10));
            Require(outcomes.Count(x=>x=="40P01")==1 && outcomes.Count(x=>x=="OK")==1,"One real deadlock victim, one released waiter.");
            await t1.RollbackAsync();await t2.RollbackAsync();
            timeline.Add(new {proof="deadlock",pid1=one.ProcessID,pid2=two.ProcessID,eventName="real 40P01 and both transactions rolled back; no product lock inversion introduced"});
        });
    }
    async Task AdditionalProofs()
    {
        await Proof("K05","concurrent-replace-and-authorized-replay",async()=>{
            var who=Guid.NewGuid();var inv=Guid.NewGuid();var original=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var nextSecret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var otherSecret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await using(var a=await Open("postgres")){await Identity(a,who);await Invite(a,inv,who,orgA,original);}
            await using var one=await Open("core_tenant");await using var two=await Open("core_tenant");await using var observer=await Open("postgres");
            await using var t1=await one.BeginTransactionAsync();await using var t2=await two.BeginTransactionAsync();
            await Context(one,ownerA,accountA,orgA);await Context(two,ownerA,accountA,orgA);
            var successor=(Guid)(await Scalar(one,"SELECT operation.replace_invitation(@id,1,'replace-one',@s)",("id",inv),("s",nextSecret)))!;
            var pending=Scalar(two,"SELECT operation.replace_invitation(@id,1,'replace-two',@s)",("id",inv),("s",otherSecret));
            await WaitBlocked(observer,two.ProcessID,one.ProcessID);await t1.CommitAsync();await Expect("P2007",()=>pending);await t2.RollbackAsync();
            await using(var replay=await Open("core_tenant")){await using var t=await replay.BeginTransactionAsync();await Context(replay,ownerA,accountA,orgA);
                Require((Guid)(await Scalar(replay,"SELECT operation.replace_invitation(@id,1,'replace-one',@s)",("id",inv),("s",otherSecret)))! ==successor,"Same receipt returns successor without secret recovery.");await t.CommitAsync();}
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM tenancy.invitations WHERE recipient_identity_id=@i AND status='Pending'",("i",who)))==1,"Exactly one Pending successor.");
            Require(Convert.ToString(await Scalar(observer,"SELECT status FROM tenancy.invitations WHERE id=@id",("id",inv)))=="Replaced","Old code cannot accept replaced invitation.");
            timeline.Add(new {proof="K05",pid1=one.ProcessID,pid2=two.ProcessID,eventName="blocking observed, old ETag rejected, authorized receipt replay"});
        });
        await Proof("V02","split-rls-not-null-catalog-and-rollback",async()=>{
            await using var observer=await Open("postgres");
            foreach(var column in new[]{"id","organization_id","account_id","role","status","version"})
                Require(Convert.ToBoolean(await Scalar(observer,"SELECT attnotnull FROM pg_catalog.pg_attribute WHERE attrelid='tenancy.memberships'::regclass AND attname=@c AND NOT attisdropped",("c",column))),"Catalog NOT NULL: tenancy.memberships."+column);
            var before=Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM tenancy.memberships"));
            // Runtime authorization is tested with the existing SELECT grant and RLS,
            // not by granting INSERT merely to reach a different constraint oracle.
            await using(var runtime=await Open("core_tenant")){
                await using var tx=await runtime.BeginTransactionAsync();
                await Context(runtime,ownerA,accountA,orgB);
                Require(Convert.ToInt64(await Scalar(runtime,"SELECT count(*) FROM sandbox.demo_records"))==0,"Runtime mismatched organization: RLS returns no rows.");
                await tx.RollbackAsync();
                await using var denied=await runtime.BeginTransactionAsync();
                await Context(runtime,ownerA,accountA,orgA);
                await Expect("42501",()=>Scalar(runtime,"INSERT INTO tenancy.memberships(id,organization_id,account_id,role,status) VALUES(@id,@o,@a,'Member','Active') RETURNING id",("id",Guid.NewGuid()),("o",orgA),("a",accountA)));
                await denied.RollbackAsync();
            }
            await using var c=await Open("g1_probe");
            await using(var tx=await c.BeginTransactionAsync()){
                await IntegrityContext(c,orgA);
                await Expect("23505",()=>Scalar(c,"INSERT INTO tenancy.memberships(id,organization_id,account_id,role,status) VALUES(@id,@o,@a,'Member','Active') RETURNING id",
                    ("id",Guid.NewGuid()),("o",orgA),("a",accountA)));await tx.RollbackAsync();
            }
            foreach(var missing in new[]{"id","organization_id","account_id","role","status","version"}){
                await using var tx=await c.BeginTransactionAsync();await IntegrityContext(c,orgA);
                var values=new Dictionary<string,string>{{"id","@id"},{"organization_id","@o"},{"account_id","@a"},{"role","'Member'"},{"status","'Active'"},{"version","1"}};
                values[missing]="NULL";
                var statement="INSERT INTO tenancy.memberships(id,organization_id,account_id,role,status,version) VALUES("+string.Join(",",values.Values)+") RETURNING id";
                // NULL organization makes probe_org WITH CHECK unknown, hence 42501
                // before ExecConstraints/NOT NULL. Other fields reach NOT NULL.
                await Expect(missing=="organization_id"?"42501":"23502",()=>Scalar(c,statement,("id",Guid.NewGuid()),("o",orgA),("a",accountA)));
                await tx.RollbackAsync();
            }
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM tenancy.memberships WHERE account_id=@a",("a",accountA)))==1,"Exactly one valid membership retained.");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM tenancy.memberships"))==before,"All rejected inserts rolled back; total membership count unchanged.");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM tenancy.memberships WHERE id IS NULL OR organization_id IS NULL OR account_id IS NULL OR role IS NULL OR status IS NULL OR version IS NULL"))==0,"No invalid nullable membership persisted.");
        });
        await Proof("V03","cross-organization-foreign-key",async()=>{
            // Isolate the FK oracle: an already-used account would fail UNIQUE(account_id) first.
            var identity=Guid.NewGuid();var account=Guid.NewGuid();
            await using(var a=await Open("postgres"))await Identity(a,identity);
            await using var c=await Open("g1_probe");await using var tx=await c.BeginTransactionAsync();await IntegrityContext(c,orgA);
            await Command(c,"INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id) VALUES(@i,@o,@a)",("i",identity),("o",orgA),("a",account));
            await Command(c,"INSERT INTO identity.enterprise_user_accounts(id,identity_id,organization_id,status) VALUES(@a,@i,@o,'Active')",("a",account),("i",identity),("o",orgA));
            await Command(c,"SELECT set_config('app.integrity_org',@o,true)",("o",orgB.ToString()));
            await Expect("23503",()=>Scalar(c,"INSERT INTO tenancy.memberships(id,organization_id,account_id,role,status) VALUES(@id,@o,@a,'Member','Active') RETURNING id",
                ("id",Guid.NewGuid()),("o",orgB),("a",account)));await tx.RollbackAsync();
        });
        await Proof("K02","bootstrap-versus-accept-both-orders",async()=>{
            foreach(var bootstrapFirst in new[]{true,false}){
                var who=Guid.NewGuid();var newOrg=Guid.NewGuid();var inv=Guid.NewGuid();var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                await using(var a=await Open("postgres")){await Identity(a,who);await Reference(a,who,secret);await Invite(a,inv,who,orgA,secret);}
                await using var cp=await Open("core_platform");await using var ci=await Open("core_identity");await using var o=await Open("postgres");
                await using var pt=await cp.BeginTransactionAsync();await using var it=await ci.BeginTransactionAsync();
                await Context(cp,platform,null,null);await Context(ci,who,null,null);
                Task<object?> Boot()=>Scalar(cp,"SELECT operation.bootstrap(@i,@o,'race-org',@k,@r)",("i",who),("o",newOrg),("k","race-"+newOrg),("r",secret));
                Task<object?> Accept()=>Scalar(ci,"SELECT operation.accept_invitation(@id,@s,1)",("id",inv),("s",secret));
                if(bootstrapFirst){
                    await Boot();var pending=Accept();await WaitBlocked(o,ci.ProcessID,cp.ProcessID);
                    await pt.CommitAsync();await Expect("P2002",()=>pending);await it.RollbackAsync();
                }else{
                    await Accept();var pending=Boot();await WaitBlocked(o,cp.ProcessID,ci.ProcessID);
                    await it.CommitAsync();await Expect("P2002",()=>pending);await pt.RollbackAsync();
                    Require(Convert.ToInt64(await Scalar(o,"SELECT count(*) FROM tenancy.organizations WHERE id=@id",("id",newOrg)))==0,"Losing bootstrap leaves no org.");
                }
                Require(Convert.ToInt64(await Scalar(o,"SELECT count(*) FROM identity.identity_consumptions WHERE identity_id=@i",("i",who)))==1,"Single consumption in both orderings.");
                timeline.Add(new {proof="K02",bootstrapFirst,bootstrapPid=cp.ProcessID,acceptPid=ci.ProcessID,eventName="real blocking, one commit and loser rollback"});
            }
        });
        await Proof("K03","revoke-versus-command-both-orders",async()=>{
            foreach(var revokeFirst in new[]{false,true}){
                var who=Guid.NewGuid();var inv=Guid.NewGuid();var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));Guid aid,mid,target=Guid.NewGuid();
                await using(var a=await Open("postgres")){
                    await Identity(a,who);await Invite(a,inv,who,orgA,secret);
                }
                aid=Guid.NewGuid();mid=Guid.NewGuid();
                await using(var a=await Open("postgres")){
                    await using var t=await a.BeginTransactionAsync();await IntegrityContext(a,orgA);
                    await Scalar(a,"SELECT id FROM identity.identities WHERE id=@i FOR UPDATE",("i",who));
                    await Scalar(a,"SELECT id FROM tenancy.organizations WHERE id=@o FOR UPDATE",("o",orgA));
                    await Command(a,"INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id) VALUES(@i,@o,@a)",("i",who),("o",orgA),("a",aid));
                    await Command(a,"INSERT INTO identity.enterprise_user_accounts(id,identity_id,organization_id,status) VALUES(@a,@i,@o,'Active')",("a",aid),("i",who),("o",orgA));
                    await Command(a,"INSERT INTO tenancy.memberships(id,organization_id,account_id,role,status) VALUES(@m,@o,@a,'Admin','Active')",("m",mid),("o",orgA),("a",aid));
                    await Command(a,"INSERT INTO sandbox.demo_records(id,organization_id,title) VALUES(@id,@o,'race')",("id",target),("o",orgA));
                    await t.CommitAsync();
                    observations.Add(new {proof=activeProof,fixture="Operator inserts initial Admin, not runtime provisioning authorization evidence"});
                }
                await using var actor=await Open("core_tenant");await using var revoker=await Open("core_tenant");await using var o=await Open("postgres");
                await using var at=await actor.BeginTransactionAsync();await using var rt=await revoker.BeginTransactionAsync();
                await Context(actor,who,aid,orgA);await Context(revoker,ownerA,accountA,orgA);
                Task<object?> Update()=>Scalar(actor,"SELECT operation.update_demo(@id,1,'race-updated')",("id",target));
                Task<object?> Revoke()=>Scalar(revoker,"SELECT operation.revoke_membership(@id,1)",("id",mid));
                if(revokeFirst){
                    await Revoke();var pending=Update();await WaitBlocked(o,actor.ProcessID,revoker.ProcessID);
                    await rt.CommitAsync();await Expect("42501",()=>pending);await at.RollbackAsync();
                }else{
                    await Update();var pending=Revoke();await WaitBlocked(o,revoker.ProcessID,actor.ProcessID);
                    await at.CommitAsync();await pending;await rt.CommitAsync();
                }
                Require(Convert.ToString(await Scalar(o,"SELECT status FROM identity.enterprise_user_accounts WHERE id=@a",("a",aid)))=="Revoked","Account revoked atomically.");
                Require(Convert.ToString(await Scalar(o,"SELECT status FROM tenancy.memberships WHERE id=@m",("m",mid)))=="Revoked","Member revoked atomically.");
                await using var next=await Open("core_tenant");await using var nt=await next.BeginTransactionAsync();await Context(next,who,aid,orgA);
                Require(Convert.ToInt64(await Scalar(next,"SELECT count(*) FROM sandbox.demo_records"))==0,"New request loses Tenant authorization.");
                await nt.RollbackAsync();
                timeline.Add(new {proof="K03",revokeFirst,actorPid=actor.ProcessID,revokerPid=revoker.ProcessID,eventName="observed lock ordering and atomic revocation"});
            }
        });
        await Proof("K06","expiry-revalidated-after-real-lock-wait",async()=>{
            var who=Guid.NewGuid();var inv=Guid.NewGuid();var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await using var o=await Open("postgres");await Identity(o,who);await Invite(o,inv,who,orgA,secret,500);
            await using var holder=await Open("core_identity");await using var waiter=await Open("core_identity");
            await using var ht=await holder.BeginTransactionAsync();await using var wt=await waiter.BeginTransactionAsync();
            await Context(holder,who,null,null);await Context(waiter,who,null,null);
            // Identity SELECT lock uses the same restricted role as the command owner via a test-only probe:
            // an operator holds only the intended row; the executing command still uses CI.
            await ht.RollbackAsync();
            await using var lockHolder=await Open("postgres");await using var lt=await lockHolder.BeginTransactionAsync();
            await Scalar(lockHolder,"SELECT id FROM identity.identities WHERE id=@i FOR UPDATE",("i",who));
            var pending=Scalar(waiter,"SELECT operation.accept_invitation(@id,@s,1)",("id",inv),("s",secret));
            await WaitBlocked(o,waiter.ProcessID,lockHolder.ProcessID);
            var deadline=Stopwatch.StartNew();
            while(!Convert.ToBoolean(await Scalar(o,"SELECT clock_timestamp()>=expires_at FROM tenancy.invitations WHERE id=@id",("id",inv)))){
                Require(deadline.Elapsed<TimeSpan.FromSeconds(3),"Server-clock expiry deadline.");await Task.Delay(10);
            }
            await lt.RollbackAsync();await Expect("P2005",()=>pending);await wt.RollbackAsync();
            Require(Convert.ToInt64(await Scalar(o,"SELECT count(*) FROM identity.identity_consumptions WHERE identity_id=@i",("i",who)))==0,"Expired invitation never consumes identity.");
            timeline.Add(new {proof="K06",waiterPid=waiter.ProcessID,blockerPid=lockHolder.ProcessID,eventName="blocking observed; server clock crossed expiry before lock release"});
        });
        await Proof("K09","Owner-and-self-revocation-denied",async()=>{
            await using var c=await Open("core_tenant");await using var t=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);
            await Expect("42501",()=>Scalar(c,"SELECT operation.revoke_membership(@id,1)",("id",memberA)));await t.RollbackAsync();
            await using var o=await Open("postgres");
            Require(Convert.ToString(await Scalar(o,"SELECT role FROM tenancy.memberships WHERE id=@m",("m",memberA)))=="Owner","Owner unchanged.");
        });
        await Proof("K10","pool-after-actual-SQL-abort",async()=>{
            await using var data=NpgsqlDataSource.Create(Connection("core_tenant",credentials["core_tenant"],true));
            int pid;
            await using(var c=await data.OpenConnectionAsync()){
                pid=c.ProcessID;await using var t=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);
                await Expect("P2007",()=>Scalar(c,"SELECT operation.update_demo(@id,99999,'not-applied')",("id",demoA)));await t.RollbackAsync();
            }
            await using(var c=await data.OpenConnectionAsync()){
                Require(c.ProcessID==pid,"Actual failed command backend reused.");
                Require(Convert.ToInt64(await Scalar(c,"SELECT count(*) FROM sandbox.demo_records"))==0,"No context after real command rollback.");
                await using var t=await c.BeginTransactionAsync();await Context(c,ownerB,accountB,orgB);
                Require(Convert.ToString(await Scalar(c,"SELECT title FROM sandbox.demo_records"))=="B-canary","B isolated on same failed-command backend.");await t.RollbackAsync();
            }
            timeline.Add(new {proof="K10",pid,eventName="same physical backend reused after real command failure and rollback"});
        });
    }

    async Task RemainingSqlProofs()
    {
        await Proof("F06","fixed-path-and-denied-shadow-object",async()=>{
            await using var observer=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname IN ('authz','operation') AND NOT coalesce(p.proconfig @> ARRAY['search_path=pg_catalog, pg_temp'],false)"))==0,"All test functions pin search_path.");
            foreach(var role in new[]{"core_identity","core_tenant","core_platform"}){
                await using var c=await Open(role);
                await using(var tx=await c.BeginTransactionAsync()){
                    await Expect("42501",()=>Scalar(c,"CREATE TABLE public.memberships(id uuid)"));await tx.RollbackAsync();
                }
                await using(var tx=await c.BeginTransactionAsync()){
                    await Expect("42501",()=>Scalar(c,"CREATE TEMP TABLE memberships(id uuid)"));await tx.RollbackAsync();
                }
            }
        });
        await Proof("F09","private-audit-receipts-direct-denials",async()=>{
            foreach(var role in new[]{"core_identity","core_tenant","core_platform"}){
                await using var c=await Open(role);
                foreach(var statement in new[]{"SELECT count(*) FROM audit.events","DELETE FROM audit.events","UPDATE audit.events SET action='tampered'","SELECT count(*) FROM operation.idempotency_records","DELETE FROM operation.idempotency_records","SELECT authz.validate_integrity()"}){
                    await using var tx=await c.BeginTransactionAsync();
                    await Expect("42501",()=>Scalar(c,statement));await tx.RollbackAsync();
                }
            }
        });
        await Proof("H01","revoked-identity-cannot-accept-other-enterprise",async()=>{
            var who=Guid.NewGuid();var inv=Guid.NewGuid();var other=Guid.NewGuid();
            var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));Guid aid,mid;
            await using var observer=await Open("postgres");await Identity(observer,who);await Invite(observer,inv,who,orgA,secret);
            await using(var c=await Open("core_identity")){await using var tx=await c.BeginTransactionAsync();await Context(c,who,null,null);await Scalar(c,"SELECT operation.accept_invitation(@id,@s,1)",("id",inv),("s",secret));await tx.CommitAsync();}
            aid=await Id(observer,"SELECT id FROM identity.enterprise_user_accounts WHERE identity_id=@i",("i",who));
            mid=await Id(observer,"SELECT id FROM tenancy.memberships WHERE account_id=@a",("a",aid));
            await using(var c=await Open("core_tenant")){await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);await Scalar(c,"SELECT operation.revoke_membership(@id,1)",("id",mid));await tx.CommitAsync();}
            // Each invitation has a globally unique secret hash, including fixtures.
            var otherSecret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await Invite(observer,other,who,orgB,otherSecret);
            await using(var c=await Open("core_identity")){await using var tx=await c.BeginTransactionAsync();await Context(c,who,null,null);await Expect("P2002",()=>Scalar(c,"SELECT operation.accept_invitation(@id,@s,1)",("id",other),("s",otherSecret)));await tx.RollbackAsync();}
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM identity.identity_consumptions WHERE identity_id=@i AND organization_id=@o AND account_id=@a",("i",who),("o",orgA),("a",aid)))==1,"Original permanent consumption retained after revocation.");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM identity.enterprise_user_accounts WHERE identity_id=@i AND organization_id=@o",("i",who),("o",orgB)))==0,"No new account in other enterprise.");
        });
    }

    async Task<(Guid Identity,Guid Account,Guid Member)> NewMemberFixture()
    {
        var who=Guid.NewGuid();var inv=Guid.NewGuid();var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var observer=await Open("postgres");await Identity(observer,who);await Invite(observer,inv,who,orgA,secret);
        await using(var c=await Open("core_identity")){await using var tx=await c.BeginTransactionAsync();await Context(c,who,null,null);await Scalar(c,"SELECT operation.accept_invitation(@id,@s,1)",("id",inv),("s",secret));await tx.CommitAsync();}
        var aid=await Id(observer,"SELECT id FROM identity.enterprise_user_accounts WHERE identity_id=@i",("i",who));
        var mid=await Id(observer,"SELECT id FROM tenancy.memberships WHERE account_id=@a",("a",aid));
        return(who,aid,mid);
    }
    async Task AuditCorrectionProofs()
    {
        await Proof("V04","owner-transitions-and-role-promotion-denied",async()=>{
            var f=await NewMemberFixture();
            // Privileged operator sets up a NOLOGIN owner's execution context, not a runtime authorization proof.
            await using(var c=await Open("postgres")){
                await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);
                await Command(c,"SET LOCAL ROLE exec_tenant");
                await Expect("42501",()=>Scalar(c,"UPDATE tenancy.memberships SET role='Owner' WHERE id=@m",("m",f.Member)));await tx.RollbackAsync();
            }
            await using(var c=await Open("g1_probe")){
                await using var tx=await c.BeginTransactionAsync();await IntegrityContext(c,orgA);
                await Expect("P1006",()=>Scalar(c,"UPDATE tenancy.memberships SET role='Owner' WHERE id=@m",("m",f.Member)));await tx.RollbackAsync();
            }
            await using(var c=await Open("core_tenant")){await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);await Scalar(c,"SELECT operation.revoke_membership(@m,1)",("m",f.Member));await tx.CommitAsync();}
            foreach(var target in new[]{("identity.enterprise_user_accounts",f.Account),("tenancy.memberships",f.Member)}){
                await using var c=await Open("postgres");await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);await IntegrityContext(c,orgA);
                await Command(c,"SET LOCAL ROLE exec_tenant");
                await Expect("P1006",()=>Scalar(c,"UPDATE "+target.Item1+" SET status='Active',version=version+1 WHERE id=@id",("id",target.Item2)));await tx.RollbackAsync();
            }
            await using var o=await Open("postgres");
            Require(Convert.ToString(await Scalar(o,"SELECT status||':'||role FROM tenancy.memberships WHERE id=@m",("m",f.Member)))=="Revoked:Member","No reactivation/promotion persisted.");
            Require(Convert.ToString(await Scalar(o,"SELECT status FROM identity.enterprise_user_accounts WHERE id=@a",("a",f.Account)))=="Revoked","Account remains revoked.");
        });
        await Proof("F01","minimal-grants-and-secret-read-denied",async()=>{
            await using var o=await Open("postgres");
            foreach(var query in new[]{
                "SELECT has_column_privilege('exec_tenant','tenancy.invitations','secret_hash','SELECT')",
                "SELECT has_column_privilege('exec_tenant','tenancy.memberships','role','UPDATE')",
                "SELECT has_table_privilege('exec_tenant','sandbox.demo_records','INSERT')",
                "SELECT has_column_privilege('exec_integrity','identity.identity_consumptions','identity_id','SELECT')",
                "SELECT has_column_privilege('exec_identity','identity.enterprise_user_accounts','version','UPDATE')",
                "SELECT has_column_privilege('exec_identity','tenancy.memberships','version','UPDATE')",
                "SELECT has_table_privilege('exec_bootstrap','identity.enterprise_user_accounts','SELECT')",
                "SELECT has_table_privilege('exec_bootstrap','tenancy.memberships','SELECT')"})Require(!Convert.ToBoolean(await Scalar(o,query)),"Removed grant is absent: "+query);
            await using var tx=await o.BeginTransactionAsync();await Context(o,ownerA,accountA,orgA);await Command(o,"SET LOCAL ROLE exec_tenant");
            await Expect("42501",()=>Scalar(o,"SELECT secret_hash FROM tenancy.invitations"));await tx.RollbackAsync();
        });
        await Proof("V11","revocation-versus-owner-reactivation-after-wait",async()=>{
            foreach(var membershipTarget in new[]{false,true}){
            var f=await NewMemberFixture();
            await using var runtime=await Open("core_tenant");await using var attacker=await Open("postgres");await using var observer=await Open("postgres");
            await using var rt=await runtime.BeginTransactionAsync();await using var at=await attacker.BeginTransactionAsync();
            await Context(runtime,ownerA,accountA,orgA);await Context(attacker,ownerA,accountA,orgA);await IntegrityContext(attacker,orgA);await Command(attacker,"SET LOCAL ROLE exec_tenant");
            await Scalar(runtime,"SELECT operation.revoke_membership(@m,1)",("m",f.Member));
            var pending=Scalar(attacker,"UPDATE "+(membershipTarget?"tenancy.memberships":"identity.enterprise_user_accounts")+" SET status='Active',version=version+1 WHERE id=@a",("a",membershipTarget?f.Member:f.Account));
            await WaitBlocked(observer,attacker.ProcessID,runtime.ProcessID);await rt.CommitAsync();await Expect("P1006",()=>pending);await at.RollbackAsync();
            Require(Convert.ToString(await Scalar(observer,"SELECT status FROM identity.enterprise_user_accounts WHERE id=@a",("a",f.Account)))=="Revoked","After lock wait OLD reflects committed revoked state, no resurrection.");
            Require(Convert.ToString(await Scalar(observer,"SELECT status FROM tenancy.memberships WHERE id=@m",("m",f.Member)))=="Revoked","Membership remains revoked after attempted concurrent reactivation.");
            timeline.Add(new {proof="V11",membershipTarget,runtimePid=runtime.ProcessID,ownerProbePid=attacker.ProcessID,eventName="Real runtime revoke versus direct owner-row mutation; not proof of complete C1/last-Owner protocol"});
            }
        });
    }

    async Task ExpectExact(NpgsqlConnection c,string sql,string state,string message,string role,Func<Task<object?>> action,string routine="exec_stmt_raise")
    {
        var session=c.UserName;var effective=Convert.ToString(await Scalar(c,"SELECT current_user"));
        try{await action();}catch(PostgresException e){
            errorContexts.TryRemove(e,out var origin);
            var ok=e.SqlState==state && e.MessageText==message && e.Routine==routine && effective==role
                && (origin.Sql==null || origin.Sql==sql) && (origin.Effective==null || origin.Effective==role);
            observations.Add(new {proof=activeProof,expectedState=state,observedState=e.SqlState,expectedMessage=message,observedMessage=e.MessageText,
                expectedRole=role,sessionUser=session,effectiveRole=effective,operation=sql,table=e.TableName,column=e.ColumnName,constraint=e.ConstraintName,routine=e.Routine,
                executionMode=session=="postgres"?"Privileged connection SET ROLE owner probe or operator fixture; not runtime login proof":"Real runtime login; operation.* invokes SECURITY DEFINER",
                diagnosticBaseline="PostgreSQL 17.11; exact routine version-specific",matched=ok});
            Require(ok,"Exact error state/message/role/statement origin matched.");return;
        }
        throw new SafetyFailure("Expected exact rejection did not occur.");
    }
    async Task P1ClosureProofs()
    {
        Guid secondOwner=Guid.NewGuid(),secondAccount=Guid.NewGuid(),secondMember=Guid.NewGuid();
        await using(var o=await Open("postgres")){
            await Identity(o,secondOwner);await using var tx=await o.BeginTransactionAsync();await IntegrityContext(o,orgA);
            await Command(o,"INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id) VALUES(@i,@o,@a)",("i",secondOwner),("o",orgA),("a",secondAccount));
            await Command(o,"INSERT INTO identity.enterprise_user_accounts(id,identity_id,organization_id,status) VALUES(@a,@i,@o,'Active')",("a",secondAccount),("i",secondOwner),("o",orgA));
            await Command(o,"INSERT INTO tenancy.memberships(id,organization_id,account_id,role,status) VALUES(@m,@o,@a,'Owner','Active')",("m",secondMember),("o",orgA),("a",secondAccount));await tx.CommitAsync();
            observations.Add(new {proof="K09",fixture="Privileged insertion of second initial Owner, not succession or runtime provisioning"});
        }
        await Proof("K09","two-active-Owners-direct-owner-revocation-denied",async()=>{
            foreach(var accountFirst in new[]{false,true}){
                await using var c=await Open("postgres");await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);await IntegrityContext(c,orgA);await Command(c,"SET LOCAL ROLE exec_tenant");
                if(accountFirst)await Command(c,"UPDATE identity.enterprise_user_accounts SET status='Revoked',version=version+1 WHERE id=@a",("a",secondAccount));
                const string sql="UPDATE tenancy.memberships SET status='Revoked',version=version+1 WHERE id=@m";
                await ExpectExact(c,sql,"P1007","Integrity.OwnerRevocationDenied","exec_tenant",()=>Scalar(c,sql,("m",secondMember)));await tx.RollbackAsync();
            }
            await using(var c=await Open("core_tenant")){await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);
                const string sql="SELECT operation.revoke_membership(@m,1)";
                await ExpectExact(c,sql,"42501","Access.Denied","core_tenant",()=>Scalar(c,sql,("m",secondMember)));await tx.RollbackAsync();}
            await using var o=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(o,"SELECT count(*) FROM tenancy.memberships m JOIN identity.enterprise_user_accounts a ON a.id=m.account_id WHERE m.organization_id=@o AND m.role='Owner' AND m.status='Active' AND a.status='Active'",("o",orgA)))==2,"Two Owners and accounts remain Active, including rollback of account-first attempt.");
        });
        await Proof("F09","invalid-invitation-inserts-issuer-and-chain",async()=>{
            var recipient=Guid.NewGuid();var original=Guid.NewGuid();var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await using(var o=await Open("postgres")){await Identity(o,recipient);await Invite(o,original,recipient,orgA,secret);}
            const string insert="INSERT INTO tenancy.invitations(id,organization_id,recipient_identity_id,issuer_membership_id,secret_hash,status,expires_at,predecessor_id) VALUES(@id,@o,@i,@m,sha256(convert_to(@s,'UTF8')),@st,clock_timestamp()+interval '1 hour',@pred) RETURNING id";
            foreach(var variant in new[]{"accepted","issuer","consumed","pending-predecessor","initial-unimplemented"}){
                await using var c=await Open("postgres");await using var tx=await c.BeginTransactionAsync();
                var target=variant=="consumed"?ownerB:recipient;await Context(c,ownerA,accountA,orgA);await Command(c,"SELECT set_config('app.recipient_id',@i,true)",("i",target.ToString()));await Command(c,"SET LOCAL ROLE exec_tenant");
                var state=variant=="accepted"?"P1010":variant=="issuer"?"P1011":variant=="consumed"?"P1012":variant=="initial-unimplemented"?"P1013":"P1014";
                var message=variant=="accepted"?"Invitation.InvalidInsert":variant=="issuer"?"Invitation.IssuerDenied":variant=="consumed"?"Invitation.RecipientIneligible":variant=="initial-unimplemented"?"Invitation.InitialIssueUnavailable":"Invitation.InvalidChain";
                await ExpectExact(c,insert,state,message,"exec_tenant",()=>Scalar(c,insert,("id",Guid.NewGuid()),("o",orgA),("i",target),("m",variant=="issuer"?secondMember:memberA),("s",Guid.NewGuid().ToString("N")),("st",variant=="accepted"?"Accepted":"Pending"),("pred",variant=="initial-unimplemented"?DBNull.Value:original)));await tx.RollbackAsync();
            }
            foreach(var forbidden in new[]{"accepted_account_id","successor_id"}){
                await using var c=await Open("postgres");await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);await Command(c,"SET LOCAL ROLE exec_tenant");
                var sql="INSERT INTO tenancy.invitations("+forbidden+") VALUES(@v)";
                await ExpectExact(c,sql,"42501","permission denied for table invitations","exec_tenant",()=>Scalar(c,sql,("v",accountA)),"aclcheck_error");await tx.RollbackAsync();
            }
            await using(var c=await Open("postgres")){
                await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);await Command(c,"SET LOCAL ROLE exec_tenant");
                const string sql="UPDATE tenancy.invitations SET successor_id=@s WHERE id=@id";
                await ExpectExact(c,sql,"P1014","Invitation.InvalidChain","exec_tenant",()=>Scalar(c,sql,("s",original),("id",original)));await tx.RollbackAsync();
            }
            await using(var c=await Open("postgres")){
                await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);await Command(c,"SET LOCAL ROLE exec_tenant");
                await IntegrityContext(c,orgA);
                await Command(c,"UPDATE tenancy.invitations SET status='Replaced',version=version+1 WHERE id=@id",("id",original));
                await ExpectExact(c,"COMMIT","P1014","Invitation.IncompleteChain","exec_tenant",async()=>{await tx.CommitAsync();return null;});
            }
            await using var observer=await Open("postgres");
            Require(Convert.ToString(await Scalar(observer,"SELECT status||':'||version FROM tenancy.invitations WHERE id=@id",("id",original)))=="Pending:1","Invalid inserts and chain mutations rolled back.");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM tenancy.invitations WHERE recipient_identity_id=@i",("i",recipient)))==1,"Initial fixture with NULL predecessor remains only invitation; no invalid insertion persisted.");
        });
    }

    async Task RejectAcl(NpgsqlConnection c,string sql,string objectName)
    {
        var effective=Convert.ToString(await Scalar(c,"SELECT current_user"));
        try{await Scalar(c,sql);}
        catch(PostgresException e){
            errorContexts.TryRemove(e,out var context);
            var ok=e.SqlState=="42501" && e.Routine!=null && e.Routine!="exec_stmt_raise"
                && e.MessageText.Contains(objectName,StringComparison.Ordinal) && context.Sql==sql && context.Effective==effective;
            observations.Add(new {proof=activeProof,statement=sql,expectedObject=objectName,role=effective,session=c.UserName,
                sqlstate=e.SqlState,message=e.MessageText,routine=e.Routine,table=e.TableName,column=e.ColumnName,constraint=e.ConstraintName,matched=ok});
            Require(ok,"Native ACL diagnostic/object/effective role/statement identity match.");return;
        }
        throw new SafetyFailure("Unexpected privilege success: "+objectName);
    }
    async Task G1ClosingProofs()
    {
        await Proof("F01","G1-runtime-table-operation-matrix",async()=>{
            await using var o=await Open("postgres");
            var objects=new List<(string Table,string Column)>();
            await using(var cmd=new NpgsqlCommand("SELECT c.oid::regclass::text,(SELECT attname FROM pg_attribute WHERE attrelid=c.oid AND attnum>0 AND NOT attisdropped ORDER BY attnum LIMIT 1) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN('identity','tenancy','sandbox','audit','operation') AND c.relkind='r' ORDER BY 1",o)){
                await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())objects.Add((reader.GetString(0),reader.GetString(1)));
            }
            foreach(var role in new[]{"core_identity","core_tenant","core_platform"}){
                await using var c=await Open(role);
                foreach(var obj in objects){
                    foreach(var sql in new[]{"SELECT * FROM "+obj.Table+" LIMIT 0","INSERT INTO "+obj.Table+" DEFAULT VALUES","UPDATE "+obj.Table+" SET "+obj.Column+"="+obj.Column+" WHERE false","DELETE FROM "+obj.Table+" WHERE false"}){
                        await using var tx=await c.BeginTransactionAsync();
                        if(role=="core_tenant" && obj.Table=="sandbox.demo_records" && sql.StartsWith("SELECT")){
                            await Context(c,ownerA,accountA,orgA);await Scalar(c,sql);Require(Convert.ToInt64(await Scalar(c,"SELECT count(*) FROM sandbox.demo_records"))==1,"Real runtime SELECT sees exactly its organization canary.");
                        }else await RejectAcl(c,sql,obj.Table.Split('.')[1]);
                        await tx.RollbackAsync();
                    }
                }
            }
            Require(Convert.ToInt64(await Scalar(o,"SELECT count(*) FROM sandbox.demo_records"))==2,"ACL matrix leaves both canaries unchanged.");
        });
        await Proof("F02","G1-DDL-TRUNCATE-protections-denied",async()=>{
            foreach(var role in new[]{"core_identity","core_tenant","core_platform"}){
                await using var c=await Open(role);
                foreach(var sql in new[]{"TRUNCATE sandbox.demo_records","ALTER TABLE sandbox.demo_records DISABLE ROW LEVEL SECURITY","ALTER TABLE tenancy.memberships DISABLE TRIGGER ALL","ALTER TABLE sandbox.demo_records OWNER TO core_tenant"}){
                    await using var tx=await c.BeginTransactionAsync();await RejectAcl(c,sql,sql.Contains("memberships")?"memberships":"demo_records");await tx.RollbackAsync();
                }
            }
        });
        await Proof("F03","G1-every-ungranted-function-runtime-call",async()=>{
            var denied=new List<(string Role,string Signature,string Name)>();
            await using(var o=await Open("postgres")){
                await using var cmd=new NpgsqlCommand("SELECT r.rolname,p.oid::regprocedure::text,p.proname FROM pg_roles r CROSS JOIN pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE r.rolname IN('core_identity','core_tenant','core_platform') AND n.nspname IN('authz','operation') AND NOT has_function_privilege(r.oid,p.oid,'EXECUTE') ORDER BY 1,2",o);
                await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())denied.Add((reader.GetString(0),reader.GetString(1),reader.GetString(2)));
            }
            foreach(var item in denied){
                await using var c=await Open(item.Role);await using var tx=await c.BeginTransactionAsync();
                var types=item.Signature.Split('(')[1].TrimEnd(')');var arguments=types.Length==0?"":string.Join(",",types.Split(',').Select(t=>"NULL::"+t));
                await RejectAcl(c,"SELECT "+item.Signature.Split('(')[0]+"("+arguments+")",item.Name);await tx.RollbackAsync();
            }
            await using var observer=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM pg_default_acl d CROSS JOIN LATERAL aclexplode(d.defaclacl) a WHERE d.defaclobjtype='f' AND a.grantee=0 AND a.privilege_type='EXECUTE'"))==0,"No PUBLIC function execution in installed default ACL.");
        });
        await Proof("F04","G1-SET-ROLE-and-self-grant-denied",async()=>{
            foreach(var role in new[]{"core_identity","core_tenant","core_platform"}){
                await using var c=await Open(role);
                foreach(var target in new[]{"exec_identity","exec_tenant","exec_bootstrap","exec_context","exec_integrity","ddl_owner","core_migrator"}){
                    await using var tx=await c.BeginTransactionAsync();await RejectAcl(c,"SET LOCAL ROLE "+target,target);await tx.RollbackAsync();
                }
                await using var grantTx=await c.BeginTransactionAsync();await RejectAcl(c,"GRANT exec_tenant TO "+role,"exec_tenant");await grantTx.RollbackAsync();
            }
        });
        await Proof("F05","G1-installed-T-SELF-REC-BOOT-boundaries",async()=>{
            var f=await NewMemberFixture();
            await using(var c=await Open("core_tenant")){
                await using var tx=await c.BeginTransactionAsync();await Context(c,f.Identity,f.Account,orgA);
                Require(Convert.ToBoolean(await Scalar(c,"SELECT authz.own_tenant_allowed(false)")),"Active Member has own read context.");
                Require(!Convert.ToBoolean(await Scalar(c,"SELECT authz.own_tenant_allowed(true)")),"Member lacks write authority.");
                await Context(c,f.Identity,accountB,orgA);
                Require(!Convert.ToBoolean(await Scalar(c,"SELECT authz.own_tenant_allowed(false)")),"Mismatched identity/account cannot resolve T.");await tx.RollbackAsync();
            }
            await using(var c=await Open("core_tenant")){await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);await Scalar(c,"SELECT operation.revoke_membership(@m,1)",("m",f.Member));await tx.CommitAsync();}
            await using(var c=await Open("core_tenant")){await using var tx=await c.BeginTransactionAsync();await Context(c,f.Identity,f.Account,orgA);Require(!Convert.ToBoolean(await Scalar(c,"SELECT authz.own_tenant_allowed(false)")),"New statement after committed revocation loses read authority.");await tx.RollbackAsync();}
            await using var o=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(o,"SELECT count(*) FROM identity.enterprise_user_accounts WHERE identity_id=@i",("i",platform)))==0,"Platform actor has no implicit tenant account.");
        });
        await Proof("V06","G1-event-coverage-multiple-updates-and-immediate-check",async()=>{
            await using var o=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(o,"SELECT count(*) FROM pg_trigger WHERE tgname IN('validate_accounts','validate_members','validate_org') AND tgdeferrable AND tginitdeferred AND (tgtype & 4)=4 AND (tgtype & 16)=16 AND tgenabled='O'"))==3,"All three final-state validators cover INSERT and UPDATE, enabled and deferred.");
            var f=await NewMemberFixture();
            await using(var c=await Open("g1_probe")){
                await using var tx=await c.BeginTransactionAsync();await IntegrityContext(c,orgA);
                await Scalar(c,"SELECT id FROM tenancy.organizations WHERE id=@o FOR UPDATE",("o",orgA));
                await Command(c,"UPDATE identity.enterprise_user_accounts SET version=version+1 WHERE id=@a",("a",f.Account));
                await Command(c,"UPDATE identity.enterprise_user_accounts SET version=version+1 WHERE id=@a",("a",f.Account));
                await Command(c,"SET CONSTRAINTS ALL IMMEDIATE");await tx.CommitAsync();
            }
            Require(Convert.ToInt64(await Scalar(o,"SELECT version FROM identity.enterprise_user_accounts WHERE id=@a",("a",f.Account)))==3,"Multiple queued events read final coherent state.");
            await using(var c=await Open("g1_probe")){
                await using var tx=await c.BeginTransactionAsync();await IntegrityContext(c,orgA);
                await Command(c,"UPDATE identity.enterprise_user_accounts SET status='Revoked' WHERE id=@a",("a",f.Account));
                await ExpectExact(c,"SET CONSTRAINTS ALL IMMEDIATE","P1003","Integrity.AccountMembership","g1_probe",()=>Scalar(c,"SET CONSTRAINTS ALL IMMEDIATE"));await tx.RollbackAsync();
            }
            foreach(var table in new[]{"identity.enterprise_user_accounts","tenancy.memberships","tenancy.organizations","identity.identity_consumptions","identity.identity_links"}){
                await using var tx=await o.BeginTransactionAsync();
                await ExpectExact(o,"DELETE FROM "+table,"P1005","Integrity.Immutable","postgres",()=>Scalar(o,"DELETE FROM "+table));await tx.RollbackAsync();
            }
            Require(Convert.ToString(await Scalar(o,"SELECT status FROM identity.enterprise_user_accounts WHERE id=@a",("a",f.Account)))=="Active","Early invalid constraint check leaves no partial revoke.");
        });
        await Proof("V09","G1-validators-no-explicit-locks-or-repair",async()=>{
            await using var o=await Open("postgres");
            foreach(var name in new[]{"authz.validate_integrity()","authz.validate_invitation_chain()"}){
                var body=Convert.ToString(await Scalar(o,"SELECT prosrc FROM pg_proc WHERE oid=@name::regprocedure",("name",name)))!;
                body=System.Text.RegularExpressions.Regex.Replace(body,@"--[^\r\n]*","");
                Require(!System.Text.RegularExpressions.Regex.IsMatch(body,@"\b(FOR\s+(UPDATE|SHARE|NO\s+KEY|KEY\s+SHARE)|pg_advisory|INSERT\s+INTO|UPDATE\s+[a-z_]+\.|DELETE\s+FROM|EXECUTE)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase),"Read-only technical validator has no explicit row/advisory lock, repair or dynamic command: "+name);
            }
            var f=await NewMemberFixture();
            await using var c=await Open("core_tenant");await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);
            await Scalar(c,"SELECT operation.revoke_membership(@m,1)",("m",f.Member));
            var waiting=Scalar(o,"SELECT id FROM tenancy.organizations WHERE id=@o FOR UPDATE",("o",orgA));
            await using var inspector=await Open("postgres");await WaitBlocked(inspector,o.ProcessID,c.ProcessID);
            timeline.Add(new {proof=activeProof,phase="before COMMIT",writerPid=c.ProcessID,observerPid=o.ProcessID,eventName="Organization lock already held by command before deferred validators"});
            await tx.CommitAsync();await waiting;
            Require(Convert.ToString(await Scalar(inspector,"SELECT status FROM identity.enterprise_user_accounts WHERE id=@a",("a",f.Account)))=="Revoked","Deferred commit completes coherent revoke after pre-acquired org lock.");
        });
        await Proof("F10","G1-semantic-capability-subtraction-probe",async()=>{
            var who=Guid.NewGuid();var inv=Guid.NewGuid();var code=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await using(var o=await Open("postgres")){await Identity(o,who);await Invite(o,inv,who,orgA,code);}
            await using(var c=await Open("postgres")){
                await using var tx=await c.BeginTransactionAsync();
                await Command(c,"REVOKE SELECT(predecessor_id,successor_id) ON tenancy.invitations FROM exec_identity");
                Require(!Convert.ToBoolean(await Scalar(c,"SELECT has_column_privilege('exec_identity','tenancy.invitations','predecessor_id','SELECT') OR has_column_privilege('exec_identity','tenancy.invitations','successor_id','SELECT')")),"Both identity chain reads are effectively absent before subtraction probe.");
                await Command(c,"SET LOCAL SESSION AUTHORIZATION core_identity");await Context(c,who,null,null);
                var first=await Scalar(c,"SELECT operation.accept_invitation(@i,@s,1)",("i",inv),("s",code));
                var replay=await Scalar(c,"SELECT operation.accept_invitation(@i,@s,1)",("i",inv),("s",code));
                Require(Equals(first,replay),"Accept and authorized replay do not need identity-owner predecessor/successor SELECT.");
                await Command(c,"SET CONSTRAINTS ALL IMMEDIATE");await tx.RollbackAsync();
            }
            var f=await NewMemberFixture();
            await using(var c=await Open("postgres")){
                await using var tx=await c.BeginTransactionAsync();await Command(c,"REVOKE SELECT(organization_id,status) ON identity.enterprise_user_accounts FROM exec_tenant");
                Require(!Convert.ToBoolean(await Scalar(c,"SELECT has_column_privilege('exec_tenant','identity.enterprise_user_accounts','organization_id','SELECT') OR has_column_privilege('exec_tenant','identity.enterprise_user_accounts','status','SELECT')")),"Both tenant account reads are effectively absent before subtraction probe.");
                await Command(c,"SET LOCAL SESSION AUTHORIZATION core_tenant");await Context(c,ownerA,accountA,orgA);
                await Scalar(c,"SELECT operation.update_demo(@id,1,'semantic-probe')",("id",demoA));
                await Scalar(c,"SELECT operation.revoke_membership(@m,1)",("m",f.Member));
                await Scalar(c,"SELECT operation.replace_invitation(@i,1,'semantic-probe',@s)",("i",inv),("s",code+"-new"));
                await Command(c,"SET CONSTRAINTS ALL IMMEDIATE");await tx.RollbackAsync();
            }
            await Write("semantic-subtraction.json",new {run,executionMode="Isolated superuser transaction locally revokes four SELECT columns and SET LOCAL SESSION AUTHORIZATION simulates command session; NOT real runtime credential proof",columns=new[]{"exec_identity:tenancy.invitations.predecessor_id:SELECT","exec_identity:tenancy.invitations.successor_id:SELECT","exec_tenant:identity.enterprise_user_accounts.organization_id:SELECT","exec_tenant:identity.enterprise_user_accounts.status:SELECT"},operations="accept/replay, update_demo, revoke_membership, replace and immediate final validation succeeded; entire changes rolled back",classification="Current bodies do not require these four READ capabilities; no escalation or cross-tenant leak demonstrated"});
            await using var observer=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT version FROM sandbox.demo_records WHERE id=@id",("id",demoA)))==1,"Subtraction probe leaves Demo unchanged.");
            Require(Convert.ToString(await Scalar(observer,"SELECT status FROM tenancy.invitations WHERE id=@id",("id",inv)))=="Pending","Accept/replace probe leaves original pending.");
        });
        await Proof("F11","G1-reduced-grants-real-runtime-ports-and-replay",async()=>{
            var who=Guid.NewGuid();var inv=Guid.NewGuid();var code=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var nextCode=code+"-replacement";object? successor;
            await using(var o=await Open("postgres")){await Identity(o,who);await Invite(o,inv,who,orgA,code);}
            await using(var c=await Open("core_tenant")){
                await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);
                Require(Convert.ToInt64(await Scalar(c,"SELECT operation.update_demo(@id,1,'runtime-regression')",("id",demoA)))==2,"Actual tenant login updates under reduced grants.");
                successor=await Scalar(c,"SELECT operation.replace_invitation(@i,1,'g1-final',@s)",("i",inv),("s",nextCode));await tx.CommitAsync();
            }
            await using(var c=await Open("core_tenant")){
                await using var tx=await c.BeginTransactionAsync();await Context(c,ownerA,accountA,orgA);
                Require(Equals(successor,await Scalar(c,"SELECT operation.replace_invitation(@i,1,'g1-final',@s)",("i",inv),("s",nextCode))),"Actual tenant login replacement replay returns same successor.");await tx.CommitAsync();
            }
            object? accepted;
            await using(var c=await Open("core_identity")){
                await using var tx=await c.BeginTransactionAsync();await Context(c,who,null,null);
                accepted=await Scalar(c,"SELECT operation.accept_invitation(@i,@s,1)",("i",successor!),("s",nextCode));await tx.CommitAsync();
            }
            await using(var c=await Open("core_identity")){
                await using var tx=await c.BeginTransactionAsync();await Context(c,who,null,null);
                Require(Equals(accepted,await Scalar(c,"SELECT operation.accept_invitation(@i,@s,1)",("i",successor!),("s",nextCode))),"Actual identity login authorized accept replay returns same account.");await tx.CommitAsync();
            }
            await using var observer=await Open("postgres");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM tenancy.invitations p JOIN tenancy.invitations s ON s.id=p.successor_id AND s.predecessor_id=p.id JOIN identity.enterprise_user_accounts a ON a.id=s.accepted_account_id WHERE p.id=@i AND p.status='Replaced' AND s.status='Accepted' AND a.identity_id=@who AND a.organization_id=@org",("i",inv),("who",who),("org",orgA)))==1,"Committed replacement chain and accepted account match recipient and organization.");
            Require(Convert.ToInt64(await Scalar(observer,"SELECT count(*) FROM audit.events WHERE actor_id=@who AND action='Accept'",("who",who)))==1,"Authorized accept replay does not duplicate committed audit.");
        });
        await using(var o=await Open("postgres")){
            await Write("function-stats.json",await Rows(o,"SELECT schemaname,funcname,calls FROM pg_stat_user_functions ORDER BY schemaname,funcname"));
            await Write("post-rollback-privileges.json",await Rows(o,"SELECT has_column_privilege('exec_identity','tenancy.invitations','predecessor_id','SELECT') AS identity_predecessor,has_column_privilege('exec_identity','tenancy.invitations','successor_id','SELECT') AS identity_successor,has_column_privilege('exec_tenant','identity.enterprise_user_accounts','organization_id','SELECT') AS tenant_account_org,has_column_privilege('exec_tenant','identity.enterprise_user_accounts','status','SELECT') AS tenant_account_status"));
        }
    }
    static async Task<object> Rows(NpgsqlConnection c,string sql)
    {
        await using var cmd=new NpgsqlCommand(sql,c);await using var reader=await cmd.ExecuteReaderAsync();var rows=new List<Dictionary<string,object?>>();
        while(await reader.ReadAsync()){var row=new Dictionary<string,object?>();for(int i=0;i<reader.FieldCount;i++)row[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(row);}return rows;
    }

    async Task Catalog()
    {
        await using var a=await Open("postgres");
        var sql=await File.ReadAllTextAsync(Path.Combine(package,"sql","inspect-catalog.sql"));
        // Same SELECT catalog queries, without psql-only variable/guard preamble.
        int start=sql.IndexOf("SELECT json_build_object('kind','environment'",StringComparison.Ordinal);
        sql=sql[start..].Replace("ROLLBACK;","",StringComparison.Ordinal);
        await using var tx=await a.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        await Command(a,"SET TRANSACTION READ ONLY; SET LOCAL search_path=pg_catalog,pg_temp");
        await using var cmd=new NpgsqlCommand(sql,a);
        await using var reader=await cmd.ExecuteReaderAsync();var lines=new List<string>();
        do {while(await reader.ReadAsync())lines.Add(reader.GetString(0));}while(await reader.NextResultAsync());
        await reader.CloseAsync();await tx.RollbackAsync();
        await File.WriteAllLinesAsync(Path.Combine(output,"catalog.jsonl"),lines);
        var graph=await ReadGraph(a);
        await Write("graph.json",graph);
    }
    static async Task<object> ReadGraph(NpgsqlConnection a)
    {
        await using var cmd=new NpgsqlCommand(@"SELECT p.oid::regprocedure::text,pg_get_userbyid(p.proowner),pg_get_functiondef(p.oid)
            FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname IN('operation','authz') ORDER BY 1",a);
        await using var r=await cmd.ExecuteReaderAsync();var list=new List<object>();
        while(await r.ReadAsync())list.Add(new {signature=r.GetString(0),owner=r.GetString(1),definition=r.GetString(2),
            reviewStatus="BODY_CAPTURED_NO_INDEPENDENT_HUMAN_REVIEW"});
        return list;
    }
    async Task Bootstrap(Guid who,Guid org,string reference)
    {
        await using var c=await Open("core_platform");await using var tx=await c.BeginTransactionAsync();
        await Context(c,platform,null,null);
        await Scalar(c,"SELECT operation.bootstrap(@i,@o,'synthetic-org',@k,@r)",
            ("i",who),("o",org),("k","bootstrap-"+org),("r",reference));
        await tx.CommitAsync();
    }
    static async Task Identity(NpgsqlConnection c,Guid id)
    {
        await Command(c,"INSERT INTO identity.identities(id,status) VALUES(@id,'Active')",("id",id));
        await Command(c,"INSERT INTO identity.identity_links(id,identity_id,issuer,subject) VALUES(@id,@i,'https://synthetic.invalid',@s)",
            ("id",Guid.NewGuid()),("i",id),("s",id.ToString()));
    }
    static async Task Reference(NpgsqlConnection c,Guid id,string text)=>await Command(c,
        "INSERT INTO identity.recipient_references(identity_id,secret_hash,expires_at) VALUES(@i,sha256(convert_to(@s,'UTF8')),clock_timestamp()+interval '1 hour')",("i",id),("s",text));
    async Task Invite(NpgsqlConnection c,Guid id,Guid who,Guid org,string text,int ttlMilliseconds=3600000)
    {
        await using var fixtureTx=await c.BeginTransactionAsync();await IntegrityContext(c,org);
        var mid=await Id(c,"SELECT m.id FROM tenancy.memberships m JOIN identity.enterprise_user_accounts a ON a.id=m.account_id WHERE a.organization_id=@o AND m.role='Owner'",("o",org));
        await Command(c,@"INSERT INTO tenancy.invitations(id,organization_id,recipient_identity_id,issuer_membership_id,secret_hash,expires_at,status)
            VALUES(@id,@o,@i,@m,sha256(convert_to(@s,'UTF8')),clock_timestamp()+@ttl*interval '1 millisecond','Pending')",
            ("id",id),("o",org),("i",who),("m",mid),("s",text),("ttl",ttlMilliseconds));
        await fixtureTx.CommitAsync();
    }
    async Task Proof(string caseId,string variant,Func<Task> action,bool complete=false,bool characterization=false)
    {
        if(!closing && !new[]{"Q07","Q08","V01","V02","V03","V04","V05","V07","V08","V11","F01","F02","F03","F06","F09","K01","K02","K03","K04","K05","K06","K07","K08","K09","H01"}.Contains(caseId))return;
        activeProof=caseId+"/"+variant;
        var watch=Stopwatch.StartNew();
        try {
            await action();proofs.Add(new {caseId,variant,status=characterization?"CARACTERIZADO":"PASS",milliseconds=watch.ElapsedMilliseconds,evidence="assertions.json plus observer and timeline"});
            results[caseId]=new CaseResult(complete?"PASS":"BLOQUEADO",complete?"Complete SQL invariant and positive fixture control executed.":"SQL subproof passed; original scenario has remaining API/OIDC/variant/review prerequisites. See subproofs.");
        }
        catch(Exception ex) {
            proofs.Add(new {caseId,variant,status="FAIL",exceptionType=ex.GetType().Name,sqlstate=(ex as PostgresException)?.SqlState,
                constraint=(ex as PostgresException)?.ConstraintName,table=(ex as PostgresException)?.TableName,routine=(ex as PostgresException)?.Routine,
                assertion=ex is SafetyFailure?ex.Message:"Unexpected execution error; sanitized."});
            results[caseId]=new CaseResult("FAIL","Unexpected oracle violation; affected stage stopped, no post-failure security weakening.");
            stoppedForP0=true;throw;
        }
    }
    async Task WaitBlocked(NpgsqlConnection observer,int waiter,int blocker)
    {
        var watch=Stopwatch.StartNew();
        while(watch.Elapsed<TimeSpan.FromSeconds(3)){
            if(Convert.ToBoolean(await Scalar(observer,"SELECT @b=ANY(pg_blocking_pids(@w))",("b",blocker),("w",waiter))))return;
            await Task.Delay(20); // Sampling only. Server blocker relation, not elapsed sleep, is the barrier oracle.
        }
        throw new SafetyFailure("Deterministic blocking barrier was not observed.");
    }
    async Task Expect(string state,Func<Task<object?>> call)
    {
        try {await call();}
        catch(PostgresException e) when(e.SqlState==state){
            errorContexts.TryRemove(e,out var context);
            bool originOk=true;
            if(state=="42501"){
                var sql=context.Sql??"";
                (string Message,string Routine) expected=sql.Contains("SET ROLE")
                    ? ("permission denied to set role", "call_string_check_hook")
                    : sql.Contains("CREATE TEMP") ? ("permission denied to create temporary tables", "InitTempTableNamespace")
                    : sql.Contains("CREATE TABLE public.") ? ("permission denied for schema public", "aclcheck_error")
                    : sql.Contains("operation.update_demo") || sql.Contains("operation.revoke_membership") ? ("Access.Denied", "exec_stmt_raise")
                    : sql.Contains("operation.bootstrap") ? ("permission denied for function bootstrap", "aclcheck_error")
                    : sql.Contains("authz.validate_integrity") ? ("permission denied for function validate_integrity", "aclcheck_error")
                    : sql.Contains("tenancy.memberships") && context.Effective=="g1_probe" ? ("new row violates row-level security policy for table \"memberships\"", "ExecWithCheckOptions")
                    : sql.Contains("tenancy.memberships") ? ("permission denied for table memberships", "aclcheck_error")
                    : sql.Contains("audit.events") ? ("permission denied for table events", "aclcheck_error")
                    : sql.Contains("operation.idempotency_records") ? ("permission denied for table idempotency_records", "aclcheck_error")
                    : sql.Contains("secret_hash") ? ("permission denied for table invitations", "aclcheck_error")
                    : sql.Contains("sandbox.demo_records") ? ("permission denied for table demo_records", "aclcheck_error")
                    : ("UNMAPPED_42501", "UNMAPPED");
                var allowedRole=activeProof.StartsWith("V02/")
                    ? context.Effective==((context.Sql??"").Contains("NULL")?"g1_probe":"core_tenant")
                    : activeProof.StartsWith("K03/") || activeProof.StartsWith("K09/") ? context.Effective=="core_tenant"
                    : activeProof.Contains("owner-transitions") || activeProof.Contains("minimal-grants") ? context.Effective=="exec_tenant"
                    : new[]{"core_identity","core_tenant","core_platform"}.Contains(context.Effective);
                originOk=e.MessageText.StartsWith(expected.Message,StringComparison.Ordinal) && e.Routine==expected.Routine && allowedRole;
            }
            if(state=="P1006")originOk=e.Routine=="exec_stmt_raise" && e.MessageText==((context.Sql??"").Contains("SET role=")?"Integrity.RoleImmutable":"Integrity.TransitionDenied") && context.Effective is "exec_tenant" or "g1_probe";
            if(activeProof.StartsWith("V02/")){
                if(state=="23505")originOk &= e.ConstraintName=="memberships_account_id_key" && e.TableName=="memberships";
                if(state=="23502"){
                    var fields=new[]{"id","organization_id","account_id","role","status","version"};
                    var values=(context.Sql??"").Split("VALUES(")[1].Split(')')[0].Split(',');
                    var expectedColumn=fields[Array.IndexOf(values,"NULL")];
                    originOk &= e.TableName=="memberships" && e.Routine=="ExecConstraints" && e.ColumnName==expectedColumn && context.Effective=="g1_probe";
                }
            }
            observations.Add(new {proof=activeProof,expectedSqlstate=state,observedSqlstate=e.SqlState,table=e.TableName,column=e.ColumnName,constraint=e.ConstraintName,routine=e.Routine,message=e.MessageText,
                sessionUser=context.Session,effectiveRole=context.Effective,operation=context.Sql?.Split(' ')[0],statementSha256=context.Sql==null?null:Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(context.Sql))),diagnosticBaseline="PostgreSQL 17.11 only; routine changes require oracle review, not weakened policy",check=originOk?"matched":"ORIGIN_MISMATCH"});
            if(!originOk)throw new SafetyFailure("SQLSTATE matched but exception origin/object/role did not match the PostgreSQL 17.11 probe oracle.");
            return;
        }
        throw new SafetyFailure("Expected SQLSTATE "+state+" was not observed.");
    }
    string Connection(string user,string pwd,bool pool)=>new NpgsqlConnectionStringBuilder{
        Host="127.0.0.1",Port=port,Database=database,Username=user,Password=pwd,
        Pooling=pool,MaxPoolSize=1,MinPoolSize=0,Timeout=5,CommandTimeout=8,
        ApplicationName="bpc-adr0009-g1-"+run,IncludeErrorDetail=false
    }.ConnectionString;
    async Task<NpgsqlConnection> Open(string user)
    {
        var cs=user=="postgres"?adminConnection:Connection(user,credentials[user],false);
        var c=new NpgsqlConnection(cs);await c.OpenAsync();
        await RequireTarget(c);
        timeline.Add(new {proof=activeProof,role=user,pid=c.ProcessID,eventName="physical connection opened"});
        await Command(c,"SET statement_timeout='8s';SET lock_timeout='4s'");
        return c;
    }
    async Task RequireTarget(NpgsqlConnection c)
    {
        Require(c.Host=="127.0.0.1" && c.Port==port && c.Database==database && database=="bpc_adr0009_g1_"+run,"Connection target guard.");
        Require(Convert.ToString(await Scalar(c,"SELECT current_database()"))==database,"Server database identity.");
    }
    static async Task Context(NpgsqlConnection c,Guid identity,Guid? account,Guid? org)
    {
        await Command(c,@"SELECT set_config('app.identity_id',@i,true),set_config('app.account_id',@a,true),
            set_config('app.organization_id',@o,true),set_config('app.recipient_id','',true)",
            ("i",identity.ToString()),("a",account?.ToString()??""),("o",org?.ToString()??""));
    }
    static async Task IntegrityContext(NpgsqlConnection c,Guid org)=>await Command(c,
        @"SELECT set_config('app.integrity_org',@o,true),set_config('app.integrity_tx',pg_current_xact_id()::text,true),
          set_config('app.operation_id',@op,true)",("o",org.ToString()),("op",Guid.NewGuid().ToString()));
    static async Task<Guid> Id(NpgsqlConnection c,string sql,params (string,object)[] values)=>(Guid)(await Scalar(c,sql,values))!;
    static async Task<object?> Scalar(NpgsqlConnection c,string sql,params (string,object)[] values)
    {
        await using var cmd=new NpgsqlCommand(sql,c);foreach(var(k,v)in values)cmd.Parameters.AddWithValue(k,v);
        await using var roleQuery=new NpgsqlCommand("SELECT current_user",c);
        var effective=Convert.ToString(await roleQuery.ExecuteScalarAsync())!;
        try {return await cmd.ExecuteScalarAsync();}
        catch(PostgresException e){errorContexts[e]=(sql,c.UserName!,effective);throw;}
    }
    static async Task Command(NpgsqlConnection c,string sql,params (string,object)[] values)
    {
        await using var cmd=new NpgsqlCommand(sql,c);foreach(var(k,v)in values)cmd.Parameters.AddWithValue(k,v);
        await cmd.ExecuteNonQueryAsync();
    }
    Task Write(string file,object value)=>File.WriteAllTextAsync(Path.Combine(output,file),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
    void Require(bool ok,string message){observations.Add(new {proof=activeProof,assertion=message,observed=ok});if(!ok)throw new SafetyFailure(message);}
    static async Task<string> Docker(string[] arguments,Dictionary<string,string>? env=null)
    {
        var p=new ProcessStartInfo("docker"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        foreach(var a in arguments)p.ArgumentList.Add(a);if(env!=null)foreach(var(k,v)in env)p.Environment[k]=v;
        using var process=Process.Start(p)??throw new SafetyFailure("Docker invocation failed.");
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();var text=await stdout;_ =await stderr;
        if(process.ExitCode!=0)throw new SafetyFailure("Docker operation failed: "+arguments[0]+"; no credential or raw stderr emitted.");
        return text;
    }
}
sealed record CaseResult(string Status,string Detail);
sealed class SafetyFailure(string message):Exception(message);
