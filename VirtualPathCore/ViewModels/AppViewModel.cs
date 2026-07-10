using VirtualPathCore.Services;

namespace VirtualPathCore.ViewModels;

public class AppViewModel
{
    public LanguageService Language => LanguageService.Instance;
    public SettingsService Settings => App.SettingsService!;
}
