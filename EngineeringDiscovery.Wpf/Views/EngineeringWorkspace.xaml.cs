using System.Windows.Controls;
using System.Diagnostics;

namespace EngineeringDiscovery.Wpf.Views
{
    public partial class EngineeringWorkspace : System.Windows.Controls.UserControl
    {
        private readonly ViewModels.WorkspaceConversationViewModel _conversationVm;

        public EngineeringWorkspace()
        {
            InitializeComponent();

            Debug.WriteLine("[ED-EP5.2] EngineeringWorkspace constructor start");

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

                    Debug.WriteLine("[ED-EP5.2] WorkspaceConversationViewModel created");

                    // Set the DataContext so XAML bindings (Messages, Draft, SendCommand) resolve
                    this.DataContext = _conversationVm;

                    Debug.WriteLine("[ED-EP5.2] DataContext set to WorkspaceConversationViewModel");

                    // Initialize conversation session (fire-and-forget, safe)
                    Debug.WriteLine("[ED-EP5.2] Starting InitializeConversationAsync");
                    _ = InitializeConversationAsync();
                }
            }
        }

        private async System.Threading.Tasks.Task InitializeConversationAsync()
        {
            try
            {
                Debug.WriteLine("[ED-EP5.2] InitializeConversationAsync started");
                await _conversationVm.InitializeAsync();
                ScrollToEnd();
                Debug.WriteLine("[ED-EP5.2] InitializeConversationAsync completed");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine("[ED-EP5.2] InitializeConversationAsync failed: " + ex);
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
