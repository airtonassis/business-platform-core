using System.Text.Json;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Tenancy;
using Business.Platform.Core.Infrastructure.Persistence;
using Npgsql;

// Disposable local harness only. Configuration (including secrets) enters via stdin, never argv/logs.
if (args.SequenceEqual(new[] { "--rotation" })) return await RotationProbe.RunAsync();
try {
    using var document = JsonDocument.Parse(await Console.In.ReadToEndAsync());
    var config = document.RootElement;
    var options = JsonSerializer.Deserialize<PostgresOptions>(config.GetRawText())!;
    if (options.Host != "127.0.0.1" || options.Name != "bpc_audit_local" || options.Username != "bpc_audit_local")
        throw new InvalidOperationException("Local disposable target required.");
    var connection = new NpgsqlConnectionStringBuilder(options.ConnectionString()) { MaxPoolSize = 1 };
    await using var pool = new AuditDataSource(NpgsqlDataSource.Create(connection.ConnectionString));
    var reader = new PostgresTenantAuditReader(pool);
    var issuer = new AuditAttestationIssuer(config.GetProperty("ActiveKid").GetString(),
        Convert.FromBase64String(config.GetProperty("ActiveKeyBase64").GetString()!),TimeProvider.System);
    Task<Business.Platform.Core.Domain.Shared.Result<IReadOnlyList<TenantAuditEvent>>> Read(string subject,int limit,string? cursor,CancellationToken token=default) {
        var org=config.GetProperty(subject=="owner-b"?"OrganizationB":"OrganizationA").GetGuid();
        var attestation=issuer.Issue(new OidcSubject("https://synthetic.invalid",subject),org,limit,cursor);
        if(attestation.IsFailure) throw new InvalidOperationException("Attestation configuration unavailable.");
        return reader.ReadAsync(attestation.Value,token);
    }
    async Task<int> Pid() {
        await using var c = await pool.Source!.OpenConnectionAsync();
        return (int)(await new NpgsqlCommand("SELECT pg_backend_pid()", c).ExecuteScalarAsync())!;
    }
    var pid = await Pid();
    var targetB = config.GetProperty("AccountB").GetGuid();
    for (var i = 0; i < 6; i++) {
        var subject = i % 2 == 0 ? "owner-a" : "owner-b";
        var result = await Read(subject,25,null);
        if (result.IsFailure || result.Value.Count == 0 ||
            result.Value.Any(x => (x.TargetAccountId == targetB) != (subject == "owner-b"))) throw new InvalidOperationException("Isolation failed.");
        if (await Pid() != pid) throw new InvalidOperationException("Same backend not proved.");
        await using var c = await pool.Source!.OpenConnectionAsync();
        if (await new NpgsqlCommand("SELECT nullif(current_setting('app.organization_id',true),'') IS NULL AND nullif(current_setting('app.account_id',true),'') IS NULL AND nullif(current_setting('app.identity_id',true),'') IS NULL",c).ExecuteScalarAsync() is not true)
            throw new InvalidOperationException("Context not reset.");
    }
    var first = await Read("owner-a",1,null);
    var row = first.Value[0];
    var next = await Read("owner-a",1,new TenantAuditPosition(row.OccurredAtUtc, row.EventId).Encode());
    if (next.IsFailure || next.Value.Any(x => x.EventId == row.EventId)) throw new InvalidOperationException("Cursor failed.");
    using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    try { await Read("owner-a",25,null,cancellation.Token); throw new InvalidOperationException("Cancellation ignored."); }
    catch (OperationCanceledException) { }
    if ((await Read("owner-b",25,null)).IsFailure)
        throw new InvalidOperationException("Recovery after cancellation failed.");
    var bad=issuer.Issue(new OidcSubject("https://synthetic.invalid","owner-a"),config.GetProperty("OrganizationA").GetGuid(),25,null).Value;
    var badMac=bad.GetMac();badMac[0]^=1;bad=new AuditAttestation(bad.GetPayload(),badMac);
    var denied=await reader.ReadAsync(bad,default);
    if(denied.IsSuccess || denied.Error.Code!="Access.Forbidden" || denied.Error.Description.Contains(bad.GetPayload()))
        throw new InvalidOperationException("Unsafe attestation failure handling.");
    if((await Read("owner-b",25,null)).IsFailure) throw new InvalidOperationException("Recovery after denied capability failed.");
    Console.WriteLine("PASS: real dedicated login, repeated same-backend A/B isolation, context reset, cursor, cancellation recovery.");
    Console.WriteLine("PASS: invalid HMAC safely rejected and pooled connection recovered.");
    return 0;
} catch (Exception error) {
    Console.WriteLine("FAIL: local Npgsql audit probe " + error.GetType().Name);
    return 1;
}
