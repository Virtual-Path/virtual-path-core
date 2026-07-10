using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using VirtualPathCore.Views;

namespace VirtualPathCore
{
    public partial class StartupWindow : Window
    {
        public StartupWindow()
        {
            InitializeComponent();

            this.Opened += OnStartupOpened;

            this.SizeChanged += OnSizeChanged;

            AnimateLogoAndText();
        }

        private void AnimateLogoAndText()
        {
            var duration = TimeSpan.FromSeconds(1.5);
            var startScale = 0.9;
            var endScale = 1.0;

            var startTime = DateTime.Now;

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };

            double Ease(double t) =>
                t < 0.5
                ? 4 * t * t * t
                : 1 - Math.Pow(-2 * t + 2, 3) / 2;

            WelcomeText.Opacity = 0;
            VersionText.Opacity = 0;

            WelcomeText.RenderTransform = new TranslateTransform(0, 20);
            VersionText.RenderTransform = new TranslateTransform(0, 20);

            timer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - startTime;
                var t = Math.Min(1.0, elapsed.TotalMilliseconds / duration.TotalMilliseconds);

                var easedT = Ease(t);

                if (LogoImage.RenderTransform is not ScaleTransform scaleTransform)
                {
                    scaleTransform = new ScaleTransform(1, 1);
                    LogoImage.RenderTransform = scaleTransform;
                }
                scaleTransform.ScaleX = startScale + (endScale - startScale) * easedT;
                scaleTransform.ScaleY = startScale + (endScale - startScale) * easedT;

                WelcomeText.Opacity = easedT;
                VersionText.Opacity = easedT;

                if (WelcomeText.RenderTransform is TranslateTransform welcomeTT)
                    welcomeTT.Y = 20 * (1 - easedT);

                if (VersionText.RenderTransform is TranslateTransform versionTT)
                    versionTT.Y = 20 * (1 - easedT);

                if (t >= 1.0)
                    timer.Stop();
            };

            timer.Start();
        }

        private async void OnStartupOpened(object? sender, EventArgs e)
        {
            await Task.Delay(1500);

            var mainWindow = new MainWindow();
            mainWindow.Show();

            this.Close();
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            var height = e.NewSize.Height;

            WelcomeText.FontSize = height * 0.05;
            VersionText.FontSize = height * 0.02;

            LogoImage.Width = e.NewSize.Width * 0.375;
            LogoImage.Height = e.NewSize.Height * 0.333;
        }
    }
}
