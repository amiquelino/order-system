using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Domain.Entities;

public class Order
{
    private Order()
    {
    }

    private Order(OrderId id, CustomerId customerId, List<OrderItem> items, OrderStatus status, DateTimeOffset createdAt)
    {
        Id = id;
        CustomerId = customerId;
        Items = items;
        Status = status;
        CreatedAt = createdAt;
    }

    public OrderId Id { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public List<OrderItem> Items { get; private set; } = [];
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Order Create(CustomerId customerId, IReadOnlyCollection<OrderItem> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.", nameof(items));
        }

        return new Order(OrderId.New(), customerId, [.. items], OrderStatus.Created, DateTimeOffset.UtcNow);
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Order is already cancelled.");
        }

        if (Status == OrderStatus.Confirmed)
        {
            throw new InvalidOperationException("A confirmed order cannot be cancelled.");
        }

        Status = OrderStatus.Cancelled;
    }

    public void BeginPaymentProcessing()
    {
        if (Status is not (OrderStatus.Created or OrderStatus.PaymentFailed))
        {
            throw new InvalidOperationException($"Cannot begin payment processing from {Status} status.");
        }

        Status = OrderStatus.PaymentPending;
    }

    public void ConfirmPayment()
    {
        if (Status != OrderStatus.PaymentPending)
        {
            throw new InvalidOperationException($"Cannot confirm payment from {Status} status.");
        }

        Status = OrderStatus.Confirmed;
    }

    public void FailPayment()
    {
        if (Status != OrderStatus.PaymentPending)
        {
            throw new InvalidOperationException($"Cannot fail payment from {Status} status.");
        }

        Status = OrderStatus.PaymentFailed;
    }
}
