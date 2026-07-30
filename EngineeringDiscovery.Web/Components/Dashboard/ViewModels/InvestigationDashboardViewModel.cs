using System.Collections.Generic;

namespace EngineeringDiscovery.Web.Components.Dashboard.ViewModels
{
    public class InvestigationDashboardViewModel
    {
        public InvestigationDashboardViewModel()
        {
            Summary = new SummaryCard();
            Projects = new Dictionary<string, ProjectNodeViewModel>(System.StringComparer.OrdinalIgnoreCase);
            Dependencies = new List<string>();
            Technologies = new List<string>();
            Findings = new List<EngineeringDiscovery.Core.Domain.Investigation.Finding>();
        }

        public SummaryCard Summary { get; set; }
        public Dictionary<string, ProjectNodeViewModel> Projects { get; set; }
        public List<string> Dependencies { get; set; }
        public List<string> Technologies { get; set; }
        public List<EngineeringDiscovery.Core.Domain.Investigation.Finding> Findings { get; set; }
    }
}
