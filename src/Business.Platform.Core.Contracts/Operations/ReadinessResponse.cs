namespace Business.Platform.Core.Contracts.Operations;

public sealed record ReadinessChecks(bool Database, bool IdentityProvider, bool EnterprisePersistence);
public sealed record ReadinessResponse(string Status, ReadinessChecks Checks);
