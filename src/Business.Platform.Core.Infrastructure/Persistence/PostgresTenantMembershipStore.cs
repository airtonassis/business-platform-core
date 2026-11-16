using System.Text.Json;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Npgsql;

namespace Business.Platform.Core.Infrastructure.Persistence;

/// <summary>Dedicated Tenant mutation pool; no fallback to identity, Platform or audit credentials.</summary>
public sealed class TenantMembershipDataSource(NpgsqlDataSource? source) : IAsyncDisposable
{
    /// <summary>Unavailable configuration is a closed dependency failure.</summary>
    public NpgsqlDataSource? Source { get; }=source;
    /// <inheritdoc />
    public ValueTask DisposeAsync()=>Source?.DisposeAsync() ?? ValueTask.CompletedTask;
}

/// <summary>Calls the explicit C08 port and commits events with the membership transition.</summary>
public sealed class PostgresTenantMembershipStore(TenantMembershipDataSource source) : ITenantMembershipStore
{
    /// <inheritdoc />
    public async Task<Result<TenantMembership>> ExecuteAsync(OidcSubject subject,Guid organization,Guid membership,
        MembershipAction action,long expectedVersion,Guid operation,CancellationToken token)
    {
        if(source.Source is null) return Fail("Dependency.Unavailable");
        try {
            await using var connection=await source.Source.OpenConnectionAsync(token);
            await using var transaction=await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,token);
            if(action!=MembershipAction.Read) {
                await using var writable=new NpgsqlCommand("SET TRANSACTION READ WRITE",connection,transaction);
                await writable.ExecuteNonQueryAsync(token);
            }
            await using var command=new NpgsqlCommand("SELECT bpc_security.tenant_membership($1,$2,$3,$4,$5,$6,$7)::text",connection,transaction);
            command.Parameters.AddWithValue(subject.Issuer);command.Parameters.AddWithValue(subject.Subject);
            command.Parameters.AddWithValue(organization);command.Parameters.AddWithValue(membership);
            command.Parameters.AddWithValue(action.ToString());command.Parameters.AddWithValue(expectedVersion);command.Parameters.AddWithValue(operation);
            var json=(string)(await command.ExecuteScalarAsync(token))!;
            var value=JsonSerializer.Deserialize<TenantMembership>(json,new JsonSerializerOptions { PropertyNameCaseInsensitive=true })!;
            await transaction.CommitAsync(token);
            return Result.Success(value);
        } catch(PostgresException e) when(e.SqlState is "P1703" or "P1704" or "P1001" or "P1002") {
            return Fail(e.SqlState switch { "P1704"=>"Resource.NotFound", "P1002"=>"Membership.VersionConflict", _=>"Access.Forbidden" });
        } catch(NpgsqlException) { return Fail("Dependency.Unavailable"); }
        catch(TimeoutException) { return Fail("Dependency.Unavailable"); }
    }
    private static Result<TenantMembership> Fail(string code)=>Result.Failure<TenantMembership>(new(code,"Membership operation cannot be completed."));
}
