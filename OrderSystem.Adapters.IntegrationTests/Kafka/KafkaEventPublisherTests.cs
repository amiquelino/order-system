using System.Text.Json;
using Bogus;
using Confluent.Kafka;
using OrderSystem.Adapters.Kafka;
using OrderSystem.Domain.Events;
using OrderSystem.Domain.ValueObjects;

namespace OrderSystem.Adapters.IntegrationTests.Kafka;

public class KafkaEventPublisherTests(KafkaContainerFixture fixture) : IClassFixture<KafkaContainerFixture>
{
    [Fact]
    public async Task PublishAsync_ProducesEventAsJsonWithEventTypeKey()
    {
        var options = new KafkaOptions
        {
            BootstrapServers = fixture.BootstrapServers,
            OrderEventsTopic = $"order-events-{Guid.NewGuid():N}"
        };

        var domainEvent = new OrderCreatedEvent(OrderId.New(), new CustomerId(new Faker().Random.Guid()), DateTimeOffset.UtcNow);

        using (var publisher = new KafkaEventPublisher(options))
        {
            await publisher.PublishAsync(domainEvent);
        }

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = fixture.BootstrapServers,
            GroupId = Guid.NewGuid().ToString(),
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        consumer.Subscribe(options.OrderEventsTopic);
        var result = consumer.Consume(TimeSpan.FromSeconds(15));

        result.Should().NotBeNull();
        result!.Message.Key.Should().Be(nameof(OrderCreatedEvent));

        var payload = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value);
        payload!.OrderId.Should().Be(domainEvent.OrderId);
        payload.CustomerId.Should().Be(domainEvent.CustomerId);
    }
}
