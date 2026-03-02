namespace FC.Engine.Domain.Enums;

public enum SubmissionStatus
{
    Draft,
    Parsing,
    Validating,
    Accepted,
    AcceptedWithWarnings,
    Rejected
}
