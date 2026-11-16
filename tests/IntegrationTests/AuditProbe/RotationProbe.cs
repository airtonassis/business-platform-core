using System.Text.Json;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Tenancy;
using Business.Platform.Core.Infrastructure.Persistence;
using Npgsql;

// Test-only process protocol. No listener/service, credential issuer or public capability output.
internal static class RotationProbe
{
    private sealed class Clock : TimeProvider
    {
        public int Offset { get; set; }
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddSeconds(Offset);
    }

    public static async Task<int> RunAsync()
    {
        try
        {
            using var document = JsonDocument.Parse((await Console.In.ReadLineAsync())!);
            var config = document.RootElement;
            var options = JsonSerializer.Deserialize<PostgresOptions>(config.GetRawText())!;
            if (options.Host != "127.0.0.1" || options.Name != "bpc_audit_local" || options.Username != "bpc_audit_local")
                throw new InvalidOperationException();
            await using var pool = new AuditDataSource(NpgsqlDataSource.Create(options.ConnectionString()));
            var reader = new PostgresTenantAuditReader(pool);
            var configuredKid = config.GetProperty("ActiveKid").GetString();
            var sourceKey = Convert.FromBase64String(config.GetProperty("ActiveKeyBase64").GetString()!);
            var clock = new Clock();
            var issuer = new AuditAttestationIssuer(configuredKid, sourceKey, clock);
            AuditAttestation? held = null;
            void Emit(object result) { Console.WriteLine(JsonSerializer.Serialize(result)); Console.Out.Flush(); }
            Emit(new { ready = true, pid = Environment.ProcessId, kid = configuredKid });
            while (await Console.In.ReadLineAsync() is { } line)
            {
                using var command = JsonDocument.Parse(line);
                var request = command.RootElement;
                var action = request.GetProperty("action").GetString();
                if (action == "stop") break;
                if (action == "mutate-config")
                {
                    configuredKid = "configuration-changed-without-restart";
                    if (sourceKey.Length > 0) sourceKey[0] ^= 1;
                }
                if (action == "clock") clock.Offset = request.GetProperty("offset").GetInt32();
                var subject = request.TryGetProperty("subject", out var sub) ? sub.GetString()! : "owner-a";
                var organization = request.TryGetProperty("organization", out var org) ? org.GetGuid()
                    : config.GetProperty(subject == "owner-b" ? "OrganizationB" : "OrganizationA").GetGuid();
                var issued = issuer.Issue(new OidcSubject("https://synthetic.invalid", subject), organization, 25, null);
                if (issued.IsFailure) { Emit(new { ok = false, code = issued.Error.Code, pid = Environment.ProcessId }); continue; }
                var cap = issued.Value;
                if (action == "capture") held = cap;
                if (action == "held") cap = held ?? throw new InvalidOperationException();
                if (action == "tamper")
                {
                    var bytes = cap.GetMac(); bytes[0] ^= 1; cap = new AuditAttestation(cap.GetPayload(), bytes);
                }
                var result = await reader.ReadAsync(cap, default);
                var targetB = config.GetProperty("AccountB").GetGuid();
                var leaked = result.IsSuccess && result.Value.Any(x =>
                    (x.TargetAccountId == targetB) != (organization == config.GetProperty("OrganizationB").GetGuid()));
                Emit(new { ok = result.IsSuccess, code = result.IsFailure ? result.Error.Code : null,
                    count = result.IsSuccess ? result.Value.Count : 0, crossTenantLeak = leaked,
                    kid = cap.GetPayload().Split('.')[1], pid = Environment.ProcessId });
            }
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ready = false, errorType = error.GetType().Name }));
            return 1;
        }
    }
}
