using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace VirtualPathCore.Services;

public class LanguageService : INotifyPropertyChanged
{
    private static LanguageService? _instance;
    public static LanguageService Instance => _instance ??= new LanguageService();

    private string _currentLanguage = "zh-CN";
    
    private readonly Dictionary<string, Dictionary<string, string>> _languages = new()
    {
        ["zh-CN"] = new Dictionary<string, string>
        {
            // Menu
            ["Menu_File"] = "文件",
            ["Menu_NewProject"] = "新建项目",
            ["Menu_OpenProject"] = "打开项目",
            ["Menu_SaveProject"] = "保存项目",
            ["Menu_Exit"] = "退出",
            ["Menu_Edit"] = "编辑",
            ["Menu_Undo"] = "撤销",
            ["Menu_Redo"] = "重做",
            ["Menu_Cut"] = "剪切",
            ["Menu_Copy"] = "复制",
            ["Menu_Paste"] = "粘贴",
            ["Menu_Window"] = "窗口",
            ["Menu_Layout"] = "布局",
            ["Menu_ResetLayout"] = "重置布局",
            ["Menu_ToggleSidebar"] = "切换侧边栏",
            ["Menu_ToggleDock"] = "切换侧边栏停靠",
            ["Menu_Help"] = "帮助",
            ["Menu_Documentation"] = "文档",
            ["Menu_About"] = "关于",
            ["Menu_Tools"] = "工具",
            ["Menu_Settings"] = "设置",

            // Startup
            ["App_Title"] = "Virtual Path 3D 引擎",
            ["App_Version"] = "版本",
            ["App_VersionNumber"] = "0.0.1",

            // Main
            ["Main_Renderer1"] = "渲染器 1 - Blender C#",
            ["Main_Renderer2"] = "渲染器 2 - Blender C++",
            ["Main_Execute"] = "执行操作",

            // Parameters
            ["Param_Panel"] = "参数控制面板",
            ["Param_MSAA"] = "MSAA 采样数",
            ["Param_Rotation"] = "旋转角度",
            ["Param_Scale"] = "缩放",
            ["Param_PosX"] = "位置 X",
            ["Param_PosY"] = "位置 Y",
            ["Param_PosZ"] = "位置 Z",

            // Create Project
            ["Create_Title"] = "新建项目",
            ["Create_Name"] = "项目名称",
            ["Create_Desc"] = "项目描述",
            ["Create_Path"] = "项目路径",
            ["Create_SelectPath"] = "选择路径",
            ["Create_Button"] = "创建",

            // Settings
            ["Settings_Title"] = "设置",
            ["Settings_Language"] = "语言",
            ["Settings_Theme"] = "主题",
            ["Settings_System"] = "跟随系统",
            ["Settings_Light"] = "浅色",
            ["Settings_Dark"] = "深色",
            ["Settings_Close"] = "关闭"
        },
        ["en-US"] = new Dictionary<string, string>
        {
            // Menu
            ["Menu_File"] = "File",
            ["Menu_NewProject"] = "New Project",
            ["Menu_OpenProject"] = "Open Project",
            ["Menu_SaveProject"] = "Save Project",
            ["Menu_Exit"] = "Exit",
            ["Menu_Edit"] = "Edit",
            ["Menu_Undo"] = "Undo",
            ["Menu_Redo"] = "Redo",
            ["Menu_Cut"] = "Cut",
            ["Menu_Copy"] = "Copy",
            ["Menu_Paste"] = "Paste",
            ["Menu_Window"] = "Window",
            ["Menu_Layout"] = "Layout",
            ["Menu_ResetLayout"] = "Reset Layout",
            ["Menu_ToggleSidebar"] = "Toggle Sidebar",
            ["Menu_ToggleDock"] = "Toggle Sidebar Dock",
            ["Menu_Help"] = "Help",
            ["Menu_Documentation"] = "Documentation",
            ["Menu_About"] = "About",
            ["Menu_Tools"] = "Tools",
            ["Menu_Settings"] = "Settings",

            // Startup
            ["App_Title"] = "Virtual Path 3D Engine",
            ["App_Version"] = "Version",
            ["App_VersionNumber"] = "0.0.1",

            // Main
            ["Main_Renderer1"] = "Renderer 1 - Blender C#",
            ["Main_Renderer2"] = "Renderer 2 - Blender C++",
            ["Main_Execute"] = "Execute Action",

            // Parameters
            ["Param_Panel"] = "Parameter Control Panel",
            ["Param_MSAA"] = "MSAA Samples",
            ["Param_Rotation"] = "Rotation Angle",
            ["Param_Scale"] = "Scale",
            ["Param_PosX"] = "Position X",
            ["Param_PosY"] = "Position Y",
            ["Param_PosZ"] = "Position Z",

            // Create Project
            ["Create_Title"] = "New Project",
            ["Create_Name"] = "Project Name",
            ["Create_Desc"] = "Project Description",
            ["Create_Path"] = "Project Path",
            ["Create_SelectPath"] = "Select Path",
            ["Create_Button"] = "Create",

            // Settings
            ["Settings_Title"] = "Settings",
            ["Settings_Language"] = "Language",
            ["Settings_Theme"] = "Theme",
            ["Settings_System"] = "System",
            ["Settings_Light"] = "Light",
            ["Settings_Dark"] = "Dark",
            ["Settings_Close"] = "Close"
        }
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? LanguageChanged;

    public string CurrentLanguage => _currentLanguage;

    public string this[string key] => Get(key);

    public void SetLanguage(string languageCode)
    {
        if (_currentLanguage == languageCode) return;

        _currentLanguage = languageCode;
        LoadJsonOverrides(languageCode);
        OnPropertyChanged(nameof(CurrentLanguage));
        LanguageChanged?.Invoke();

        if (_languages.TryGetValue(languageCode, out var lang))
        {
            foreach (var key in lang.Keys)
            {
                OnPropertyChanged($"L_{key}");
            }
        }
    }

    private void LoadJsonOverrides(string languageCode)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "Languages", $"{languageCode}.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "VirtualPathCore", "Resources", "Languages", $"{languageCode}.json");
                if (!File.Exists(path)) return;
            }

            string json = File.ReadAllText(path);
            var jsonData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (jsonData == null) return;

            if (!_languages.ContainsKey(languageCode))
                _languages[languageCode] = new Dictionary<string, string>();

            var lang = _languages[languageCode];
            FlattenJson(jsonData, "", lang);
        }
        catch { }
    }

    private static void FlattenJson(Dictionary<string, JsonElement> data, string prefix, Dictionary<string, string> output)
    {
        foreach (var kvp in data)
        {
            string key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}_{kvp.Key}";
            if (kvp.Value.ValueKind == JsonValueKind.Object && kvp.Value.EnumerateObject().Any())
            {
                var nested = new Dictionary<string, JsonElement>();
                foreach (var prop in kvp.Value.EnumerateObject())
                    nested[prop.Name] = prop.Value;
                FlattenJson(nested, key, output);
            }
            else if (kvp.Value.ValueKind == JsonValueKind.String)
            {
                output[key] = kvp.Value.GetString() ?? key;
            }
        }
    }

    public string Get(string key)
    {
        if (_languages.TryGetValue(_currentLanguage, out var lang) && lang.TryGetValue(key, out var value))
        {
            return value;
        }
        return key;
    }

    public string L_Menu_File => Get("Menu_File");
    public string L_Menu_NewProject => Get("Menu_NewProject");
    public string L_Menu_OpenProject => Get("Menu_OpenProject");
    public string L_Menu_SaveProject => Get("Menu_SaveProject");
    public string L_Menu_Exit => Get("Menu_Exit");
    public string L_Menu_Edit => Get("Menu_Edit");
    public string L_Menu_Undo => Get("Menu_Undo");
    public string L_Menu_Redo => Get("Menu_Redo");
    public string L_Menu_Cut => Get("Menu_Cut");
    public string L_Menu_Copy => Get("Menu_Copy");
    public string L_Menu_Paste => Get("Menu_Paste");
    public string L_Menu_Window => Get("Menu_Window");
    public string L_Menu_Layout => Get("Menu_Layout");
    public string L_Menu_ResetLayout => Get("Menu_ResetLayout");
    public string L_Menu_ToggleSidebar => Get("Menu_ToggleSidebar");
    public string L_Menu_ToggleDock => Get("Menu_ToggleDock");
    public string L_Menu_Help => Get("Menu_Help");
    public string L_Menu_Documentation => Get("Menu_Documentation");
    public string L_Menu_About => Get("Menu_About");
    public string L_Menu_Tools => Get("Menu_Tools");
    public string L_Menu_Settings => Get("Menu_Settings");

    public string L_App_Title => Get("App_Title");
    public string L_App_Version => Get("App_Version");
    public string L_App_VersionNumber => Get("App_VersionNumber");

    public string L_Main_Renderer1 => Get("Main_Renderer1");
    public string L_Main_Renderer2 => Get("Main_Renderer2");
    public string L_Main_Execute => Get("Main_Execute");

    public string L_Param_Panel => Get("Param_Panel");
    public string L_Param_MSAA => Get("Param_MSAA");
    public string L_Param_Rotation => Get("Param_Rotation");
    public string L_Param_Scale => Get("Param_Scale");
    public string L_Param_PosX => Get("Param_PosX");
    public string L_Param_PosY => Get("Param_PosY");
    public string L_Param_PosZ => Get("Param_PosZ");

    public string L_Create_Title => Get("Create_Title");
    public string L_Create_Name => Get("Create_Name");
    public string L_Create_Desc => Get("Create_Desc");
    public string L_Create_Path => Get("Create_Path");
    public string L_Create_SelectPath => Get("Create_SelectPath");
    public string L_Create_Button => Get("Create_Button");

    public string L_Settings_Title => Get("Settings_Title");
    public string L_Settings_Language => Get("Settings_Language");
    public string L_Settings_Theme => Get("Settings_Theme");
    public string L_Settings_System => Get("Settings_System");
    public string L_Settings_Light => Get("Settings_Light");
    public string L_Settings_Dark => Get("Settings_Dark");
    public string L_Settings_Close => Get("Settings_Close");

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public static class Lang
{
    public static LanguageService Instance => LanguageService.Instance;
}