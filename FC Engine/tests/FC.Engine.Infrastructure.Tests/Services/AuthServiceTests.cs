using FC.Engine.Application.Services;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace FC.Engine.Infrastructure.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IPortalUserRepository> _portalUserRepositoryMock;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _portalUserRepositoryMock = new Mock<IPortalUserRepository>();
        _sut = new AuthService(_portalUserRepositoryMock.Object);
    }

    // ──────────────────────────────────────────────
    // ValidateLogin
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ValidateLogin_WithValidCredentials_ReturnsUserAndUpdatesLastLoginAt()
    {
        // Arrange
        var password = "SecureP@ssw0rd!";
        var hashedPassword = AuthService.HashPassword(password);
        var user = new PortalUser
        {
            Id = 1,
            Username = "testuser",
            DisplayName = "Test User",
            Email = "test@example.com",
            PasswordHash = hashedPassword,
            Role = PortalRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            LastLoginAt = null
        };

        _portalUserRepositoryMock
            .Setup(r => r.GetByUsername("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _portalUserRepositoryMock
            .Setup(r => r.Update(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ValidateLogin("testuser", password);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Username.Should().Be("testuser");
        result.LastLoginAt.Should().NotBeNull();
        result.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _portalUserRepositoryMock.Verify(
            r => r.Update(It.Is<PortalUser>(u => u.Id == 1 && u.LastLoginAt != null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateLogin_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        _portalUserRepositoryMock
            .Setup(r => r.GetByUsername("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PortalUser?)null);

        // Act
        var result = await _sut.ValidateLogin("nonexistent", "anypassword");

        // Assert
        result.Should().BeNull();

        _portalUserRepositoryMock.Verify(
            r => r.Update(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateLogin_WithInactiveUser_ReturnsNull()
    {
        // Arrange
        var password = "SecureP@ssw0rd!";
        var hashedPassword = AuthService.HashPassword(password);
        var user = new PortalUser
        {
            Id = 2,
            Username = "inactiveuser",
            DisplayName = "Inactive User",
            Email = "inactive@example.com",
            PasswordHash = hashedPassword,
            Role = PortalRole.Viewer,
            IsActive = false,
            CreatedAt = DateTime.UtcNow.AddDays(-60),
            LastLoginAt = null
        };

        _portalUserRepositoryMock
            .Setup(r => r.GetByUsername("inactiveuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.ValidateLogin("inactiveuser", password);

        // Assert
        result.Should().BeNull();

        _portalUserRepositoryMock.Verify(
            r => r.Update(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateLogin_WithWrongPassword_ReturnsNull()
    {
        // Arrange
        var correctPassword = "CorrectP@ssw0rd!";
        var wrongPassword = "WrongP@ssw0rd!";
        var hashedPassword = AuthService.HashPassword(correctPassword);
        var user = new PortalUser
        {
            Id = 3,
            Username = "testuser",
            DisplayName = "Test User",
            Email = "test@example.com",
            PasswordHash = hashedPassword,
            Role = PortalRole.Approver,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            LastLoginAt = null
        };

        _portalUserRepositoryMock
            .Setup(r => r.GetByUsername("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.ValidateLogin("testuser", wrongPassword);

        // Assert
        result.Should().BeNull();

        _portalUserRepositoryMock.Verify(
            r => r.Update(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────
    // CreateUser
    // ──────────────────────────────────────────────

    [Fact]
    public async Task CreateUser_WithValidData_CreatesUserWithHashedPassword()
    {
        // Arrange
        var username = "newuser";
        var displayName = "New User";
        var email = "newuser@example.com";
        var password = "NewUserP@ss1!";
        var role = PortalRole.Approver;

        _portalUserRepositoryMock
            .Setup(r => r.UsernameExists(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        PortalUser? capturedUser = null;
        _portalUserRepositoryMock
            .Setup(r => r.Create(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()))
            .Callback<PortalUser, CancellationToken>((u, _) => capturedUser = u)
            .ReturnsAsync((PortalUser u, CancellationToken _) => u);

        // Act
        var result = await _sut.CreateUser(username, displayName, email, password, role);

        // Assert
        _portalUserRepositoryMock.Verify(
            r => r.Create(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedUser.Should().NotBeNull();
        capturedUser!.Username.Should().Be(username);
        capturedUser.DisplayName.Should().Be(displayName);
        capturedUser.Email.Should().Be(email);
        capturedUser.Role.Should().Be(role);
        capturedUser.IsActive.Should().BeTrue();
        capturedUser.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capturedUser.PasswordHash.Should().NotBeNullOrWhiteSpace();
        capturedUser.PasswordHash.Should().NotBe(password);
        capturedUser.PasswordHash.Should().Contain(":");
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUsername_ThrowsInvalidOperationException()
    {
        // Arrange
        var username = "existinguser";

        _portalUserRepositoryMock
            .Setup(r => r.UsernameExists(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = () => _sut.CreateUser(username, "Display", "email@example.com", "P@ssw0rd!", PortalRole.Viewer);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*'{username}'*");

        _portalUserRepositoryMock.Verify(
            r => r.Create(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateUser_HashedPasswordFormat_IsSaltColonHashBase64()
    {
        // Arrange
        var username = "formatuser";
        var password = "F0rmatP@ss!";

        _portalUserRepositoryMock
            .Setup(r => r.UsernameExists(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        PortalUser? capturedUser = null;
        _portalUserRepositoryMock
            .Setup(r => r.Create(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()))
            .Callback<PortalUser, CancellationToken>((u, _) => capturedUser = u)
            .ReturnsAsync((PortalUser u, CancellationToken _) => u);

        // Act
        await _sut.CreateUser(username, "Format User", "format@example.com", password, PortalRole.Viewer);

        // Assert
        capturedUser.Should().NotBeNull();

        var parts = capturedUser!.PasswordHash.Split(':');
        parts.Should().HaveCount(2, "password hash should be in the format 'base64salt:base64hash'");

        // Verify both parts are valid Base64 strings
        var saltAction = () => Convert.FromBase64String(parts[0]);
        saltAction.Should().NotThrow("the salt portion should be valid Base64");

        var hashAction = () => Convert.FromBase64String(parts[1]);
        hashAction.Should().NotThrow("the hash portion should be valid Base64");

        // Salt and hash should have non-trivial lengths
        Convert.FromBase64String(parts[0]).Length.Should().BeGreaterThan(0);
        Convert.FromBase64String(parts[1]).Length.Should().BeGreaterThan(0);
    }

    // ──────────────────────────────────────────────
    // ChangePassword
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_WithValidUser_UpdatesPasswordHash()
    {
        // Arrange
        var userId = 10;
        var oldPassword = "OldP@ssw0rd!";
        var newPassword = "NewP@ssw0rd!";
        var oldHash = AuthService.HashPassword(oldPassword);

        var user = new PortalUser
        {
            Id = userId,
            Username = "changeuser",
            DisplayName = "Change User",
            Email = "change@example.com",
            PasswordHash = oldHash,
            Role = PortalRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-20),
            LastLoginAt = DateTime.UtcNow.AddDays(-1)
        };

        _portalUserRepositoryMock
            .Setup(r => r.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _portalUserRepositoryMock
            .Setup(r => r.Update(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ChangePassword(userId, newPassword);

        // Assert
        _portalUserRepositoryMock.Verify(
            r => r.Update(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()),
            Times.Once);

        user.PasswordHash.Should().NotBe(oldHash, "the password hash should have been updated");
        user.PasswordHash.Should().Contain(":");

        // Verify the new hash is valid Base64 salt:hash format
        var parts = user.PasswordHash.Split(':');
        parts.Should().HaveCount(2);
        var saltAction = () => Convert.FromBase64String(parts[0]);
        saltAction.Should().NotThrow();
        var hashAction = () => Convert.FromBase64String(parts[1]);
        hashAction.Should().NotThrow();
    }

    [Fact]
    public async Task ChangePassword_WithNonExistentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = 999;

        _portalUserRepositoryMock
            .Setup(r => r.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PortalUser?)null);

        // Act
        var act = () => _sut.ChangePassword(userId, "SomeP@ss!");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*User not found*");

        _portalUserRepositoryMock.Verify(
            r => r.Update(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────
    // HashPassword (static)
    // ──────────────────────────────────────────────

    [Fact]
    public void HashPassword_WithSamePassword_ProducesDifferentHashes()
    {
        // Arrange
        var password = "SameP@ssw0rd!";

        // Act
        var hash1 = AuthService.HashPassword(password);
        var hash2 = AuthService.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2, "different salts should produce different hashes for the same password");

        // Also verify the salt portions are different
        var salt1 = hash1.Split(':')[0];
        var salt2 = hash2.Split(':')[0];
        salt1.Should().NotBe(salt2, "each hash operation should use a unique random salt");
    }

    [Fact]
    public void HashPassword_OutputFormat_IsTwoBase64StringsSeparatedByColon()
    {
        // Arrange
        var password = "F0rm@tTest!";

        // Act
        var result = AuthService.HashPassword(password);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain(":");

        var parts = result.Split(':');
        parts.Should().HaveCount(2, "hash format should be exactly 'base64salt:base64hash'");

        // Validate the salt is valid Base64 and has expected length (16 bytes)
        var saltBytes = Convert.FromBase64String(parts[0]);
        saltBytes.Should().NotBeEmpty("salt should have content");
        saltBytes.Should().HaveCount(16, "salt should be 128 bits (16 bytes)");

        // Validate the hash is valid Base64 and has expected length (32 bytes)
        var hashBytes = Convert.FromBase64String(parts[1]);
        hashBytes.Should().NotBeEmpty("hash should have content");
        hashBytes.Should().HaveCount(32, "hash should be 256 bits (32 bytes)");
    }

    // ──────────────────────────────────────────────
    // Roundtrip: HashPassword + ValidateLogin
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ValidateLogin_WithHashFromHashPassword_ReturnsUserSuccessfully()
    {
        // Arrange
        var username = "roundtripuser";
        var password = "R0undTr!pP@ss";
        var hashedPassword = AuthService.HashPassword(password);

        var user = new PortalUser
        {
            Id = 42,
            Username = username,
            DisplayName = "Roundtrip User",
            Email = "roundtrip@example.com",
            PasswordHash = hashedPassword,
            Role = PortalRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastLoginAt = null
        };

        _portalUserRepositoryMock
            .Setup(r => r.GetByUsername(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _portalUserRepositoryMock
            .Setup(r => r.Update(It.IsAny<PortalUser>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ValidateLogin(username, password);

        // Assert
        result.Should().NotBeNull("the password hash from HashPassword should be verifiable by ValidateLogin");
        result!.Id.Should().Be(42);
        result.Username.Should().Be(username);
        result.LastLoginAt.Should().NotBeNull();

        _portalUserRepositoryMock.Verify(
            r => r.Update(It.Is<PortalUser>(u => u.Id == 42), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
