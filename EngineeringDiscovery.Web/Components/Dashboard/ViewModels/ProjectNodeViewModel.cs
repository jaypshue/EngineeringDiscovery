using System.Collections.Generic;

namespace EngineeringDiscovery.Web.Components.Dashboard.ViewModels
{
    public class ProjectNodeViewModel
    {
        public ProjectNodeViewModel()
        {
            Namespaces = new Dictionary<string, NamespaceNodeViewModel>(System.StringComparer.OrdinalIgnoreCase);
        }

        public string Name { get; set; } = string.Empty;
        public Dictionary<string, NamespaceNodeViewModel> Namespaces { get; set; }
        public List<string> Observations { get; set; } = new();
    }
}
