using DevSync.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DevSync.Tests;

public sealed class DatabaseConnectionTests
{
    [Fact]
    public void Container_components_preserve_password_punctuation_and_service_host()
    {
        const string password = "p;a\"ss=word";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Postgres:Host"] = "postgres", ["Postgres:Database"] = "devsync",
            ["Postgres:Username"] = "devsync", ["Postgres:Password"] = password
        }).Build();
        var connection = new NpgsqlConnectionStringBuilder(DatabaseConnection.Resolve(config));
        Assert.Equal("postgres", connection.Host);
        Assert.Equal(5432, connection.Port);
        Assert.Equal(password, connection.Password);
    }

    [Fact]
    public void Explicit_connection_string_takes_precedence_and_incomplete_configuration_stays_unconfigured()
    {
        var explicitConnection = "Host=localhost;Database=native;Username=native;Password=test";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = explicitConnection, ["Postgres:Host"] = "postgres"
        }).Build();
        Assert.Equal(explicitConnection, DatabaseConnection.Resolve(config));
        config["ConnectionStrings:DefaultConnection"] = "";
        Assert.Null(DatabaseConnection.Resolve(config));
    }
}
