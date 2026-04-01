using System.Windows;
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

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<GitHubAuthService>();
        services.AddSingleton<ThreadManager>();
        services.AddSingleton<SchedulerService>();
        services.AddSingleton<CopilotService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
    }
}
