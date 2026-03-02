using System.Security.Cryptography;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace FC.Engine.Application.Services;

public class AuthService
{
    private readonly IPortalUserRepository _userRepo;

    public AuthService(IPortalUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<PortalUser?> ValidateLogin(string username, string password, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByUsername(username, ct);
        if (user is null || !user.IsActive)
            return null;

        if (!VerifyPassword(password, user.PasswordHash))
            return null;

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepo.Update(user, ct);

        return user;
    }

    public async Task<PortalUser> CreateUser(
        string username, string displayName, string email,
        string password, PortalRole role, CancellationToken ct = default)
    {
        if (await _userRepo.UsernameExists(username, ct))
            throw new InvalidOperationException($"Username '{username}' already exists");

        var user = new PortalUser
        {
            Username = username,
            DisplayName = displayName,
            Email = email,
            PasswordHash = HashPassword(password),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        return await _userRepo.Create(user, ct);
    }

    public async Task ChangePassword(int userId, string newPassword, CancellationToken ct = default)
    {
        var user = await _userRepo.GetById(userId, ct)
            ?? throw new InvalidOperationException("User not found");

        user.PasswordHash = HashPassword(newPassword);
        await _userRepo.Update(user, ct);
    }

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = KeyDerivation.Prf(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100_000,
            numBytesRequested: 32);

        // Store as salt:hash in base64
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);

        var actualHash = KeyDerivation.Prf(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100_000,
            numBytesRequested: 32);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
