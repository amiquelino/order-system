using System.Collections.Concurrent;
using Bogus;
using Microsoft.Playwright;

namespace OrderSystem.EndToEndTests;

public class CreateOrderE2ETests : IClassFixture<AppHostFixture>, IAsyncLifetime
{
    private readonly AppHostFixture _fixture;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public CreateOrderE2ETests(AppHostFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task CreatingAnOrder_ThatIsDeclinedThenRetried_ConfirmsLiveOnTheStatusPage()
    {
        var page = await _browser.NewPageAsync();
        var productName = new Faker().Commerce.ProductName();
        var consoleMessages = new ConcurrentBag<string>();

        page.Console += (_, msg) =>
        {
            consoleMessages.Add(msg.Text);
            Console.WriteLine($"[browser:{msg.Type}] {msg.Text}");
        };
        page.PageError += (_, error) => Console.WriteLine($"[browser:pageerror] {error}");
        page.RequestFailed += (_, request) => Console.WriteLine($"[browser:requestfailed] {request.Url} {request.Failure}");

        await page.GotoAsync(_fixture.FrontendUrl, new PageGotoOptions { Timeout = 60_000 });

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
        await page.WaitForURLAsync("**/status/*", new PageWaitForURLOptions
        {
            Timeout = 45_000,
            WaitUntil = WaitUntilState.Commit
        });

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
}
