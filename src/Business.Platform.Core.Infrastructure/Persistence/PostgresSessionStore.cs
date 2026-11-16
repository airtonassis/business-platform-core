using Npgsql;
using NpgsqlTypes;

namespace Business.Platform.Core.Infrastructure.Persistence;

// Technical store only: no business context, shared Core pool, or enterprise SQL authority.
public sealed class PostgresSessionStore : IDisposable
{
    private readonly NpgsqlDataSource source;
    public PostgresSessionStore(PostgresOptions options)
    {
        if(options.Name!="bpc_sessions_local" || options.Username!="bpc_session_runtime" ||
           !(options.Host is "localhost" or "127.0.0.1" or "::1"))
            throw new ArgumentException("Local session database configuration is invalid.");
        var connection=new NpgsqlConnectionStringBuilder(options.ConnectionString()) {
            ApplicationName="bpc-local-sessions",Options="-c default_transaction_read_only=off",Timeout=3,CommandTimeout=3
        };
        source=NpgsqlDataSource.Create(connection.ConnectionString);
    }
    public async Task StoreAsync(string key,string? old,byte[] identity,byte[] ticket,string subjectScope,string? sidScope,string? pairScope,long issued)
    {
        await using var command=source.CreateCommand("SELECT bpc_session.store_oidc_session($1,$2,$3,$4,$5,$6,$7,$8)");
        command.Parameters.AddWithValue(key);
        command.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Text,Value=(object?)old??DBNull.Value});
        command.Parameters.AddWithValue(identity);command.Parameters.AddWithValue(ticket);
        command.Parameters.AddWithValue(subjectScope);
        command.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Text,Value=(object?)sidScope??DBNull.Value});
        command.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Text,Value=(object?)pairScope??DBNull.Value});
        command.Parameters.AddWithValue(issued);
        await command.ExecuteNonQueryAsync();
    }
    public async Task<bool> LogoutAsync(string receipt,byte[] contentHash,string? subject,string? sid,string selector,long issued,long expires)
    {
        await using var command=source.CreateCommand("SELECT bpc_session.apply_oidc_logout($1,$2,$3,$4,$5,$6,$7)");
        command.Parameters.AddWithValue(receipt);command.Parameters.AddWithValue(contentHash);
        command.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Text,Value=(object?)subject??DBNull.Value});
        command.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Text,Value=(object?)sid??DBNull.Value});
        command.Parameters.AddWithValue(selector);command.Parameters.AddWithValue(issued);command.Parameters.AddWithValue(expires);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
    public async Task RenewAsync(string key,byte[] identity,byte[] ticket)
    {
        await using var command=source.CreateCommand("SELECT bpc_session.renew_session($1,$2,$3)");
        command.Parameters.AddWithValue(key);command.Parameters.AddWithValue(identity);command.Parameters.AddWithValue(ticket);
        await command.ExecuteNonQueryAsync();
    }
    public async Task<(byte[] Ticket,DateTimeOffset Expires)?> RetrieveAsync(string key)
    {
        await using var command=source.CreateCommand("SELECT * FROM bpc_session.retrieve_session($1)");
        command.Parameters.AddWithValue(key);await using var reader=await command.ExecuteReaderAsync();
        return await reader.ReadAsync()? (reader.GetFieldValue<byte[]>(0),reader.GetFieldValue<DateTimeOffset>(1)):null;
    }
    public async Task RemoveAsync(string key)
    {
        await using var command=source.CreateCommand("SELECT bpc_session.remove_session($1)");
        command.Parameters.AddWithValue(key);await command.ExecuteNonQueryAsync();
    }
    public void Dispose()=>source.Dispose();
}
