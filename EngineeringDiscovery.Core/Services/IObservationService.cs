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

        // Event raised when the ObservationEngine has applied updates to EngineeringModel
        // for a specific session id. Subscribers may use this to refresh UI or trigger
        // partner consumption. The sessionId parameter may be null when the update is global.
        event Func<Guid?, Task>? StateUpdated;
    }
}
