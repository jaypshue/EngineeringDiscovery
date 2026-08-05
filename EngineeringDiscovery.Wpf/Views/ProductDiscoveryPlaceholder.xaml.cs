namespace EngineeringDiscovery.Wpf.Views
{
    public partial class ProductDiscoveryPlaceholder : System.Windows.Controls.UserControl
    {
        public ProductDiscoveryPlaceholder()
        {
            InitializeComponent();

            if (FindName("ContinueButton") is System.Windows.Controls.Button continueButton)
            {
                continueButton.Click += ContinueButton_Click;
            }
        }

        private void ContinueButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var win = System.Windows.Window.GetWindow(this) as global::EngineeringDiscovery.Wpf.MainWindow
                ?? System.Windows.Application.Current?.MainWindow as global::EngineeringDiscovery.Wpf.MainWindow;

            if (win != null)
            {
                win.HostContent.Content = new EngineeringWorkspace();
            }
        }
    }
}
