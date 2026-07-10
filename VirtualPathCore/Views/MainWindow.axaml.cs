using Avalonia.Controls;
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
