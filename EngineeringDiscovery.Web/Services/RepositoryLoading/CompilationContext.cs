using System.Collections.Generic;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    // Language-agnostic compilation context produced by repository providers.
    internal class CompilationContext
    {
        public string ProjectName { get; set; } = string.Empty;

        public string? ProjectFilePath { get; set; }

        public List<TypeDescriptor> Types { get; } = new List<TypeDescriptor>();
    }
}
