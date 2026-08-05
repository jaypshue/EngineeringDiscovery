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

                // Create a new EngineeringModel via the orchestrator, then navigate to ProductDefinitionView with model id
                var sp = EngineeringDiscovery.Wpf.App.ServiceProvider;
                if (sp != null)
                {
                    var orchestrator = sp.GetService(typeof(EngineeringDiscovery.Core.Services.IEnginerringConversationOrchestrator)) as EngineeringDiscovery.Core.Services.IEnginerringConversationOrchestrator;
                    if (orchestrator != null)
                    {
                        var model = orchestrator.CreateModelAsync(idea).GetAwaiter().GetResult();
                        win.HostContent.Content = new ProductDefinitionView(model.Id);
                        return;
                    }
                }

                // Fallback: if orchestrator not available, create a temporary GUID-based model and navigate
                if (Guid.TryParse(idea, out var parsedGuid))
                {
                    win.HostContent.Content = new ProductDefinitionView(parsedGuid);
                }
                else
                {
                    // Create a temporary EngineeringModel via repository directly as a last resort
                    var sp2 = EngineeringDiscovery.Wpf.App.ServiceProvider;
                    var repo = sp2?.GetService(typeof(EngineeringDiscovery.Core.Services.IEngineeringModelRepository)) as EngineeringDiscovery.Core.Services.IEngineeringModelRepository;
                    if (repo != null)
                    {
                        var temp = new EngineeringDiscovery.Core.Domain.EngineeringModel.EngineeringModel { OriginalIdea = idea };
                        repo.CreateAsync(temp).GetAwaiter().GetResult();
                        win.HostContent.Content = new ProductDefinitionView(temp.Id);
                    }
                    else
                    {
                        // As a last-possible fallback, navigate to a ProductDefinitionView with Guid.Empty
                        win.HostContent.Content = new ProductDefinitionView(Guid.Empty);
                    }
                }
            }
        }
    }
}
