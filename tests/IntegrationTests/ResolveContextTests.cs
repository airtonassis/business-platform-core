using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class ResolveContextTests
{
    private sealed class Authentication(bool authenticated) : IAuthenticatedSubject
    {
        public OidcSubject? GetSubject() => authenticated ? new("https://issuer.example", "subject-A") : null;
    }
    private sealed class Reader(bool provisioned) : IEnterpriseContextReader
    {
        public bool IsProvisioned => provisioned;
        public int Calls { get; private set; }
        public EnterpriseContextSnapshot Snapshot { get; set; } = null!;
        public Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject subject, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Calls++; return Task.FromResult(Result.Success(Snapshot)); }
    }
    [Fact]
    public async Task Missing_authentication_or_unprovisioned_storage_never_queries_or_becomes_not_linked()
    {
        var reader = new Reader(false);
        var anonymous = await new ResolveEnterpriseContext(new Authentication(false), reader).ExecuteAsync(null, default);
        Assert.Equal("Authentication.Required", anonymous.Error.Code);
        var blocked = await new ResolveEnterpriseContext(new Authentication(true), reader).ExecuteAsync(null, default);
        Assert.Equal("Enterprise.PersistencePending", blocked.Error.Code);
        Assert.Equal(0, reader.Calls);
    }
    [Fact]
    public async Task Every_call_reloads_membership_and_revocation_removes_context()
    {
        var identity = Guid.NewGuid(); var account = Guid.NewGuid(); var organization = Guid.NewGuid();
        var active = new EnterpriseBindingFacts(identity, account, organization, account, organization, true, true, true, "Member");
        var reader = new Reader(true) { Snapshot = new(identity, new(BindingHistoryState.Consumed, organization), [active]) };
        var useCase = new ResolveEnterpriseContext(new Authentication(true), reader);
        Assert.Equal(EnterpriseContextState.Allowed, (await useCase.ExecuteAsync(organization, default)).Value.State);
        reader.Snapshot = reader.Snapshot with { Bindings = [active with { MembershipActive = false }] };
        var denied = (await useCase.ExecuteAsync(organization, default)).Value;
        Assert.Equal(EnterpriseContextState.Denied, denied.State);
        Assert.Null(denied.Context);
        Assert.Equal(2, reader.Calls);
    }
    [Fact]
    public async Task Cancelled_resolution_does_not_read_persistence()
    {
        var reader = new Reader(true);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ResolveEnterpriseContext(new Authentication(true), reader).ExecuteAsync(null, cancellation.Token));
        Assert.Equal(0, reader.Calls);
    }
    [Fact]
    public async Task Parallel_policy_evaluations_do_not_share_a_mutable_tenant_context()
    {
        var identity = Guid.NewGuid(); var account = Guid.NewGuid(); var organization = Guid.NewGuid();
        var other = Guid.NewGuid();
        EnterpriseBindingFacts[] snapshot = [new(identity, account, organization, account, organization, true, true, true, "Member")];
        var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(() => {
            var decision = EnterpriseContextPolicy.Evaluate(identity, new(BindingHistoryState.Consumed, organization), snapshot,
                i % 2 == 0 ? organization : other);
            Assert.Equal(i % 2 == 0 ? EnterpriseContextState.Allowed : EnterpriseContextState.Denied, decision.State);
            Assert.Equal(i % 2 == 0 ? organization : (Guid?)null, decision.Context?.OrganizationId);
        }));
        await Task.WhenAll(tasks); // In-memory policy only, not a PostgreSQL concurrency proof or G3.
    }
}
