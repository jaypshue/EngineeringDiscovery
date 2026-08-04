using System.Windows;
using EngineeringDiscovery.Wpf.ViewModels;

namespace EngineeringDiscovery.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
