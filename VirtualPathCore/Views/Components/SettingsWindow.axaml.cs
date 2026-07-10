using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using VirtualPathCore.Services;

namespace VirtualPathCore.Views.Components;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var settings = App.SettingsService;
        if (settings == null) return;

        string lang = settings.GetLanguage();
        if (lang == "zh-CN") LanguageComboBox.SelectedIndex = 0;
        else LanguageComboBox.SelectedIndex = 1;

        var theme = settings.GetTheme();
        if (theme == ThemeVariant.Light) ThemeComboBox.SelectedIndex = 1;
        else if (theme == ThemeVariant.Dark) ThemeComboBox.SelectedIndex = 2;
        else ThemeComboBox.SelectedIndex = 0;
    }

    private void LanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
        {
            App.SettingsService?.SetLanguage(langCode);
            System.Diagnostics.Debug.WriteLine($"Language changed to: {langCode}");
        }
    }

    private void ThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
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

        System.Diagnostics.Debug.WriteLine($"Theme changed to: {theme}");
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}