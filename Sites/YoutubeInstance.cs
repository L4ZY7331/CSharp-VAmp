using CSharpVAmp.Core;
using CSharpVAmp.Models;
using CSharpVAmp.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CSharpVAmp.Sites;

public class YoutubeInstance : InstanceBase
{
    public override string SiteName => "YOUTUBE";
    public override string SiteUrl => "youtube.com";

    private readonly Dictionary<string, string> _localStorage;
    private StatusInfo? _statusInfo;

    private class StatusInfo
    {
        public double LastActiveResumeTime { get; set; }
        public DateTime LastActiveTimestamp { get; set; }
        public string? LastStreamId { get; set; }
    }

    public YoutubeInstance(
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
        var nowTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var nextYearTimestampMs = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeMilliseconds();

        _localStorage = new Dictionary<string, string>
        {
            {
                "yt-player-quality",
                $"{{\"data\":\"{{\\\"quality\\\":144,\\\"previousQuality\\\":144}}\",\"expiration\":{nextYearTimestampMs},\"creation\":{nowTimestampMs}}}"
            }
        };
    }

    protected override async Task TodoAfterSpawnAsync()
    {
        await GotoWithRetryAsync("https://www.youtube.com/");
        await Task.Delay(1000);

        try
        {
            if (Page != null)
            {
                await Page.ClickAsync(".eom-button-row.style-scope.ytd-consent-bump-v2-lightbox > ytd-button-renderer:nth-child(1) button",
                    new PageClickOptions { Timeout = 10000 });
            }
        }
        catch (Exception)
        {
            Logger.LogWarning("Cookie consent banner not found/clicked.");
        }

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
            await Page.WaitForSelectorAsync(".ytd-player", new PageWaitForSelectorOptions { Timeout = 30000 });
            await Task.Delay(5000);

            var isPaused = await Page.EvaluateAsync<bool>(
                "document.querySelector('div#movie_player').classList.contains('paused-mode')");
            if (isPaused)
            {
                await Page.Keyboard.PressAsync("Space");
            }

            await Page.Keyboard.PressAsync("f");
        }

        ReportStatus(InstanceStatus.Initialized);
    }

    protected override async Task TodoEveryLoopAsync()
    {
        if (Page == null) return;

        var notPlaying = await Page.QuerySelectorAsync("div.html5-video-player:not(.playing-mode)");
        if (notPlaying != null)
        {
            await Page.Keyboard.PressAsync("Space");
        }

        try
        {
            await Page.ClickAsync("button.ytp-ad-skip-button-modern", new PageClickOptions { Timeout = 100 });
        }
        catch (Exception)
        {
        }
    }

    protected override async Task UpdateStatusAsync()
    {
        if (Page == null) return;

        var currentTime = DateTime.Now;
        _statusInfo ??= new StatusInfo
        {
            LastActiveResumeTime = 0,
            LastActiveTimestamp = currentTime - TimeSpan.FromSeconds(10),
            LastStreamId = null
        };

        var timeSinceLastActivity = currentTime - _statusInfo.LastActiveTimestamp;
        if (timeSinceLastActivity < TimeSpan.FromSeconds(15))
        {
            ReportStatus(InstanceStatus.Watching);
            return;
        }

        try
        {
            var currentResumeTime = await Page.EvaluateAsync<double>(
                @"() => {
                    const element = document.querySelector('.ytp-progress-bar');
                    return element ? parseFloat(element.getAttribute('aria-valuenow')) : 0;
                }");

            if (currentResumeTime > 0)
            {
                if (currentResumeTime > _statusInfo.LastActiveResumeTime)
                {
                    _statusInfo.LastActiveTimestamp = currentTime;
                    _statusInfo.LastActiveResumeTime = currentResumeTime;
                    ReportStatus(InstanceStatus.Watching);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to get resume time");
        }

        ReportStatus(InstanceStatus.Buffering);
    }
}

