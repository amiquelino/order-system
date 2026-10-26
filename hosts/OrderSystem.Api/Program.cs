using OrderSystem.Adapters.Kafka;
using OrderSystem.Adapters.Postgres;
using OrderSystem.Adapters.RabbitMQ;
using OrderSystem.Api.Contracts;
using OrderSystem.Application.Commands;
using OrderSystem.Application.Inventory;
using OrderSystem.Application.Payments;
using OrderSystem.Application.Queries;
using OrderSystem.Application.Sagas;
using OrderSystem.Domain.ValueObjects;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendCorsPolicy";

// Aspire only requests port 3000 for the frontend; if that's taken it falls back
// to a different one, so this can't be a compile-time constant. Under Aspire,
// AppHost.cs passes the frontend's actual resolved endpoint as FRONTEND_URL;
// standalone (Option C in the README), it falls back to the conventional port.
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

builder.Services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();
builder.Services.AddSingleton<IInventoryService, SimulatedInventoryService>();
builder.Services.AddScoped<OrderFulfillmentSagaOrchestrator>();

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(CreateOrderCommand).Assembly);
    opts.CodeGeneration.AlwaysUseServiceLocationFor<OrderDbContext>();
});

builder.Services.AddPostgresAdapter(builder.Configuration.GetConnectionString("orderdb") ?? string.Empty);

builder.Services.AddKafkaAdapter(options =>
{
    options.BootstrapServers = builder.Configuration.GetConnectionString("kafka") ?? string.Empty;
});

builder.Services.AddRabbitMqAdapter(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("rabbitmq") ?? string.Empty;
});

var app = builder.Build();

app.UseCors(FrontendCorsPolicy);

app.MapPost("/orders", async (CreateOrderRequest request, IMessageBus bus, CancellationToken cancellationToken) =>
{
    var command = new CreateOrderCommand(
        new CustomerId(request.CustomerId),
        request.Items.Select(i => new CreateOrderItemDto(i.ProductName, i.Quantity, i.UnitPrice, i.Currency)).ToList());

    var orderId = await bus.InvokeAsync<OrderId>(command, cancellationToken);
    return Results.Created($"/orders/{orderId}", new { OrderId = orderId.Value });
});

app.MapGet("/orders/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken cancellationToken) =>
{
    try
    {
        var status = await bus.InvokeAsync<OrderStatus>(new GetOrderStatusQuery(new OrderId(id)), cancellationToken);
        return Results.Ok(new { OrderId = id, Status = status.ToString() });
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});

app.MapPost("/orders/{id:guid}/cancel", async (Guid id, IMessageBus bus, CancellationToken cancellationToken) =>
{
    try
    {
        await bus.InvokeAsync(new CancelOrderCommand(new OrderId(id)), cancellationToken);
        return Results.NoContent();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});

app.Run();
