using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    internal class RepositoryLoader
    {
        private readonly List<IRepositoryProvider> _providers;

        public RepositoryLoader()
        {
            // Register available providers here. Additional providers can be added in future.
            _providers = new List<IRepositoryProvider>
            {
                new CSharpRepositoryProvider(),
                new JavaRepositoryProvider()
            };
        }

        public IReadOnlyList<CompilationContext> Load(string repositoryRoot)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot)) return Array.Empty<CompilationContext>();

            foreach (var p in _providers)
            {
                try
                {
                    if (p.CanLoad(repositoryRoot))
                    {
                        var contexts = p.Load(repositoryRoot);
                        if (contexts != null && contexts.Count > 0) return contexts;
                    }
                }
                catch { }
            }

            return Array.Empty<CompilationContext>();
        }
    }
}
