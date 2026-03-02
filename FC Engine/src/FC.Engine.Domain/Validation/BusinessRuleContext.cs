using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Validation;

public class BusinessRuleContext
{
    public ReturnPeriod ReturnPeriod { get; }
    public DateOnly ReportingDate => ReturnPeriod.ReportingDate;

    public BusinessRuleContext(ReturnPeriod returnPeriod)
    {
        ReturnPeriod = returnPeriod;
    }
}
