using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace EngineeringDiscovery.Desktop;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new MainPage();
    }
}
