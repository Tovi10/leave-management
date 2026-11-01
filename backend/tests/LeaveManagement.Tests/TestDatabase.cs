
using LeaveManagement.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace LeaveManagement.Tests;

public sealed class TestDatabase : IAsyncLifetime
{
    private readonly List<SqliteConnection> _sqliteConnections = new();
    private readonly List<string> _databaseNames = new();
    private PostgreSqlContainer? _postgres;

    public static bool UseSqlite => string.Equals(
        Environment.GetEnvironmentVariable("Database__Provider"),
        "Sqlite",
        StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        if (UseSqlite) return;

        _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await _postgres.StartAsync();
    }

    public LeaveDbContext NewDb(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<LeaveDbContext>();

        if (UseSqlite)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            _sqliteConnections.Add(connection);
            options.UseSqlite(connection);
        }
        else
        {
            if (databaseName is null)
            {
                databaseName = $"leave_test_{Guid.NewGuid():N}";

                var adminBuilder =
                    new NpgsqlConnectionStringBuilder(_postgres!.GetConnectionString())
                    {
                        Database = "postgres"
                    };

                using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
                connection.Open();

                using var command = new NpgsqlCommand(
                    $"CREATE DATABASE \"{databaseName}\"",
                    connection);

                command.ExecuteNonQuery();
                _databaseNames.Add(databaseName);
            }

            var testBuilder =
                new NpgsqlConnectionStringBuilder(_postgres!.GetConnectionString())
                {
                    Database = databaseName
                };

            options.UseNpgsql(testBuilder.ConnectionString);
        }

        var db = new LeaveDbContext(options.Options);
        db.Database.EnsureCreated();

        return db;
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _sqliteConnections)
            connection.Dispose();

        if (_postgres is not null)
        {
            // Close pooled connections before dropping test databases.
            NpgsqlConnection.ClearAllPools();

            var adminBuilder =
                new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
                {
                    Database = "postgres"
                };

            await using var connection =
                new NpgsqlConnection(adminBuilder.ConnectionString);

            await connection.OpenAsync();

            foreach (var databaseName in _databaseNames)
            {
                await using var command = new NpgsqlCommand(
                    $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
                    connection);

                await command.ExecuteNonQueryAsync();
            }

            await _postgres.DisposeAsync();
        }
    }
}
