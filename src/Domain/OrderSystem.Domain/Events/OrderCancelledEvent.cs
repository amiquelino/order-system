using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Domain.Events;

public record OrderCancelledEvent(OrderId OrderId, DateTimeOffset OccurredAt) : IDomainEvent;
