using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests;

/// <summary>
/// A simple IDbContextFactory adapter that returns an existing MetadataDbContext instance.
/// Use this in tests where a single shared in-memory context is needed.
/// </summary>
internal sealed class TestDbContextFactory : IDbContextFactory<MetadataDbContext>
{
    private readonly MetadataDbContext _db;

    public TestDbContextFactory(MetadataDbContext db) => _db = db;

    public MetadataDbContext CreateDbContext() => _db;
}
