using System;
using System.Xml.Linq;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading.FallbackParsing
{
    internal static class RootNamespaceResolver
    {
        public static string? GetRootNamespaceFromCsproj(string csprojPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(csprojPath)) return null;
                var doc = XDocument.Load(csprojPath);
                var rootNs = doc.Descendants().FirstOrDefault(x => string.Equals(x.Name.LocalName, "RootNamespace", StringComparison.OrdinalIgnoreCase))?.Value;
                if (!string.IsNullOrWhiteSpace(rootNs)) return rootNs.Trim();
            }
            catch { }
            return null;
        }
    }
}
