using System.Text.Json;
using System.Net.Http.Json;
using Business.Platform.Core.Application.Platform;
using Business.Platform.Core.Domain.Tenancy;
using Business.Platform.Core.Infrastructure.Persistence;
using Business.Platform.Core.Infrastructure.Platform;
using Npgsql;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class ProvisioningDatabaseFactAttribute : FactAttribute
{
    public ProvisioningDatabaseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BPC_P1C_PORT") is null)
            Skip = "Requires the authorized P1-C disposable PostgreSQL harness.";
    }
}

/// <summary>Real runtime logins/ports. Superuser connections below are fixture/oracle only.</summary>
public sealed class P1CProvisioningTests
{
    private const string Issuer = "https://p1c-synthetic.invalid";
    private static string Password => Environment.GetEnvironmentVariable("BPC_P1C_PASSWORD")!;
    private static string Connection(string login) => new NpgsqlConnectionStringBuilder
    {
        Host = "127.0.0.1", Port = int.Parse(Environment.GetEnvironmentVariable("BPC_P1C_PORT")!),
        Database = "bpc_audit_local", Username = login, Password = Password,
        IncludeErrorDetail = false, Pooling = false, Timeout = 3, CommandTimeout = 10
    }.ConnectionString;

    private static async Task<object?> Sql(string login, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(Connection(login));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return await command.ExecuteScalarAsync();
    }

    private sealed class Runtime : IAsyncDisposable
    {
        private readonly NpgsqlDataSource identity = NpgsqlDataSource.Create(Connection("bpc_p1c_identity"));
        private readonly NpgsqlDataSource platform = NpgsqlDataSource.Create(Connection("bpc_p1c_platform"));
        public IPlatformStore Store => new PostgresPlatformStore(identity, new PlatformDataSource(platform));
        public PlatformOperations Operations => new(Store);
        public PostgresEnterpriseContextReader Reader => new(identity);
        public async ValueTask DisposeAsync() { await platform.DisposeAsync(); await identity.DisposeAsync(); }
    }

    private static OidcSubject Actor() => new(Issuer, "operator-" + Guid.NewGuid());
    private static async Task<(OidcSubject Subject, IssuedOwnerReference Reference)> Recipient(Runtime runtime)
    {
        var subject = new OidcSubject(Issuer, "recipient-" + Guid.NewGuid());
        var issued = await runtime.Operations.IssueAsync(subject, "Synthetic recipient", default);
        Assert.True(issued.IsSuccess);
        return (subject, issued.Value);
    }

    [ProvisioningDatabaseFact]
    public async Task Lost_HTTP_success_response_recovers_existing_committed_receipt_without_duplicate_effects()
    {
        await using var runtime = new Runtime(); var target = await Recipient(runtime);
        await using var host = new PlatformHttpTests.Host(runtime.Store);
        using var client = new HttpClient(new LoseFirstSuccess(host.Server.CreateHandler())) { BaseAddress = new Uri("https://localhost") };
        var subject = "lost-response-" + Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Authorization = new("Bearer", host.Token("valid", subject));
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var body = new { userId = target.Reference.UserId, recipientReference = target.Reference.Reference, name = "Lost response" };
        await Assert.ThrowsAsync<HttpRequestException>(() => client.PostAsJsonAsync("/api/v1/platform/organizations", body));
        PlatformOperationId.TryCreate(key, out var operation);
        Assert.Equal(1L, await Sql("postgres", "SELECT count(*) FROM bpc_security.platform_receipts WHERE operation_id=$1", operation));
        var replay = await client.PostAsJsonAsync("/api/v1/platform/organizations", body);
        Assert.Equal(200, (int)replay.StatusCode);
        var payload = await replay.Content.ReadFromJsonAsync<PlatformCreation>(); Assert.True(payload!.Replayed);
        Assert.Equal(1L, await Sql("postgres", "SELECT count(*) FROM bpc_security.platform_events WHERE operation_id=$1", operation));
        Assert.Equal(1L, await Sql("postgres", "SELECT count(*) FROM identity.identity_consumptions WHERE identity_id=$1", target.Reference.UserId));
    }

    private sealed class LoseFirstSuccess(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private bool lost;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (!lost && response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                lost = true; response.Dispose();
                // Controlled loss AFTER the real HTTP endpoint committed; not a network fault injector.
                throw new HttpRequestException("Synthetic response loss");
            }
            return response;
        }
    }

    [ProvisioningDatabaseFact]
    public async Task Signed_HTTP_operator_calls_real_Application_and_PostgreSQL_with_current_authorization()
    {
        await using var runtime = new Runtime(); var target = await Recipient(runtime);
        await using var host = new PlatformHttpTests.Host(runtime.Store); using var client = host.CreateClient();
        var subject = "http-operator-" + Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        var body = new { userId = target.Reference.UserId, recipientReference = target.Reference.Reference, name = "HTTP real PostgreSQL" };
        client.DefaultRequestHeaders.Authorization = new("Bearer", host.Token("no-role", subject));
        Assert.Equal(403, (int)(await client.PostAsJsonAsync("/api/v1/platform/owner-preview", body)).StatusCode);
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        Assert.Equal(403, (int)(await client.PostAsJsonAsync("/api/v1/platform/organizations", body)).StatusCode);
        Assert.Equal(0L, await Sql("postgres", "SELECT count(*) FROM identity.identity_consumptions WHERE identity_id=$1", target.Reference.UserId));
        client.DefaultRequestHeaders.Authorization = new("Bearer", host.Token("valid", subject));
        Assert.Equal(200, (int)(await client.PostAsJsonAsync("/api/v1/platform/owner-preview", body)).StatusCode);
        Assert.Equal(201, (int)(await client.PostAsJsonAsync("/api/v1/platform/organizations", body)).StatusCode);
        Assert.Equal(200, (int)(await client.PostAsJsonAsync("/api/v1/platform/organizations", body)).StatusCode);
        // Rights must be checked again even though a durable receipt now exists.
        client.DefaultRequestHeaders.Authorization = new("Bearer", host.Token("no-mfa", subject));
        Assert.Equal(403, (int)(await client.PostAsJsonAsync("/api/v1/platform/organizations", body)).StatusCode);
        Assert.Equal(1L, await Sql("postgres", "SELECT count(*) FROM identity.identity_consumptions WHERE identity_id=$1", target.Reference.UserId));
    }

    [ProvisioningDatabaseFact]
    public async Task Legitimate_bootstrap_and_durable_replay_do_not_enrol_operator()
    {
        await using var runtime = new Runtime(); var target = await Recipient(runtime); var actor = Actor(); var key = Guid.NewGuid().ToString();
        var result = await runtime.Operations.CreateAsync(actor, target.Reference.UserId, target.Reference.Reference, "Local", key, default);
        Assert.True(result.IsSuccess); Assert.False(result.Value.Replayed);
        // A separate pool/process boundary reads the committed receipt, not an in-memory result.
        await using var second = new Runtime();
        var replay = await second.Operations.CreateAsync(actor, target.Reference.UserId, target.Reference.Reference, "Local", key, default);
        Assert.True(replay.IsSuccess); Assert.True(replay.Value.Replayed);
        Assert.Equal(result.Value with { Replayed = true }, replay.Value);
        var context = await second.Reader.ReadAsync(actor, default);
        Assert.True(context.IsSuccess); Assert.Empty(context.Value.Bindings);
        PlatformOperationId.TryCreate(key, out var operation);
        Assert.Equal(1L, await Sql("postgres", "SELECT count(*) FROM bpc_security.platform_receipts WHERE operation_id=$1", operation));
        Assert.Equal(1L, await Sql("postgres", "SELECT count(*) FROM bpc_security.platform_events WHERE operation_id=$1", operation));
    }

    [ProvisioningDatabaseFact]
    public async Task Same_operation_concurrency_returns_one_bootstrap_and_one_receipt()
    {
        await using var runtime = new Runtime(); var target = await Recipient(runtime); var actor = Actor(); var key = Guid.NewGuid().ToString();
        using var start = new SemaphoreSlim(0);
        async Task<Business.Platform.Core.Domain.Shared.Result<PlatformCreation>> Run()
        { await start.WaitAsync(); return await runtime.Operations.CreateAsync(actor, target.Reference.UserId, target.Reference.Reference, "Concurrent", key, default); }
        var first = Run(); var second = Run(); start.Release(2);
        var results = await Task.WhenAll(first, second);
        Assert.All(results, x => Assert.True(x.IsSuccess));
        Assert.Single(results, x => !x.Value.Replayed);
        Assert.Equal(results[0].Value.OrganizationId, results[1].Value.OrganizationId);
    }

    [ProvisioningDatabaseFact]
    public async Task Different_operations_competing_for_one_identity_cannot_transfer_it()
    {
        await using var runtime = new Runtime(); var target = await Recipient(runtime); var actor = Actor();
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(i => runtime.Operations.CreateAsync(actor,
            target.Reference.UserId, target.Reference.Reference, "Competing " + i, Guid.NewGuid().ToString(), default)));
        Assert.Single(results, x => x.IsSuccess);
        Assert.Single(results, x => x.IsFailure);
        Assert.Equal(1L, await Sql("postgres", "SELECT count(*) FROM identity.identity_consumptions WHERE identity_id=$1", target.Reference.UserId));
        Assert.Equal(1L, await Sql("postgres", "SELECT count(*) FROM identity.enterprise_user_accounts WHERE identity_id=$1", target.Reference.UserId));
    }

    [ProvisioningDatabaseFact]
    public async Task Payload_other_actor_and_other_recipient_cannot_recover_cross_tenant_receipt()
    {
        await using var runtime = new Runtime(); var a = await Recipient(runtime); var b = await Recipient(runtime); var actor = Actor(); var key = Guid.NewGuid().ToString();
        Assert.True((await runtime.Operations.CreateAsync(actor, a.Reference.UserId, a.Reference.Reference, "A", key, default)).IsSuccess);
        Assert.True((await runtime.Operations.CreateAsync(actor, b.Reference.UserId, b.Reference.Reference, "B", key, default)).IsFailure);
        Assert.True((await runtime.Operations.CreateAsync(Actor(), a.Reference.UserId, a.Reference.Reference, "A", key, default)).IsFailure);
        Assert.Equal("Idempotency.PayloadMismatch", (await runtime.Operations.CreateAsync(actor, a.Reference.UserId, a.Reference.Reference, "Changed", key, default)).Error.Code);
    }

    [ProvisioningDatabaseFact]
    public async Task Audit_failure_rolls_back_consumption_organization_and_receipts()
    {
        await using var runtime = new Runtime(); var target = await Recipient(runtime); var key = Guid.NewGuid().ToString(); var actor = Actor();
        var before = await Sql("postgres", "SELECT count(*) FROM tenancy.organizations");
        await Sql("postgres", "CREATE FUNCTION bpc_security.p1c_fail_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION USING ERRCODE='P1999',MESSAGE='Synthetic audit failure'; END $$; REVOKE ALL ON FUNCTION bpc_security.p1c_fail_audit() FROM PUBLIC; CREATE TRIGGER p1c_fail_audit BEFORE INSERT ON bpc_security.platform_events FOR EACH ROW EXECUTE FUNCTION bpc_security.p1c_fail_audit();");
        try
        {
            Assert.True((await runtime.Operations.CreateAsync(actor, target.Reference.UserId, target.Reference.Reference, "Rollback", key, default)).IsFailure);
            Assert.Equal(before, await Sql("postgres", "SELECT count(*) FROM tenancy.organizations"));
            Assert.Equal(0L, await Sql("postgres", "SELECT count(*) FROM identity.identity_consumptions WHERE identity_id=$1", target.Reference.UserId));
            PlatformOperationId.TryCreate(key, out var operation);
            Assert.Equal(0L, await Sql("postgres", "SELECT count(*) FROM bpc_security.platform_receipts WHERE operation_id=$1", operation));
        }
        finally { await Sql("postgres", "DROP TRIGGER p1c_fail_audit ON bpc_security.platform_events; DROP FUNCTION bpc_security.p1c_fail_audit();"); }
        Assert.True((await runtime.Operations.CreateAsync(actor, target.Reference.UserId, target.Reference.Reference, "Rollback", key, default)).IsSuccess);
    }

    [ProvisioningDatabaseFact]
    public async Task Expired_proof_blocks_receipt_replay_without_erasing_committed_result()
    {
        await using var runtime = new Runtime(); var target = await Recipient(runtime); var actor = Actor(); var key = Guid.NewGuid().ToString();
        Assert.True((await runtime.Operations.CreateAsync(actor, target.Reference.UserId, target.Reference.Reference, "Committed", key, default)).IsSuccess);
        // Fixture time travel, not a recovery operation available to the runtime.
        await Sql("postgres", "UPDATE identity.owner_references SET expires_at=clock_timestamp()-interval '1 second' WHERE identity_id=$1", target.Reference.UserId);
        var replay = await runtime.Operations.CreateAsync(actor, target.Reference.UserId, target.Reference.Reference, "Committed", key, default);
        Assert.True(replay.IsFailure);
        Assert.Equal(1L, await Sql("postgres", "SELECT count(*) FROM bpc_security.platform_receipts WHERE target_identity_id=$1", target.Reference.UserId));
    }

    [ProvisioningDatabaseFact]
    public async Task Revoked_member_cannot_be_provisioned_elsewhere_or_reactivated()
    {
        await using var runtime = new Runtime(); var owner = await Recipient(runtime); var target = await Recipient(runtime); var actor = Actor(); var key = Guid.NewGuid().ToString();
        var created = await runtime.Operations.CreateAsync(actor, owner.Reference.UserId, owner.Reference.Reference, "Owner retained", key, default);
        Assert.True(created.IsSuccess);
        var account = Guid.NewGuid(); var membership = Guid.NewGuid();
        // Privileged fixture prepares a Member, not an alternate runtime invitation/bootstrap port.
        // Owner is untouched; actual revocation below uses the E2-02 runtime entry.
        await using (var connection = new NpgsqlConnection(Connection("postgres")))
        {
            await connection.OpenAsync(); await using var transaction = await connection.BeginTransactionAsync();
            var statements = new (string Sql, object[] Values)[] {
                ("INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id,origin) VALUES($1,$2,$3,'PlatformBootstrap')", [target.Reference.UserId, created.Value.OrganizationId, account]),
                ("INSERT INTO identity.enterprise_user_accounts(account_id,identity_id,organization_id,status) VALUES($1,$2,$3,'Active')", [account, target.Reference.UserId, created.Value.OrganizationId]),
                ("INSERT INTO tenancy.memberships(membership_id,organization_id,account_id,status,role) VALUES($1,$2,$3,'Active','Member')", [membership, created.Value.OrganizationId, account]) };
            foreach (var statement in statements)
            {
                await using var command = new NpgsqlCommand(statement.Sql, connection, transaction);
                foreach (var value in statement.Values) command.Parameters.AddWithValue(value);
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        await Sql("bpc_p1c_tenant", "SELECT bpc_security.tenant_membership($1,$2,$3,$4,'Revoke',1,$5)", owner.Subject.Issuer, owner.Subject.Subject, created.Value.OrganizationId, membership, Guid.NewGuid());
        Assert.True((await runtime.Operations.CreateAsync(actor, target.Reference.UserId, target.Reference.Reference, "Other", Guid.NewGuid().ToString(), default)).IsFailure);
        Assert.True((await runtime.Operations.IssueAsync(target.Subject, "Synthetic", default)).IsFailure);
        Assert.Equal("Revoked", await Sql("postgres", "SELECT status FROM tenancy.memberships WHERE membership_id=$1", membership));
        Assert.Equal(1L, await Sql("postgres", "SELECT count(*) FROM identity.identity_consumptions WHERE identity_id=$1", target.Reference.UserId));
        Assert.Equal("Owner", await Sql("postgres", "SELECT role FROM tenancy.memberships WHERE membership_id=$1", created.Value.MembershipId));
    }

    [ProvisioningDatabaseFact]
    public async Task Runtime_login_cannot_use_direct_DML_receipts_or_legacy_bootstrap()
    {
        foreach (var sql in new[] { "SELECT * FROM bpc_security.platform_receipts", "UPDATE identity.identities SET status='Active'", "DELETE FROM tenancy.memberships", "SELECT bpc_security.bootstrap_organization(gen_random_uuid(),'Forbidden',gen_random_uuid())", "SET ROLE bpc_e1_boot_exec" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => Sql("bpc_p1c_platform", sql));
            Assert.Equal("42501", error.SqlState);
        }
        await using var restricted = NpgsqlDataSource.Create(Connection("bpc_p1c_identity"));
        var store = new PostgresPlatformStore(restricted, new PlatformDataSource(restricted));
        var result = await store.PreviewAsync(Guid.NewGuid(), new byte[32], default);
        Assert.True(result.IsFailure);
    }
}
