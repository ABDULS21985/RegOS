namespace FC.Engine.Domain.Enums;

public enum SubmissionStatus
{
    Draft = 0,
    Parsing = 1,
    Validating = 2,
    Accepted = 3,
    AcceptedWithWarnings = 4,
    Rejected = 5
}
