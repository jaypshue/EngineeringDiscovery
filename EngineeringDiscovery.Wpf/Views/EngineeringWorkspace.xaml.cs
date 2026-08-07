using System.Diagnostics;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Wpf.Views
{
    public partial class EngineeringWorkspace : System.Windows.Controls.UserControl
    {
        // EngineeringWorkspace is a pure view. It must be hosted with an EngineeringWorkspaceViewModel
        // set as its DataContext. The canonical ownership hierarchy is:
        //  - EngineeringWorkspaceViewModel
        //      - Conversation : WorkspaceConversationViewModel
        //      - Package      : EngineeringPackageViewModel
        // Future workspace artifacts (Context, Evidence, Investigation, etc.) will be added as
        // sibling properties on EngineeringWorkspaceViewModel so the view can bind to them as
        // {Binding Context}, {Binding Evidence}, etc.

        public EngineeringWorkspace()
        {
            InitializeComponent();
            Debug.WriteLine("[ED-EP7] EngineeringWorkspace constructed (pure view - DataContext must be provided by host)");
        }

        // Helper to route repository discovery intent through the workspace VM's EngineeringEngine.
        // This is intentionally not invoked automatically.
        public async Task BeginRepositoryDiscoveryAsync(string repositoryPath)
        {
            if (DataContext is EngineeringDiscovery.Wpf.ViewModels.EngineeringWorkspaceViewModel vm && vm.Engine != null)
            {
                await vm.Engine.BeginRepositoryDiscovery(repositoryPath).ConfigureAwait(false);
            }
        }

        private async void DiscoverRepository_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                var sp = EngineeringDiscovery.Wpf.App.ServiceProvider;
                var ws = sp?.GetService(typeof(EngineeringDiscovery.Core.Services.WorkspaceState)) as EngineeringDiscovery.Core.Services.WorkspaceState;
                var repoPath = ws?.ActiveWorkspace?.RepositoryPath;

                if (string.IsNullOrWhiteSpace(repoPath))
                {
                    var sel = sp?.GetService(typeof(EngineeringDiscovery.Wpf.Services.RepositorySelectionService)) as EngineeringDiscovery.Wpf.Services.RepositorySelectionService;
                    repoPath = sel?.SelectedPath;
                }

                if (!string.IsNullOrWhiteSpace(repoPath))
                {
                    if (DataContext is EngineeringDiscovery.Wpf.ViewModels.EngineeringWorkspaceViewModel vm &&
                        vm.Engine != null)
                    {
                        await vm.Engine.BeginRepositoryDiscovery(repoPath).ConfigureAwait(false);
                    }
                    else
                    {
                        var engine = new EngineeringDiscovery.Wpf.Services.EngineeringEngine(new EngineeringDiscovery.Wpf.Services.RepositoryDiscoveryService());
                        await engine.BeginRepositoryDiscovery(repoPath).ConfigureAwait(false);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
