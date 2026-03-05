using System.Reflection;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Portal.Components.Shared;
using FC.Engine.Portal.Services;
using FluentAssertions;
using Xunit;

namespace FC.Engine.Integration.Tests.Notifications;

public class NotificationBellTests
{
    [Fact]
    public void NotificationBell_Badge_Updates_Without_Refresh()
    {
        var applyMethod = typeof(NotificationBell).GetMethod(
            "ApplyRealtimePayload",
            BindingFlags.NonPublic | BindingFlags.Static);
        applyMethod.Should().NotBeNull();

        var existing = Enumerable.Range(1, 10).Select(i => new NotificationModel
        {
            Id = i,
            Type = NotificationType.SystemAnnouncement,
            EventType = "system.announcement",
            Priority = NotificationPriority.Normal,
            Title = $"Existing {i}",
            Message = "Existing message",
            IsRead = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-i),
            TimeAgo = $"{i}m ago"
        }).ToList();

        var payload = new NotificationPayload
        {
            Title = "Fresh update",
            Message = "Your return was approved.",
            EventType = "return.approved",
            Priority = NotificationPriority.Normal,
            ActionUrl = "/submissions/1001",
            Timestamp = DateTime.UtcNow
        };

        var result = ((int UnreadCount, List<NotificationModel> RecentNotifications))applyMethod!
            .Invoke(null, new object[] { 4, existing, payload })!;

        result.UnreadCount.Should().Be(5);
        result.RecentNotifications.Should().HaveCount(10);
        result.RecentNotifications[0].Title.Should().Be("Fresh update");
        result.RecentNotifications[0].Message.Should().Be("Your return was approved.");
        result.RecentNotifications[0].EventType.Should().Be("return.approved");
        result.RecentNotifications[0].Link.Should().Be("/submissions/1001");
    }
}
