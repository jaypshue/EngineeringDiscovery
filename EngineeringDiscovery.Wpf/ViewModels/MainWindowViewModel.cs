using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EngineeringDiscovery.Wpf.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel()
    {
        NewInvestigationCommand = new RelayCommand(() => System.Windows.MessageBox.Show("New Investigation (placeholder)"));
        OpenRepositoryCommand = new RelayCommand(() => System.Windows.MessageBox.Show("Open Repository (placeholder)"));
        ExitCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());
    }

    public ICommand NewInvestigationCommand { get; }
    public ICommand OpenRepositoryCommand { get; }
    public ICommand ExitCommand { get; }
}
