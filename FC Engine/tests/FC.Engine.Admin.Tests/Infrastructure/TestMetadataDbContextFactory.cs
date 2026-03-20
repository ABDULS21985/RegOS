using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FC.Engine.Admin.Tests.Infrastructure;

internal sealed class TestMetadataDbContextFactory : IDbContextFactory<MetadataDbContext>
{
    private readonly DbContextOptions<MetadataDbContext> _options;

    public TestMetadataDbContextFactory(string databaseName)
    {
        var root = new InMemoryDatabaseRoot();
        _options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(databaseName, root)
            .Options;
    }

    public MetadataDbContext CreateDbContext() => new(_options);

    public ValueTask<MetadataDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(CreateDbContext());
}
