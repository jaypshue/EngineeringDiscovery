using CommunityToolkit.Mvvm.ComponentModel;

namespace EngineeringDiscovery.Wpf.ViewModels;

public partial class RepositoryExplorerViewModel : ObservableObject
{
    public RepositoryExplorerViewModel()
    {
        Title = "Repository Explorer";
    }

    public string Title { get; set; }
}
