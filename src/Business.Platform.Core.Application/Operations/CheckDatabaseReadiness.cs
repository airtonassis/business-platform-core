namespace Business.Platform.Core.Application.Operations;

/// <summary>Operational dependency check; does not accept a business scenario or sprint.</summary>
public sealed class CheckDatabaseReadiness(IDatabaseReadiness database)
{
    /// <summary>Checks the database adapter without exposing connection details.</summary>
    public Task<bool> ExecuteAsync(CancellationToken cancellationToken) => database.CheckAsync(cancellationToken);
}
