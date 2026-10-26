using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderSystem.Adapters.Kafka;
using OrderSystem.Domain.Events;
using OrderSystem.NotificationService.Hubs;

namespace OrderSystem.NotificationService.Consumers;

public class KafkaOrderEventsConsumer(
    KafkaOptions options,
    IHubContext<OrderHub> hubContext,
    ILogger<KafkaOrderEventsConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => Run(stoppingToken), stoppingToken);
    }

    private void Run(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = "notification-service",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        consumer.Subscribe(options.OrderEventsTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;

            try
            {
                result = consumer.Consume(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Failed to consume an order event");
                continue;
            }

            var eventType = result.Message.Headers.TryGetLastBytes("event-type", out var bytes)
                ? Encoding.UTF8.GetString(bytes)
                : null;

            if (eventType != nameof(OrderStatusChangedEvent))
            {
                continue;
            }

            try
            {
                var statusChanged = JsonSerializer.Deserialize<OrderStatusChangedEvent>(result.Message.Value);

                if (statusChanged is not null)
                {
                    hubContext.Clients.All.SendAsync(
                        "OrderStatusChanged",
                        statusChanged.OrderId.Value.ToString(),
                        statusChanged.NewStatus.ToString(),
                        stoppingToken).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process an OrderStatusChangedEvent");
            }
        }

        consumer.Close();
    }
}
