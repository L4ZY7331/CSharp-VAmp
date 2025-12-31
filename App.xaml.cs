using System.IO;
using System.Windows;
using CSharpVAmp.Services;
using CSharpVAmp.ViewModels;
using CSharpVAmp.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CSharpVAmp;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();

        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
        var proxyFilePath = Path.Combine(exeDirectory, "proxy", appSettings.ProxyFileName);
        
        services.AddSingleton<ProxyGetter>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ProxyGetter>>();
            var getter = new ProxyGetter(proxyFilePath, logger);
            logger.LogInformation("ProxyGetter created with {Count} proxies", getter.Count);
            return getter;
        });

        services.AddSingleton<ScreenManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ScreenManager>>();
            return new ScreenManager(appSettings.WindowWidth, appSettings.WindowHeight, logger);
        });

        services.AddSingleton<RestartChecker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RestartChecker>>();
            return new RestartChecker(appSettings.RestartIntervalSeconds, logger);
        });

        services.AddSingleton<InstanceManager>(sp =>
        {
            var proxyGetter = sp.GetRequiredService<ProxyGetter>();
            var screenManager = sp.GetRequiredService<ScreenManager>();
            var restartChecker = sp.GetRequiredService<RestartChecker>();
            var logger = sp.GetRequiredService<ILogger<InstanceManager>>();

            var manager = new InstanceManager(
                proxyGetter,
                screenManager,
                restartChecker,
                appSettings.Headless,
                appSettings.AutoRestart,
                appSettings.SpawnIntervalSeconds,
                logger);

            restartChecker.SetManager(manager);

            return manager;
        });

        services.AddTransient<MainViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = new MainWindow();
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.DataContext = mainViewModel;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

public class AppSettings
{
    public int SpawnThreadCount { get; set; } = 3;
    public int DeleteThreadCount { get; set; } = 10;
    public string ProxyFileName { get; set; } = "proxy_list.txt";
    public bool Headless { get; set; } = true;
    public bool AutoRestart { get; set; } = false;
    public int SpawnIntervalSeconds { get; set; } = 2;
    public int WindowWidth { get; set; } = 500;
    public int WindowHeight { get; set; } = 300;
    public int RestartIntervalSeconds { get; set; } = 1200;
}

