using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using VirtualPathCore.Views;

namespace VirtualPathCore.ViewModels
{
    public class CreateProjectViewModel : ReactiveObject
    {
        private string projectName = "";
        private string projectDescription = "";
        private string projectPath = "";

        /// <summary>
        /// 获取或设置项目名称。
        /// </summary>
        public string ProjectName
        {
            get => projectName;
            set => this.RaiseAndSetIfChanged(ref projectName, value);
        }

        /// <summary>
        /// 获取或设置项目描述。
        /// </summary>
        public string ProjectDescription
        {
            get => projectDescription;
            set => this.RaiseAndSetIfChanged(ref projectDescription, value);
        }

        /// <summary>
        /// 获取或设置项目路径。
        /// </summary>
        public string ProjectPath
        {
            get => projectPath;
            set => this.RaiseAndSetIfChanged(ref projectPath, value);
        }

        /// <summary>
        /// 创建项目命令。
        /// </summary>
        public ReactiveCommand<Unit, Unit> CreateProjectCommand { get; }

        /// <summary>
        /// 选择项目路径命令。
        /// </summary>
        public ReactiveCommand<Unit, Unit> SelectProjectPathCommand { get; }

        /// <summary>
        /// CreateProjectViewModel 构造函数，初始化命令。
        /// </summary>
        public CreateProjectViewModel()
        {
            CreateProjectCommand = ReactiveCommand.Create(CreateProject, outputScheduler: AvaloniaScheduler.Instance);
            SelectProjectPathCommand = ReactiveCommand.Create(SelectProjectPath, outputScheduler: AvaloniaScheduler.Instance);
        }

        /// <summary>
        /// 创建项目的方法，负责检查输入并生成项目文件和目录。
        /// </summary>
        private async void CreateProject()
        {
            // 检查 ProjectPath 是否为空
            if (string.IsNullOrEmpty(ProjectPath))
            {
                // 显示错误信息提示用户输入项目路径
                var messageBox = MessageBoxManager.GetMessageBoxStandard("Error", "Project path cannot be empty", ButtonEnum.Ok, Icon.Error);
                await messageBox.ShowAsync();
                return;
            }

            // 创建项目目录
            Directory.CreateDirectory(ProjectPath);

            // 检查并设置默认项目名称和描述
            if (string.IsNullOrEmpty(ProjectName))
            {
                ProjectName = "Default Project Name"; // 默认项目名称值
            }
            if (string.IsNullOrEmpty(ProjectDescription))
            {
                ProjectDescription = "Default Project Description"; // 默认项目描述值
            }

            // 在项目目录中创建项目配置文件
            File.WriteAllText(Path.Combine(ProjectPath, "project.config"), $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Project Name=\"{ProjectName}\" Description=\"{ProjectDescription}\" Version=\"1.0\"/>");

            // 这里可以添加更多创建项目的逻辑，比如创建目录结构、文件等

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow?.Close();
                desktop.MainWindow = new MainWindow();
                desktop.MainWindow.Show();
            }
        }

        /// <summary>
        /// 选择项目保存路径的方法，允许用户选择文件夹。
        /// </summary>
        private async void SelectProjectPath()
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null) return;

            var folders = await mainWindow.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Project Save Path",
                SuggestedStartLocation = await mainWindow.StorageProvider.TryGetFolderFromPathAsync(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
            });

            if (folders.Count > 0)
            {
                ProjectPath = folders[0].Path.LocalPath;
            }
        }
    }
}
