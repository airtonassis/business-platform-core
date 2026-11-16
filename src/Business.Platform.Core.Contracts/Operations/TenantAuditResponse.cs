namespace Business.Platform.Core.Contracts.Operations;

public sealed record TenantAuditEventResponse(Guid EventId, DateTimeOffset OccurredAtUtc, string Kind, Guid TargetAccountId);
public sealed record TenantAuditResponse(IReadOnlyList<TenantAuditEventResponse> Items, string? NextCursor);
