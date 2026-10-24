using System.Text.Json;
using Bogus;
using OrderSystem.Adapters.RabbitMQ;
using OrderSystem.Application.Payments;
using OrderSystem.Domain.ValueObjects;
using RabbitMQ.Client;

namespace OrderSystem.Adapters.IntegrationTests.RabbitMQ;

public class RabbitMqPaymentRetryPublisherTests(RabbitMqContainerFixture fixture) : IClassFixture<RabbitMqContainerFixture>
{
    [Fact]
    public async Task ScheduleRetryAsync_DelaysInWaitQueue_ThenLandsInMainQueueAfterTtl()
    {
        var options = new RabbitMqOptions
        {
            ConnectionString = fixture.ConnectionString,
            PaymentRetryExchange = $"payment-retry-exchange-{Guid.NewGuid():N}",
            PaymentRetryQueue = $"payment-retry-queue-{Guid.NewGuid():N}",
            PaymentRetryWaitQueue = $"payment-retry-wait-queue-{Guid.NewGuid():N}",
            RetryDelayMilliseconds = 1000
        };

        var reason = new Faker().Lorem.Sentence(4);
        var message = new PaymentRetryMessage(OrderId.New(), Attempt: 2, Reason: reason);

        await using (var publisher = new RabbitMqPaymentRetryPublisher(options))
        {
            await publisher.ScheduleRetryAsync(message);
        }

        var factory = new ConnectionFactory { Uri = new Uri(options.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Immediately after publishing, the message should still be sitting out its
        // delay in the wait queue, not yet visible in the main queue.
        var immediateResult = await channel.BasicGetAsync(options.PaymentRetryQueue, autoAck: true);
        immediateResult.Should().BeNull();

        // Give the wait queue's TTL time to expire and dead-letter the message back
        // into the main queue.
        await Task.Delay(TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds + 2000));

        var delivered = await channel.BasicGetAsync(options.PaymentRetryQueue, autoAck: true);
        delivered.Should().NotBeNull();

        var payload = JsonSerializer.Deserialize<PaymentRetryMessage>(delivered!.Body.Span);
        payload!.OrderId.Should().Be(message.OrderId);
        payload.Attempt.Should().Be(2);
        payload.Reason.Should().Be(reason);
    }
}
