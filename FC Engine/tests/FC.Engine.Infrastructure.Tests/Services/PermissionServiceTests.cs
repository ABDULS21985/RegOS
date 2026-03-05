using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests.Services;

public class PermissionServiceTests
{
    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetadataDbContext(options);
    }

    [Fact]
    public async Task Admin_Role_Has_All_Permissions_Except_PlatformAdmin()
    {
        await using var db = CreateDb();
        var sut = new PermissionService(db);

        var permissions = await sut.GetPermissions(Guid.NewGuid(), "Admin");

        permissions.Should().Contain("submission.create");
        permissions.Should().Contain("billing.manage");
        permissions.Should().NotContain("admin.platform");
    }

    [Fact]
    public async Task Maker_Cannot_Approve_Submissions()
    {
        await using var db = CreateDb();
        var sut = new PermissionService(db);

        var canApprove = await sut.HasPermission(Guid.NewGuid(), "Maker", "submission.approve");
        var canCreate = await sut.HasPermission(Guid.NewGuid(), "Maker", "submission.create");

        canApprove.Should().BeFalse();
        canCreate.Should().BeTrue();
    }

    [Fact]
    public async Task Custom_Role_With_Subset_Of_Permissions()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();

        var role = new Role
        {
            TenantId = tenantId,
            RoleName = "OpsLimited",
            IsSystemRole = false,
            IsActive = true
        };
        var p1 = new Permission { PermissionCode = "report.read", Description = "Read reports", Category = "report", IsActive = true };
        var p2 = new Permission { PermissionCode = "submission.read", Description = "Read submissions", Category = "submission", IsActive = true };
        db.Roles.Add(role);
        db.Permissions.AddRange(p1, p2);
        await db.SaveChangesAsync();

        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = p1.Id });
        await db.SaveChangesAsync();

        var sut = new PermissionService(db);
        var permissions = await sut.GetPermissions(tenantId, "OpsLimited");

        permissions.Should().ContainSingle().Which.Should().Be("report.read");
    }
}
