using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Audit;
using FC.Engine.Infrastructure.Metadata;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FC.Engine.Infrastructure.Tests.Audit;

public class EvidencePackageServiceTests
{
    [Fact]
    public async Task ZIP_Contains_All_Seven_Artifacts()
    {
        await using var db = CreateDb(nameof(ZIP_Contains_All_Seven_Artifacts));
        var tenantId = Guid.NewGuid();
        var submission = await SeedSubmission(db, tenantId);
        var (storage, storedFiles) = CreateStorage();
        var sut = CreateService(db, tenantId, storage);

        var package = await sut.GenerateAsync(submission.Id, "test-user");

        storedFiles.Should().ContainSingle();
        var zipBytes = storedFiles.Values.First();

        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entryNames = archive.Entries.Select(e => e.FullName).ToList();
        entryNames.Should().Contain("data_snapshot.json");
        entryNames.Should().Contain("data_hash.sha256");
        entryNames.Should().Contain("approval_chain.json");
        entryNames.Should().Contain("validation_report.json");
        entryNames.Should().Contain("audit_trail.json");
        entryNames.Should().Contain("attachment_manifest.json");
        entryNames.Should().Contain("submission_metadata.json");
    }

    [Fact]
    public async Task Package_Hash_Matches_ZIP_Content()
    {
        await using var db = CreateDb(nameof(Package_Hash_Matches_ZIP_Content));
        var tenantId = Guid.NewGuid();
        var submission = await SeedSubmission(db, tenantId);
        var (storage, storedFiles) = CreateStorage();
        var sut = CreateService(db, tenantId, storage);

        var package = await sut.GenerateAsync(submission.Id, "test-user");

        var zipBytes = storedFiles.Values.First();
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(zipBytes));

        package.PackageHash.Should().Be(expectedHash);
    }

    [Fact]
    public async Task StoragePath_Uses_Immutable_Upload()
    {
        await using var db = CreateDb(nameof(StoragePath_Uses_Immutable_Upload));
        var tenantId = Guid.NewGuid();
        var submission = await SeedSubmission(db, tenantId);
        var (storage, _) = CreateStorage();
        var sut = CreateService(db, tenantId, storage);

        var package = await sut.GenerateAsync(submission.Id, "test-user");

        package.StoragePath.Should().StartWith("evidence/");
        package.StoragePath.Should().Contain(tenantId.ToString());
    }

    [Fact]
    public async Task Missing_Submission_Throws()
    {
        await using var db = CreateDb(nameof(Missing_Submission_Throws));
        var tenantId = Guid.NewGuid();
        var (storage, _) = CreateStorage();
        var sut = CreateService(db, tenantId, storage);

        var act = () => sut.GenerateAsync(9999, "test-user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*9999*not found*");
    }

    [Fact]
    public async Task Package_Record_Saved_To_Database()
    {
        await using var db = CreateDb(nameof(Package_Record_Saved_To_Database));
        var tenantId = Guid.NewGuid();
        var submission = await SeedSubmission(db, tenantId);
        var (storage, _) = CreateStorage();
        var sut = CreateService(db, tenantId, storage);

        var package = await sut.GenerateAsync(submission.Id, "test-user");

        var saved = await db.EvidencePackages.SingleAsync();
        saved.SubmissionId.Should().Be(submission.Id);
        saved.TenantId.Should().Be(tenantId);
        saved.GeneratedBy.Should().Be("test-user");
        saved.FileSizeBytes.Should().BeGreaterThan(0);
    }

    private static MetadataDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MetadataDbContext(options);
    }

    private static async Task<Submission> SeedSubmission(MetadataDbContext db, Guid tenantId)
    {
        var institution = new Institution
        {
            InstitutionName = "Test Bank",
            InstitutionCode = "TB001",
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync();

        var period = new ReturnPeriod
        {
            TenantId = tenantId,
            Year = 2026,
            Month = 1,
            Frequency = "Monthly",
            ReportingDate = new DateTime(2026, 1, 31),
            IsOpen = true,
            CreatedAt = DateTime.UtcNow,
            Status = "Open",
            DeadlineDate = DateTime.UtcNow.AddDays(30)
        };
        db.ReturnPeriods.Add(period);
        await db.SaveChangesAsync();

        var submission = Submission.Create(institution.Id, period.Id, "CBN_MBR", tenantId);
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        return submission;
    }

    private static (IFileStorageService Service, Dictionary<string, byte[]> Files) CreateStorage()
    {
        var files = new Dictionary<string, byte[]>();
        var mock = new Mock<IFileStorageService>();
        mock.Setup(s => s.UploadImmutableAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, Stream, string, CancellationToken>(async (path, content, _, _) =>
            {
                using var ms = new MemoryStream();
                await content.CopyToAsync(ms);
                files[path] = ms.ToArray();
                return path;
            });
        return (mock.Object, files);
    }

    private static EvidencePackageService CreateService(
        MetadataDbContext db,
        Guid tenantId,
        IFileStorageService storage)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.CurrentTenantId).Returns(tenantId);

        var dataRepo = new Mock<IGenericDataRepository>();
        dataRepo.Setup(r => r.GetBySubmission(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReturnDataRecord?)null);

        return new EvidencePackageService(db, dataRepo.Object, storage, tenantContext.Object);
    }
}
