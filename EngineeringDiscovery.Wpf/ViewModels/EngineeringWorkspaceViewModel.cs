using System;
using System.ComponentModel;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Services;

namespace EngineeringDiscovery.Wpf.ViewModels
{
    public class EngineeringWorkspaceViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public WorkspaceConversationViewModel Conversation { get; }

        public EngineeringPackageViewModel Package { get; }

        public EngineeringWorkspaceViewModel(IEngineeringPartner partner)
        {
            if (partner == null) throw new ArgumentNullException(nameof(partner));

            // Create child VMs synchronously so they are fully initialized before exposure to the view
            Package = new EngineeringPackageViewModel();
            Conversation = new WorkspaceConversationViewModel(partner);

            // Asynchronously initialize conversation without blocking construction
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await Conversation.InitializeAsync();

            // After conversation init, perform any package sync on UI thread via Application.Current.Dispatcher
            try
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    await dispatcher.BeginInvoke(new Action(() =>
                    {
                        // Minimal initial package sync
                        Package.Purpose = $"Implement {Package.Version} – Live Engineering Package";
                    }));
                }
                else
                {
                    Package.Purpose = $"Implement {Package.Version} – Live Engineering Package";
                }
            }
            catch
            {
                // ignore dispatcher issues during early initialization
            }
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
