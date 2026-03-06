using System.Security.Claims;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Hubs;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FC.Engine.Infrastructure.Tests.Hubs;

public class ReturnLockHubTests
{
    private static readonly Guid TestTenantId = Guid.NewGuid();
    private const int TestUserId = 42;
    private const string TestUserName = "test-user";

    [Fact]
    public async Task SendHeartbeat_Extends_Lock_Expiry()
    {
        await using var db = CreateDb(nameof(SendHeartbeat_Extends_Lock_Expiry));
        var lockService = new ReturnLockService(db);
        await lockService.AcquireLock(TestTenantId, 101, TestUserId, TestUserName);

        var originalLock = await db.ReturnLocks.SingleAsync();
        var originalExpiry = originalLock.ExpiresAt;

        // Simulate small time passing
        await Task.Delay(50);

        var (hub, _) = CreateHub(db, lockService);
        var response = await hub.SendHeartbeat(101);

        response.Acquired.Should().BeTrue();

        var updatedLock = await db.ReturnLocks.SingleAsync();
        updatedLock.ExpiresAt.Should().BeAfter(originalExpiry);
    }

    [Fact]
    public async Task SendHeartbeat_Returns_False_When_Lock_Owned_By_Other()
    {
        await using var db = CreateDb(nameof(SendHeartbeat_Returns_False_When_Lock_Owned_By_Other));
        var lockService = new ReturnLockService(db);

        // Another user holds the lock
        await lockService.AcquireLock(TestTenantId, 201, 99, "OtherUser");

        var (hub, mockCaller) = CreateHub(db, lockService);
        var response = await hub.SendHeartbeat(201);

        response.Acquired.Should().BeFalse();
        response.Message.Should().Contain("OtherUser");

        // Verify LockLost was sent to caller
        mockCaller.Verify(c => c.SendCoreAsync(
            "LockLost",
            It.Is<object?[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendHeartbeat_Returns_False_When_No_Lock_Exists()
    {
        await using var db = CreateDb(nameof(SendHeartbeat_Returns_False_When_No_Lock_Exists));
        var lockService = new ReturnLockService(db);

        var (hub, _) = CreateHub(db, lockService);
        var response = await hub.SendHeartbeat(999);

        response.Acquired.Should().BeFalse();
        response.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task OnDisconnectedAsync_Releases_Lock()
    {
        await using var db = CreateDb(nameof(OnDisconnectedAsync_Releases_Lock));
        var lockService = new ReturnLockService(db);
        await lockService.AcquireLock(TestTenantId, 301, TestUserId, TestUserName);

        (await db.ReturnLocks.CountAsync()).Should().Be(1);

        var (hub, _) = CreateHub(db, lockService);

        // Simulate JoinSubmission to register the connection
        await hub.JoinSubmission(301);

        // Simulate disconnect
        await hub.OnDisconnectedAsync(null);

        (await db.ReturnLocks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OnDisconnectedAsync_Without_Join_Does_Not_Throw()
    {
        await using var db = CreateDb(nameof(OnDisconnectedAsync_Without_Join_Does_Not_Throw));
        var lockService = new ReturnLockService(db);

        var (hub, _) = CreateHub(db, lockService, connectionId: "no-join-conn");

        var act = () => hub.OnDisconnectedAsync(null);
        await act.Should().NotThrowAsync();
    }

    private static MetadataDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MetadataDbContext(options);
    }

    private static (ReturnLockHub Hub, Mock<ISingleClientProxy> MockCaller) CreateHub(
        MetadataDbContext db,
        IReturnLockService lockService,
        string connectionId = "test-connection-1")
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(lockService);
        var provider = services.BuildServiceProvider();

        var hub = new ReturnLockHub(
            provider,
            NullLogger<ReturnLockHub>.Instance);

        // Mock HubCallerContext
        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns(connectionId);

        var claims = new List<Claim>
        {
            new("TenantId", TestTenantId.ToString()),
            new(ClaimTypes.NameIdentifier, TestUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        mockContext.Setup(c => c.User).Returns(principal);

        // Mock Clients
        var mockCaller = new Mock<ISingleClientProxy>();
        var mockGroupProxy = new Mock<IClientProxy>();
        var mockClients = new Mock<IHubCallerClients>();
        mockClients.Setup(c => c.Caller).Returns(mockCaller.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockGroupProxy.Object);

        // Mock Groups
        var mockGroups = new Mock<IGroupManager>();
        mockGroups.Setup(g => g.AddToGroupAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockGroups.Setup(g => g.RemoveFromGroupAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hub.Context = mockContext.Object;
        hub.Clients = mockClients.Object;
        hub.Groups = mockGroups.Object;

        return (hub, mockCaller);
    }
}
