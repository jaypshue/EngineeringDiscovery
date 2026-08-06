using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Core.Tests
{
    public class InMemoryEngineeringModelRepository : EngineeringDiscovery.Core.Services.IEngineeringModelRepository
    {
        private readonly ConcurrentDictionary<Guid, EngineeringModel> _store = new();

        public Task<EngineeringModel> CreateAsync(EngineeringModel model)
        {
            _store[model.Id] = model;
            return Task.FromResult(model);
        }

        public Task<EngineeringModel?> GetAsync(Guid id)
        {
            _store.TryGetValue(id, out var m);
            return Task.FromResult(m);
        }

        public Task UpdateAsync(EngineeringModel model)
        {
            _store[model.Id] = model;
            return Task.CompletedTask;
        }
    }
}
