using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Domain.Entities;

public class OrderItem
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public Money UnitPrice { get; set; } = new Money(0, string.Empty);
}
