using Npgsql;

namespace Giretra.Model;

public static class ConnectionStringBuilder
{
    /// <summary>
    /// Resolves the PostgreSQL connection string from environment variables.
    /// First checks for <c>GIRETRA_CONNECTION_STRING</c>; if not set,
    /// builds the connection string from individual <c>Giretra_Db_*</c> variables.
    /// </summary>
    public static string FromEnvironment()
    {
        var full = Environment.GetEnvironmentVariable("GIRETRA_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(full))
            return full;

        var host = Environment.GetEnvironmentVariable("Giretra_Db_Host")
            ?? throw new InvalidOperationException(
                "Database configuration missing. Set either GIRETRA_CONNECTION_STRING " +
                "or the individual Giretra_Db_* variables (Giretra_Db_Host, Giretra_Db_User, Giretra_Db_Password).");

        var port = Environment.GetEnvironmentVariable("Giretra_Db_Port") ?? "5432";
        var name = Environment.GetEnvironmentVariable("Giretra_Db_Name") ?? "giretra";

        var user = "giretra_app";

        var password = Environment.GetEnvironmentVariable("Giretra_Db_Password")
            ?? throw new InvalidOperationException("Giretra_Db_Password environment variable is not set.");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.Parse(port),
            Database = name,
            Username = user,
            Password = password
        };
        return builder.ConnectionString;
    }
}
