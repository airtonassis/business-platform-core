using System.Text.Json;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Npgsql;
namespace Business.Platform.Core.Infrastructure.Persistence;

/// <summary>Only narrow SQL ports; no tenant data query, DML, or bootstrap credential in runtime.</summary>
public sealed class PostgresEnterpriseContextReader(NpgsqlDataSource source) : IEnterpriseContextReader, IAuthenticatedIdentityBootstrap
{
    public bool IsProvisioned => true;
    public async Task<bool> CheckProvisionedAsync(CancellationToken cancellationToken)
    {
        try {
            await using var connection = await source.OpenConnectionAsync(cancellationToken);
            await using var query = new NpgsqlCommand("SELECT bpc_security.persistence_ready()", connection);
            return await query.ExecuteScalarAsync(cancellationToken) is true;
        } catch (NpgsqlException) { return false; }
        catch (TimeoutException) { return false; }
    }
    public async Task RegisterAsync(OidcSubject subject, CancellationToken cancellationToken)
    {
        if (!await CheckProvisionedAsync(cancellationToken)) throw new NpgsqlException("Enterprise schema unavailable.");
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        // Login defaults read-only; this separate authentication operation has only EXECUTE,
        // never direct INSERT or organization bootstrap. Cancellation rolls the whole transaction back.
        await using var writable = new NpgsqlCommand("SET TRANSACTION READ WRITE", connection, tx);
        await writable.ExecuteNonQueryAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT bpc_security.register_identity($1,$2)", connection, tx);
        command.Parameters.AddWithValue(subject.Issuer); command.Parameters.AddWithValue(subject.Subject);
        await command.ExecuteScalarAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }
    public async Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject subject, CancellationToken cancellationToken)
    {
        try {
            if (!await CheckProvisionedAsync(cancellationToken))
                return Result.Failure<EnterpriseContextSnapshot>(new("Dependency.Unavailable","Enterprise schema unavailable."));
            await using var connection = await source.OpenConnectionAsync(cancellationToken);
            await using var tx = await connection.BeginTransactionAsync(cancellationToken);
            await using var query = new NpgsqlCommand("SELECT bpc_security.resolve_context($1,$2)::text", connection, tx);
            query.Parameters.AddWithValue(subject.Issuer); query.Parameters.AddWithValue(subject.Subject);
            var payload = (string)(await query.ExecuteScalarAsync(cancellationToken))!;
            using var parsed = JsonDocument.Parse(payload);
            var data = parsed.RootElement;
            var state = data.GetProperty("state").GetString();
            var id = data.TryGetProperty("identityId",out var identity) ? identity.GetGuid() : Guid.Empty;
            EnterpriseContextSnapshot snapshot;
            if (state == "NotLinked") snapshot = new(id,new(BindingHistoryState.NeverConsumed,null),[]);
            else if (state == "Denied") snapshot = new(id,new(BindingHistoryState.Unknown,null),[],false);
            else if (state == "Linked") {
                var org = data.GetProperty("organizationId").GetGuid();
                var account = data.GetProperty("accountId").GetGuid();
                snapshot = new(id,new(BindingHistoryState.Consumed,org),[new(id,account,org,account,org,true,true,true,
                    data.GetProperty("role").GetString()!, data.GetProperty("organizationName").GetString()!,data.GetProperty("membershipId").GetGuid())]);
            } else snapshot = new(id,new(BindingHistoryState.Unknown,null),[]);
            await tx.RollbackAsync(cancellationToken);
            return Result.Success(snapshot);
        } catch (NpgsqlException) { return Result.Failure<EnterpriseContextSnapshot>(new("Dependency.Unavailable","Enterprise dependency unavailable.")); }
        catch (TimeoutException) { return Result.Failure<EnterpriseContextSnapshot>(new("Dependency.Unavailable","Enterprise dependency unavailable.")); }
    }
}
