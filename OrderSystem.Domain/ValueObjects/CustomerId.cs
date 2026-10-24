namespace OrderSystem.Domain.ValueObjects;

public readonly record struct CustomerId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
