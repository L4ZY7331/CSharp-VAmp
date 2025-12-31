using System.Collections.Concurrent;
using CSharpVAmp.Core;
using CSharpVAmp.Models;
using CSharpVAmp.Utils;
using Microsoft.Extensions.Logging;

namespace CSharpVAmp.Services;

public class InstanceManager
{
    private readonly ConcurrentDictionary<int, InstanceBase> _browserInstances = new();
    private readonly object _managerLock = new();
    private readonly ILogger<InstanceManager> _logger;
    private readonly ProxyGetter _proxyGetter;
    private readonly ScreenManager _screenManager;
    private readonly RestartChecker _restartChecker;
    private bool _headless;
    private bool _autoRestart;
    private readonly int _spawnIntervalSeconds;

    private int _nextInstanceId = 1;

    public int InstancesAliveCount { get; private set; }
    public int InstancesWatchingCount { get; private set; }
    public Dictionary<int, InstanceStatus> InstancesOverview { get; private set; } = new();

    public InstanceManager(
        ProxyGetter proxyGetter,
        ScreenManager screenManager,
        RestartChecker restartChecker,
        bool headless,
        bool autoRestart,
        int spawnIntervalSeconds,
        ILogger<InstanceManager> logger)
    {
        _proxyGetter = proxyGetter;
        _screenManager = screenManager;
        _restartChecker = restartChecker;
        _headless = headless;
        _autoRestart = autoRestart;
        _spawnIntervalSeconds = spawnIntervalSeconds;
        _logger = logger;
    }

    public bool Headless
    {
        get => _headless;
        set
        {
            _headless = value;
            _logger.LogInformation("Headless mode set to {Value}", value);
        }
    }

    public bool AutoRestart
    {
        get => _autoRestart;
        set
        {
            _autoRestart = value;
            _logger.LogInformation("AutoRestart mode set to {Value}", value);
            ReconfigureAutoRestartStatus();
        }
    }

    public async Task SpawnInstanceAsync(string? targetUrl = null)
    {
        if (string.IsNullOrEmpty(targetUrl))
        {
            throw new ArgumentException("No target URL provided", nameof(targetUrl));
        }

        var instanceId = Interlocked.Increment(ref _nextInstanceId) - 1;

        _ = Task.Run(async () =>
        {
            try
            {
                await SpawnInstanceThreadAsync(targetUrl, instanceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to spawn instance {Id}", instanceId);
            }
        });
    }

    public async Task SpawnInstancesAsync(int count, string? targetUrl = null)
    {
        for (var i = 0; i < count; i++)
        {
            await SpawnInstanceAsync(targetUrl);
            if (i < count - 1)
            {
                await Task.Delay(_spawnIntervalSeconds * 1000);
            }
        }
    }

    private async Task SpawnInstanceThreadAsync(string targetUrl, int browserInstanceId)
    {
        lock (_managerLock)
        {
            var proxy = _proxyGetter.GetProxy();

            ScreenLocation? screenLocation;
            if (_headless)
            {
                screenLocation = _screenManager.GetDefaultLocation();
            }
            else
            {
                screenLocation = _screenManager.GetFreeScreenLocation();
            }

            if (screenLocation == null)
            {
                Console.WriteLine("no screen space left");
                return;
            }

            var serverIp = proxy?.Server ?? "no proxy";
            _logger.LogInformation("Ordered instance {Id}, proxy {ServerIp}", browserInstanceId, serverIp);

            var instanceLogger = new LoggerAdapter<CSharpVAmp.Core.InstanceBase>(_logger);

            var instance = InstanceFactory.CreateInstance(
                targetUrl,
                browserInstanceId,
                proxy,
                screenLocation,
                _headless,
                _autoRestart,
                InstanceStatusReportCallback,
                instanceLogger);

            _browserInstances[browserInstanceId] = instance;
        }

        var instanceToStart = _browserInstances[browserInstanceId];
        await instanceToStart.StartAsync();

        if (_browserInstances.TryRemove(browserInstanceId, out _))
        {
            _logger.LogInformation("Removed instance {Id} from manager", browserInstanceId);
        }
    }

    private void InstanceStatusReportCallback(int instanceId, InstanceStatus instanceStatus)
    {
        _logger.LogInformation("{Status} instance {Id}", instanceStatus.ToString().ToUpper(), instanceId);
        UpdateInstancesOverview();
        UpdateInstancesAliveCount();
        UpdateInstancesWatchingCount();
        ReconfigureAutoRestartStatus();
    }

    private void UpdateInstancesAliveCount()
    {
        InstancesAliveCount = _browserInstances.Values
            .Count(instance => instance.Status != InstanceStatus.Shutdown);
    }

    private void UpdateInstancesWatchingCount()
    {
        InstancesWatchingCount = _browserInstances.Values
            .Count(instance => instance.Status == InstanceStatus.Watching);
    }

    private void UpdateInstancesOverview()
    {
        var newOverview = new Dictionary<int, InstanceStatus>();
        foreach (var (instanceId, instance) in _browserInstances)
        {
            if (instance.Status != InstanceStatus.Shutdown)
            {
                newOverview[instanceId] = instance.Status;
            }
        }
        InstancesOverview = newOverview;
    }

    private void ReconfigureAutoRestartStatus()
    {
        if (InstancesAliveCount > 0 && _autoRestart)
        {
            _restartChecker.Start();
        }
        else
        {
            _restartChecker.Stop();
        }
    }

    public bool QueueCommand(int instanceId, InstanceCommands command)
    {
        if (!_browserInstances.TryGetValue(instanceId, out var instance))
        {
            return false;
        }

        instance.Command = command;
        return true;
    }

    public void DeleteLatest()
    {
        if (_browserInstances.IsEmpty)
        {
            Console.WriteLine("No instances found");
            return;
        }

        var latestKey = _browserInstances.Keys.Max();
        DeleteSpecific(latestKey);
    }

    public void DeleteSpecific(int instanceId)
    {
        if (!_browserInstances.TryGetValue(instanceId, out var instance))
        {
            Console.WriteLine($"Instance ID {instanceId} not found. Unable to shutdown.");
            return;
        }

        Console.WriteLine($"Issuing shutdown of instance #{instanceId}");
        instance.Command = InstanceCommands.Exit;
    }

    public void DeleteAllInstances()
    {
        foreach (var instanceId in _browserInstances.Keys.ToList())
        {
            DeleteSpecific(instanceId);
        }
    }

    public InstanceBase? GetOldestInstance()
    {
        if (_browserInstances.IsEmpty)
        {
            return null;
        }

        return _browserInstances.Values
            .OrderBy(instance => instance.LastRestartTime)
            .FirstOrDefault();
    }

    public IEnumerable<InstanceBase> GetAllInstances()
    {
        return _browserInstances.Values;
    }
}

internal class LoggerAdapter<T> : ILogger<T>
{
    private readonly ILogger _innerLogger;

    public LoggerAdapter(ILogger innerLogger)
    {
        _innerLogger = innerLogger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _innerLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _innerLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _innerLogger.Log(logLevel, eventId, state, exception, formatter);
    }
}

