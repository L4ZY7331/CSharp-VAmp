using CSharpVAmp.Services;
using CSharpVAmp.Utils;
using Microsoft.Extensions.Logging;

namespace CSharpVAmp.Services;

public class RestartChecker
{
    private InstanceManager? _manager;
    private readonly int _restartIntervalSeconds;
    private readonly ILogger<RestartChecker> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _workerTask;

    public RestartChecker(
        int restartIntervalSeconds,
        ILogger<RestartChecker> logger)
    {
        _restartIntervalSeconds = restartIntervalSeconds;
        _logger = logger;
    }

    public void SetManager(InstanceManager manager)
    {
        _manager = manager;
    }

    public void Start()
    {
        if (_manager == null) return;
        
        if (_workerTask != null && !_workerTask.IsCompleted)
        {
            return; // Already running
        }

        _logger.LogInformation("Restarter enabled.");
        _cancellationTokenSource = new CancellationTokenSource();
        _workerTask = Task.Run(() => RestartLoopAsync(_cancellationTokenSource.Token));
    }

    public void Stop()
    {
        if (_cancellationTokenSource == null)
        {
            return;
        }

        _logger.LogInformation("Restarter disabled.");
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = null;
    }

    private async Task RestartLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_manager == null) break;
                var instancesCount = _manager.InstancesAliveCount;
                if (instancesCount == 0)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                var sleepTime = _restartIntervalSeconds / instancesCount;
                await Task.Delay(sleepTime * 1000, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var instance = _manager?.GetOldestInstance();
                if (instance != null)
                {
                    _logger.LogInformation("Restarting oldest instance {Id}. Restart interval: {Interval}s",
                        instance.Id, sleepTime);
                    instance.Command = InstanceCommands.Restart;
                    instance.LastRestartTime = DateTime.Now;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in restart loop");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
}

