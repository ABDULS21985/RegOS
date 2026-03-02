namespace FC.Engine.Domain.ValueObjects;

public record MoneyAmount
{
    public decimal Value { get; }

    public MoneyAmount(decimal value)
    {
        Value = Math.Round(value, 2);
    }

    public static MoneyAmount Zero => new(0m);

    public static implicit operator decimal(MoneyAmount amount) => amount.Value;
    public static implicit operator MoneyAmount(decimal value) => new(value);

    public override string ToString() => Value.ToString("N2");
}
