using System;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Core.Services
{
    public interface IEngineeringModelRepository
    {
        Task<EngineeringModel> CreateAsync(EngineeringModel model);

        Task<EngineeringModel?> GetAsync(Guid id);

        Task UpdateAsync(EngineeringModel model);
    }
}
