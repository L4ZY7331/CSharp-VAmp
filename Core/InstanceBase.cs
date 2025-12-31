using System.Diagnostics;
using CSharpVAmp.Models;
using CSharpVAmp.Services;
using CSharpVAmp.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CSharpVAmp.Core;

public abstract class InstanceBase
{
    protected readonly ILogger<InstanceBase> Logger;
    protected readonly ProxyInfo? Proxy;
    protected readonly string TargetUrl;
    protected readonly bool Headless;
    protected readonly bool AutoRestart;
    protected readonly ScreenLocation LocationInfo;
    protected readonly Action<int, InstanceStatus> StatusReporter;

    public int Id { get; }
    public InstanceStatus Status { get; protected set; } = InstanceStatus.Starting;
    public InstanceCommands Command { get; set; } = InstanceCommands.None;
    public DateTime LastRestartTime { get; set; } = DateTime.Now;

    protected IPlaywright? Playwright;
    protected IBrowser? Browser;
    protected IBrowserContext? Context;
    protected IPage? Page;

    protected InstanceBase(
        int instanceId,
        ProxyInfo? proxy,
        string targetUrl,
        ScreenLocation locationInfo,
        bool headless,
        bool autoRestart,
        Action<int, InstanceStatus> statusReporter,
        ILogger<InstanceBase> logger)
    {
        Id = instanceId;
        Proxy = proxy;
        TargetUrl = targetUrl;
        LocationInfo = locationInfo;
        Headless = headless;
        AutoRestart = autoRestart;
        StatusReporter = statusReporter;
        Logger = logger;
    }

    public abstract string SiteName { get; }
    public abstract string SiteUrl { get; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SpawnPageAsync();
            await TodoAfterSpawnAsync();
            await LoopAndCheckAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            var message = ex.Message.Length > 25 ? ex.Message[..25] : ex.Message;
            Logger.LogError(ex, "{SiteName} Instance {Id} died: {ExceptionType}: {Message}", 
                SiteName, Id, ex.GetType().Name, message);
            Console.WriteLine($"{SiteName} Instance {Id} died: {ex.GetType().Name}:{message}... Please see cvamp.log.");
        }
        finally
        {
            Logger.LogInformation("ENDED: instance {Id}", Id);
            Console.WriteLine($"Instance {Id} shutting down");
            Status = InstanceStatus.Shutdown;
            await CleanUpPlaywrightAsync();
            LocationInfo.Free = true;
        }
    }

    protected virtual async Task SpawnPageAsync(bool restart = false)
    {
        Status = restart ? InstanceStatus.Restarting : InstanceStatus.Starting;

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        var chromiumArgs = new List<string>
        {
            $"--window-position={LocationInfo.X},{LocationInfo.Y}",
            "--no-sandbox",
            "--disable-setuid-sandbox",
            "--no-first-run",
            "--disable-blink-features=AutomationControlled",
            "--mute-audio",
            "--webrtc-ip-handling-policy=disable_non_proxied_udp",
            "--force-webrtc-ip-handling-policy"
        };

        if (Headless)
        {
            chromiumArgs.Add("--headless");
        }

        var proxyDict = Proxy?.ToPlaywrightProxy();

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Channel = "chrome",
            Headless = false,
            Args = chromiumArgs
        };

        if (proxyDict != null)
        {
            launchOptions.Proxy = proxyDict;
        }

        Browser = await Playwright.Chromium.LaunchAsync(launchOptions);

        var majorVersion = Browser.Version.Split('.')[0];
        var contextOptions = new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 800, Height = 600 },
            UserAgent = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{majorVersion}.0.0.0 Safari/537.36"
        };

        if (proxyDict != null)
        {
            contextOptions.Proxy = proxyDict;
        }

        Context = await Browser.NewContextAsync(contextOptions);
        Page = await Context.NewPageAsync();
        await Page.AddInitScriptAsync("navigator.webdriver = false;");
    }

    protected async Task GotoWithRetryAsync(string url, int maxTries = 3, int timeout = 20000)
    {
        for (var attempt = 1; attempt <= maxTries; attempt++)
        {
            try
            {
                if (Page == null) throw new InvalidOperationException("Page is null");
                await Page.GotoAsync(url, new PageGotoOptions { Timeout = timeout });
                return;
            }
            catch (Exception)
            {
                Logger.LogWarning("Instance {Id} failed connection attempt #{Attempt}", Id, attempt);
                if (attempt == maxTries)
                {
                    throw;
                }
            }
        }
    }

    protected virtual async Task TodoAfterSpawnAsync()
    {
        Status = InstanceStatus.Initialized;
        await GotoWithRetryAsync(TargetUrl);
    }

    protected virtual async Task TodoAfterLoadAsync()
    {
        await GotoWithRetryAsync(TargetUrl);
        await Task.Delay(1000);
    }

    protected virtual async Task TodoEveryLoopAsync()
    {
        await Task.CompletedTask;
    }

    protected virtual Task UpdateStatusAsync()
    {
        return Task.CompletedTask;
    }

    protected async Task ReloadPageAsync()
    {
        if (Page == null) throw new InvalidOperationException("Page is null");
        await Page.ReloadAsync(new PageReloadOptions { Timeout = 30000 });
        await TodoAfterLoadAsync();
    }

    protected async Task SaveScreenshotAsync()
    {
        if (Page == null) throw new InvalidOperationException("Page is null");
        var filename = $"{DateTime.Now:yyyyMMdd_HHmmss}_instance{Id}.png";
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = filename });
        Console.WriteLine($"Saved screenshot of instance id {Id}");
    }

    private async Task LoopAndCheckAsync(CancellationToken cancellationToken)
    {
        const int pageTimeoutSeconds = 10;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (Page == null) break;

            await Task.Delay(pageTimeoutSeconds * 1000, cancellationToken);
            await TodoEveryLoopAsync();
            await UpdateStatusAsync();

            switch (Command)
            {
                case InstanceCommands.Restart:
                    await CleanUpPlaywrightAsync();
                    await SpawnPageAsync(restart: true);
                    await TodoAfterSpawnAsync();
                    Command = InstanceCommands.None;
                    break;

                case InstanceCommands.Screenshot:
                    await SaveScreenshotAsync();
                    Command = InstanceCommands.None;
                    break;

                case InstanceCommands.Refresh:
                    Console.WriteLine($"Manual refresh of instance id {Id}");
                    await ReloadPageAsync();
                    Command = InstanceCommands.None;
                    break;

                case InstanceCommands.Exit:
                    return;

                case InstanceCommands.None:
                    break;
            }
        }
    }

    protected async Task CleanUpPlaywrightAsync()
    {
        try
        {
            Page?.CloseAsync();
            Context?.CloseAsync();
            Browser?.CloseAsync();
            Playwright?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error cleaning up Playwright for instance {Id}", Id);
        }
    }

    protected void ReportStatus(InstanceStatus newStatus)
    {
        if (Status == newStatus) return;
        Status = newStatus;
        StatusReporter(Id, newStatus);
    }
}

