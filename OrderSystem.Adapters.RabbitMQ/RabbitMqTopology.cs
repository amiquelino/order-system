using RabbitMQ.Client;

namespace OrderSystem.Adapters.RabbitMQ;

internal static class RabbitMqTopology
{
    public static async Task DeclareAsync(IChannel channel, RabbitMqOptions options, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: options.PaymentRetryExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: options.PaymentRetryQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: options.PaymentRetryQueue,
            exchange: options.PaymentRetryExchange,
            routingKey: options.PaymentRetryQueue,
            cancellationToken: cancellationToken);

        // No consumer ever reads from this queue directly: messages sit here until
        // x-message-ttl expires, then get dead-lettered back into the exchange above,
        // landing in the main queue. That TTL-plus-DLX round trip is the delay.
        var waitQueueArguments = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = options.RetryDelayMilliseconds,
            ["x-dead-letter-exchange"] = options.PaymentRetryExchange,
            ["x-dead-letter-routing-key"] = options.PaymentRetryQueue
        };

        await channel.QueueDeclareAsync(
            queue: options.PaymentRetryWaitQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: waitQueueArguments,
            cancellationToken: cancellationToken);
    }
}
