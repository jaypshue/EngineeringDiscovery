using System;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Observations;

namespace EngineeringDiscovery.Core.Services
{
    public interface IObservationService
    {
        // Ingest a new observation. Implementations should persist the observation and
        // trigger enrichment/inference pipelines.
        Task IngestAsync(Observation observation);

        // Optional: a simple synchronous publish for subscribers; keep minimal for now.
        event Func<Observation, Task>? ObservationReceived;
    }
}
