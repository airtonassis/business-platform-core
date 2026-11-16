namespace Business.Platform.Core.Domain.Tenancy;

/// <summary>Exact OIDC key; email is intentionally not an identity or authorization key.</summary>
public sealed record OidcSubject
{
    /// <summary>Creates an opaque key from an already validated identity provider token.</summary>
    public OidcSubject(string issuer, string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        Issuer = issuer; Subject = subject;
    }
    /// <summary>Exact validated issuer; never normalized or merged by email.</summary>
    public string Issuer { get; }
    /// <summary>Exact subject within its issuer.</summary>
    public string Subject { get; }
    /// <inheritdoc />
    public override string ToString() => "OidcSubject [redacted]";
}

/// <summary>Business facts, not database rows or an approved physical history schema.</summary>
public enum BindingHistoryState
{
    /// <summary>Authoritative history is unavailable or inconsistent.</summary>
    Unknown,
    /// <summary>The identity is authoritatively known never to have consumed an account.</summary>
    NeverConsumed,
    /// <summary>The identity remains consumed, independently of current active rows.</summary>
    Consumed
}

/// <summary>Authoritative permanent binding facts; does not prescribe their physical storage.</summary>
public sealed record EnterpriseBindingHistory(BindingHistoryState State, Guid? OrganizationId);

/// <summary>Coherent account/membership/organization facts loaded in one transaction by a future approved adapter.</summary>
public sealed record EnterpriseBindingFacts(Guid IdentityId, Guid AccountId, Guid OrganizationId,
    Guid MembershipAccountId, Guid MembershipOrganizationId,
    bool AccountActive, bool MembershipActive, bool OrganizationActive, string Role = "", string OrganizationName = "", Guid MembershipId = default);

/// <summary>Internal business decision, deliberately independent from pending HTTP status/DTO contracts.</summary>
public enum EnterpriseContextState
{
    /// <summary>An active coherent binding authorizes its own organization.</summary>
    Allowed,
    /// <summary>No binding has ever been consumed; this grants no enterprise access.</summary>
    NotLinked,
    /// <summary>Access is denied without returning another organization's context.</summary>
    Denied,
    /// <summary>Authoritative data is incomplete, conflicting, or incoherent.</summary>
    Inconsistent
}

/// <summary>Internal resolved context, returned only for an allowed decision.</summary>
public sealed record EnterpriseContext(Guid AccountId, Guid OrganizationId, string Role = "", string OrganizationName = "", Guid MembershipId = default);

/// <summary>Cannot carry organization details on denial, absence, or inconsistency.</summary>
public sealed class EnterpriseContextDecision
{
    internal EnterpriseContextDecision(EnterpriseContextState state, EnterpriseContext? context = null)
    { State = state; Context = context; }
    /// <summary>Internal decision; transport mapping is not defined here.</summary>
    public EnterpriseContextState State { get; }
    /// <summary>Present only for allowed access.</summary>
    public EnterpriseContext? Context { get; }
}

/// <summary>Implements approved permanent-binding rules without choosing pending persistence or HTTP mechanisms.</summary>
public static class EnterpriseContextPolicy
{
    /// <summary>Evaluates one authoritative snapshot; scoped reads require an active Tenant role. Route organization is never authority.</summary>
    public static EnterpriseContextDecision Evaluate(Guid identityId, EnterpriseBindingHistory history,
        IReadOnlyList<EnterpriseBindingFacts> bindings, Guid? requestedOrganization, bool identityActive = true)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(bindings);
        // Preserve the Domain assembly allowlist; do not introduce System.Linq.
        var rows = new EnterpriseBindingFacts[bindings.Count];
        for (var i = 0; i < rows.Length; i++) rows[i] = bindings[i];
        EnterpriseContextDecision Inconsistent() => new(EnterpriseContextState.Inconsistent);
        if (!identityActive) return new(EnterpriseContextState.Denied);
        if (identityId == Guid.Empty || history.State == BindingHistoryState.Unknown ||
            !Enum.IsDefined(history.State)) return Inconsistent();
        if (history.State == BindingHistoryState.NeverConsumed)
        {
            if (history.OrganizationId is not null || rows.Length != 0) return Inconsistent();
            return new(requestedOrganization is null ? EnterpriseContextState.NotLinked : EnterpriseContextState.Denied);
        }
        if (history.OrganizationId is null || history.OrganizationId == Guid.Empty || rows.Length != 1)
            return Inconsistent();
        var row = rows[0];
        if (row is null || row.IdentityId != identityId || row.AccountId == Guid.Empty ||
            row.OrganizationId != history.OrganizationId || row.MembershipAccountId != row.AccountId ||
            row.MembershipOrganizationId != row.OrganizationId) return Inconsistent();
        if (!row.AccountActive || !row.MembershipActive || !row.OrganizationActive ||
            (requestedOrganization is not null && (requestedOrganization != row.OrganizationId ||
                row.Role is not ("Owner" or "Admin" or "Member"))))
            return new(EnterpriseContextState.Denied);
        return new(EnterpriseContextState.Allowed, new(row.AccountId, row.OrganizationId, row.Role, row.OrganizationName, row.MembershipId));
    }
}
