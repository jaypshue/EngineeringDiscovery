namespace EngineeringDiscovery.Web.Components.Dashboard.ViewModels
{
    public class SummaryCard
    {
        public string Repository { get; set; } = string.Empty;
        public string Solution { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public int ProjectCount { get; set; }
        public int NamespaceCount { get; set; }
        public int TypeCount { get; set; }
        public int MemberCount { get; set; }
        public int TechnologyCount { get; set; }
        public int FindingCount { get; set; }
    }
}
