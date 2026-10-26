using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderSystem.Application.Sagas;
using OrderSystem.Domain.Ports;

namespace OrderSystem.Adapters.Postgres;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresAdapter(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IOrderRepository, PostgresOrderRepository>();
        services.AddScoped<IOrderFulfillmentSagaRepository, PostgresOrderFulfillmentSagaRepository>();
        return services;
    }
}
