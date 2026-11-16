using Business.Platform.Core.Domain.Tenancy;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class EnterpriseContextTests
{
    private static readonly Guid Identity = Guid.NewGuid(), Account = Guid.NewGuid(), A = Guid.NewGuid(), B = Guid.NewGuid();
    private static EnterpriseBindingFacts Active => new(Identity, Account, A, Account, A, true, true, true, "Member");

    [Fact]
    public void Active_binding_resolves_only_the_historically_bound_organization()
    {
        var result = EnterpriseContextPolicy.Evaluate(Identity, new(BindingHistoryState.Consumed, A), [Active], A);
        Assert.Equal(EnterpriseContextState.Allowed, result.State);
        Assert.Equal(A, result.Context!.OrganizationId);
        Assert.Equal(Account, result.Context.AccountId);
        var cross = EnterpriseContextPolicy.Evaluate(Identity, new(BindingHistoryState.Consumed, A), [Active], B);
        Assert.Equal(EnterpriseContextState.Denied, cross.State);
        Assert.Null(cross.Context);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("membership")]
    [InlineData("organization")]
    public void Revoked_or_inactive_binding_never_becomes_not_linked(string inactive)
    {
        var facts = inactive switch { "account" => Active with { AccountActive = false },
            "membership" => Active with { MembershipActive = false }, _ => Active with { OrganizationActive = false } };
        var result = EnterpriseContextPolicy.Evaluate(Identity, new(BindingHistoryState.Consumed, A), [facts], null);
        Assert.Equal(EnterpriseContextState.Denied, result.State);
        Assert.Null(result.Context);
    }

    [Fact]
    public void Consumed_identity_cannot_be_reassigned_even_if_current_rows_are_coherent()
    {
        var moved = Active with { OrganizationId = B, MembershipOrganizationId = B };
        var result = EnterpriseContextPolicy.Evaluate(Identity, new(BindingHistoryState.Consumed, A), [moved], B);
        Assert.Equal(EnterpriseContextState.Inconsistent, result.State);
        Assert.Null(result.Context);
    }

    [Fact]
    public void Not_linked_requires_verified_absence_of_historical_consumption_and_current_binding()
    {
        Assert.Equal(EnterpriseContextState.NotLinked,
            EnterpriseContextPolicy.Evaluate(Identity, new(BindingHistoryState.NeverConsumed, null), [], null).State);
        foreach (var history in new[] { new EnterpriseBindingHistory(BindingHistoryState.Unknown, null),
                     new EnterpriseBindingHistory(BindingHistoryState.Consumed, A) })
        {
            var result = EnterpriseContextPolicy.Evaluate(Identity, history, [], null);
            Assert.Equal(EnterpriseContextState.Inconsistent, result.State);
            Assert.Null(result.Context);
        }
        Assert.Equal(EnterpriseContextState.Denied,
            EnterpriseContextPolicy.Evaluate(Identity, new(BindingHistoryState.NeverConsumed, null), [], A).State);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("account")]
    [InlineData("organization")]
    [InlineData("empty")]
    [InlineData("duplicate")]
    public void Inconsistent_snapshots_never_select_first_row_or_return_a_context(string fault)
    {
        var facts = fault switch { "identity" => Active with { IdentityId = Guid.NewGuid() },
            "account" => Active with { MembershipAccountId = Guid.NewGuid() },
            "organization" => Active with { MembershipOrganizationId = B },
            "empty" => Active with { AccountId = Guid.Empty }, _ => Active };
        EnterpriseBindingFacts[] rows = fault == "duplicate" ? [facts, facts] : [facts];
        var result = EnterpriseContextPolicy.Evaluate(Identity, new(BindingHistoryState.Consumed, A), rows, null);
        Assert.Equal(EnterpriseContextState.Inconsistent, result.State);
        Assert.Null(result.Context);
    }

    [Fact]
    public void Oidc_identity_uses_exact_issuer_subject_not_email_or_case_folding()
    {
        var first = new OidcSubject("https://issuer.example", "subject-A");
        Assert.NotEqual(first, new OidcSubject("https://issuer.example", "subject-a"));
        Assert.NotEqual(first, new OidcSubject("https://other.example", "subject-A"));
        Assert.DoesNotContain("subject-A", first.ToString());
    }
}
