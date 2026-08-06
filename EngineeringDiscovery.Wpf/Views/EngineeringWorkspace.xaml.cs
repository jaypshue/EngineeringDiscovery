using System.Diagnostics;

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
    }
}
