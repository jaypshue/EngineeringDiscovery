using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    internal interface IRepositoryProvider
    {
        bool CanLoad(string repositoryRoot);

        IReadOnlyList<CompilationContext> Load(string repositoryRoot);
    }
}
