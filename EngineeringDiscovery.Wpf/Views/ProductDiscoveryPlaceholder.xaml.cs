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

            // Focus the idea textbox when the view loads so the user can start typing immediately.
            Loaded += (s, e) =>
            {
                if (FindName("IdeaText") is System.Windows.Controls.TextBox tb)
                {
                    tb.Focus();
                    System.Windows.Input.Keyboard.Focus(tb);
                }
            };
        }

        private void ContinueButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var win = System.Windows.Window.GetWindow(this) as global::EngineeringDiscovery.Wpf.MainWindow
                ?? System.Windows.Application.Current?.MainWindow as global::EngineeringDiscovery.Wpf.MainWindow;

            if (win != null)
            {
                // Navigate into the Product Definition flow and pass the entered idea
                var idea = string.Empty;
                if (FindName("IdeaText") is System.Windows.Controls.TextBox tb)
                {
                    idea = tb.Text ?? string.Empty;
                }

                // Product Discovery flow removed from startup path: navigate to EngineeringWorkspace using the Engineering Partner
                var sp = EngineeringDiscovery.Wpf.App.ServiceProvider;
                if (sp != null)
                {
                    var partner = sp.GetService(typeof(EngineeringDiscovery.Core.Services.IEngineeringPartner)) as EngineeringDiscovery.Core.Services.IEngineeringPartner;
                    if (partner != null)
                    {
                        // Start an engineering session using the entered idea and navigate to the EngineeringWorkspace
                        _ = partner.StartSessionAsync(idea).GetAwaiter().GetResult();
                        win.HostContent.Content = new EngineeringWorkspace();
                        return;
                    }
                }

                // Fallback: if EngineeringPartner not available, navigate to EngineeringWorkspace directly
                win.HostContent.Content = new EngineeringWorkspace();
            }
        }
    }
}
