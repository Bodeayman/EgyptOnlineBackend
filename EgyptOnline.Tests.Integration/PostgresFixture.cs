using EgyptOnline.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EgyptOnline.Tests.Integration;

/// <summary>
/// Shared fixture that owns ONE real PostgreSQL connection per test collection.
/// Wipes and recreates the schema before the collection runs, then lets each
/// individual test reset data via ResetDatabaseAsync().
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string _connectionString;

    public PostgresFixture()
    {
        // Prefer an explicit env-var so GitHub Actions can inject it easily,
        // then fall back to appsettings.json.
        var envCs = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(envCs))
        {
            _connectionString = envCs;
            return;
        }

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        _connectionString = config.GetConnectionString("TestDb")
            ?? throw new InvalidOperationException(
                "TEST_DB_CONNECTION env var or TestDb connection string must be set.");
    }

    public DbContextOptions<ApplicationDbContext> Options =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

    public ApplicationDbContext GetDbContext() => new(Options);

    public async Task InitializeAsync()
    {
        using var db = GetDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Deletes and recreates all tables — call from each test's InitializeAsync.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var db = GetDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// XUnit collection fixture — all integration test classes that share a database
/// declare [Collection("Integration")] to get one fixture instance.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<PostgresFixture> { }
