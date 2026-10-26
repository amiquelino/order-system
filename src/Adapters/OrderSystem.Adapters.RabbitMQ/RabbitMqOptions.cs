namespace OrderSystem.Adapters.RabbitMQ;

public class RabbitMqOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string PaymentRetryExchange { get; set; } = "payment-retry-exchange";
    public string PaymentRetryQueue { get; set; } = "payment-retry-queue";
    public string PaymentRetryWaitQueue { get; set; } = "payment-retry-wait-queue";
    public int RetryDelayMilliseconds { get; set; } = 4000;
}
