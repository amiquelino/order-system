using System.Collections.Concurrent;
using Bogus;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace OrderSystem.EndToEndTests;

public class CreateOrderE2ETests : IClassFixture<AppHostFixture>, IAsyncLifetime
{
    // CI uploads this directory as an artifact when the job fails, so the trace of a
    // failed run can be opened at https://trace.playwright.dev (or locally with
    // `pwsh bin/Debug/net10.0/playwright.ps1 show-trace <file>`).
    private static readonly string TraceDirectory = Path.Combine(AppContext.BaseDirectory, "playwright-traces");

    private readonly AppHostFixture _fixture;
    private readonly ITestOutputHelper _output;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;

    public CreateOrderE2ETests(AppHostFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _context = await _browser.NewContextAsync();

        await _context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });
    }

    public async Task DisposeAsync()
    {
        // Saved on every run (cheap, overwritten next run); CI only uploads it on failure.
        var tracePath = Path.Combine(TraceDirectory, $"{nameof(CreateOrderE2ETests)}.zip");
        await _context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
        Log($"Playwright trace saved to {tracePath}");

        await _context.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task CreatingAnOrder_ThatIsDeclinedThenRetried_ConfirmsLiveOnTheStatusPage()
    {
        var page = await _context.NewPageAsync();
        var productName = new Faker().Commerce.ProductName();
        var consoleMessages = new ConcurrentBag<string>();

        // Routed through ITestOutputHelper, not Console.WriteLine: xUnit v2 doesn't
        // capture Console output, so it never reached the CI log. dotnet test prints
        // this output under "Standard Output Messages" for a failed test.
        page.Console += (_, msg) =>
        {
            consoleMessages.Add(msg.Text);
            Log($"[browser:{msg.Type}] {msg.Text}");
        };
        page.PageError += (_, error) => Log($"[browser:pageerror] {error}");
        page.RequestFailed += (_, request) => Log($"[browser:requestfailed] {request.Method} {request.Url} {request.Failure}");
        page.Response += (_, response) =>
        {
            if (response.Status >= 400 || response.Request.Method == "POST")
            {
                Log($"[browser:response] {response.Request.Method} {response.Url} -> {response.Status}");
            }
        };

        await page.GotoAsync(_fixture.FrontendUrl, new PageGotoOptions { Timeout = 60_000 });

        // The fixture warms the server-side compile, but in Next.js dev mode the
        // client bundle still has to load and React has to hydrate before the
        // form's onSubmit handler is attached. Clicking earlier triggers a native
        // form submit (full reload of "/"), so the app never navigates to the
        // status page — the cause of an observed CI flake. React tags every
        // hydrated host element with a "__reactProps$<id>" key; wait for it on the
        // form instead of guessing with a delay.
        await page.WaitForFunctionAsync(
            "() => { const form = document.querySelector('form'); return !!form && Object.keys(form).some(k => k.startsWith('__reactProps')); }",
            null,
            new PageWaitForFunctionOptions { Timeout = 60_000 });

        // $500 total: SimulatedPaymentGateway declines the first attempt and only
        // approves on retry, so the page really does have to live-update via
        // SignalR after navigating — a cheap order would resolve synchronously
        // before the browser even gets to the status page.
        await page.FillAsync("input[placeholder='Product name']", productName);
        await page.FillAsync("input[placeholder='Quantity']", "1");
        await page.FillAsync("input[placeholder='Unit price']", "500");

        await page.ClickAsync("button:has-text('Create order')");

        // WaitUntil defaults to "Load", which also waits for the page's resources to
        // finish loading. Next.js dev-mode cold-compiles /status/[id] on this first
        // navigation to it (10-20s server-side), so the response headers alone can eat
        // most of a 30s "Load" budget; "Commit" only needs the URL/navigation itself,
        // and the retrying assertions below cover actual content readiness anyway.
        try
        {
            await page.WaitForURLAsync("**/status/*", new PageWaitForURLOptions
            {
                Timeout = 45_000,
                WaitUntil = WaitUntilState.Commit
            });
        }
        catch (TimeoutException ex)
        {
            // The create page only navigates on a successful POST /orders; on any
            // error it stays put and renders the message. Surface what's on screen
            // so the failure says *why* instead of just "timeout".
            var pageText = await page.Locator("main").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 5_000 });
            throw new TimeoutException(
                $"Never navigated to /status/* after submitting the order; still on '{page.Url}'. Page content:\n{pageText}",
                ex);
        }

        var statusParagraph = page.Locator("p", new PageLocatorOptions { HasText = "Status:" });

        // Not asserting on the intermediate "PaymentFailed" text: on this slow-to-load
        // (dev-mode, this environment) frontend, the RabbitMQ-delayed retry can finish
        // server-side before the browser even finishes connecting, so the DOM can go
        // straight from "Loading..." to "Confirmed" without ever rendering the
        // transient state. The retry having happened is implied by reaching Confirmed
        // at all, since $500 always declines on the simulated gateway's first attempt.
        // No reload here: the RabbitMQ-delayed retry resolves in the background and
        // the frontend's existing SignalR connection pushes the new status straight in.
        await Assertions.Expect(statusParagraph)
            .ToContainTextAsync("Confirmed", new LocatorAssertionsToContainTextOptions { Timeout = 30_000 });

        // The Confirmed assertion above can theoretically pass off the page's initial
        // fetch alone, without the SignalR push ever mattering, if the RabbitMQ retry
        // happens to finish before the frontend even connects (a real race in this
        // environment - see AppHostFixture). That would make this test pass even if
        // OrderSystem.NotificationService were completely unreachable. Assert the hub
        // connection itself actually succeeded, independent of that race, so a broken
        // NotificationService (the actual thing this test exists to protect) still
        // fails the test either way.
        Assert.Contains(consoleMessages, message => message.Contains("WebSocket connected"));
    }

    // Playwright raises events on its own threads, and some (e.g. console messages
    // during teardown) can arrive after xUnit has closed this test's output; losing
    // those lines is fine, crashing the event dispatcher over them is not.
    private void Log(string message)
    {
        try
        {
            _output.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
