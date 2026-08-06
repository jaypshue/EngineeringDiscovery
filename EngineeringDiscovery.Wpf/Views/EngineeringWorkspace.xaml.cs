using System.Windows.Controls;
using System.Diagnostics;

namespace EngineeringDiscovery.Wpf.Views
{
    public partial class EngineeringWorkspace : System.Windows.Controls.UserControl
    {
        private static int _instanceCounter = 0;
        private readonly int _instanceId;
        private readonly ViewModels.WorkspaceConversationViewModel _conversationVm;
        // The workspace now expects to be hosted with a view model owning the conversation and package VMs.

        public EngineeringWorkspace()
        {
            _instanceId = System.Threading.Interlocked.Increment(ref _instanceCounter);
            InitializeComponent();

            Debug.WriteLine($"[ED-EP7] EngineeringWorkspace #{_instanceId} constructor start");

            // The view is now purely presentation. The host should set DataContext to a workspace view model that
            // owns both the Conversation VM and the Package VM. If no DataContext is provided, we leave the control
            // in a neutral state to avoid initialization-time side effects that previously caused UI thread loops.
            Debug.WriteLine($"[ED-EP7] EngineeringWorkspace #{_instanceId} constructed (no VM creation in code-behind)");
        }

        private async System.Threading.Tasks.Task InitializeConversationAsync()
        {
            try
            {
                Debug.WriteLine($"[ED-EP7] InitializeConversationAsync #{_instanceId} started");
                await _conversationVm.InitializeAsync();
                ScrollToEnd();
                Debug.WriteLine($"[ED-EP7] InitializeConversationAsync #{_instanceId} completed");
                // Inform the host VM that conversation initialization has completed. The host ViewModel should
                // subscribe to conversation lifecycle and update Package properties as needed. Avoid direct access
                // to view resources or ViewModel instances from code-behind.
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[ED-EP7] InitializeConversationAsync #{_instanceId} failed: " + ex);
                // Surface the error temporarily by adding a conversation message
                try
                {
                    _conversationVm?.Messages.Add(new ViewModels.ConversationMessage { Speaker = "System", Text = "Conversation initialization failed: " + ex.Message });
                }
                catch { }
            }
        }


        private void ScrollToEnd()
        {
            try
            {
                ConversationScroll?.ScrollToEnd();
            }
            catch { }
        }

        private void EngineeringPackage_Preview_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Legacy click handler retained for compatibility. Prefer binding to Package.PreviewCommand from XAML.
            if (this.DataContext is ViewModels.EngineeringWorkspaceViewModel host && host.Package != null)
            {
                host.Package.Preview();
            }
        }

        private void EngineeringPackage_SendToCopilot_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Legacy click handler retained for compatibility. Prefer binding to Package.SendToCopilotCommand from XAML.
            if (this.DataContext is ViewModels.EngineeringWorkspaceViewModel host && host.Package != null)
            {
                host.Package.SendToCopilot();
            }
        }
    }
}
