using System.Windows.Controls;
using System.Diagnostics;

namespace EngineeringDiscovery.Wpf.Views
{
    public partial class EngineeringWorkspace : System.Windows.Controls.UserControl
    {
        private static int _instanceCounter = 0;
        private readonly int _instanceId;
        private readonly ViewModels.WorkspaceConversationViewModel _conversationVm;

        public EngineeringWorkspace()
        {
            _instanceId = System.Threading.Interlocked.Increment(ref _instanceCounter);
            InitializeComponent();

            Debug.WriteLine($"[ED-EP7] EngineeringWorkspace #{_instanceId} constructor start");

            // Resolve IEngineeringPartner via App.ServiceProvider and attach conversation VM when available
            var sp = App.ServiceProvider;
            if (sp == null)
            {
                Debug.WriteLine("[ED-EP5.2] App.ServiceProvider is null");
            }
            else
            {
                Debug.WriteLine("[ED-EP5.2] App.ServiceProvider resolved");
                var partner = sp.GetService(typeof(EngineeringDiscovery.Core.Services.IEngineeringPartner)) as EngineeringDiscovery.Core.Services.IEngineeringPartner;
                if (partner == null)
                {
                    Debug.WriteLine("[ED-EP5.2] IEngineeringPartner not registered in ServiceProvider");
                }
                else
                {
                    Debug.WriteLine("[ED-EP5.2] IEngineeringPartner resolved: " + partner.GetType().FullName);
                    _conversationVm = new ViewModels.WorkspaceConversationViewModel(partner);

                    Debug.WriteLine($"[ED-EP7] WorkspaceConversationViewModel #{_instanceId} created");

                    // Set the DataContext so XAML bindings (Messages, Draft, SendCommand) resolve
                    this.DataContext = _conversationVm;

                    Debug.WriteLine($"[ED-EP7] DataContext set to WorkspaceConversationViewModel #{_instanceId}");

                    // Initialize conversation session (fire-and-forget, safe)
                    Debug.WriteLine($"[ED-EP7] Starting InitializeConversationAsync #{_instanceId}");
                    _ = InitializeConversationAsync();
                }
            }
        }

        private async System.Threading.Tasks.Task InitializeConversationAsync()
        {
            try
            {
                Debug.WriteLine($"[ED-EP7] InitializeConversationAsync #{_instanceId} started");
                await _conversationVm.InitializeAsync();
                ScrollToEnd();
                Debug.WriteLine($"[ED-EP7] InitializeConversationAsync #{_instanceId} completed");
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
    }
}
