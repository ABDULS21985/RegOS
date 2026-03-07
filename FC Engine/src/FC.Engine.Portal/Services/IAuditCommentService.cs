using FC.Engine.Domain.Models;

namespace FC.Engine.Portal.Services;

public interface IAuditCommentService
{
    Task<List<TimelineReply>> GetRepliesAsync(int submissionId, string eventId, CancellationToken ct = default);
    Task<TimelineReply> AddReplyAsync(int submissionId, string eventId, string authorName, string content, CancellationToken ct = default);
}
