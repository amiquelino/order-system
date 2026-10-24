using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Application.Sagas;

public class OrderFulfillmentSagaState
{
    private OrderFulfillmentSagaState()
    {
    }

    private OrderFulfillmentSagaState(OrderId orderId, bool isStockReserved, DateTimeOffset updatedAt)
    {
        OrderId = orderId;
        IsStockReserved = isStockReserved;
        UpdatedAt = updatedAt;
    }

    public OrderId OrderId { get; private set; }
    public bool IsStockReserved { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static OrderFulfillmentSagaState Start(OrderId orderId) =>
        new(orderId, isStockReserved: false, DateTimeOffset.UtcNow);

    public void MarkStockReserved()
    {
        IsStockReserved = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // Deliberately a safe no-op rather than a throw (unlike Order's guarded
    // transitions): both the exhausted-retry path and a manual cancellation
    // can reach this for the same order, and releasing twice or releasing
    // nothing must not be an error (FR-006, FR-009).
    public bool ReleaseStockIfReserved()
    {
        if (!IsStockReserved)
        {
            return false;
        }

        IsStockReserved = false;
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }
}
