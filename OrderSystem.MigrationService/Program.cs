using OrderSystem.Adapters.Postgres;
using OrderSystem.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPostgresAdapter(builder.Configuration.GetConnectionString("orderdb") ?? string.Empty);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
