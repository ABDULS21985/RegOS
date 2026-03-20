using System.Text;
using System.Xml.Schema;
using FC.Engine.Application.Services;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.Notifications;
using FluentAssertions;
using Moq;

namespace FC.Engine.Infrastructure.Tests.Services;

public class IngestionOrchestratorNotificationTests
{
    [Fact]
    public async Task Submission_For_Review_Notifies_Checkers()
    {
        var tenantId = Guid.NewGuid();
        var cache = new Mock<ITemplateMetadataCache>();
        var xsdGenerator = new Mock<IXsdGenerator>();
        var xmlParser = new Mock<IGenericXmlParser>();
        var dataRepo = new Mock<IGenericDataRepository>();
        var submissionRepo = new Mock<ISubmissionRepository>();
        var formulaEvaluator = new Mock<IFormulaEvaluator>();
        var crossSheetValidator = new Mock<ICrossSheetValidator>();
        var businessRuleEvaluator = new Mock<IBusinessRuleEvaluator>();
        var tenantContext = new Mock<ITenantContext>();
        var notificationOrchestrator = new Mock<INotificationOrchestrator>();

        tenantContext.SetupGet(x => x.CurrentTenantId).Returns(tenantId);

        cache.Setup(x => x.GetPublishedTemplate("MFCR 300", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedTemplate
            {
                TemplateId = 99,
                ReturnCode = "MFCR 300",
                Name = "Capital Adequacy",
                StructuralCategory = "FixedRow",
                PhysicalTableName = "mfcr_300",
                CurrentVersion = new CachedTemplateVersion
                {
                    Id = 100,
                    VersionNumber = 2,
                    Fields = new List<TemplateField>
                    {
                        new()
                        {
                            FieldName = "amount",
                            DisplayName = "Amount",
                            DataType = FieldDataType.Money,
                            IsRequired = false
                        }
                    }.AsReadOnly()
                }
            });

        xsdGenerator.Setup(x => x.GenerateSchema("MFCR 300", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XmlSchemaSet());

        xmlParser.Setup(x => x.Parse(It.IsAny<Stream>(), "MFCR 300", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var record = new ReturnDataRecord("MFCR 300", 1, StructuralCategory.FixedRow);
                var row = new ReturnDataRow();
                row.SetValue("amount", 500m);
                record.AddRow(row);
                return record;
            });

        submissionRepo.Setup(x => x.Add(It.IsAny<Submission>(), It.IsAny<CancellationToken>()))
            .Callback<Submission, CancellationToken>((s, _) => s.Id = 412)
            .Returns(Task.CompletedTask);
        submissionRepo.Setup(x => x.Update(It.IsAny<Submission>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        dataRepo.Setup(x => x.DeleteBySubmission("MFCR 300", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        dataRepo.Setup(x => x.Save(It.IsAny<ReturnDataRecord>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        formulaEvaluator.Setup(x => x.Evaluate(It.IsAny<ReturnDataRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ValidationError>());
        crossSheetValidator.Setup(x => x.Validate(
                It.IsAny<ReturnDataRecord>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ValidationError>());
        businessRuleEvaluator.Setup(x => x.Evaluate(
                It.IsAny<ReturnDataRecord>(),
                It.IsAny<Submission>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ValidationError>());

        var validationOrchestrator = new ValidationOrchestrator(
            cache.Object,
            formulaEvaluator.Object,
            crossSheetValidator.Object,
            businessRuleEvaluator.Object);

        var sut = new IngestionOrchestrator(
            cache.Object,
            xsdGenerator.Object,
            xmlParser.Object,
            dataRepo.Object,
            submissionRepo.Object,
            validationOrchestrator,
            tenantContext: tenantContext.Object,
            notificationOrchestrator: notificationOrchestrator.Object);

        await using var xmlStream = new MemoryStream(Encoding.UTF8.GetBytes("<Return><Amount>500</Amount></Return>"));

        var result = await sut.Process(
            xmlStream,
            "MFCR 300",
            institutionId: 17,
            returnPeriodId: 202603,
            new SubmissionReviewNotificationContext
            {
                NotifySubmittedForReview = true,
                SubmittedByName = "Ada Maker",
                InstitutionName = "Acme Microfinance",
                PeriodLabel = "Mar 2026",
                PortalBaseUrl = "https://portal.regos.app"
            },
            CancellationToken.None);

        result.Status.Should().BeOneOf(SubmissionStatus.Accepted.ToString(), SubmissionStatus.AcceptedWithWarnings.ToString());

        notificationOrchestrator.Verify(x => x.Notify(
            It.Is<NotificationRequest>(n =>
                n.TenantId == tenantId &&
                n.EventType == NotificationEvents.ReturnSubmittedForReview &&
                n.RecipientInstitutionId == 17 &&
                n.RecipientRoles.Contains("Checker") &&
                n.RecipientRoles.Contains("Admin") &&
                n.ActionUrl == "/submissions/412"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
