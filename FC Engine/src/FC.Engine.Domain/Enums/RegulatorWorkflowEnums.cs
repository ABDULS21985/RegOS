namespace FC.Engine.Domain.Enums;

public enum RegulatorReceiptStatus
{
    Received = 0,
    UnderReview = 1,
    Accepted = 2,
    FinalAccepted = 3,
    QueriesRaised = 4,
    ResponseReceived = 5
}

public enum ExaminerQueryStatus
{
    Open = 0,
    Responded = 1,
    Resolved = 2,
    Escalated = 3
}

public enum ExaminerQueryPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum ExaminationProjectStatus
{
    Draft = 0,
    InProgress = 1,
    Completed = 2,
    Archived = 3
}

public enum EarlyWarningSeverity
{
    Amber = 0,
    Red = 1
}
