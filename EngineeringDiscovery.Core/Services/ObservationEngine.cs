using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Observations;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using System.Linq;

namespace EngineeringDiscovery.Core.Services
{
    // Minimal Observation Engine implementation. Responsibilities:
    // - persist raw observations (in-memory for now)
    // - publish ObservationReceived event
    // - perform tiny enrichment/inference steps synchronously
    // - update EngineeringModel via IEngineeringModelRepository when applicable
    public class ObservationEngine : IObservationService
    {
        private readonly IEngineeringModelRepository _modelRepository;
        private readonly ConcurrentDictionary<Guid, Observation> _store = new();

        public event Func<Observation, Task>? ObservationReceived;
        public event Func<Guid?, Task>? StateUpdated;

        public ObservationEngine(IEngineeringModelRepository modelRepository)
        {
            _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
        }

        public async Task IngestAsync(Observation observation)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            _store[observation.Id] = observation;

            // Publish to subscribers
            var handler = ObservationReceived;
            if (handler != null)
            {
                try
                {
                    await handler.Invoke(observation).ConfigureAwait(false);
                }
                catch { /* swallow subscriber exceptions for now */ }
            }

            // Minimal enrichment/inference pipeline
            await EnrichAndInferAsync(observation).ConfigureAwait(false);

            // Notify subscribers that state may have changed for this session
            var sHandler = StateUpdated;
            if (sHandler != null)
            {
                try
                {
                    await sHandler.Invoke(observation.SessionId).ConfigureAwait(false);
                }
                catch { }
            }
        }

        private async Task EnrichAndInferAsync(Observation obs)
        {
            // Simple enrich: if UserMessage, extract last sentence as a lightweight fact
            if (string.Equals(obs.Type, "UserMessage", StringComparison.OrdinalIgnoreCase))
            {
                // Parse payload JSON { text: "..." }
                try
                {
                    using var doc = JsonDocument.Parse(obs.PayloadJson ?? "{}");
                    if (doc.RootElement.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                    {
                        var text = t.GetString() ?? string.Empty;
                        var lastSentence = text.Split(new[]{'.','!','?'}, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).LastOrDefault() ?? text;
                        // Persist a KnownFact: LastUserMessage
                        if (obs.SessionId.HasValue)
                        {
                            var model = await _modelRepository.GetAsync(obs.SessionId.Value).ConfigureAwait(false);
                            if (model != null)
                            {
                                model.KnownFacts.Add(new EngineeringFact { Key = "LastUserMessage", Value = text });
                                model.KnownFacts.Add(new EngineeringFact { Key = "LastUserSentence", Value = lastSentence });
                                await _modelRepository.UpdateAsync(model).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch { /* ignore JSON parse errors for now */ }
            }

            // RepoAttached: set RepositoryPath known fact and infer RepositoryIndexed=false (scanner to follow)
            if (string.Equals(obs.Type, "RepoAttached", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var doc = JsonDocument.Parse(obs.PayloadJson ?? "{}");
                    if (doc.RootElement.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String)
                    {
                        var path = p.GetString() ?? string.Empty;
                        if (obs.SessionId.HasValue)
                        {
                            var model = await _modelRepository.GetAsync(obs.SessionId.Value).ConfigureAwait(false);
                            if (model != null)
                            {
                                model.KnownFacts.Add(new EngineeringFact { Key = "RepositoryPath", Value = path });
                                model.KnownFacts.Add(new EngineeringFact { Key = "RepositoryIndexed", Value = "False" });
                                await _modelRepository.UpdateAsync(model).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch { }
            }

            // Minimal rule: if BuildResult with success=false and there is a recent PackageGenerated fact, infer ImplementationDrift
            if (string.Equals(obs.Type, "BuildResult", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var doc = JsonDocument.Parse(obs.PayloadJson ?? "{}");
                    if (doc.RootElement.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.False && obs.SessionId.HasValue)
                    {
                        var model = await _modelRepository.GetAsync(obs.SessionId.Value).ConfigureAwait(false);
                        if (model != null)
                        {
                            if (model.KnownFacts.Any(f => string.Equals(f.Key, "LastPackageGenerated", StringComparison.OrdinalIgnoreCase)))
                            {
                                model.KnownFacts.Add(new EngineeringFact { Key = "ImplementationDrift", Value = "True" });
                                await _modelRepository.UpdateAsync(model).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch { }
            }
        }
    }
}
