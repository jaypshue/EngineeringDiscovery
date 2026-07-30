using System.Collections.Generic;

namespace EngineeringDiscovery.Web.Components.Dashboard.ViewModels
{
    public class NamespaceNodeViewModel
    {
        public NamespaceNodeViewModel()
        {
            Types = new Dictionary<string, TypeNodeViewModel>(System.StringComparer.OrdinalIgnoreCase);
        }

        public string Name { get; set; } = string.Empty;
        public Dictionary<string, TypeNodeViewModel> Types { get; set; }
        public List<string> Observations { get; set; } = new();
    }
}
