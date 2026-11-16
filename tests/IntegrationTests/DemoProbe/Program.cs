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
    var store=new PostgresDemoRecordStore(pool);var org=config.GetProperty("Organization").GetGuid();var orgB=config.GetProperty("OtherOrganization").GetGuid();var foreign=config.GetProperty("OtherRecord").GetGuid();
    var owner=new OidcSubject("https://synthetic.invalid","owner-a");var member=new OidcSubject("https://synthetic.invalid","member-a");
    void Check(bool passed,string label) { if(!passed) throw new InvalidOperationException(label);Console.WriteLine("PASS: "+label); }
    var key=Guid.NewGuid();var created=await store.CreateAsync(owner,org,"adapter",key,default);
    Check(created.IsSuccess && !created.Value.Replayed,"adapter create commit");
    var item=created.Value.Item;
    var replay=await store.CreateAsync(owner,org,"adapter",key,default);Check(replay.IsSuccess && replay.Value.Replayed && replay.Value.Item==item,"adapter replay");
    var mismatch=await store.CreateAsync(owner,org,"other",key,default);Check(mismatch.IsFailure && mismatch.Error.Code=="Idempotency.PayloadMismatch","adapter payload mismatch rollback");
    var updated=await store.UpdateAsync(owner,org,item.Id,"adapter-updated",1,default);Check(updated.IsSuccess && updated.Value.Version==2,"adapter update commit");
    var stale=await store.UpdateAsync(owner,org,item.Id,"overwrite",1,default);Check(stale.IsFailure && stale.Error.Code=="DemoRecord.VersionConflict","adapter stale rollback");
    var denied=await store.CreateAsync(member,org,"escalation",Guid.NewGuid(),default);Check(denied.IsFailure && denied.Error.Code=="Access.Forbidden","adapter member write denied");
    var revoked=await store.ReadAsync(new("https://synthetic.invalid","admin-a"),org,item.Id,default);Check(revoked.IsFailure,"adapter revoked identity denied");
    var invalid=await store.ListAsync(owner,org,101,0,default);Check(invalid.IsFailure,"adapter invalid page denied by SQL");
    var page=await store.ListAsync(member,org,25,0,default);Check(page.IsSuccess && page.Value.Items.All(x=>x.OrganizationId==org),"adapter member reads local page");
    int? backend=null;
    for(var i=0;i<4;i++) {
        var cross=await store.ReadAsync(owner,orgB,foreign,default);Check(cross.IsFailure && cross.Error.Code=="Resource.NotFound","adapter cross tenant denial "+i);
        var b=await store.ReadAsync(new("https://synthetic.invalid","owner-b"),orgB,foreign,default);Check(b.IsSuccess && b.Value.OrganizationId==orgB,"adapter recovery B "+i);
        var own=await store.ReadAsync(member,org,item.Id,default);Check(own.IsSuccess && own.Value.Version==2,"adapter recovery A "+i);
        await using var connection=await pool.Source!.OpenConnectionAsync();
        var pid=(int)(await new NpgsqlCommand("SELECT pg_backend_pid()",connection).ExecuteScalarAsync())!;
        Check(!backend.HasValue || backend==pid,"same physical pooled backend "+i);backend=pid;
        Check(await new NpgsqlCommand("SELECT nullif(current_setting('app.organization_id',true),'') IS NULL AND nullif(current_setting('app.identity_id',true),'') IS NULL AND nullif(current_setting('app.account_id',true),'') IS NULL",connection).ExecuteScalarAsync() is true,"transaction context reset "+i);
    }
    return 0;
} catch(Exception error) { Console.WriteLine("FAIL: demo probe "+error.GetType().Name);return 1; }
