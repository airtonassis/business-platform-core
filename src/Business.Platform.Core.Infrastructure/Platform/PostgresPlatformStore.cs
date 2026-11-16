using System.Text.Json;
using Business.Platform.Core.Application.Platform;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Npgsql;

namespace Business.Platform.Core.Infrastructure.Platform;

/// <summary>Separately owned Platform credential pool; it must retain normal connection reset behavior.</summary>
public sealed class PlatformDataSource(NpgsqlDataSource source) : IAsyncDisposable
{
    public NpgsqlDataSource Source { get; } = source;
    public ValueTask DisposeAsync() => Source.DisposeAsync();
}

/// <summary>Parameterized SECURITY DEFINER ports, with explicit transaction modes and no automatic retries.</summary>
public sealed class PostgresPlatformStore(NpgsqlDataSource identitySource, PlatformDataSource? platformSource = null) : IPlatformStore
{
    public Task<Result<RecipientReferenceIssued>> IssueAsync(OidcSubject subject, byte[] hash, string displayName, CancellationToken cancellationToken) =>
        ExecuteAsync(identitySource, "SELECT bpc_security.issue_reference($1,$2,$3,$4)::text",
            [subject.Issuer, subject.Subject, hash, displayName], true,
            data => new RecipientReferenceIssued(data.GetProperty("userId").GetGuid(), data.GetProperty("expiresAt").GetDateTimeOffset()), cancellationToken);

    public Task<Result<OwnerPreview>> PreviewAsync(Guid userId, byte[] hash, CancellationToken cancellationToken) =>
        ExecuteAsync(platformSource?.Source, "SELECT bpc_security.preview_owner($1,$2)::text", [userId, hash], false,
            data => new OwnerPreview(data.GetProperty("userId").GetGuid(), data.GetProperty("displayName").GetString()!), cancellationToken);

    public Task<Result<PlatformCreation>> CreateAsync(OidcSubject actor, Guid userId, byte[] hash, string name, Guid operation, CancellationToken cancellationToken) =>
        ExecuteAsync(platformSource?.Source, "SELECT bpc_security.platform_bootstrap($1,$2,$3,$4,$5,$6)::text",
            [actor.Issuer, actor.Subject, userId, hash, name, operation], true,
            data => new PlatformCreation(data.GetProperty("organizationId").GetGuid(), data.GetProperty("accountId").GetGuid(),
                data.GetProperty("membershipId").GetGuid(), data.GetProperty("replayed").GetBoolean()), cancellationToken);

    private static async Task<Result<T>> ExecuteAsync<T>(NpgsqlDataSource? source, string sql, object[] parameters,
        bool write, Func<JsonElement, T> parse, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source is null) return Result.Failure<T>(Unavailable());
        var committing = false;
        try
        {
            await using var connection = await source.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var mode = new NpgsqlCommand(write ? "SET TRANSACTION READ WRITE" : "SET TRANSACTION READ ONLY", connection, transaction))
                await mode.ExecuteNonQueryAsync(cancellationToken);
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter);
            var payload = (string)(await command.ExecuteScalarAsync(cancellationToken))!;
            using var document = JsonDocument.Parse(payload);
            var result = parse(document.RootElement);
            if (write)
            {
                committing = true;
                await transaction.CommitAsync(cancellationToken);
                committing = false;
            }
            else await transaction.RollbackAsync(cancellationToken);
            return Result.Success(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (PostgresException exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Result.Failure<T>(committing ? Unconfirmed() : Map(exception.SqlState));
        }
        catch (NpgsqlException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Result.Failure<T>(committing ? Unconfirmed() : Unavailable());
        }
        catch (TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Result.Failure<T>(committing ? Unconfirmed() : Unavailable());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Result.Failure<T>(committing ? Unconfirmed() : Unavailable());
        }
    }

    private static Error Map(string sqlState) => sqlState switch
    {
        "P1004" => new("Recipient.Unavailable", "Recipient reference unavailable.", ErrorType.NotFound),
        "P1409" => new("Idempotency.PayloadMismatch", "The operation identifier was used with different input.", ErrorType.Conflict),
        "P1429" => new("Reference.RateLimited", "Reference issuance limit reached."),
        "P1001" => new("Access.Unavailable", "Access unavailable.", ErrorType.Forbidden),
        _ => Unavailable()
    };

    private static Error Unavailable() => new("Dependency.Unavailable", "Platform dependency unavailable.");
    private static Error Unconfirmed() => new("Operation.Unconfirmed", "Operation outcome could not be confirmed; retain the operation identifier.");
}
