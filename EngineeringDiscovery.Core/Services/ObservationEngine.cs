using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Observations;
using System.Linq;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Ingests observations, preserves the existing session facts pipeline, and routes
    /// recognized project-state observations through IProjectStateService.
    /// </summary>
    public class ObservationEngine : IObservationService
    {
        private readonly IEngineeringModelRepository _modelRepository;
        private readonly IProjectStateService? _projectStateService;
        private readonly ConcurrentDictionary<Guid, Observation> _store = new();
        private static readonly JsonSerializerOptions s_payloadOptions = CreatePayloadOptions();

        public event Func<Observation, Task>? ObservationReceived;
        public event Func<Guid?, Task>? StateUpdated;

        public ObservationEngine(IEngineeringModelRepository modelRepository, IProjectStateService? projectStateService = null)
        {
            _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
            _projectStateService = projectStateService;
        }

        public async Task IngestAsync(Observation observation)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            _store[observation.Id] = observation;

            var handler = ObservationReceived;
            if (handler != null)
            {
                try { await handler.Invoke(observation).ConfigureAwait(false); }
                catch { }
            }

            await EnrichAndInferAsync(observation).ConfigureAwait(false);

            var sHandler = StateUpdated;
            if (sHandler != null)
            {
                try { await sHandler.Invoke(observation.SessionId).ConfigureAwait(false); }
                catch { }
            }
        }

        private async Task EnrichAndInferAsync(Observation obs)
        {
            if (string.Equals(obs.Type, "UserMessage", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var doc = JsonDocument.Parse(obs.PayloadJson ?? "{}");
                    if (doc.RootElement.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String && obs.SessionId.HasValue)
                    {
                        var text = textElement.GetString() ?? string.Empty;
                        var lastSentence = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).LastOrDefault() ?? text;
                        var model = await _modelRepository.GetAsync(obs.SessionId.Value).ConfigureAwait(false);
                        if (model != null)
                        {
                            model.KnownFacts.Add(new EngineeringFact { Key = "LastUserMessage", Value = text });
                            model.KnownFacts.Add(new EngineeringFact { Key = "LastUserSentence", Value = lastSentence });
                            await _modelRepository.UpdateAsync(model).ConfigureAwait(false);
                        }
                    }
                }
                catch { }
            }

            if (string.Equals(obs.Type, "RepoAttached", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var doc = JsonDocument.Parse(obs.PayloadJson ?? "{}");
                    if (doc.RootElement.TryGetProperty("path", out var pathElement) && pathElement.ValueKind == JsonValueKind.String && obs.SessionId.HasValue)
                    {
                        var path = pathElement.GetString() ?? string.Empty;
                        var model = await _modelRepository.GetAsync(obs.SessionId.Value).ConfigureAwait(false);
                        if (model != null)
                        {
                            model.KnownFacts.Add(new EngineeringFact { Key = "RepositoryPath", Value = path });
                            model.KnownFacts.Add(new EngineeringFact { Key = "RepositoryIndexed", Value = "False" });
                            await _modelRepository.UpdateAsync(model).ConfigureAwait(false);
                        }
                    }
                }
                catch { }
            }

            if (string.Equals(obs.Type, "BuildResult", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var doc = JsonDocument.Parse(obs.PayloadJson ?? "{}");
                    if (doc.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False && obs.SessionId.HasValue)
                    {
                        var model = await _modelRepository.GetAsync(obs.SessionId.Value).ConfigureAwait(false);
                        if (model != null && model.KnownFacts.Any(f => string.Equals(f.Key, "LastPackageGenerated", StringComparison.OrdinalIgnoreCase)))
                        {
                            model.KnownFacts.Add(new EngineeringFact { Key = "ImplementationDrift", Value = "True" });
                            await _modelRepository.UpdateAsync(model).ConfigureAwait(false);
                        }
                    }
                }
                catch { }
            }

            ApplyProjectStateObservation(obs);
        }

        private void ApplyProjectStateObservation(Observation observation)
        {
            if (_projectStateService is null) return;

            try
            {
                var type = observation.Type ?? string.Empty;
                if (string.Equals(type, "ProjectIdentityUpdated", StringComparison.OrdinalIgnoreCase))
                {
                    var identity = DeserializePayload<ProjectIdentity>(observation, "identity");
                    if (identity is not null) _projectStateService.SetProjectIdentity(identity);
                    return;
                }

                if (string.Equals(type, "WorkerStarted", StringComparison.OrdinalIgnoreCase))
                {
                    var engagement = DeserializePayload<WorkerEngagement>(observation, "engagement") ?? new WorkerEngagement();
                    if (!PayloadHasProperty(observation, "engagement", "id")) engagement.Id = observation.Id;
                    _projectStateService.RecordWorkerEngagement(engagement);
                    return;
                }

                if (string.Equals(type, "WorkerCompleted", StringComparison.OrdinalIgnoreCase))
                {
                    var engagementId = GetGuid(observation, "engagementId") ?? GetGuid(observation, "id");
                    if (!engagementId.HasValue) return;
                    var outcome = GetEnum(observation, "outcome", EngagementOutcome.Completed);
                    var acceptance = GetEnum(observation, "acceptance", AcceptanceStatus.Pending);
                    _projectStateService.UpdateEngagementOutcome(engagementId.Value, outcome, acceptance);
                    return;
                }

                if (string.Equals(type, "WorkAccepted", StringComparison.OrdinalIgnoreCase))
                {
                    var engagementId = GetGuid(observation, "engagementId") ?? GetGuid(observation, "id");
                    if (!engagementId.HasValue) return;
                    var acceptance = GetEnum(observation, "acceptance", AcceptanceStatus.Accepted);
                    _projectStateService.UpdateEngagementOutcome(engagementId.Value, EngagementOutcome.Completed, acceptance);
                    return;
                }

                if (string.Equals(type, "CapabilityVerified", StringComparison.OrdinalIgnoreCase))
                {
                    var capability = DeserializePayload<CompletedCapability>(observation, "capability") ?? new CompletedCapability();
                    if (!PayloadHasProperty(observation, "capability", "id")) capability.Id = observation.Id;
                    capability.Acceptance = CapabilityAcceptance.Accepted;
                    _projectStateService.RecordCompletedCapability(capability);
                    return;
                }

                if (string.Equals(type, "IssueReported", StringComparison.OrdinalIgnoreCase))
                {
                    var issue = DeserializePayload<KnownIssue>(observation, "issue") ?? new KnownIssue();
                    if (!PayloadHasProperty(observation, "issue", "id")) issue.Id = observation.Id;
                    _projectStateService.RecordKnownIssue(issue);
                    return;
                }

                if (string.Equals(type, "IssueResolved", StringComparison.OrdinalIgnoreCase))
                {
                    var issueId = GetGuid(observation, "issueId") ?? GetGuid(observation, "id");
                    if (!issueId.HasValue) return;
                    _projectStateService.ResolveIssue(issueId.Value, GetString(observation, "resolution") ?? string.Empty);
                }
            }
            catch
            {
                // A malformed or out-of-order observation must not break the existing
                // ingestion pipeline. Valid observations are applied through the service.
            }
        }

        private static T? DeserializePayload<T>(Observation observation, string nestedProperty)
        {
            using var document = JsonDocument.Parse(observation.PayloadJson ?? "{}");
            var element = document.RootElement;
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(nestedProperty, out var nested))
            {
                element = nested;
            }
            return element.Deserialize<T>(s_payloadOptions);
        }

        private static bool PayloadHasProperty(Observation observation, string nestedProperty, string propertyName)
        {
            using var document = JsonDocument.Parse(observation.PayloadJson ?? "{}");
            var element = document.RootElement;
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(nestedProperty, out var nested)) element = nested;
            return element.ValueKind == JsonValueKind.Object &&
                   (element.TryGetProperty(propertyName, out _) || element.TryGetProperty(char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1), out _));
        }

        private static Guid? GetGuid(Observation observation, string propertyName)
        {
            using var document = JsonDocument.Parse(observation.PayloadJson ?? "{}");
            if (!document.RootElement.TryGetProperty(propertyName, out var element)) return null;
            if (element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var parsed)) return parsed;
            if (element.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.GetString())) return null;
            if (element.ValueKind == JsonValueKind.String) return null;
            if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null) return null;
            return element.TryGetGuid(out var guid) ? guid : null;
        }

        private static string? GetString(Observation observation, string propertyName)
        {
            using var document = JsonDocument.Parse(observation.PayloadJson ?? "{}");
            return document.RootElement.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
        }

        private static TEnum GetEnum<TEnum>(Observation observation, string propertyName, TEnum fallback)
            where TEnum : struct, Enum
        {
            var value = GetString(observation, propertyName);
            return !string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
                ? parsed
                : fallback;
        }

        private static JsonSerializerOptions CreatePayloadOptions()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}
