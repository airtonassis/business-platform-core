using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Business.Platform.Core.Infrastructure.Persistence;

/// <summary>Separate dedicated audit pool. Never falls back to the general API credential.</summary>
public sealed class AuditDataSource(NpgsqlDataSource? source) : IAsyncDisposable
{
    public NpgsqlDataSource? Source { get; } = source;
    public ValueTask DisposeAsync() => Source?.DisposeAsync() ?? ValueTask.CompletedTask;
}

/// <summary>Calls only the narrow read function; no organization or role is sent to PostgreSQL.</summary>
public sealed class PostgresTenantAuditReader(AuditDataSource source) : ITenantAuditReader
{
    public async Task<Result<IReadOnlyList<TenantAuditEvent>>> ReadAsync(AuditAttestation attestation, CancellationToken cancellationToken)
    {
        if (source.Source is null) return Failure("Dependency.Unavailable");
        try {
            await using var connection = await source.Source.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT event_id,occurred_at,kind,target_account_id FROM bpc_security.read_tenant_audit($1,$2)", connection, transaction);
            command.Parameters.AddWithValue(attestation.GetPayload());
            command.Parameters.AddWithValue(NpgsqlDbType.Bytea, attestation.GetMac());
            var items = new List<TenantAuditEvent>();
            await using (var rows = await command.ExecuteReaderAsync(cancellationToken)) {
                while (await rows.ReadAsync(cancellationToken)) items.Add(new(rows.GetGuid(0), new DateTimeOffset(rows.GetDateTime(1)), rows.GetString(2), rows.GetGuid(3)));
            }
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success<IReadOnlyList<TenantAuditEvent>>(items);
        } catch (PostgresException error) when (error.SqlState is "P1400" or "P1403" or "P1404" or "P1500" or "P1601") {
            return Failure(error.SqlState switch { "P1400" => "Audit.InvalidPage", "P1403" or "P1601" => "Access.Forbidden", "P1404" => "Resource.NotFound", _ => "Context.Inconsistent" });
        } catch (NpgsqlException) { return Failure("Dependency.Unavailable"); }
        catch (TimeoutException) { return Failure("Dependency.Unavailable"); }
    }
    private static Result<IReadOnlyList<TenantAuditEvent>> Failure(string code) =>
        Result.Failure<IReadOnlyList<TenantAuditEvent>>(new(code, "Audit query cannot be completed."));
}
