using System.Diagnostics;
using FC.Engine.Application.DTOs;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;

namespace FC.Engine.Application.Services;

public class IngestionOrchestrator
{
    private readonly ITemplateMetadataCache _cache;
    private readonly IXsdGenerator _xsdGenerator;
    private readonly IGenericXmlParser _xmlParser;
    private readonly IGenericDataRepository _dataRepo;
    private readonly ISubmissionRepository _submissionRepo;
    private readonly ValidationOrchestrator _validationOrchestrator;

    public IngestionOrchestrator(
        ITemplateMetadataCache cache,
        IXsdGenerator xsdGenerator,
        IGenericXmlParser xmlParser,
        IGenericDataRepository dataRepo,
        ISubmissionRepository submissionRepo,
        ValidationOrchestrator validationOrchestrator)
    {
        _cache = cache;
        _xsdGenerator = xsdGenerator;
        _xmlParser = xmlParser;
        _dataRepo = dataRepo;
        _submissionRepo = submissionRepo;
        _validationOrchestrator = validationOrchestrator;
    }

    public async Task<SubmissionResultDto> Process(
        Stream xmlStream, string returnCode, int institutionId, int returnPeriodId,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Create submission record
        var submission = Submission.Create(institutionId, returnPeriodId, returnCode);
        await _submissionRepo.Add(submission, ct);

        try
        {
            // 2. Resolve template
            var template = await _cache.GetPublishedTemplate(returnCode, ct);
            submission.SetTemplateVersion(template.CurrentVersion.Id);
            submission.MarkParsing();
            await _submissionRepo.Update(submission, ct);

            // 3. XSD validation
            var schemaErrors = await ValidateXsd(xmlStream, returnCode, ct);
            if (schemaErrors.Count > 0)
            {
                var schemaReport = ValidationReport.Create(submission.Id);
                foreach (var err in schemaErrors)
                    schemaReport.AddError(err);
                schemaReport.FinalizeAt(DateTime.UtcNow);

                submission.AttachValidationReport(schemaReport);
                submission.MarkRejected();
                submission.ProcessingDurationMs = (int)sw.ElapsedMilliseconds;
                await _submissionRepo.Update(submission, ct);

                return MapResult(submission);
            }

            // 4. Reset stream and parse XML
            xmlStream.Position = 0;
            var record = await _xmlParser.Parse(xmlStream, returnCode, ct);

            // 5. Run validation pipeline
            submission.MarkValidating();
            await _submissionRepo.Update(submission, ct);

            var report = await _validationOrchestrator.Validate(
                record, submission, institutionId, returnPeriodId, ct);

            submission.AttachValidationReport(report);

            // 6. Persist data if valid
            if (report.IsValid || !report.HasErrors)
            {
                // Delete any previous data for this submission
                await _dataRepo.DeleteBySubmission(returnCode, submission.Id, ct);
                await _dataRepo.Save(record, submission.Id, ct);

                submission.MarkAccepted();
                if (report.HasWarnings)
                    submission.MarkAcceptedWithWarnings();
            }
            else
            {
                submission.MarkRejected();
            }

            report.FinalizeAt(DateTime.UtcNow);
            submission.ProcessingDurationMs = (int)sw.ElapsedMilliseconds;
            await _submissionRepo.Update(submission, ct);

            return MapResult(submission);
        }
        catch (Exception ex)
        {
            submission.MarkRejected();
            submission.ProcessingDurationMs = (int)sw.ElapsedMilliseconds;

            var errorReport = ValidationReport.Create(submission.Id);
            errorReport.AddError(new ValidationError
            {
                RuleId = "SYSTEM",
                Field = "N/A",
                Message = $"Processing error: {ex.Message}",
                Severity = ValidationSeverity.Error,
                Category = ValidationCategory.Schema
            });
            errorReport.FinalizeAt(DateTime.UtcNow);
            submission.AttachValidationReport(errorReport);

            await _submissionRepo.Update(submission, ct);
            return MapResult(submission);
        }
    }

    private async Task<List<ValidationError>> ValidateXsd(Stream xmlStream, string returnCode, CancellationToken ct)
    {
        var errors = new List<ValidationError>();
        try
        {
            var schemaSet = await _xsdGenerator.GenerateSchema(returnCode, ct);
            var settings = new System.Xml.XmlReaderSettings
            {
                ValidationType = System.Xml.ValidationType.Schema,
                Schemas = schemaSet
            };
            settings.ValidationEventHandler += (_, e) =>
            {
                errors.Add(new ValidationError
                {
                    RuleId = "XSD",
                    Field = "XML",
                    Message = e.Message,
                    Severity = e.Severity == System.Xml.Schema.XmlSeverityType.Error
                        ? ValidationSeverity.Error : ValidationSeverity.Warning,
                    Category = ValidationCategory.Schema
                });
            };

            using var reader = System.Xml.XmlReader.Create(xmlStream, settings);
            while (await reader.ReadAsync()) { }
        }
        catch (Exception ex)
        {
            errors.Add(new ValidationError
            {
                RuleId = "XSD",
                Field = "XML",
                Message = $"Schema validation failed: {ex.Message}",
                Severity = ValidationSeverity.Error,
                Category = ValidationCategory.Schema
            });
        }

        return errors;
    }

    private static SubmissionResultDto MapResult(Submission submission)
    {
        var dto = new SubmissionResultDto
        {
            SubmissionId = submission.Id,
            ReturnCode = submission.ReturnCode,
            Status = submission.Status.ToString(),
            ProcessingDurationMs = submission.ProcessingDurationMs
        };

        if (submission.ValidationReport != null)
        {
            dto.ValidationReport = new ValidationReportDto
            {
                IsValid = submission.ValidationReport.IsValid,
                ErrorCount = submission.ValidationReport.ErrorCount,
                WarningCount = submission.ValidationReport.WarningCount,
                Errors = submission.ValidationReport.Errors.Select(e => new ValidationErrorDto
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

        return dto;
    }
}
