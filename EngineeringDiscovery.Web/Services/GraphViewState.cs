namespace EngineeringDiscovery.Web.Services
{
    public class GraphViewState
    {
        public double Zoom { get; set; }
        public object? Pan { get; set; }
        public string? SelectedNode { get; set; }
        public bool Inheritance { get; set; } = true;
        public bool Implementation { get; set; } = true;
        public bool Dependency { get; set; } = true;
    }
}
