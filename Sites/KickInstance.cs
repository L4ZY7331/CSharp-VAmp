using CSharpVAmp.Core;
using CSharpVAmp.Models;
using CSharpVAmp.Utils;
using Microsoft.Extensions.Logging;

namespace CSharpVAmp.Sites;

public class KickInstance : InstanceBase
{
    public override string SiteName => "KICK";
    public override string SiteUrl => "kick.com";

    private readonly Dictionary<string, string> _localStorage = new()
    {
        { "agreed_to_mature_content", "true" },
        { "kick_cookie_accepted", "true" },
        { "kick_video_size", "160p" }
    };

    public KickInstance(
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
        await GotoWithRetryAsync("https://kick.com/terms-of-service");
        await Task.Delay(5000);

        if (Page != null)
        {
            foreach (var (key, value) in _localStorage)
            {
                await Page.EvaluateAsync($"window.localStorage.setItem('{key}','{value}');");
            }
        }

        await GotoWithRetryAsync(TargetUrl);
        await Task.Delay(1000);

        if (Page != null)
        {
            var content = await Page.ContentAsync();
            if (content.ToLower().Contains("cloudflare"))
            {
                throw new CloudflareBlockException("Blocked by Cloudflare.");
            }
        }

        ReportStatus(InstanceStatus.Initialized);
    }

    protected override async Task TodoEveryLoopAsync()
    {
        if (Page != null)
        {
            await Page.Keyboard.PressAsync("Tab");
        }
    }
}

public class CloudflareBlockException : Exception
{
    public CloudflareBlockException(string message) : base(message) { }
}

