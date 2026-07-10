using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VirtualPathCore.Services;

namespace VirtualPathCore
{
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
                desktop.MainWindow = new StartupWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
