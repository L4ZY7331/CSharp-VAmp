using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpVAmp.Models;
using CSharpVAmp.Services;
using CSharpVAmp.Utils;
using Microsoft.Extensions.Logging;

namespace CSharpVAmp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly InstanceManager _instanceManager;
    private readonly ILogger<MainViewModel> _logger;
    private readonly DispatcherTimer _refreshTimer;
    private int _cpuRamUpdateCounter = 0;

    [ObservableProperty]
    private string _channelUrl = "https://www.twitch.tv/channel_name";

    [ObservableProperty]
    private int _proxiesAvailable;

    [ObservableProperty]
    private int _aliveInstances;

    [ObservableProperty]
    private int _watchingInstances;

    [ObservableProperty]
    private string _cpuUsage = "0.00% CPU";

    [ObservableProperty]
    private string _ramUsage = "0.00% RAM";

    [ObservableProperty]
    private bool _headless;

    [ObservableProperty]
    private bool _autoRestart;

    [ObservableProperty]
    private string _logText = string.Empty;

    public ObservableCollection<InstanceBoxViewModel> InstanceBoxes { get; } = new();

    private readonly ProxyGetter _proxyGetter;
    
    public MainViewModel(InstanceManager instanceManager, ProxyGetter proxyGetter, ILogger<MainViewModel> logger)
    {
        _instanceManager = instanceManager;
        _proxyGetter = proxyGetter;
        _logger = logger;
        
        ProxiesAvailable = proxyGetter.Count;
        
        Headless = instanceManager.Headless;
        AutoRestart = instanceManager.AutoRestart;

        for (var i = 0; i < 250; i++)
        {
            InstanceBoxes.Add(new InstanceBoxViewModel(i, instanceManager));
        }

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(750)
        };
        _refreshTimer.Tick += (_, _) => RefreshStatus();
        _refreshTimer.Start();

        var consoleWriter = new ConsoleWriter(OnLogReceived);
        Console.SetOut(consoleWriter);

        Task.Run(async () =>
        {
            await Task.Delay(500);
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => RefreshStatus()));
            }
        }).ConfigureAwait(false);
    }

    private void OnLogReceived(string message)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            LogText += message;
            if (LogText.Length > 10000)
            {
                LogText = LogText[^5000..];
            }
        });
    }

    private void RefreshStatus()
    {
        ProxiesAvailable = _proxyGetter.Count;
        
        AliveInstances = _instanceManager.InstancesAliveCount;
        WatchingInstances = _instanceManager.InstancesWatchingCount;

        _cpuRamUpdateCounter++;
        if (_cpuRamUpdateCounter >= 4)
        {
            _cpuRamUpdateCounter = 0;
            _ = Task.Run(() =>
            {
                var cpuPercent = GetCpuUsage();
                var ramPercent = GetRamUsage();
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    CpuUsage = $" {cpuPercent:F2}% CPU";
                    RamUsage = $" {ramPercent:F2}% RAM";
                });
            });
        }

        var overview = _instanceManager.InstancesOverview;
        var index = 0;
        foreach (var (instanceId, status) in overview)
        {
            if (index < InstanceBoxes.Count)
            {
                InstanceBoxes[index].UpdateStatus(status, instanceId);
                index++;
            }
        }

        for (var i = index; i < InstanceBoxes.Count; i++)
        {
            InstanceBoxes[i].UpdateStatus(InstanceStatus.Inactive, null);
        }
    }

    private static float GetCpuUsage()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("select LoadPercentage from Win32_Processor");
            var collection = searcher.Get();
            foreach (ManagementObject obj in collection)
            {
                var value = obj["LoadPercentage"];
                obj.Dispose();
                return Convert.ToSingle(value);
            }
        }
        catch
        {
        }
        return 0;
    }

    private static float GetRamUsage()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            var collection = searcher.Get();
            foreach (ManagementObject obj in collection)
            {
                var total = Convert.ToUInt64(obj["TotalVisibleMemorySize"]);
                var free = Convert.ToUInt64(obj["FreePhysicalMemory"]);
                var used = total - free;
                obj.Dispose();
                return (float)(used * 100.0 / total);
            }
        }
        catch
        {
        }
        return 0;
    }

    [RelayCommand]
    private async Task SpawnOne()
    {
        Console.WriteLine("Spawning one instance. Please wait for alive & watching instances increase.");
        await _instanceManager.SpawnInstanceAsync(ChannelUrl);
    }

    [RelayCommand]
    private async Task SpawnThree()
    {
        Console.WriteLine("Spawning three instances. Please wait for alive & watching instances increase.");
        await _instanceManager.SpawnInstancesAsync(3, ChannelUrl);
    }

    [RelayCommand]
    private void DeleteOne()
    {
        Console.WriteLine("Destroying one instance. Please wait for alive & watching instances decrease.");
        _instanceManager.DeleteLatest();
    }

    [RelayCommand]
    private void DeleteAll()
    {
        Console.WriteLine("Destroying all instances. Please wait for alive & watching instances decrease.");
        _instanceManager.DeleteAllInstances();
    }

    partial void OnHeadlessChanged(bool value)
    {
        _instanceManager.Headless = value;
    }

    partial void OnAutoRestartChanged(bool value)
    {
        _instanceManager.AutoRestart = value;
    }
}

public class ConsoleWriter : System.IO.TextWriter
{
    private readonly Action<string> _onWrite;

    public ConsoleWriter(Action<string> onWrite)
    {
        _onWrite = onWrite;
    }

    public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

    public override void Write(char value)
    {
        _onWrite(value.ToString());
    }

    public override void Write(string? value)
    {
        if (value != null)
        {
            _onWrite(value);
        }
    }

    public override void WriteLine(string? value)
    {
        Write(value + Environment.NewLine);
    }
}

