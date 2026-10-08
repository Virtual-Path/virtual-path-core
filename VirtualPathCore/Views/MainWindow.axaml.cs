using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.Views
{
    public partial class MainWindow : Window
    {
        private bool isSidebarOnLeft = true;
        private Action? _rendererReadyCallback;

        public MainWindow()
        {
            InitializeComponent();

            KeyDown += OnKeyDown;

            MainViewControl.RendererReady += NotifyRendererReady;
            if (MainViewControl.IsRendererReady)
                NotifyRendererReady();
        }

        public void SetRendererReadyCallback(Action callback)
        {
            _rendererReadyCallback = callback;
        }

        public void NotifyRendererReady()
        {
            LoadingOverlay.IsVisible = false;
            _rendererReadyCallback?.Invoke();
        }

        public bool IsRendererReady() => MainViewControl.IsRendererReady;

        public void ShowLoadingOverlay()
        {
            LoadingOverlay.IsVisible = true;
        }

        private void SceneObjectList_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.Source is Control control && control.DataContext is SceneObjectViewModel vm)
            {
                var dragData = new Avalonia.Input.DataObject();
                dragData.Set("SceneObjectViewModel", vm);
                Avalonia.Input.DragDrop.DoDragDrop(e, dragData, Avalonia.Input.DragDropEffects.Move);
                e.Handled = true;
            }
        }

        private void SceneObjectList_DragOver(object? sender, Avalonia.Input.DragEventArgs e)
        {
            e.DragEffects = Avalonia.Input.DragDropEffects.Move;
            e.Handled = true;
        }

        private void SceneObjectList_Drop(object? sender, Avalonia.Input.DragEventArgs e)
        {
            var droppedVm = e.Data.Get("SceneObjectViewModel") as SceneObjectViewModel;
            if (droppedVm != null)
            {
                var target = SceneObjectList.SelectedItem as SceneObjectViewModel;
                if (target != null && droppedVm != target)
                {
                    var parentVm = FindParentViewModel(droppedVm);
                    if (parentVm != null)
                        parentVm.RemoveChildViewModel(droppedVm);

                    target.AddChildViewModel(droppedVm);
                    IsModified = true;
                }
            }
            e.Handled = true;
        }

        private SceneObjectViewModel? FindParentViewModel(SceneObjectViewModel vm)
        {
            if (DataContext is MainViewModel mainVm)
            {
                foreach (var child in mainVm.SceneObjects)
                {
                    if (FindParentRecursive(child, vm) is SceneObjectViewModel parent)
                        return parent;
                }
            }
            return null;
        }

        private SceneObjectViewModel? FindParentRecursive(SceneObjectViewModel parent, SceneObjectViewModel target)
        {
            foreach (var child in parent.Children)
            {
                if (child == target)
                    return parent;
                var result = FindParentRecursive(child, target);
                if (result != null)
                    return result;
            }
            return null;
        }

        private bool IsModified
        {
            get => DataContext is MainViewModel vm && vm.IsModified;
            set
            {
                if (DataContext is MainViewModel vm)
                    vm.IsModified = value;
            }
        }

        private void OnObjectNameKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox tb && tb.DataContext is SceneObjectViewModel vm)
            {
                vm.Name = tb.Text ?? string.Empty;
                e.Handled = true;
            }
        }

        private void OnObjectNameLostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is SceneObjectViewModel vm)
            {
                vm.Name = tb.Text ?? string.Empty;
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            if (e.KeyModifiers == KeyModifiers.Control)
            {
                switch (e.Key)
                {
                    case Key.Z: vm.UndoCommand.Execute(null); e.Handled = true; break;
                    case Key.Y: vm.RedoCommand.Execute(null); e.Handled = true; break;
                    case Key.N: vm.NewProjectCommand.Execute(null); e.Handled = true; break;
                    case Key.O: vm.OpenProjectCommand.Execute(null); e.Handled = true; break;
                    case Key.S: vm.SaveProjectCommand.Execute(null); e.Handled = true; break;
                }
            }
            else if (e.KeyModifiers == KeyModifiers.None)
            {
                var drawingService = MainViewControl.DrawingService;
                switch (e.Key)
                {
                    case Key.F: vm.FrameSelectedCommand.Execute(null); e.Handled = true; break;
                    case Key.Delete: vm.DeleteSelectedCommand.Execute(null); e.Handled = true; break;
                    case Key.W:
                        drawingService?.Orbit(0, -2); e.Handled = true; break;
                    case Key.S:
                        drawingService?.Orbit(0, 2); e.Handled = true; break;
                    case Key.A:
                        drawingService?.Orbit(-2, 0); e.Handled = true; break;
                    case Key.D:
                        drawingService?.Orbit(2, 0); e.Handled = true; break;
                    case Key.Q:
                        drawingService?.Pan(-2, 0); e.Handled = true; break;
                    case Key.E:
                        drawingService?.Pan(2, 0); e.Handled = true; break;
                    case Key.Add:
                    case Key.OemPlus:
                        drawingService?.Zoom(10); e.Handled = true; break;
                    case Key.Subtract:
                    case Key.OemMinus:
                        drawingService?.Zoom(-10); e.Handled = true; break;
                }
            }
            else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
            {
                switch (e.Key)
                {
                    case Key.S: vm.SaveProjectAsCommand.Execute(null); e.Handled = true; break;
                }
            }
        }

        private void ToggleSidebar(object sender, RoutedEventArgs e)
        {
            SidebarBorder.IsVisible = !SidebarBorder.IsVisible;
            if (SidebarBorder.IsVisible)
            {
                Grid.SetColumnSpan(ContentBorder, 1);
                Grid.SetColumn(ContentBorder, 1);
            }
            else
            {
                Grid.SetColumnSpan(ContentBorder, 2);
                Grid.SetColumn(ContentBorder, 0);
            }
        }

        private void ToggleDock(object sender, RoutedEventArgs e)
        {
            if (isSidebarOnLeft)
            {
                Grid.SetColumn(SidebarBorder, 2);
                Grid.SetColumn(PropertiesBorder, 0);
            }
            else
            {
                Grid.SetColumn(SidebarBorder, 0);
                Grid.SetColumn(PropertiesBorder, 2);
            }
            isSidebarOnLeft = !isSidebarOnLeft;
        }

    }
}
