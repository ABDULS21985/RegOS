using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests;

/// <summary>
/// A simple IDbContextFactory adapter for tests.
///
/// When constructed with a MetadataDbContext instance, returns that same instance
/// on every call (legacy mode — only safe when the consumer does not dispose).
///
/// When constructed with a database name, creates a fresh MetadataDbContext on each
/// call pointing to the same in-memory database (safe when the consumer disposes).
///
/// When constructed with DbContextOptions, creates a fresh MetadataDbContext on each
/// call using those options.
/// </summary>
internal sealed class TestDbContextFactory : IDbContextFactory<MetadataDbContext>
{
    private readonly MetadataDbContext? _sharedInstance;
    private readonly DbContextOptions<MetadataDbContext>? _options;

    /// <summary>
    /// Legacy: returns the same context instance every time.
    /// Only use when the consumer does NOT dispose the context.
    /// </summary>
    public TestDbContextFactory(MetadataDbContext db) => _sharedInstance = db;

    /// <summary>
    /// Creates a new context per call, sharing the same in-memory database by name.
    /// Safe when the consumer disposes contexts.
    /// </summary>
    public TestDbContextFactory(string databaseName)
    {
        _options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    /// <summary>
    /// Creates a new context per call using the given options.
    /// </summary>
    public TestDbContextFactory(DbContextOptions<MetadataDbContext> options)
    {
        _options = options;
    }

    public MetadataDbContext CreateDbContext()
    {
        if (_sharedInstance is not null)
            return _sharedInstance;

        return new MetadataDbContext(_options!);
    }
}
