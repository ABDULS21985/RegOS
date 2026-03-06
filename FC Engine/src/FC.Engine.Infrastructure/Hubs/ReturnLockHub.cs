using System.Collections.Concurrent;
using System.Security.Claims;
using FC.Engine.Domain.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Hubs;

[Authorize]
public class ReturnLockHub : Hub
{
    private static readonly ConcurrentDictionary<string, LockConnection> ActiveConnections = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReturnLockHub> _logger;

    public ReturnLockHub(IServiceProvider serviceProvider, ILogger<ReturnLockHub> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task JoinSubmission(int submissionId)
    {
        var groupName = GetGroupName(submissionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var (tenantId, userId) = ExtractClaims();
        if (tenantId.HasValue && userId.HasValue)
        {
            ActiveConnections[Context.ConnectionId] = new LockConnection(
                tenantId.Value, submissionId, userId.Value);
        }
    }

    public async Task LeaveSubmission(int submissionId)
    {
        var groupName = GetGroupName(submissionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        ActiveConnections.TryRemove(Context.ConnectionId, out _);
    }

    public async Task<HeartbeatResponse> SendHeartbeat(int submissionId)
    {
        var (tenantId, userId) = ExtractClaims();
        if (!tenantId.HasValue || !userId.HasValue)
        {
            return new HeartbeatResponse(false, "Authentication context missing.");
        }

        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IReturnLockService>();

        var result = await lockService.Heartbeat(tenantId.Value, submissionId, userId.Value);

        if (!result.Acquired)
        {
            await Clients.Caller.SendAsync("LockLost", new
            {
                submissionId,
                lockedBy = result.UserName,
                message = result.Message ?? "Lock lost."
            });
        }

        return new HeartbeatResponse(result.Acquired, result.Message);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ActiveConnections.TryRemove(Context.ConnectionId, out var connection))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var lockService = scope.ServiceProvider.GetRequiredService<IReturnLockService>();

                await lockService.ReleaseLock(
                    connection.TenantId,
                    connection.SubmissionId,
                    connection.UserId);

                var groupName = GetGroupName(connection.SubmissionId);
                await Clients.Group(groupName).SendAsync("LockReleased", new
                {
                    submissionId = connection.SubmissionId,
                    releasedBy = connection.UserId
                });

                _logger.LogInformation(
                    "Auto-released lock for submission {SubmissionId} on disconnect (user {UserId})",
                    connection.SubmissionId, connection.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to auto-release lock for submission {SubmissionId} on disconnect",
                    connection.SubmissionId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private (Guid? TenantId, int? UserId) ExtractClaims()
    {
        var tenantStr = Context.User?.FindFirst("TenantId")?.Value
            ?? Context.User?.FindFirst("tid")?.Value;
        Guid? tenantId = Guid.TryParse(tenantStr, out var tid) ? tid : null;

        var userStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        int? userId = int.TryParse(userStr, out var uid) ? uid : null;

        return (tenantId, userId);
    }

    private static string GetGroupName(int submissionId) => $"lock:submission:{submissionId}";

    internal sealed record LockConnection(Guid TenantId, int SubmissionId, int UserId);
}

public record HeartbeatResponse(bool Acquired, string? Message);
