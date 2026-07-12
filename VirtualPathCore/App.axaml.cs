using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Threading.Tasks;
using VirtualPathCore.Services;
using VirtualPathCore.Views;

namespace VirtualPathCore;

public class App : Application
{
    public static SettingsService SettingsService { get; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startup = new StartupWindow();
            startup.Show();

            _ = InitializeAsync(startup, desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeAsync(StartupWindow startup, IClassicDesktopStyleApplicationLifetime desktop)
    {
        startup.ReportProgress("Loading settings...", 0.05);
        SettingsService.Load();
        LanguageService.Instance.SetLanguage("en-US");
        await YieldUI();

        startup.ReportProgress("Initializing services...", 0.15);
        var sceneService = new SceneService();
        var mainWindow = new MainWindow();
        mainWindow.DataContext = new ViewModels.MainViewModel(sceneService);
        await YieldUI();

        var tcs = new TaskCompletionSource();
        mainWindow.SetRendererReadyCallback(() =>
        {
            tcs.TrySetResult();
        });

        startup.ReportProgress("Starting 3D engine...", 0.30);
        mainWindow.Show();
        await YieldUI();

        if (!mainWindow.IsRendererReady())
            mainWindow.ShowLoadingOverlay();

        await tcs.Task;

        startup.ReportProgress("Building scene...", 0.80);
        await YieldUI();

        startup.ReportProgress("Ready!", 1.0);
        await YieldUI();

        desktop.MainWindow = mainWindow;
        startup.Complete();
    }

    private static async Task YieldUI() =>
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
}
