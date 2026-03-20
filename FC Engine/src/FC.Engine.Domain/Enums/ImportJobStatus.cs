namespace FC.Engine.Domain.Enums;

public enum ImportJobStatus
{
    Uploaded,
    Parsed,
    MappingReview,
    Validated,
    Staged,
    Committed,
    Failed
}
