using System.Windows;

namespace EngineeringDiscovery.Wpf.Baseline
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // show welcome view by default
            HostContent.Content = new Views.WelcomeView();
        }
    }
}
