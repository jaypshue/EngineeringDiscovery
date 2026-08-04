using System.Windows;

namespace EngineeringDiscovery.Wpf.Services;

public class WindowManager : IWindowManager
{
    public void ShowWindow(object window)
    {
        if (window is Window w) w.Show();
    }
}
