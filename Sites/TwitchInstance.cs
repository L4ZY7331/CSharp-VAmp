using System.Text.Json;
using CSharpVAmp.Core;
using CSharpVAmp.Models;
using CSharpVAmp.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CSharpVAmp.Sites;

public class TwitchInstance : InstanceBase
{
    public override string SiteName => "TWITCH";
    public override string SiteUrl => "twitch.tv";

    private readonly Dictionary<string, string> _localStorage = new()
    {
        { "mature", "true" },
        { "video-muted", "{\"default\": \"false\"}" },
        { "volume", "0.5" },
        { "video-quality", "{\"default\": \"160p30\"}" },
        { "lowLatencyModeEnabled", "false" }
    };

    private StatusInfo? _statusInfo;

    private class StatusInfo
    {
        public double LastActiveResumeTime { get; set; }
        public DateTime LastActiveTimestamp { get; set; }
        public string? LastStreamId { get; set; }
    }

    public TwitchInstance(
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
        await GotoWithRetryAsync("https://www.twitch.tv/p/en/partners/");

        try
        {
            if (Page != null)
            {
                await Page.ClickAsync("button[data-a-target=consent-banner-accept]", new PageClickOptions { Timeout = 15000 });
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

            await Page.SetViewportSizeAsync(LocationInfo.Width, LocationInfo.Height);
        }

        await GotoWithRetryAsync(TargetUrl);
        await Task.Delay(1000);

        if (Page != null)
        {
            await Page.WaitForSelectorAsync(".persistent-player", new PageWaitForSelectorOptions { Timeout = 15000 });
            await Page.Keyboard.PressAsync("Alt+t");
            await Task.Delay(1000);

            try
            {
                await Page.ClickAsync("button[data-a-target=content-classification-gate-overlay-start-watching-button]",
                    new PageClickOptions { Timeout = 3000 });
            }
            catch (Exception)
            {
                Logger.LogInformation("Mature button not found/clicked.");
            }
        }

        ReportStatus(InstanceStatus.Initialized);
    }

    protected override async Task TodoAfterLoadAsync()
    {
        if (Page == null) return;

        await Page.WaitForSelectorAsync(".persistent-player", new PageWaitForSelectorOptions { Timeout = 30000 });
        await Task.Delay(1000);
        await Page.Keyboard.PressAsync("Alt+t");
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
        if (timeSinceLastActivity < TimeSpan.FromSeconds(10))
        {
            ReportStatus(InstanceStatus.Watching);
            return;
        }

        var fetchedResumeTimes = await Page.EvaluateAsync<string>("window.localStorage.getItem('livestreamResumeTimes');");
        if (!string.IsNullOrEmpty(fetchedResumeTimes))
        {
            try
            {
                var resumeTimesDict = JsonSerializer.Deserialize<Dictionary<string, double>>(fetchedResumeTimes);
                if (resumeTimesDict != null && resumeTimesDict.Count > 0)
                {
                    var currentStreamId = resumeTimesDict.Keys.Last();
                    var currentResumeTime = resumeTimesDict.Values.Last();

                    if (string.IsNullOrEmpty(_statusInfo.LastStreamId))
                    {
                        _statusInfo.LastStreamId = currentStreamId;
                    }

                    if (currentStreamId != _statusInfo.LastStreamId)
                    {
                        _statusInfo.LastStreamId = currentStreamId;
                        _statusInfo.LastActiveResumeTime = 0;
                    }

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
                Logger.LogWarning(ex, "Failed to parse resume times");
            }
        }

        ReportStatus(InstanceStatus.Buffering);
    }
}

