using Microsoft.Extensions.DependencyInjection;
using OrderSystem.Application.Payments;

namespace OrderSystem.Adapters.RabbitMQ;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqAdapter(this IServiceCollection services, Action<RabbitMqOptions> configureOptions)
    {
        var options = new RabbitMqOptions();
        configureOptions(options);
        services.AddSingleton(options);
        services.AddSingleton<IPaymentRetryQueue, RabbitMqPaymentRetryPublisher>();
        services.AddHostedService<RabbitMqPaymentRetryConsumer>();
        return services;
    }
}
