using FC.Engine.Domain.Models;

namespace FC.Engine.Portal.Services;

/// <summary>
/// In-memory audit comment store. Persists replies for the lifetime of the server process.
/// Replace with a database-backed implementation for production persistence.
/// </summary>
public sealed class InMemoryAuditCommentService : IAuditCommentService
{
    private readonly Dictionary<string, List<TimelineReply>> _store = new();
    private readonly Lock _lock = new();
    private int _nextId;

    private static string Key(int submissionId, string eventId) => $"{submissionId}:{eventId}";

    public Task<List<TimelineReply>> GetRepliesAsync(int submissionId, string eventId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var k = Key(submissionId, eventId);
            var list = _store.TryGetValue(k, out var v) ? v : [];
            return Task.FromResult(list.ToList());
        }
    }

    public Task<TimelineReply> AddReplyAsync(int submissionId, string eventId, string authorName, string content, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var k = Key(submissionId, eventId);
            if (!_store.TryGetValue(k, out var list))
                _store[k] = list = [];

            var initials = GetInitials(authorName);
            var reply = new TimelineReply(++_nextId, authorName, initials, content.Trim(), DateTime.UtcNow);
            list.Add(reply);
            return Task.FromResult(reply);
        }
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0][0].ToString().ToUpperInvariant()
            : $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }
}
