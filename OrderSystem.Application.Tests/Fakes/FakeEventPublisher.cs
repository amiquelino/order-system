using OrderSystem.Domain.Events;
using OrderSystem.Domain.Ports;

namespace OrderSystem.Application.Tests.Fakes;

public class FakeEventPublisher : IEventPublisher
{
    private readonly List<IDomainEvent> _published = [];

    public IReadOnlyList<IDomainEvent> PublishedEvents => _published;

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _published.Add(domainEvent);
        return Task.CompletedTask;
    }
}
