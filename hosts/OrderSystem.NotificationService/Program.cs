using OrderSystem.Adapters.Kafka;
using OrderSystem.NotificationService.Consumers;
using OrderSystem.NotificationService.Hubs;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendCorsPolicy";

// Same reasoning as OrderSystem.Api: the frontend's actual resolved endpoint is only
// known at Aspire orchestration time, so AppHost.cs passes it in as FRONTEND_URL.
var frontendUrl = (builder.Configuration["FRONTEND_URL"] ?? "http://localhost:3000").TrimEnd('/');

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(frontendUrl)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("cache") ?? string.Empty);

builder.Services.AddSingleton(new KafkaOptions
{
    BootstrapServers = builder.Configuration.GetConnectionString("kafka") ?? string.Empty
});

builder.Services.AddHostedService<KafkaOrderEventsConsumer>();

var app = builder.Build();

app.UseCors(FrontendCorsPolicy);

app.MapHub<OrderHub>("/orderhub");

app.Run();
