using CSharpVAmp.Core;
using CSharpVAmp.Models;
using CSharpVAmp.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CSharpVAmp.Sites;

public class ChzzkInstance : InstanceBase
{
    public override string SiteName => "CHZZK";
    public override string SiteUrl => "chzzk.naver.com";

    private readonly Dictionary<string, string> _localStorage = new()
    {
        {
            "live-player-video-track",
            "{\"label\":\"360p\",\"kind\":\"low-latency\",\"width\":640,\"height\":360}"
        }
    };

    public ChzzkInstance(
        int instanceId,
        ProxyInfo? proxy,
        string targetUrl,
        ScreenLocation locationInfo,
        bool headless,
        bool autoRestart,
        Action<int, InstanceStatus> statusReporter,
        ILogger<InstanceBase> logger)
        : base(instanceId, proxy, targetUrl, locationInfo, headless, autoRestart, statusReporter, logger)
    {
    }

    protected override async Task TodoAfterSpawnAsync()
    {
        await GotoWithRetryAsync("https://chzzk.naver.com/category");
        await Task.Delay(1000);

        if (Page != null)
        {
            foreach (var (key, value) in _localStorage)
            {
                await Page.EvaluateAsync($"window.localStorage.setItem('{key}','{value}');");
            }
        }

        await GotoWithRetryAsync(TargetUrl);

        if (Page != null)
        {
            await Page.WaitForSelectorAsync("#live_player_layout", new PageWaitForSelectorOptions { Timeout = 30000 });
            await Task.Delay(1000);
            await Page.Keyboard.PressAsync("f");

            await Page.SetViewportSizeAsync(LocationInfo.Width, LocationInfo.Height);
        }

        ReportStatus(InstanceStatus.Initialized);
    }

    protected override async Task TodoAfterLoadAsync()
    {
        if (Page == null) return;

        await Page.WaitForSelectorAsync("#live_player_layout", new PageWaitForSelectorOptions { Timeout = 30000 });
        await Task.Delay(1000);
        await Page.Keyboard.PressAsync("f");
    }

    protected override async Task TodoEveryLoopAsync()
    {
        try
        {
            if (Page != null)
            {
                await Page.ClickAsync("button.btn_skip", new PageClickOptions { Timeout = 100 });
            }
        }
        catch (Exception)
        {
        }
    }

    protected override async Task UpdateStatusAsync()
    {
        if (Page == null) return;

        try
        {
            var html = await Page.EvaluateAsync<string>("document.querySelector('div#live_player_layout').innerHTML");
            if (html.Contains("pzp-pc--live"))
            {
                if (!html.Contains("pzp-pc--loading"))
                {
                    ReportStatus(InstanceStatus.Watching);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to check status");
        }

        ReportStatus(InstanceStatus.Buffering);
    }
}

