using OrderSystem.Domain.Events;

namespace OrderSystem.Domain.Ports;

public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
