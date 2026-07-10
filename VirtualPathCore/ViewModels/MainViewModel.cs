using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VirtualPathCore.Models;
using VirtualPathCore.Services;

namespace VirtualPathCore.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly SceneService _sceneService = null!;
    private string _projectFilePath = "";
    private bool _isModified;

    public SceneService SceneService => _sceneService;

    public ObservableCollection<SceneObjectViewModel> SceneObjects => _sceneService.SceneObjects;

    [ObservableProperty]
    private SceneObjectViewModel? _selectedObject;

    [ObservableProperty]
    private int _msaaSamples = App.SettingsService?.GetMsaaSamples() ?? 4;

    public bool IsModified
    {
        get => _isModified;
        set => SetProperty(ref _isModified, value);
    }

    public string Title => string.IsNullOrEmpty(_projectFilePath)
        ? "Virtual Path 3D Engine - Untitled"
        : $"Virtual Path 3D Engine - {Path.GetFileName(_projectFilePath)}";

    public MainViewModel() { }

    public MainViewModel(SceneService sceneService)
    {
        _sceneService = sceneService;
    }

    public void SetSelectedObject(SceneObjectViewModel? vm)
    {
        SelectedObject = vm;
        OnPropertyChanged(nameof(SelectedObject));
    }

    [RelayCommand]
    private void AddCube()
    {
        var vm = _sceneService.AddCube($"Cube {_sceneService.SceneObjects.Count + 1}");
        SetSelectedObject(vm);
        IsModified = true;
    }

    [RelayCommand]
    private void AddSphere()
    {
        var vm = _sceneService.AddSphere($"Sphere {_sceneService.SceneObjects.Count + 1}");
        SetSelectedObject(vm);
        IsModified = true;
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedObject == null) return;
        _sceneService.RemoveObject(SelectedObject);
        SetSelectedObject(null);
        IsModified = true;
    }

    [RelayCommand]
    private void NewProject()
    {
        _sceneService.Clear();
        _projectFilePath = "";
        IsModified = false;
        OnPropertyChanged(nameof(Title));
    }

    [RelayCommand]
    private async Task OpenProject()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Project",
            FileTypeFilter = new[] { new FilePickerFileType("Virtual Path Project") { Patterns = new[] { "*.vpproj" } } }
        });

        if (files.Count > 0)
        {
            LoadProject(files[0].Path.LocalPath);
        }
    }

    [RelayCommand]
    private void SaveProject()
    {
        if (string.IsNullOrEmpty(_projectFilePath))
        {
            SaveProjectAsCommand.Execute(null);
            return;
        }
        WriteProjectFile(_projectFilePath);
    }

    [RelayCommand]
    private async Task SaveProjectAs()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Project As",
            DefaultExtension = "vpproj",
            FileTypeChoices = new[] { new FilePickerFileType("Virtual Path Project") { Patterns = new[] { "*.vpproj" } } }
        });

        if (file != null)
        {
            _projectFilePath = file.Path.LocalPath;
            WriteProjectFile(_projectFilePath);
            OnPropertyChanged(nameof(Title));
        }
    }

    [RelayCommand]
    private async Task ImportModel()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import 3D Model",
            FileTypeFilter = new[] { new FilePickerFileType("3D Models") { Patterns = new[] { "*.glb", "*.gltf" } } }
        });

        if (files.Count > 0)
        {
            var vm = _sceneService.AddSphere($"Imported {Path.GetFileNameWithoutExtension(files[0].Name)}");
            SetSelectedObject(vm);
            IsModified = true;
        }
    }

    [RelayCommand]
    private void ClearScene()
    {
        _sceneService.Clear();
        SetSelectedObject(null);
        IsModified = true;
    }

    [RelayCommand]
    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void LoadProject(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<ProjectData>(json);
            if (data == null) return;

            _sceneService.Clear();
            _projectFilePath = path;
            IsModified = false;
            OnPropertyChanged(nameof(Title));
        }
        catch { }
    }

    private void WriteProjectFile(string path)
    {
        try
        {
            var data = new ProjectData
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Path = path,
                LastModified = DateTime.Now
            };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            IsModified = false;
        }
        catch { }
    }

    private static Window? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
