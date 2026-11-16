using System.Text.Json;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Business.Platform.Core.Infrastructure.Persistence;

/// <summary>Tenant pool issues/cancels/replaces; identity pool accepts only its recipient's invitation.</summary>
public sealed class PostgresInvitationStore(TenantMembershipDataSource tenant,NpgsqlDataSource identity) : IInvitationStore
{
    private static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
    /// <inheritdoc />
    public Task<Result<InvitationOutcome>> ManageAsync(OidcSubject subject,InvitationCommand command,CancellationToken token)=>
        Call<InvitationOutcome>(tenant.Source,subject,command,null,false,0,token);
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InvitationDetails>>> ListAsync(OidcSubject subject,InvitationCommand command,CancellationToken token)
    {
        var result=await Call<InvitationDetails[]>(tenant.Source,subject,command,null,false,0,token);
        return result.IsFailure?Result.Failure<IReadOnlyList<InvitationDetails>>(result.Error):Result.Success<IReadOnlyList<InvitationDetails>>(result.Value);
    }
    /// <inheritdoc />
    public Task<Result<InvitationOutcome>> RecipientAsync(OidcSubject subject,Guid invitationId,byte[] hash,bool accept,long version,CancellationToken token)=>
        Call<InvitationOutcome>(identity,subject,new(Guid.Empty,"Recipient",invitationId),hash,accept,version,token);
    private static async Task<Result<T>> Call<T>(NpgsqlDataSource? pool,OidcSubject subject,InvitationCommand request,byte[]? hash,bool accept,long version,CancellationToken token)
    {
        if(pool is null)return Fail<T>("Dependency.Unavailable");
        var recipient=request.Action=="Recipient";
        var writable=recipient?accept:request.Action is "Issue" or "Cancel" or "Replace";
        try {
            await using var connection=await pool.OpenConnectionAsync(token);
            await using var transaction=await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,token);
            if(writable){await using var write=new NpgsqlCommand("SET TRANSACTION READ WRITE",connection,transaction);await write.ExecuteNonQueryAsync(token);}
            await using var command=new NpgsqlCommand(recipient?
                "SELECT bpc_security.recipient_invitation($1,$2,$3,$4,$5,$6)::text":
                "SELECT bpc_security.manage_invitation($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)::text",connection,transaction);
            void Add(NpgsqlDbType type,object? value)=>command.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=type,Value=value??DBNull.Value});
            Add(NpgsqlDbType.Text,subject.Issuer);Add(NpgsqlDbType.Text,subject.Subject);
            if(recipient){Add(NpgsqlDbType.Uuid,request.InvitationId);Add(NpgsqlDbType.Bytea,hash);Add(NpgsqlDbType.Boolean,accept);Add(NpgsqlDbType.Bigint,version);}
            else {
                Add(NpgsqlDbType.Uuid,request.OrganizationId);Add(NpgsqlDbType.Text,request.Action);Add(NpgsqlDbType.Uuid,request.InvitationId);
                Add(NpgsqlDbType.Uuid,request.RecipientUserId);Add(NpgsqlDbType.Bytea,request.ReferenceHash);Add(NpgsqlDbType.Bytea,request.SecretHash);
                Add(NpgsqlDbType.Bigint,request.Version);Add(NpgsqlDbType.Uuid,request.Key);Add(NpgsqlDbType.Integer,request.Limit);
                Add(NpgsqlDbType.TimestampTz,request.Before?.Time);Add(NpgsqlDbType.Uuid,request.Before?.EventId);Add(NpgsqlDbType.Text,request.StateFilter);
            }
            var text=(string)(await command.ExecuteScalarAsync(token))!;
            var value=JsonSerializer.Deserialize<T>(text,Json)!;
            await transaction.CommitAsync(token);return Result.Success(value);
        } catch(PostgresException e) {
            return Fail<T>(e.SqlState switch {
                "P1800"=>"Invitation.InvalidRequest","P1803"=>"Access.Forbidden","P1804"=>"Invitation.Unavailable",
                "P1809" or "23505"=>"Invitation.Conflict","P1004"=>"Recipient.Unavailable","P1002"=>"Invitation.VersionConflict",
                "P1003"=>"Idempotency.PayloadMismatch",
                "P1429"=>"RateLimit.Exceeded",_=>"Dependency.Unavailable"
            });
        } catch(NpgsqlException){return Fail<T>(writable?"Operation.OutcomeUnknown":"Dependency.Unavailable");}
        catch(TimeoutException){return Fail<T>(writable?"Operation.OutcomeUnknown":"Dependency.Unavailable");}
    }
    private static Result<T> Fail<T>(string code)=>Result.Failure<T>(new(code,"Invitation operation cannot be completed."));
}
