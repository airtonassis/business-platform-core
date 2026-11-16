using System.Text.Json;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Tenancy;
using Business.Platform.Core.Infrastructure.Persistence;
using Npgsql;

try {
    using var document=JsonDocument.Parse(await Console.In.ReadToEndAsync());var c=document.RootElement;
    var options=JsonSerializer.Deserialize<PostgresOptions>(c.GetRawText())!;
    if(options.Host!="127.0.0.1" || options.Name!="bpc_audit_local" || options.Username!="bpc_invitation_local")throw new InvalidOperationException();
    var tenantConnection=new NpgsqlConnectionStringBuilder(options.ConnectionString()){MaxPoolSize=1};
    await using var tenant=new TenantMembershipDataSource(NpgsqlDataSource.Create(tenantConnection.ConnectionString));
    tenantConnection.Username=c.GetProperty("IdentityUsername").GetString();
    await using var identity=NpgsqlDataSource.Create(tenantConnection.ConnectionString);
    var store=new PostgresInvitationStore(tenant,identity);var org=c.GetProperty("Organization").GetGuid();var other=c.GetProperty("OtherOrganization").GetGuid();
    var id=c.GetProperty("Invitation").GetGuid();var hash=Convert.FromHexString(c.GetProperty("HashHex").GetString()!);
    var recipient=new OidcSubject("https://synthetic.invalid","recipient-probe");var owner=new OidcSubject("https://synthetic.invalid","owner-a");
    if((await store.ManageAsync(owner,new(org,"Get",id),default)).IsFailure)throw new InvalidOperationException();
    if((await store.RecipientAsync(recipient,id,hash,false,0,default)).IsFailure)throw new InvalidOperationException();
    var accepted=await store.RecipientAsync(recipient,id,hash,true,1,default);
    if(accepted.IsFailure || accepted.Value.Invitation.State!="Accepted")throw new InvalidOperationException();
    var replay=await store.RecipientAsync(recipient,id,hash,true,0,default);
    if(replay.IsFailure || !replay.Value.Replayed || accepted.Value.Invitation.AccountId!=replay.Value.Invitation.AccountId)throw new InvalidOperationException();
    var denied=await store.RecipientAsync(owner,id,hash,true,1,default);
    if(denied.IsSuccess || denied.Error.Code!="Invitation.Unavailable")throw new InvalidOperationException();
    var mismatch=await store.ManageAsync(owner,new(org,"Issue",RecipientUserId:c.GetProperty("Recipient").GetGuid(),ReferenceHash:new byte[32],SecretHash:new byte[32],Key:c.GetProperty("OperationKey").GetGuid()),default);
    if(mismatch.IsSuccess || mismatch.Error.Code!="Idempotency.PayloadMismatch")throw new InvalidOperationException();
    var filtered=await store.ListAsync(owner,new(org,"List",StateFilter:"Accepted"),default);
    if(filtered.IsFailure || !filtered.Value.Any(x=>x.InvitationId==id) || filtered.Value.Any(x=>x.State!="Accepted"))throw new InvalidOperationException();
    var invalidFilter=await store.ListAsync(owner,new(org,"List",StateFilter:"Rejected"),default);
    if(invalidFilter.IsSuccess || invalidFilter.Error.Code!="Invitation.InvalidRequest")throw new InvalidOperationException();
    int? pid=null;
    for(var i=0;i<4;i++) {
        if((await store.ListAsync(owner,new(other,"List"),default)).IsSuccess)throw new InvalidOperationException();
        if((await store.ListAsync(new("https://synthetic.invalid","owner-b"),new(other,"List"),default)).IsFailure)throw new InvalidOperationException();
        await using var connection=await tenant.Source!.OpenConnectionAsync();
        var next=(int)(await new NpgsqlCommand("SELECT pg_backend_pid()",connection).ExecuteScalarAsync())!;
        if(pid.HasValue && pid!=next)throw new InvalidOperationException();pid=next;
        if(await new NpgsqlCommand("SELECT nullif(current_setting('app.organization_id',true),'') IS NULL AND nullif(current_setting('app.account_id',true),'') IS NULL AND nullif(current_setting('app.identity_id',true),'') IS NULL",connection).ExecuteScalarAsync() is not true)throw new InvalidOperationException();
    }
    Console.WriteLine("PASS: real Tenant/Identity login ports, recipient accept/replay, wrong identity and payload mismatch denial, same-backend cross-Tenant recovery and GUC reset.");return 0;
} catch(Exception error){Console.WriteLine("FAIL: local invitation probe "+error.GetType().Name);return 1;}
