using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WinFrame.Services;
using WinFrame.ViewModels;

namespace WinFrame;

public partial class App : Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Disposes all IDisposable singletons: GitHubAuthService, CopilotService,
        // and MainViewModel (which cancels in-flight async operations).
        Services.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<GitHubAuthService>();
        services.AddSingleton<ThreadManager>();
        services.AddSingleton<SchedulerService>();
        services.AddSingleton<CopilotService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
    }
}
