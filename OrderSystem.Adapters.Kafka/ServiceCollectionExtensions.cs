using Microsoft.Extensions.DependencyInjection;
using OrderSystem.Domain.Ports;

namespace OrderSystem.Adapters.Kafka;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKafkaAdapter(this IServiceCollection services, Action<KafkaOptions> configureOptions)
    {
        var options = new KafkaOptions();
        configureOptions(options);
        services.AddSingleton(options);
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        return services;
    }
}
