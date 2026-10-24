namespace OrderSystem.Domain.ValueObjects;

public enum OrderStatus
{
    Created,
    PaymentPending,
    PaymentFailed,
    Confirmed,
    Cancelled
}
