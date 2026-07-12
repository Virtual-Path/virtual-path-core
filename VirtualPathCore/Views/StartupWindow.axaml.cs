using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace VirtualPathCore;

public partial class StartupWindow : Window
{
    public StartupWindow()
    {
        InitializeComponent();

        LogoImage.RenderTransform = new ScaleTransform(0.95, 0.95);
        LogoImage.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
    }

    public void ReportProgress(string status, double progress, bool isIndeterminate = false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = status;
            if (isIndeterminate)
            {
                ProgressBar.IsIndeterminate = true;
            }
            else
            {
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = Math.Clamp(progress, 0, 1);
            }

            double scale = 0.95 + 0.05 * progress;
            if (LogoImage.RenderTransform is ScaleTransform st)
            {
                st.ScaleX = scale;
                st.ScaleY = scale;
            }
        }, DispatcherPriority.Normal);
    }

    public void Complete()
    {
        Close();
    }
}
