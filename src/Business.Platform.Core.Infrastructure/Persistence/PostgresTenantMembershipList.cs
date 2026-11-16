using System.Text.Json;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Npgsql;

namespace Business.Platform.Core.Infrastructure.Persistence;

/// <summary>Dedicated Tenant pool and read-only transaction.</summary>
public sealed class PostgresTenantMembershipList(TenantMembershipDataSource source):ITenantMembershipList
{
    /// <inheritdoc />
    public async Task<Result<TenantMembershipPage>> ReadAsync(OidcSubject subject,Guid organization,int limit,int offset,CancellationToken token)
    {
        if(source.Source is null)return Fail("Dependency.Unavailable");
        try {
            await using var connection=await source.Source.OpenConnectionAsync(token);
            await using var tx=await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,token);
            await using(var readOnly=new NpgsqlCommand("SET TRANSACTION READ ONLY",connection,tx))await readOnly.ExecuteNonQueryAsync(token);
            await using var command=new NpgsqlCommand("SELECT bpc_security.list_tenant_memberships($1,$2,$3,$4,$5)::text",connection,tx);
            command.Parameters.AddWithValue(subject.Issuer);command.Parameters.AddWithValue(subject.Subject);command.Parameters.AddWithValue(organization);command.Parameters.AddWithValue(limit);command.Parameters.AddWithValue(offset);
            var value=JsonSerializer.Deserialize<TenantMembershipPage>((string)(await command.ExecuteScalarAsync(token))!,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
            await tx.CommitAsync(token);return Result.Success(value);
        } catch(PostgresException error) when(error.SqlState is "P1703" or "P1704" or "P1700") {
            return Fail(error.SqlState switch{"P1703"=>"Access.Forbidden","P1700"=>"Membership.InvalidPage",_=>"Resource.NotFound"});
        } catch(NpgsqlException) {return Fail("Dependency.Unavailable");}
        catch(TimeoutException) {return Fail("Dependency.Unavailable");}
    }
    private static Result<TenantMembershipPage> Fail(string code)=>Result.Failure<TenantMembershipPage>(new(code,"Membership list unavailable."));
}
