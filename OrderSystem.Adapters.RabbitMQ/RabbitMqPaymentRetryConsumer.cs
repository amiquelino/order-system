using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderSystem.Application.Commands;
using OrderSystem.Application.Payments;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Wolverine;

namespace OrderSystem.Adapters.RabbitMQ;

public class RabbitMqPaymentRetryConsumer(
    RabbitMqOptions options,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqPaymentRetryConsumer> logger) : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { Uri = new Uri(options.ConnectionString) };
        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await RabbitMqTopology.DeclareAsync(_channel, options, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<PaymentRetryMessage>(eventArgs.Body.Span);

                if (message is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                    await bus.InvokeAsync(new RetryPaymentCommand(message.OrderId, message.Attempt), stoppingToken);
                }

                await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process payment retry message");
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: options.PaymentRetryQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken: cancellationToken);
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken: cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}
