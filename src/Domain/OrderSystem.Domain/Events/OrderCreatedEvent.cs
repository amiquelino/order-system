using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Domain.Events;

public record OrderCreatedEvent(OrderId OrderId, CustomerId CustomerId, DateTimeOffset OccurredAt) : IDomainEvent;
