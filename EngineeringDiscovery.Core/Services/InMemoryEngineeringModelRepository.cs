using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Core.Services
{
    public class InMemoryEngineeringModelRepository : IEngineeringModelRepository
    {
        private readonly ConcurrentDictionary<Guid, EngineeringModel> _store = new();

        public Task<EngineeringModel> CreateAsync(EngineeringModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            _store[model.Id] = model;
            return Task.FromResult(model);
        }

        public Task<EngineeringModel?> GetAsync(Guid id)
        {
            _store.TryGetValue(id, out var model);
            return Task.FromResult(model);
        }

        public Task UpdateAsync(EngineeringModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            _store[model.Id] = model;
            return Task.CompletedTask;
        }
    }
}
