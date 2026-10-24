using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace OrderSystem.EndToEndTests;

public class AppHostFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public string FrontendUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<global::Program>();
        _app = await appHost.BuildAsync();

        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();

        await _app.StartAsync();

        await resourceNotificationService
            .WaitForResourceAsync("frontend", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(5));

        FrontendUrl = _app.GetEndpoint("frontend", "http").ToString().TrimEnd('/');

        // "Running" only means the npm process started, not that Next.js has finished
        // its cold compile and is actually serving — poll with real HTTP requests
        // rather than leaving this to Playwright's own navigation timeout. On a
        // loaded/slow host (this environment's /mnt/c filesystem plus concurrent
        // container CPU/memory contention), cold compile alone can take minutes.
        await WaitUntilRespondingAsync(FrontendUrl, TimeSpan.FromMinutes(5));

        // Next.js dev mode (Turbopack) compiles each route lazily, on its first
        // request. Doing that compile mid-test, while the browser is mid-navigation,
        // has been observed to trigger a Turbopack panic ("Failed to write app
        // endpoint ... Next.js package not found") under this host's disk/CPU
        // pressure, which then forces a full page reload back to "/" and breaks the
        // test's navigation assertion. Force the compile here instead, with no test
        // timeout budget at risk, using a throwaway id — the route template compiles
        // the same regardless of whether the id resolves to a real order.
        await WaitUntilRespondingAsync($"{FrontendUrl}/status/00000000-0000-0000-0000-000000000000", TimeSpan.FromMinutes(5));
    }

    private static async Task WaitUntilRespondingAsync(string url, TimeSpan timeout)
    {
        using var httpClient = new HttpClient();
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            try
            {
                using var response = await httpClient.GetAsync(url, cts.Token);
                return;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
            }
        }

        throw new TimeoutException($"'{url}' did not start responding within {timeout}.");
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
