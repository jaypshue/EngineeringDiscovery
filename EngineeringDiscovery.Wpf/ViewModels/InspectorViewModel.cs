using CommunityToolkit.Mvvm.ComponentModel;

namespace EngineeringDiscovery.Wpf.ViewModels;

public partial class InspectorViewModel : ObservableObject
{
    public InspectorViewModel()
    {
        Title = "Inspector";
    }

    public string Title { get; set; }
}
