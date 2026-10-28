using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DevSync.Infrastructure.Persistence;

public static class DatabaseConnection
{
    public static string? Resolve(IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connection)) return connection;
        var host = configuration["Postgres:Host"];
        var database = configuration["Postgres:Database"];
        var username = configuration["Postgres:Username"];
        var password = configuration["Postgres:Password"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) ||
            string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return null;
        // Build components safely: punctuation in passwords must remain data.
        return new NpgsqlConnectionStringBuilder
        {
            Host = host, Port = configuration.GetValue("Postgres:Port", 5432),
            Database = database, Username = username, Password = password
        }.ConnectionString;
    }
}
