using System;
using System.Threading.Tasks;
using System.Diagnostics;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Core.Services
{
    // Minimal EngineeringPartner implementation that composes existing services.
    // This establishes the abstraction and basic workflow for the conversation-first experience.
    public class EngineeringPartner : IEngineeringPartner
    {
        private readonly IEngineeringModelRepository _repository;
        private readonly IEngineeringConversationService? _conversationService;

        public EngineeringPartner(IEngineeringModelRepository repository, IEngineeringConversationService? conversationService = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _conversationService = conversationService;
        }

        public async Task<EngineeringModel> StartSessionAsync(string openingStatement)
        {
            var model = new EngineeringModel();
            var startTimestamp = DateTime.UtcNow;
            Debug.WriteLine($"[ED-EP7] StartSessionAsync invoked. OpeningStatement='{openingStatement}'. Timestamp={startTimestamp:o}");
            model.OriginalIdea = openingStatement ?? string.Empty;
            model.Status = EngineeringStatus.Discovering;

            // Seed the working memory by recording the opening statement as a KnownFact
            model.KnownFacts.Add(new EngineeringFact { Key = "OpeningStatement", Value = openingStatement ?? string.Empty });

            // Persist a working copy for the session
            await _repository.CreateAsync(model).ConfigureAwait(false);
            Debug.WriteLine($"[ED-EP7] StartSessionAsync: model created with Id={model.Id}");

            // At startup, do NOT invoke the external conversation service. Seed a deterministic greeting so startup is deterministic
            // and does not trigger Product Discovery questions automatically.
            var greeting = "Hello — I'm EngineOS. Tell me about your idea and I'll help you explore it.";
            model.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = greeting, TimestampUtc = DateTime.UtcNow });
            await _repository.UpdateAsync(model).ConfigureAwait(false);

            return model;
        }

        public async Task<string> SendMessageAsync(Guid sessionId, string message)
        {
            var model = await _repository.GetAsync(sessionId).ConfigureAwait(false);
            Debug.WriteLine($"[ED-EP6] SendMessageAsync called for session {sessionId}. User message: {message}");

            if (model == null)
            {
                Debug.WriteLine($"[ED-EP6] Session {sessionId} not found in repository");
                return "I couldn't locate the session. Please try starting a new conversation.";
            }
            // 1) Understand: record the user message and extract simple intents/facts
            model.Conversation.Add(new ConversationEntry { Speaker = "User", Message = message, TimestampUtc = DateTime.UtcNow });

            var intents = AnalyzeMessage(message);
            var newFacts = ExtractFactsFromMessage(message, intents);
            foreach (var f in newFacts)
            {
                model.KnownFacts.Add(f);
            }

            // 2) Remember: persist updated working memory before making recommendations
            await _repository.UpdateAsync(model).ConfigureAwait(false);

            // 3) Guide: determine the single best next recommendation
            var recommendation = DetermineRecommendation(model, intents);
            model.KnownFacts.Add(new EngineeringFact { Key = "LastRecommendation", Value = recommendation });

            // 4) Coordinate: decide whether another participant or action should be involved
            var coordination = DetermineCoordination(model, intents);
            if (!string.IsNullOrWhiteSpace(coordination))
            {
                model.KnownFacts.Add(new EngineeringFact { Key = "Coordination", Value = coordination });
            }

            // 5) Recover: detect inconsistencies and prefer recovery before continuing
            var recovery = DetectRecoveryNeeded(model, intents);
            if (!string.IsNullOrWhiteSpace(recovery))
            {
                // Prepend recovery recommendation to the guidance
                recommendation = recovery + "\n\n" + recommendation;
                model.KnownFacts.Add(new EngineeringFact { Key = "LastRecoveryRecommendation", Value = recovery });
            }

            // Compose a conversational reply. Prefer LLM-driven text when available, but
            // append our recommendation so the conversation projects Engineering State.
            string llmReply = null;
            if (_conversationService != null)
            {
                try
                {
                    Debug.WriteLine($"[ED-EP6] Prompt sent to conversation service for session {sessionId}");
                    llmReply = await _conversationService.RespondAsync(model).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ED-EP6] Exception from conversation service for session {sessionId}: {ex}");
                    llmReply = null;
                }
            }

            string reply;
            if (string.IsNullOrWhiteSpace(llmReply))
            {
                // Construct a focused reply that surfaces the recommendation
                reply = ComposeFallbackReply(message, recommendation, coordination);
            }
            else
            {
                // Use the LLM reply but ensure the partner's recommendation appears
                reply = llmReply.Trim();
                reply += "\n\nRecommendation: " + recommendation;
                if (!string.IsNullOrWhiteSpace(coordination))
                {
                    reply += "\nCoordination: " + coordination;
                }
            }

            // Record EngineOS reply and persist the augmented working memory
            model.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = reply, TimestampUtc = DateTime.UtcNow });
            await _repository.UpdateAsync(model).ConfigureAwait(false);

            Debug.WriteLine($"[ED-EP6] Engineering Partner reply recorded for session {sessionId}");
            return reply;
        }

        private string[] AnalyzeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return Array.Empty<string>();
            var m = message.ToLowerInvariant();
            var intents = new System.Collections.Generic.List<string>();
            if (m.Contains("generate") || m.Contains("create") || m.Contains("scaffold") || m.Contains("package")) intents.Add("GeneratePackage");
            if (m.Contains("architect") || m.Contains("architecture") || m.Contains("design")) intents.Add("DiscussArchitecture");
            if (m.Contains("how") || m.Contains("what") || m.Contains("why") || m.Contains("help")) intents.Add("Question");
            if (m.Contains("fix") || m.Contains("bug") || m.Contains("fail") || m.Contains("error")) intents.Add("RunAnalysis");
            if (m.Contains("change") || m.Contains("update") || m.Contains("now")) intents.Add("IntentChange");
            return intents.ToArray();
        }

        private System.Collections.Generic.IEnumerable<EngineeringFact> ExtractFactsFromMessage(string message, string[] intents)
        {
            // Minimal fact extraction: record last user intent and a snapshot of the last message
            yield return new EngineeringFact { Key = "LastUserMessage", Value = message };
            if (intents != null && intents.Length > 0)
            {
                yield return new EngineeringFact { Key = "LastUserIntent", Value = string.Join(',', intents) };
            }
        }

        private string DetermineRecommendation(EngineeringModel model, string[] intents)
        {
            // Simple heuristic: if user intends to generate a package but no repository fact exists,
            // recommend attaching a repository first. Otherwise recommend the most direct next step.
            var hasRepo = model.KnownFacts.Exists(f => string.Equals(f.Key, "RepositoryPath", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(f.Value));
            if (intents != null && System.Array.IndexOf(intents, "GeneratePackage") >= 0)
            {
                if (!hasRepo)
                {
                    return "I recommend attaching the target repository (Repository Discovery) so generated packages can be placed and validated against the codebase.";
                }
                return "I recommend generating a small scaffold package for review. I'll produce a preview; review before applying.";
            }

            if (intents != null && System.Array.IndexOf(intents, "DiscussArchitecture") >= 0)
            {
                return "I recommend we outline the desired architecture with key components and tradeoffs. I can propose 2-3 options and a small experiment to validate one.";
            }

            if (intents != null && System.Array.IndexOf(intents, "RunAnalysis") >= 0)
            {
                return "I recommend running a targeted analysis (build/tests or repository scanning) to gather evidence before making a definitive recommendation.";
            }

            // Default guidance: propose clarifying or incremental steps
            return "Ask me to clarify your objective or provide a short description of what success looks like; I'll recommend a small next step.";
        }

        private string DetermineCoordination(EngineeringModel model, string[] intents)
        {
            // Decide whether to involve additional participants
            if (intents != null && System.Array.IndexOf(intents, "GeneratePackage") >= 0)
            {
                return "PackageGeneration";
            }
            if (intents != null && System.Array.IndexOf(intents, "DiscussArchitecture") >= 0)
            {
                return "Architect";
            }
            return string.Empty;
        }

        private string DetectRecoveryNeeded(EngineeringModel model, string[] intents)
        {
            // Simple recovery heuristics: if user indicates intent change, suggest reconciling assumptions
            if (intents != null && System.Array.IndexOf(intents, "IntentChange") >= 0)
            {
                return "Your intent appears to have changed. I recommend we review the current assumptions and decisions and mark any that need revalidation before proceeding.";
            }
            return string.Empty;
        }

        private string ComposeFallbackReply(string message, string recommendation, string coordination)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("I have recorded your message and updated my understanding.");
            if (!string.IsNullOrWhiteSpace(recommendation))
            {
                sb.Append("\n\nRecommendation: ");
                sb.Append(recommendation);
            }
            if (!string.IsNullOrWhiteSpace(coordination))
            {
                sb.Append("\nCoordination: ");
                sb.Append(coordination);
            }
            return sb.ToString();
        }

        public Task<EngineeringModel?> GetWorkingMemoryAsync(Guid sessionId)
        {
            return _repository.GetAsync(sessionId);
        }

        public async Task AcceptEvidenceAsync(Guid sessionId, EngineeringFact fact)
        {
            var model = await _repository.GetAsync(sessionId).ConfigureAwait(false);
            if (model == null) return;
            model.KnownFacts.Add(fact);
            await _repository.UpdateAsync(model).ConfigureAwait(false);
        }
    }
}
