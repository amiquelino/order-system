using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using OrderSystem.Domain.Events;
using OrderSystem.Domain.Ports;

namespace OrderSystem.Adapters.Kafka;

public class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _options;

    public KafkaEventPublisher(KafkaOptions options)
    {
        _options = options;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers
        }).Build();
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var eventType = domainEvent.GetType();

        var message = new Message<string, string>
        {
            Key = eventType.Name,
            Value = JsonSerializer.Serialize(domainEvent, eventType),
            Headers = new Headers { { "event-type", Encoding.UTF8.GetBytes(eventType.Name) } }
        };

        await _producer.ProduceAsync(_options.OrderEventsTopic, message, cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
