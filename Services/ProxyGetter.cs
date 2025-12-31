using System.Collections.Concurrent;
using System.IO;
using CSharpVAmp.Models;
using Microsoft.Extensions.Logging;

namespace CSharpVAmp.Services;

public class ProxyGetter
{
    private readonly ConcurrentQueue<ProxyInfo> _proxyQueue = new();
    private readonly ILogger<ProxyGetter> _logger;
    private readonly string _proxyFilePath;

    public int Count => _proxyQueue.Count;

    public ProxyGetter(string proxyFilePath, ILogger<ProxyGetter> logger)
    {
        _proxyFilePath = proxyFilePath;
        _logger = logger;
        BuildProxyList();
    }

    private void BuildProxyList()
    {
        if (!File.Exists(_proxyFilePath))
        {
            _logger.LogWarning("Proxy file not found: {Path}", _proxyFilePath);
            return;
        }

        try
        {
            var lines = File.ReadAllLines(_proxyFilePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            foreach (var line in lines)
            {
                var proxy = ParseProxyLine(line.Trim());
                if (proxy != null)
                {
                    _proxyQueue.Enqueue(proxy);
                }
            }

            _logger.LogInformation("Loaded {Count} proxies from {Path}", _proxyQueue.Count, _proxyFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load proxies from {Path}", _proxyFilePath);
            throw new FileNotFoundException($"Unable to find or read {_proxyFilePath}", ex);
        }
    }

    private ProxyInfo? ParseProxyLine(string line)
    {
        var parts = line.Split(':');
        
        if (parts.Length == 4)
        {
            var ip = parts[0];
            var port = parts[1];
            var username = parts[2];
            var password = parts[3];

            if (username.Equals("username", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Skipping proxy with placeholder username: {Line}", line);
                return null;
            }

            return new ProxyInfo
            {
                Server = $"http://{ip}:{port}",
                Username = username,
                Password = password
            };
        }
        else if (parts.Length == 2)
        {
            var ip = parts[0];
            var port = parts[1];

            return new ProxyInfo
            {
                Server = $"http://{ip}:{port}",
                Username = string.Empty,
                Password = string.Empty
            };
        }
        else
        {
            _logger.LogWarning("Invalid proxy format: {Line}", line);
            return null;
        }
    }

    public ProxyInfo? GetProxy()
    {
        if (_proxyQueue.IsEmpty)
        {
            return null;
        }

        if (_proxyQueue.TryDequeue(out var proxy))
        {
            _proxyQueue.Enqueue(proxy);
            return proxy;
        }

        return null;
    }
}

