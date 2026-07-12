using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using VirtualPathCore.Services;

namespace VirtualPathCore.Views.Components;

public partial class SettingsWindow : Window
{
    private bool _isLoading = true;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadCurrentSettings();
        _isLoading = false;
    }

    private void LoadCurrentSettings()
    {
        var settings = App.SettingsService;
        if (settings == null) return;

        string lang = settings.GetLanguage();
        LanguageComboBox.SelectedIndex = lang == "zh-CN" ? 0 : 1;

        var theme = settings.GetTheme();
        ThemeComboBox.SelectedIndex = theme == ThemeVariant.Light ? 1 : theme == ThemeVariant.Dark ? 2 : 0;
    }

    private void LanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
        {
            App.SettingsService?.SetLanguage(langCode);
        }
    }

    private void ThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        ThemeVariant theme = ThemeComboBox.SelectedIndex switch
        {
            1 => ThemeVariant.Light,
            2 => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        App.SettingsService?.SetTheme(theme);

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = theme;
        }
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
