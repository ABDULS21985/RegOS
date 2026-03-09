namespace FC.Engine.Domain.Enums;

public enum BatchSubmissionStatus
{
    Pending,
    Signing,
    Dispatching,
    Submitted,
    Acknowledged,
    Processing,
    Accepted,
    QueriesRaised,
    FinalAccepted,
    Rejected,
    Failed
}

public enum ChannelIntegrationMethod { REST, SFTP, SOAP, XML_UPLOAD }
public enum RegulatoryQueryType { Clarification, Amendment, AdditionalData }
public enum RegulatoryQueryPriority { Low, Normal, High, Critical }
public enum RegulatoryQueryStatus { Open, InProgress, Responded, Closed }
