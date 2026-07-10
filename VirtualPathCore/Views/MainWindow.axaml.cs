using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VirtualPathCore.Services;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.Views
{
    public partial class MainWindow : Window
    {
        private bool isSidebarOnLeft = true;

        public MainWindow()
        {
            InitializeComponent();
            WindowState = WindowState.Maximized;

            var sceneService = new SceneService(null!);
            DataContext = new MainViewModel(sceneService);

            KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            if (e.KeyModifiers == KeyModifiers.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        vm.UndoCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Y:
                        vm.RedoCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.N:
                        vm.NewProjectCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.O:
                        vm.OpenProjectCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.S:
                        vm.SaveProjectCommand.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
            else if (e.KeyModifiers == KeyModifiers.None)
            {
                switch (e.Key)
                {
                    case Key.F:
                        vm.FrameSelectedCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Delete:
                        vm.DeleteSelectedCommand.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
            else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
            {
                switch (e.Key)
                {
                    case Key.S:
                        vm.SaveProjectAsCommand.Execute(null);
                        e.Handled = true;
                        break;
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
                Grid.SetColumn(AIChatBorder, 0);
            }
            else
            {
                Grid.SetColumn(SidebarBorder, 0);
                Grid.SetColumn(AIChatBorder, 2);
            }
            isSidebarOnLeft = !isSidebarOnLeft;
        }
    }
}
