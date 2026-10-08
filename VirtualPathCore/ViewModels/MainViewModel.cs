using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
using Silk.NET.Maths;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Helpers;
using VirtualPathCore.Models;
using VirtualPathCore.Services;
using VirtualPathCore.Services.UndoRedo;

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

    public bool HasSelectedObject => SelectedObject != null;
    public bool HasNoSelectedObject => SelectedObject == null;

    // Grid settings
    [ObservableProperty]
    private bool _showGrid = true;

    // Camera settings
    [ObservableProperty]
    private string _cameraInfo = "";

    // Viewport toggles
    [ObservableProperty]
    private bool _showTopView;

    [ObservableProperty]
    private bool _showFrontView;

    [ObservableProperty]
    private bool _showRightView;

    // Gizmo mode
    [ObservableProperty]
    private GizmoMode _gizmoMode = GizmoMode.Translate;

    // Scene tree search
    [ObservableProperty]
    private string _searchFilter = "";

    // Undo/Redo
    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    // Animation
    [ObservableProperty]
    private bool _isAnimating;

    [ObservableProperty]
    private double _animationTime;

    // Light properties for selected light
    [ObservableProperty]
    private float _lightIntensity = 1.0f;

    [ObservableProperty]
    private float _lightR = 1.0f;

    [ObservableProperty]
    private float _lightG = 1.0f;

    [ObservableProperty]
    private float _lightB = 1.0f;

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
        _sceneService.Scene.PropertyChanged += OnScenePropertyChanged;
    }

    partial void OnSelectedObjectChanged(SceneObjectViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedObject));
        OnPropertyChanged(nameof(HasNoSelectedObject));
        UpdateLightProperties();
        _sceneService.SelectedObject = value;
    }

    partial void OnShowGridChanged(bool value)
    {
        _sceneService.Scene.ShowGrid = value;
    }

    partial void OnShowTopViewChanged(bool value) => _sceneService.Scene.ShowTopView = value;
    partial void OnShowFrontViewChanged(bool value) => _sceneService.Scene.ShowFrontView = value;
    partial void OnShowRightViewChanged(bool value) => _sceneService.Scene.ShowRightView = value;

    [RelayCommand]
    private void ToggleTopView() => ShowTopView = !ShowTopView;
    [RelayCommand]
    private void ToggleFrontView() => ShowFrontView = !ShowFrontView;
    [RelayCommand]
    private void ToggleRightView() => ShowRightView = !ShowRightView;

    partial void OnSearchFilterChanged(string value)
    {
        ApplySearchFilter();
    }

    private void OnScenePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Scene.ShowGrid))
            ShowGrid = _sceneService.Scene.ShowGrid;
    }

    private void ApplySearchFilter()
    {
        foreach (var vm in _sceneService.SceneObjects)
            ApplyFilterToItem(vm);
    }

    private void ApplyFilterToItem(SceneObjectViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(SearchFilter))
        {
            vm.IsVisibleInTree = true;
        }
        else
        {
            vm.IsVisibleInTree = vm.Name.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase);
        }
        foreach (var child in vm.Children)
            ApplyFilterToItem(child);
    }

    private void UpdateLightProperties()
    {
        var lightObj = SelectedObject?.SceneObject;
        if (lightObj == null)
        {
            LightIntensity = 1;
            LightR = 1;
            LightG = 1;
            LightB = 1;
            return;
        }
        var light = lightObj.GetCustomProperty<Light>("Light");
        if (light != null)
        {
            LightIntensity = light.Intensity;
            LightR = light.Color.X;
            LightG = light.Color.Y;
            LightB = light.Color.Z;
        }
        else
        {
            LightIntensity = 1;
            LightR = 1;
            LightG = 1;
            LightB = 1;
        }
    }

    public void SetSelectedObject(SceneObjectViewModel? vm)
    {
        SelectedObject = vm;
        if (vm != null)
            _sceneService.SelectedObject = vm;
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
    private void AddCylinder()
    {
        var vm = _sceneService.AddCylinder($"Cylinder {_sceneService.SceneObjects.Count + 1}");
        SetSelectedObject(vm);
        IsModified = true;
    }

    [RelayCommand]
    private void AddCone()
    {
        var vm = _sceneService.AddCone($"Cone {_sceneService.SceneObjects.Count + 1}");
        SetSelectedObject(vm);
        IsModified = true;
    }

    [RelayCommand]
    private void AddTorus()
    {
        var vm = _sceneService.AddTorus($"Torus {_sceneService.SceneObjects.Count + 1}");
        SetSelectedObject(vm);
        IsModified = true;
    }

    [RelayCommand]
    private void AddDirectionalLight()
    {
        var light = new DirectionalLight { Name = $"Directional Light", Direction = new Vector3D<float>(0.5f, -0.8f, 0.6f) };
        _sceneService.Scene.Lighting.DirectionalLight = light;
        var obj = new SceneObject { Name = light.Name };
        obj.SetCustomProperty("Light", light);
        _sceneService.Scene.AddObject(obj);
        var vm = new SceneObjectViewModel(obj);
        _sceneService.SceneObjects.Add(vm);
        _sceneService.SubscribeTo(vm);
        SetSelectedObject(vm);
        IsModified = true;
    }

    [RelayCommand]
    private void AddPointLight()
    {
        var light = new PointLight { Name = $"Point Light", Position = new Vector3D<float>(0, 3, 0) };
        _sceneService.Scene.Lighting.PointLights.Add(light);
        var obj = new SceneObject { Name = light.Name };
        obj.SetCustomProperty("Light", light);
        _sceneService.Scene.AddObject(obj);
        var vm = new SceneObjectViewModel(obj);
        _sceneService.SceneObjects.Add(vm);
        _sceneService.SubscribeTo(vm);
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
    private void DuplicateSelected()
    {
        if (SelectedObject == null) return;
        var vm = _sceneService.DuplicateObject(SelectedObject);
        SetSelectedObject(vm);
        IsModified = true;
    }

    [RelayCommand]
    private async Task SelectAlbedoMap()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null || SelectedObject == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Albedo Map",
            FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga" } } }
        });

        if (files.Count > 0)
        {
            SelectedObject.AlbedoMapPath = files[0].Path.LocalPath;
            IsModified = true;
        }
    }

    [RelayCommand]
    private async Task SelectNormalMap()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null || SelectedObject == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Normal Map",
            FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga" } } }
        });

        if (files.Count > 0)
        {
            SelectedObject.NormalMapPath = files[0].Path.LocalPath;
            IsModified = true;
        }
    }

    [RelayCommand]
    private void FrameSelected()
    {
        if (_sceneService.SelectedObject == null) return;
        var pos = _sceneService.SelectedObject.SceneObject.Transform.Position;
        var camera = _sceneService.Scene.MainCamera;
        if (camera != null)
        {
            camera.LookAt(pos);
            CameraInfo = $"Framed: {_sceneService.SelectedObject.Name}";
        }
    }

    [RelayCommand]
    private void ResetCamera()
    {
        if (_sceneService.Scene.MainCamera is not Camera camera) return;
        camera.Reset();
        CameraInfo = "Camera reset";
    }

    [RelayCommand]
    private void AddKeyframe()
    {
        if (SelectedObject == null) return;
        _sceneService.Animation ??= new AnimationService();
        _sceneService.Animation.AddKeyframe(
            SelectedObject.SceneObject,
            AnimationTime,
            SelectedObject.SceneObject.Transform.Position,
            SelectedObject.SceneObject.Transform.Rotation,
            SelectedObject.SceneObject.Transform.Scale);
    }

    [RelayCommand]
    private void PlayAnimation()
    {
        _sceneService.Animation ??= new AnimationService();
        _sceneService.Animation.Play();
        IsAnimating = true;
    }

    [RelayCommand]
    private void PauseAnimation()
    {
        _sceneService.Animation?.Pause();
        IsAnimating = false;
    }

    [RelayCommand]
    private void StopAnimation()
    {
        _sceneService.Animation?.Stop();
        IsAnimating = false;
        AnimationTime = 0;
    }

    partial void OnGizmoModeChanged(GizmoMode value)
    {
        _sceneService.GizmoMode = value;
    }

    [RelayCommand]
    private void SetGizmoTranslate() => GizmoMode = GizmoMode.Translate;

    [RelayCommand]
    private void SetGizmoRotate() => GizmoMode = GizmoMode.Rotate;

    [RelayCommand]
    private void SetGizmoScale() => GizmoMode = GizmoMode.Scale;

    [RelayCommand]
    private void Undo()
    {
        UndoRedoManager.Instance.Undo();
        CanUndo = UndoRedoManager.Instance.CanUndo;
        CanRedo = UndoRedoManager.Instance.CanRedo;
    }

    [RelayCommand]
    private void Redo()
    {
        UndoRedoManager.Instance.Redo();
        CanUndo = UndoRedoManager.Instance.CanUndo;
        CanRedo = UndoRedoManager.Instance.CanRedo;
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
            try
            {
                var importer = new ModelImporterService();
                var objects = importer.ImportModel(files[0].Path.LocalPath, _sceneService);
                foreach (var vm in objects)
                {
                    _sceneService.SceneObjects.Add(vm);
                    _sceneService.SubscribeTo(vm);
                }
                if (objects.Count > 0)
                    SetSelectedObject(objects[0]);
                IsModified = true;
            }
            catch
            {
                var vm = _sceneService.AddSphere($"Imported {Path.GetFileNameWithoutExtension(files[0].Name)}");
                SetSelectedObject(vm);
                IsModified = true;
            }
        }
    }

    [RelayCommand]
    private async Task ExportModel()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export 3D Model",
            DefaultExtension = "gltf",
            FileTypeChoices = new[] { new FilePickerFileType("GLTF") { Patterns = new[] { "*.gltf" } } }
        });

        if (file != null)
        {
            try
            {
                var exporter = new ModelExporterService();
                exporter.ExportScene(file.Path.LocalPath, _sceneService.SceneObjects.ToList());
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] Exported scene to: {file.Path.LocalPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] Failed to export model: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var vm in _sceneService.SceneObjects)
            SetExpanded(vm, true);
    }

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var vm in _sceneService.SceneObjects)
            SetExpanded(vm, false);
    }

    private void SetExpanded(SceneObjectViewModel vm, bool expanded)
    {
        vm.IsExpanded = expanded;
        foreach (var child in vm.Children)
            SetExpanded(child, expanded);
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Failed to load project: {ex.Message}");
        }
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

            foreach (var vm in _sceneService.SceneObjects)
            {
                if (vm.SceneObject.Material != null)
                {
                    var mat = vm.SceneObject.Material;
                    data.Materials.Add(new SerializedMaterial
                    {
                        Name = mat.Name,
                        Albedo = new[] { mat.Albedo.X, mat.Albedo.Y, mat.Albedo.Z, mat.Albedo.W },
                        Metallic = mat.Metallic,
                        Roughness = mat.Roughness,
                        Emissive = new[] { mat.Emissive.X, mat.Emissive.Y, mat.Emissive.Z },
                        EmissiveIntensity = mat.EmissiveIntensity,
                        AlbedoMapPath = mat.GetCustomProperty<string>("AlbedoMapPath"),
                        NormalMapPath = mat.GetCustomProperty<string>("NormalMapPath")
                    });
                }
            }

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            IsModified = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Failed to save project: {ex.Message}");
        }
    }

    private static Window? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}

public enum GizmoMode
{
    Translate,
    Rotate,
    Scale
}
