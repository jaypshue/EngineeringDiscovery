using System.Collections.Generic;

namespace EngineeringDiscovery.Web.Components.Dashboard.ViewModels
{
    public class TypeNodeViewModel
    {
        public TypeNodeViewModel()
        {
            Members = new List<string>();
        }

        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = "class";
        public List<string> Members { get; set; }
        public List<string> Observations { get; set; } = new();
    }
}
