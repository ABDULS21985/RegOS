using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Application.DTOs;

namespace FC.Engine.Application.Services;

public class IngestionOrchestrator
{
    private readonly IXmlParser _xmlParser;
    private readonly IXsdSchemaProvider _schemaProvider;
    private readonly IValidationEngine _validationEngine;
    private readonly ISubmissionRepository _submissionRepo;
    private readonly IReturnRepository _returnRepo;

    public IngestionOrchestrator(
        IXmlParser xmlParser,
        IXsdSchemaProvider schemaProvider,
        IValidationEngine validationEngine,
        ISubmissionRepository submissionRepo,
        IReturnRepository returnRepo)
    {
        _xmlParser = xmlParser;
        _schemaProvider = schemaProvider;
        _validationEngine = validationEngine;
        _submissionRepo = submissionRepo;
        _returnRepo = returnRepo;
    }

    public async Task<SubmissionResultDto> ProcessSubmission(
        Stream xmlStream,
        string returnCode,
        int institutionId,
        int returnPeriodId,
        CancellationToken ct)
    {
        var rc = ReturnCode.Parse(returnCode);

        // 1. Create submission record
        var submission = Submission.Create(institutionId, returnPeriodId, returnCode);
        await _submissionRepo.Add(submission, ct);

        // 2. XSD validation (structural)
        if (_schemaProvider.HasSchema(rc))
        {
            var xsd = _schemaProvider.GetSchema(rc);
            var xsdErrors = _xmlParser.ValidateAgainstXsd(xmlStream, xsd);
            if (xsdErrors.Any(e => e.Severity == ValidationSeverity.Error))
            {
                submission.MarkRejected();
                var xsdReport = ValidationReport.Create(submission.Id);
                xsdReport.AddErrors(xsdErrors);
                xsdReport.FinalizeAt(DateTime.UtcNow);
                submission.AttachValidationReport(xsdReport);
                await _submissionRepo.Update(submission, ct);
                return MapResult(submission);
            }
            xmlStream.Position = 0;
        }

        // 3. Parse XML into domain model
        submission.MarkParsing();
        var returnData = _xmlParser.Parse(xmlStream, rc);

        // 4. Run validation engine
        submission.MarkValidating();
        var report = await _validationEngine.Validate(returnData, submission, ct);

        // 5. Decision
        if (report.IsValid && !report.HasWarnings)
        {
            await _returnRepo.Save(returnData, submission.Id, ct);
            submission.MarkAccepted();
        }
        else if (!report.HasErrors && report.HasWarnings)
        {
            await _returnRepo.Save(returnData, submission.Id, ct);
            submission.MarkAcceptedWithWarnings();
        }
        else
        {
            submission.MarkRejected();
        }

        submission.AttachValidationReport(report);
        await _submissionRepo.Update(submission, ct);

        return MapResult(submission);
    }

    public async Task<SubmissionDto?> GetSubmission(int id, CancellationToken ct)
    {
        var submission = await _submissionRepo.GetById(id, ct);
        if (submission == null) return null;

        return new SubmissionDto
        {
            Id = submission.Id,
            InstitutionCode = submission.Institution?.InstitutionCode ?? "",
            ReturnCode = submission.ReturnCodeValue,
            Status = submission.Status.ToString(),
            SubmittedAt = submission.SubmittedAt,
            ErrorCount = submission.ValidationReport?.ErrorCount,
            WarningCount = submission.ValidationReport?.WarningCount
        };
    }

    public async Task<ValidationReportDto?> GetValidationReport(int submissionId, CancellationToken ct)
    {
        var submission = await _submissionRepo.GetByIdWithReport(submissionId, ct);
        if (submission?.ValidationReport == null) return null;

        var report = submission.ValidationReport;
        return new ValidationReportDto
        {
            SubmissionId = submissionId,
            IsValid = report.IsValid,
            ErrorCount = report.ErrorCount,
            WarningCount = report.WarningCount,
            ValidatedAt = report.ValidatedAt,
            Errors = report.Errors.Select(e => new ValidationErrorDto
            {
                RuleId = e.RuleId,
                Field = e.Field,
                Message = e.Message,
                Severity = e.Severity.ToString(),
                Category = e.Category.ToString(),
                ExpectedValue = e.ExpectedValue,
                ActualValue = e.ActualValue,
                ReferencedReturnCode = e.ReferencedReturnCode
            }).ToList()
        };
    }

    private static SubmissionResultDto MapResult(Submission submission)
    {
        var result = new SubmissionResultDto
        {
            SubmissionId = submission.Id,
            Status = submission.Status.ToString()
        };

        if (submission.ValidationReport != null)
        {
            var report = submission.ValidationReport;
            result.ValidationReport = new ValidationReportDto
            {
                SubmissionId = submission.Id,
                IsValid = report.IsValid,
                ErrorCount = report.ErrorCount,
                WarningCount = report.WarningCount,
                ValidatedAt = report.ValidatedAt,
                Errors = report.Errors.Select(e => new ValidationErrorDto
                {
                    RuleId = e.RuleId,
                    Field = e.Field,
                    Message = e.Message,
                    Severity = e.Severity.ToString(),
                    Category = e.Category.ToString(),
                    ExpectedValue = e.ExpectedValue,
                    ActualValue = e.ActualValue,
                    ReferencedReturnCode = e.ReferencedReturnCode
                }).ToList()
            };
        }

        return result;
    }
}
