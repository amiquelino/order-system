using System.Text.Json;
using OrderSystem.Application.Payments;
using RabbitMQ.Client;

namespace OrderSystem.Adapters.RabbitMQ;

public class RabbitMqPaymentRetryPublisher(RabbitMqOptions options) : IPaymentRetryQueue, IAsyncDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public async Task ScheduleRetryAsync(PaymentRetryMessage message, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await RabbitMqTopology.DeclareAsync(channel, options, cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        // Publish directly into the wait queue (default exchange, routing key = queue
        // name) so this message sits out the delay before the consumer ever sees it.
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: options.PaymentRetryWaitQueue,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory { Uri = new Uri(options.ConnectionString) };
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _connectionLock.Dispose();
    }
}
