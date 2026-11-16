using System.Text.Json;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Tenancy;
using Business.Platform.Core.Infrastructure.Persistence;
using Npgsql;

try {
    using var document=JsonDocument.Parse(await Console.In.ReadToEndAsync());var config=document.RootElement;
    var options=JsonSerializer.Deserialize<PostgresOptions>(config.GetRawText())!;
    if(options.Host!="127.0.0.1" || options.Name!="bpc_audit_local" || options.Username!="bpc_tenant_local") throw new InvalidOperationException();
    await using var pool=new TenantMembershipDataSource(NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(options.ConnectionString()){MaxPoolSize=1}.ConnectionString));
    var store=new PostgresTenantMembershipStore(pool);
    var organization=config.GetProperty("Organization").GetGuid();var membership=config.GetProperty("Membership").GetGuid();
    var owner=new OidcSubject("https://synthetic.invalid","owner-a");
    var read=await store.ExecuteAsync(owner,organization,membership,MembershipAction.Read,0,Guid.Empty,default);
    if(read.IsFailure) throw new InvalidOperationException();
    var promoted=await store.ExecuteAsync(owner,organization,membership,MembershipAction.Promote,read.Value.Version,Guid.NewGuid(),default);
    if(promoted.IsFailure || promoted.Value.Role!="Admin") throw new InvalidOperationException();
    var stale=await store.ExecuteAsync(owner,organization,membership,MembershipAction.Demote,read.Value.Version,Guid.NewGuid(),default);
    if(stale.IsSuccess || stale.Error.Code!="Membership.VersionConflict") throw new InvalidOperationException();
    if((await store.ExecuteAsync(owner,organization,membership,MembershipAction.Demote,promoted.Value.Version,Guid.NewGuid(),default)).IsFailure) throw new InvalidOperationException();
    var orgB=config.GetProperty("OtherOrganization").GetGuid();var memberB=config.GetProperty("OtherMembership").GetGuid();
    int? backend=null;
    for(var i=0;i<4;i++) {
        var foreign=await store.ExecuteAsync(owner,orgB,memberB,MembershipAction.Read,0,Guid.Empty,default);
        if(foreign.IsSuccess || foreign.Error.Code!="Resource.NotFound") throw new InvalidOperationException();
        var b=await store.ExecuteAsync(new("https://synthetic.invalid","owner-b"),orgB,memberB,MembershipAction.Read,0,Guid.Empty,default);
        if(b.IsFailure || b.Value.OrganizationId!=orgB) throw new InvalidOperationException();
        await using var connection=await pool.Source!.OpenConnectionAsync();
        var pid=(int)(await new NpgsqlCommand("SELECT pg_backend_pid()",connection).ExecuteScalarAsync())!;
        if(backend.HasValue && backend!=pid) throw new InvalidOperationException();backend=pid;
        if(await new NpgsqlCommand("SELECT nullif(current_setting('app.organization_id',true),'') IS NULL AND nullif(current_setting('app.account_id',true),'') IS NULL AND nullif(current_setting('app.identity_id',true),'') IS NULL",connection).ExecuteScalarAsync() is not true) throw new InvalidOperationException();
    }
    Console.WriteLine("PASS: dedicated Tenant login, adapter commit/rollback, version conflict, repeated same-backend cross-tenant denial/recovery and clean GUCs.");return 0;
} catch(Exception error) { Console.WriteLine("FAIL: membership probe "+error.GetType().Name);return 1; }
