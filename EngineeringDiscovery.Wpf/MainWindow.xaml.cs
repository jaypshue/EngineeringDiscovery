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

            // Instrument center Border child for runtime composition investigation (ED-307)
            this.Loaded += MainWindow_Loaded;
            this.ContentRendered += MainWindow_ContentRendered;
            this.Dispatcher.BeginInvoke(new System.Action(() => LogCenterBorderState("ApplicationIdle (scheduled)")), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            // Show WelcomeView in HostContent on startup
            this.Loaded += (s, e) =>
            {
                try
                {
                    HostContent.Content = new Views.WelcomeView();
                }
                catch { }
            };
        }

        // Developer navigation menu handlers (temporary)
        private void NavigateToWelcome_Click(object sender, RoutedEventArgs e)
        {
            HostContent.Content = new Views.WelcomeView();
        }

        private void NavigateToCorporate_Click(object sender, RoutedEventArgs e)
        {
            HostContent.Content = new Views.EngineeringWorkspace();
        }

        private void NavigateToFreeRange_Click(object sender, RoutedEventArgs e)
        {
            HostContent.Content = new Views.ProductDiscoveryPlaceholder();
        }

        private void NavigateToKnowledgeGraph_Click(object sender, RoutedEventArgs e)
        {
            HostContent.Content = new Views.KnowledgeGraphPlaceholder();
        }

        private void NavigateToInspector_Click(object sender, RoutedEventArgs e)
        {
            HostContent.Content = new Views.InspectorPlaceholder();
        }

        private object? _initialChildRef;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LogCenterBorderState("Loaded");
        }

        private void MainWindow_ContentRendered(object? sender, System.EventArgs e)
        {
            LogCenterBorderState("ContentRendered");
        }

        private void LogCenterBorderState(string phase)
        {
            try
            {
                var child = (this.FindName("CenterBorder") as System.Windows.Controls.Border)?.Child;
                var childType = child?.GetType().FullName ?? "(null)";
                var refHash = child is null ? "(null)" : child.GetHashCode().ToString();
                var childCount = 0;
                if (child is System.Windows.Controls.Panel p)
                {
                    childCount = p.Children.Count;
                }

                var changed = false;
                if (_initialChildRef == null && child != null)
                {
                    _initialChildRef = child;
                }
                else if (_initialChildRef != null && !ReferenceEquals(_initialChildRef, child))
                {
                    changed = true;
                }

                System.Diagnostics.Debug.WriteLine($"[ED-307] {phase}: CenterBorder.Child Type={childType}, Hash={refHash}, ChildCount={childCount}, Changed={changed}");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ED-307] LogCenterBorderState error: {ex}");
            }
        }
    }
}
