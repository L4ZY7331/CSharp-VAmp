using CSharpVAmp.Core;
using CSharpVAmp.Models;
using CSharpVAmp.Sites;
using CSharpVAmp.Utils;
using Microsoft.Extensions.Logging;

namespace CSharpVAmp.Services;

public static class InstanceFactory
{
    private static readonly Dictionary<string, Func<int, ProxyInfo?, string, ScreenLocation, bool, bool, Action<int, InstanceStatus>, ILogger<InstanceBase>, InstanceBase>> SiteFactories = new()
    {
        { "twitch.tv", (id, proxy, url, loc, headless, autoRestart, reporter, logger) =>
            new TwitchInstance(id, proxy, url, loc, headless, autoRestart, reporter, logger) },
        { "kick.com", (id, proxy, url, loc, headless, autoRestart, reporter, logger) =>
            new KickInstance(id, proxy, url, loc, headless, autoRestart, reporter, logger) },
        { "youtube.com", (id, proxy, url, loc, headless, autoRestart, reporter, logger) =>
            new YoutubeInstance(id, proxy, url, loc, headless, autoRestart, reporter, logger) },
        { "chzzk.naver.com", (id, proxy, url, loc, headless, autoRestart, reporter, logger) =>
            new ChzzkInstance(id, proxy, url, loc, headless, autoRestart, reporter, logger) }
    };

    public static InstanceBase CreateInstance(
        string targetUrl,
        int instanceId,
        ProxyInfo? proxy,
        ScreenLocation locationInfo,
        bool headless,
        bool autoRestart,
        Action<int, InstanceStatus> statusReporter,
        ILogger<InstanceBase> logger)
    {
        foreach (var (siteUrl, factory) in SiteFactories)
        {
            if (targetUrl.Contains(siteUrl, StringComparison.OrdinalIgnoreCase))
            {
                return factory(instanceId, proxy, targetUrl, locationInfo, headless, autoRestart, statusReporter, logger);
            }
        }

        return new UnknownInstance(instanceId, proxy, targetUrl, locationInfo, headless, autoRestart, statusReporter, logger);
    }
}

