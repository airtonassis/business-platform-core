using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class TenantAuditTests
{
    private static IAuditAttestationIssuer Issuer => new AuditAttestationIssuer("test",System.Security.Cryptography.RandomNumberGenerator.GetBytes(32),TimeProvider.System);
    private static readonly Guid Org = Guid.NewGuid();
    private sealed class Subject : IAuthenticatedSubject { public OidcSubject? GetSubject() => new("https://synthetic.invalid", "owner"); }
    private sealed class Context(string role, bool active = true) : IEnterpriseContextReader
    {
        public bool IsProvisioned => true;
        public Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject s, CancellationToken t)
        {
            var id = Guid.NewGuid(); var account = Guid.NewGuid();
            return Task.FromResult(Result.Success(new EnterpriseContextSnapshot(id, new(BindingHistoryState.Consumed, Org),
                [new(id, account, Org, account, Org, true, active, true, role)])));
        }
    }
    private sealed class Reader : ITenantAuditReader
    {
        public int Calls;
        public IReadOnlyList<TenantAuditEvent> Items { get; set; } = [];
        public Task<Result<IReadOnlyList<TenantAuditEvent>>> ReadAsync(AuditAttestation attestation, CancellationToken t)
        { Calls++; return Task.FromResult(Result.Success(Items)); }
    }
    [Theory][InlineData("Owner", true)][InlineData("Admin", true)][InlineData("Member", false)][InlineData("PlatformOperator", false)]
    public async Task Direct_application_authorization(string role, bool allowed)
    {
        var auth = new Subject(); var reader = new Reader();
        var result = await new ReadTenantAudit(new(auth, new Context(role)), auth, reader, Issuer).ExecuteAsync(Org);
        Assert.Equal(allowed, result.IsSuccess); Assert.Equal(allowed ? 1 : 0, reader.Calls);
    }
    [Theory][InlineData(0)][InlineData(101)][InlineData(-1)]
    public async Task Invalid_limits_do_not_read_audit(int limit)
    {
        var auth = new Subject(); var reader = new Reader();
        var result = await new ReadTenantAudit(new(auth, new Context("Owner")), auth, reader, Issuer).ExecuteAsync(Org, limit);
        Assert.Equal("Audit.InvalidPage", result.Error.Code); Assert.Equal(0, reader.Calls);
    }
    [Fact]
    public async Task Other_organization_and_revoked_membership_do_not_read()
    {
        var auth = new Subject(); var reader = new Reader();
        foreach (var setup in new[] { (new Context("Owner"), Guid.NewGuid()), (new Context("Owner", false), Org) }) {
            var result = await new ReadTenantAudit(new(auth, setup.Item1), auth, reader, Issuer).ExecuteAsync(setup.Item2);
            Assert.Equal("Resource.NotFound", result.Error.Code);
        }
        Assert.Equal(0, reader.Calls);
    }
    [Theory][InlineData("")][InlineData("!")][InlineData("AAAA")]
    public void Invalid_cursor_is_rejected(string cursor) => Assert.False(TenantAuditPosition.TryDecode(cursor, out _));
    [Fact]
    public async Task Cursor_preserves_microseconds_and_last_returned_event()
    {
        var auth = new Subject(); var reader = new Reader();
        var time = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero).AddTicks(1234560);
        var first = new TenantAuditEvent(Guid.NewGuid(), time, "MembershipRevoked", Guid.NewGuid());
        reader.Items = [first, first with { EventId = Guid.NewGuid() }];
        var page = await new ReadTenantAudit(new(auth, new Context("Admin")), auth, reader, Issuer).ExecuteAsync(Org, 1);
        Assert.Single(page.Value.Items); Assert.True(TenantAuditPosition.TryDecode(page.Value.NextCursor, out var cursor));
        Assert.Equal(time, cursor!.Time); Assert.Equal(first.EventId, cursor.EventId);
    }
}
