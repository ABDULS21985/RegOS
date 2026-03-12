using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.Notifications;
using FC.Engine.Portal.Services;
using FC.Engine.Portal.Tests.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace FC.Engine.Portal.Tests.Services;

public class NotificationServiceTests
{
    [Fact]
    public async Task NotifyDeadlineApproaching_Uses_Module_Aware_Submit_Link_When_Template_Module_Is_Known()
    {
        var tenantId = Guid.NewGuid();
        var captured = new List<NotificationRequest>();

        var notificationRepo = new Mock<IPortalNotificationRepository>();
        var orchestrator = new Mock<INotificationOrchestrator>();
        orchestrator
            .Setup(x => x.Notify(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((request, _) => captured.Add(request))
            .Returns(Task.CompletedTask);

        var userRepo = new Mock<IInstitutionUserRepository>();
        var institutionRepo = new Mock<IInstitutionRepository>();
        institutionRepo
            .Setup(x => x.GetById(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Institution { Id = 44, TenantId = tenantId });

        var brandingService = new Mock<ITenantBrandingService>();
        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetPublishedTemplate("CAP_BUF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedTemplate
            {
                ReturnCode = "CAP_BUF",
                ModuleCode = "CAPITAL_SUPERVISION",
                CurrentVersion = new CachedTemplateVersion()
            });

        var sut = new NotificationService(
            notificationRepo.Object,
            orchestrator.Object,
            userRepo.Object,
            institutionRepo.Object,
            new TestTenantContext { CurrentTenantId = tenantId },
            brandingService.Object,
            templateCache.Object);

        await sut.NotifyDeadlineApproaching(
            44,
            "CAP_BUF",
            "Capital Buffer Register",
            DateTime.UtcNow.AddDays(3),
            3);

        captured.Should().ContainSingle();
        captured[0].ActionUrl.Should().Be("/submit?module=CAPITAL_SUPERVISION&returnCode=CAP_BUF");
        captured[0].Priority.Should().Be(NotificationPriority.Normal);
    }
}
