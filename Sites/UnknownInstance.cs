using CSharpVAmp.Core;
using CSharpVAmp.Models;
using CSharpVAmp.Utils;
using Microsoft.Extensions.Logging;

namespace CSharpVAmp.Sites;

public class UnknownInstance : InstanceBase
{
    public override string SiteName => "UNKNOWN";
    public override string SiteUrl => string.Empty;

    public UnknownInstance(
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

    protected override async Task TodoEveryLoopAsync()
    {
        if (Page != null)
        {
            await Page.Keyboard.PressAsync("Tab");
        }
    }

    protected override async Task TodoAfterSpawnAsync()
    {
        await GotoWithRetryAsync(TargetUrl);
        await Task.Delay(1000);
    }
}

