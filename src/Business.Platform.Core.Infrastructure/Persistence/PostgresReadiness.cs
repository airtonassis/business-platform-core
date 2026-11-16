using Business.Platform.Core.Application.Operations;
using Npgsql;

namespace Business.Platform.Core.Infrastructure.Persistence;

public sealed class PostgresReadiness(NpgsqlDataSource source) : IDatabaseReadiness
{
    public async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await source.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var query = new NpgsqlCommand("""
                SELECT NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb
                  AND NOT r.rolcreaterole AND NOT r.rolreplication
                  AND NOT has_schema_privilege(current_user, 'public', 'CREATE')
                  AND current_setting('transaction_read_only')='on'
                FROM pg_roles r WHERE r.rolname=current_user
                """, connection, transaction);
            var healthy = await query.ExecuteScalarAsync(cancellationToken) is true;
            await transaction.RollbackAsync(cancellationToken);
            return healthy;
        }
        catch (NpgsqlException) { return false; }
        catch (TimeoutException) { return false; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }
}
