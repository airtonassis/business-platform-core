using System.Text.Json;
using Business.Platform.Core.Infrastructure.Persistence;
using Business.Platform.Core.Domain.Tenancy;
using Npgsql;

try {
    using var data=JsonDocument.Parse(await Console.In.ReadToEndAsync());var config=data.RootElement;
    var options=JsonSerializer.Deserialize<PostgresOptions>(config.GetRawText())!;
    if(options.Host!="127.0.0.1" || options.Name!="bpc_audit_local" || options.Username!="bpc_tenant_local")throw new InvalidOperationException();
    await using var pool=new TenantMembershipDataSource(NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(options.ConnectionString()){MaxPoolSize=1}.ConnectionString));
    var store=new PostgresTenantMembershipList(pool);var org=config.GetProperty("Organization").GetGuid();var other=config.GetProperty("OtherOrganization").GetGuid();
    void Check(bool result,string label){if(!result)throw new InvalidOperationException();Console.WriteLine("PASS: "+label);}
    foreach(var actor in new[]{"owner-a","admin-a"}) {
        var result=await store.ReadAsync(new("https://synthetic.invalid",actor),org,25,0,default);
        Check(result.IsSuccess && result.Value.Items.Length==5 && result.Value.Items.All(x=>x.OrganizationId==org),actor+" adapter list");
    }
    foreach(var actor in new[]{"member-a","revoked-a","inactive-a","no-link","missing"}) {
        var result=await store.ReadAsync(new("https://synthetic.invalid",actor),org,25,0,default);Check(result.IsFailure,actor+" denied");
    }
    var bounds=await store.ReadAsync(new("https://synthetic.invalid","owner-a"),org,101,0,default);Check(bounds.IsFailure && bounds.Error.Code=="Membership.InvalidPage","bounds mapping");
    int? backend=null;
    for(var i=0;i<3;i++) {
        var a=await store.ReadAsync(new("https://synthetic.invalid","owner-a"),other,25,0,default);Check(a.IsFailure && a.Error.Code=="Resource.NotFound","foreign denial "+i);
        var b=await store.ReadAsync(new("https://synthetic.invalid","owner-b"),other,25,0,default);Check(b.IsSuccess && b.Value.Items.Length==1 && b.Value.Items[0].OrganizationId==other,"B recovery "+i);
        await using var connection=await pool.Source!.OpenConnectionAsync();var pid=(int)(await new NpgsqlCommand("SELECT pg_backend_pid()",connection).ExecuteScalarAsync())!;
        Check(!backend.HasValue || backend==pid,"same physical backend "+i);backend=pid;
        Check(await new NpgsqlCommand("SELECT nullif(current_setting('app.organization_id',true),'') IS NULL AND nullif(current_setting('app.identity_id',true),'') IS NULL",connection).ExecuteScalarAsync() is true,"context reset "+i);
    }
    return 0;
}catch(Exception error){Console.WriteLine("FAIL: membership list probe "+error.GetType().Name);return 1;}
