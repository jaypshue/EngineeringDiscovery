using CommunityToolkit.Mvvm.ComponentModel;

namespace EngineeringDiscovery.Wpf.ViewModels;

public partial class OutputViewModel : ObservableObject
{
    public OutputViewModel()
    {
        Title = "Output";
    }

    public string Title { get; set; }
}
