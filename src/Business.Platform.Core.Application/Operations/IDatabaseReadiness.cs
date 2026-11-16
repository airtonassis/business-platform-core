namespace Business.Platform.Core.Application.Operations;

/// <summary>Checks dependency connectivity without reading enterprise data or applying migrations.</summary>
public interface IDatabaseReadiness
{
    /// <summary>Returns whether the dependency is available under a restricted operational login.</summary>
    Task<bool> CheckAsync(CancellationToken cancellationToken);
}
