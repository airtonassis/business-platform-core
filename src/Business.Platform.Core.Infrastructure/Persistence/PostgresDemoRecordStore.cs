using System.Text.Json;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Business.Platform.Core.Infrastructure.Persistence;

/// <summary>Existing dedicated Tenant pool, parameterized SQL ports and explicit transactions.</summary>
public sealed class PostgresDemoRecordStore(TenantMembershipDataSource source):IDemoRecordStore
{
    private static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
    /// <inheritdoc />
    public Task<Result<DemoRecord>> ReadAsync(OidcSubject subject,Guid org,Guid id,CancellationToken token)=>
        Execute<DemoRecord>(subject,org,"read_demo_records",false,[P(NpgsqlDbType.Uuid,id),P(NpgsqlDbType.Integer,25),P(NpgsqlDbType.Integer,0)],token);
    /// <inheritdoc />
    public Task<Result<DemoRecordPage>> ListAsync(OidcSubject subject,Guid org,int limit,int offset,CancellationToken token)=>
        Execute<DemoRecordPage>(subject,org,"read_demo_records",false,[P(NpgsqlDbType.Uuid,DBNull.Value),P(NpgsqlDbType.Integer,limit),P(NpgsqlDbType.Integer,offset)],token);
    /// <inheritdoc />
    public Task<Result<DemoRecordCreated>> CreateAsync(OidcSubject subject,Guid org,string title,Guid key,CancellationToken token)=>
        Execute<DemoRecordCreated>(subject,org,"create_demo_record",true,[P(NpgsqlDbType.Text,title),P(NpgsqlDbType.Uuid,key)],token);
    /// <inheritdoc />
    public Task<Result<DemoRecord>> UpdateAsync(OidcSubject subject,Guid org,Guid id,string title,long version,CancellationToken token)=>
        Execute<DemoRecord>(subject,org,"update_demo_record",true,[P(NpgsqlDbType.Uuid,id),P(NpgsqlDbType.Text,title),P(NpgsqlDbType.Bigint,version)],token);
    private static NpgsqlParameter P(NpgsqlDbType type,object value)=>new(){NpgsqlDbType=type,Value=value};
    private async Task<Result<T>> Execute<T>(OidcSubject subject,Guid org,string port,bool write,NpgsqlParameter[] parameters,CancellationToken token)
    {
        if(source.Source is null) return Fail<T>("Dependency.Unavailable");
        var committing=false;
        try {
            await using var connection=await source.Source.OpenConnectionAsync(token);
            await using var transaction=await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,token);
            if(write) {
                await using var writable=new NpgsqlCommand("SET TRANSACTION READ WRITE",connection,transaction);
                await writable.ExecuteNonQueryAsync(token);
            }
            // Port is selected exclusively by the methods above, never from request data.
            await using var command=new NpgsqlCommand("SELECT bpc_security."+port+"("+string.Join(',',Enumerable.Range(1,3+parameters.Length).Select(i=>"$"+i))+ ")::text",connection,transaction);
            command.Parameters.AddWithValue(subject.Issuer);command.Parameters.AddWithValue(subject.Subject);command.Parameters.AddWithValue(org);
            command.Parameters.AddRange(parameters);
            var json=(string)(await command.ExecuteScalarAsync(token))!;
            var value=JsonSerializer.Deserialize<T>(json,Json)??throw new JsonException();
            committing=true;
            await transaction.CommitAsync(token);
            return Result.Success(value);
        } catch(PostgresException error) {
            return Fail<T>(error.SqlState switch {
                "P1904"=>"Resource.NotFound","P1903"=>"Access.Forbidden","P1900"=>"DemoRecord.InvalidInput",
                "P1002"=>"DemoRecord.VersionConflict","P1003"=>"Idempotency.PayloadMismatch","P1909"=>"Idempotency.Expired",
                _=>"Dependency.Unavailable"});
        } catch(NpgsqlException) { return Fail<T>(write && committing?"Operation.OutcomeUnknown":"Dependency.Unavailable"); }
        catch(TimeoutException) { return Fail<T>(write && committing?"Operation.OutcomeUnknown":"Dependency.Unavailable"); }
        catch(OperationCanceledException) when(write && committing) { return Fail<T>("Operation.OutcomeUnknown"); }
        catch(JsonException) { return Fail<T>("Dependency.Unavailable"); }
    }
    private static Result<T> Fail<T>(string code)=>Result.Failure<T>(new(code,"DemoRecord operation cannot be completed."));
}
