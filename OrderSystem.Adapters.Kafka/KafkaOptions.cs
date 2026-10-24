namespace OrderSystem.Adapters.Kafka;

public class KafkaOptions
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string OrderEventsTopic { get; set; } = "order-events";
}
