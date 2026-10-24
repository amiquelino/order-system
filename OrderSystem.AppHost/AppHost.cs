var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgWeb();

var postgres = postgresServer.AddDatabase("orderdb");

var kafka = builder.AddKafka("kafka")
    .WithDataVolume()
    .WithKafkaUI();

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume()
    .WithManagementPlugin();

var cache = builder.AddRedis("cache")
    .WithDataVolume();

var migrationService = builder.AddProject<Projects.OrderSystem_MigrationService>("migrationservice")
    .WithReference(postgres)
    .WaitFor(postgres);

// Api hosts no real-time connections itself (that's NotificationService's job), so it
// stays fully stateless across replicas — no Redis/backplane reference needed here.
var api = builder.AddProject<Projects.OrderSystem_Api>("api")
    .WithReference(postgres)
    .WithReference(kafka)
    .WithReference(rabbitmq)
    .WaitFor(postgres)
    .WaitFor(kafka)
    .WaitFor(rabbitmq)
    .WaitForCompletion(migrationService)
    .WithReplicas(2);

// Real-time push lives in its own service: it consumes OrderStatusChangedEvent off
// Kafka and forwards it to connected SignalR clients. Isolating this from the (larger,
// horizontally-scaled) Api tier means only this one small service carries SignalR's
// sticky-session requirement if it's ever scaled beyond a single replica.
var notifications = builder.AddProject<Projects.OrderSystem_NotificationService>("notifications")
    .WithReference(kafka)
    .WithReference(cache)
    .WaitFor(kafka)
    .WaitFor(cache);

var frontend = builder.AddJavaScriptApp("frontend", "../order-system-frontend")
    .WithReference(api)
    .WithReference(notifications)
    .WaitFor(api)
    .WaitFor(notifications)
    // The frontend reads these specific NEXT_PUBLIC_* names (inlined by Next.js at
    // compile time); .WithReference(...) alone only sets the generic
    // services__*__* discovery variables, which this code never reads.
    .WithEnvironment("NEXT_PUBLIC_API_URL", api.GetEndpoint("http"))
    .WithEnvironment("NEXT_PUBLIC_NOTIFICATIONS_URL", notifications.GetEndpoint("http"))
    .WithHttpEndpoint(env: "PORT", port: 3000)
    .WithExternalHttpEndpoints();

// The requested port (3000) is only a preference — if it's taken, Aspire falls
// back to a different one — so neither service's CORS policy can hardcode it;
// both need the frontend's actual resolved endpoint.
api.WithEnvironment("FRONTEND_URL", frontend.GetEndpoint("http"));
notifications.WithEnvironment("FRONTEND_URL", frontend.GetEndpoint("http"));

builder.Build().Run();
