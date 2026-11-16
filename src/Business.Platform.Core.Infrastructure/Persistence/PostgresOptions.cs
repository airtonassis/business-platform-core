using Npgsql;

namespace Business.Platform.Core.Infrastructure.Persistence;

public sealed class PostgresOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 5432;
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    public string ConnectionString()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(Username);
        ArgumentException.ThrowIfNullOrWhiteSpace(Password);
        if (Port is < 1 or > 65535 || Username.Equals("postgres", StringComparison.OrdinalIgnoreCase) ||
            Username.Contains("bootstrap", StringComparison.OrdinalIgnoreCase) ||
            Username.Contains("migrator", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Operational database configuration is invalid.");
        return new NpgsqlConnectionStringBuilder { Host = Host, Port = Port, Database = Name,
            Username = Username, Password = Password, Pooling = true, MinPoolSize = 0,
            MaxPoolSize = 5, Multiplexing = false, NoResetOnClose = false, IncludeErrorDetail = false,
            Timeout = 3, CommandTimeout = 3, ApplicationName = "business-platform-core-api",
            Options = "-c default_transaction_read_only=on -c statement_timeout=3000 -c lock_timeout=2000"
        }.ConnectionString;
    }

    public override string ToString() => "PostgresOptions [redacted]";
}
