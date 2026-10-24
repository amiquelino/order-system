using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Domain.Events;

public record OrderStatusChangedEvent(OrderId OrderId, OrderStatus NewStatus, DateTimeOffset OccurredAt) : IDomainEvent;
