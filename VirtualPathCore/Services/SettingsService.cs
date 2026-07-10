using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;

namespace VirtualPathCore.Services;

public class SettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VirtualPathCore");

    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    public SettingsService()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                System.Diagnostics.Debug.WriteLine($"Settings loaded from: {SettingsFile}");
                System.Diagnostics.Debug.WriteLine($"Loaded language: {_settings.Language}");
            }
            else
            {
                _settings = new AppSettings();
                System.Diagnostics.Debug.WriteLine("No settings file found, using defaults");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings load error: {ex.Message}");
            _settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(SettingsFile, json);
            System.Diagnostics.Debug.WriteLine($"Settings saved to: {SettingsFile}");
            System.Diagnostics.Debug.WriteLine($"Settings content: {json}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings save error: {ex.Message}");
        }
    }

    public void SetLanguage(string languageCode)
    {
        _settings.Language = languageCode;
        Save();
    }

    public string GetLanguage() => _settings.Language;

    public void SetTheme(ThemeVariant theme)
    {
        _settings.Theme = theme.ToString();
        Save();
    }

    public ThemeVariant GetTheme()
    {
        return _settings.Theme?.ToLower() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    public int GetMsaaSamples() => _settings.MsaaSamples;
    public void SetMsaaSamples(int samples)
    {
        _settings.MsaaSamples = samples;
        Save();
    }

    public void AddRecentProject(string path)
    {
        _settings.RecentProjects.Remove(path);
        _settings.RecentProjects.Insert(0, path);
        if (_settings.RecentProjects.Count > 10)
            _settings.RecentProjects.RemoveRange(10, _settings.RecentProjects.Count - 10);
        _settings.LastProjectPath = path;
        Save();
    }

    public string[] GetRecentProjects() => _settings.RecentProjects.ToArray();
}

public class AppSettings
{
    public string Language { get; set; } = "zh-CN";
    public string? Theme { get; set; } = "Default";
    public string? LastProjectPath { get; set; }
    public List<string> RecentProjects { get; set; } = new();
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
    public bool WindowMaximized { get; set; } = false;
    public int MsaaSamples { get; set; } = 4;
}