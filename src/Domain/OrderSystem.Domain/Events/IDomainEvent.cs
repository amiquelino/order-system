namespace OrderSystem.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
