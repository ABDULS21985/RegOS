namespace FC.Engine.Domain.ValueObjects;

[AttributeUsage(AttributeTargets.Property)]
public class LineCodeAttribute : Attribute
{
    public string Code { get; }

    public LineCodeAttribute(string code)
    {
        Code = code;
    }
}
