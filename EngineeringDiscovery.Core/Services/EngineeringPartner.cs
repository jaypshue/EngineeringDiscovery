using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using EngineeringDiscovery.Core.Domain.ProjectState;

namespace EngineeringDiscovery.Core.Services
{
    // Minimal EngineeringPartner implementation that composes existing services.
    // This establishes the abstraction and basic workflow for the conversation-first experience.
    public class EngineeringPartner : IEngineeringPartner, IStructuredConversationPartner
    {
        private readonly IEngineeringModelRepository _repository;
        private readonly IEngineeringConversationService? _conversationService;
        private readonly IEngineeringStateQuery? _stateQuery;
        private readonly IProjectStateService? _projectStateService;
        private readonly IEngineeringConversationCapabilityService? _capabilityService;
        private Guid? _lastSessionId;

        public EngineeringPartner(
            IEngineeringModelRepository repository,
            IEngineeringConversationService? conversationService = null,
            IEngineeringStateQuery? stateQuery = null,
            IProjectStateService? projectStateService = null,
            IEngineeringConversationCapabilityService? capabilityService = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _conversationService = conversationService;
            _stateQuery = stateQuery;
            _projectStateService = projectStateService;
            _capabilityService = capabilityService;
        }

        public Task<EngineeringModel> StartSessionAsync(string openingStatement)
            => StartSessionAsync(openingStatement, reuseExisting: true);

        public async Task<EngineeringModel> StartSessionAsync(string openingStatement, bool reuseExisting)
        {
            // Empty opening statements reuse the prior session only when the caller explicitly
            // permits it. Import uses reuseExisting: false to establish ownership of a new
            // repository conversation while normal workspace navigation preserves history.
            if (reuseExisting && string.IsNullOrWhiteSpace(openingStatement) && _lastSessionId.HasValue)
            {
                var existing = await _repository.GetAsync(_lastSessionId.Value).ConfigureAwait(false);
                if (existing != null)
                {
                    Debug.WriteLine($"[ED-EP7] StartSessionAsync reusing existing session {_lastSessionId.Value}");
                    return existing;
                }
            }

            var model = new EngineeringModel();
            var startTimestamp = DateTime.UtcNow;
            Debug.WriteLine($"[ED-EP7] StartSessionAsync invoked. OpeningStatement='{openingStatement}'. Timestamp={startTimestamp:o}");
            model.OriginalIdea = openingStatement ?? string.Empty;
            model.Status = EngineeringStatus.Discovering;

            // Seed the working memory by recording the opening statement as a KnownFact
            model.KnownFacts.Add(new EngineeringFact { Key = "OpeningStatement", Value = openingStatement ?? string.Empty });

            // Persist a working copy for the session
            await _repository.CreateAsync(model).ConfigureAwait(false);
            _lastSessionId = model.Id;
            Debug.WriteLine($"[ED-EP7] StartSessionAsync: model created with Id={model.Id}");

            // At startup, do NOT invoke the external conversation service. Seed a deterministic greeting so startup is deterministic
            // and does not trigger Product Discovery questions automatically.
            var greeting = _capabilityService is null
                ? "Hello — I'm EngineOS. Tell me about your idea and I'll help you explore it."
                : ComposeContextualGreeting(_capabilityService.GetContext());
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
            // Special-case: if the message indicates a repository was just imported, synthesize an
            // evidence-based repository understanding immediately so the conversation begins from
            // what EngineOS already knows about the codebase.
            try
            {
                if (!string.IsNullOrWhiteSpace(message) && message.StartsWith("Repository imported", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract path from the import message and ensure it's available as a KnownFact
                    // so ComposeRepositoryUnderstanding can use it for folder name resolution
                    var pathFromMessage = message.Replace("Repository imported at ", "", StringComparison.OrdinalIgnoreCase).Trim();
                    if (!string.IsNullOrWhiteSpace(pathFromMessage))
                    {
                        model.KnownFacts.RemoveAll(f => string.Equals(f.Key, "RepositoryPath", StringComparison.OrdinalIgnoreCase));
                        model.KnownFacts.Add(new EngineeringFact { Key = "RepositoryPath", Value = pathFromMessage });
                    }

                    var summary = ComposeRepositoryUnderstanding(model);
                    // Do NOT add a user message for this system-triggered import notification.
                    // Record only the EngineOS reply and persist the augmented working memory.
                    model.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = summary, TimestampUtc = DateTime.UtcNow });
                    // Record the summary as a KnownFact for traceability
                    model.KnownFacts.Add(new EngineeringFact { Key = "InitialRepositoryUnderstanding", Value = summary });
                    await _repository.UpdateAsync(model).ConfigureAwait(false);
                    Debug.WriteLine($"[ED-EP6] Repository understanding produced for session {sessionId}");
                    return summary;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ED-EP6] Exception while composing repository understanding: {ex}");
            }
            // 1) Understand: record the user message and extract simple intents/facts
            model.Conversation.Add(new ConversationEntry { Speaker = "User", Message = message, TimestampUtc = DateTime.UtcNow });

            var intents = AnalyzeMessage(message);
            var newFacts = ExtractFactsFromMessage(message, intents);
            foreach (var f in newFacts)
            {
                model.KnownFacts.Add(f);
            }

            // Inject authoritative engineering state context
            string projectStateContext = string.Empty;
            if (_stateQuery != null)
            {
                try
                {
                    projectStateContext = _stateQuery.GenerateStatusSummary();
                }
                catch
                {
                    // Never let state query failures break chat
                    projectStateContext = string.Empty;
                }
            }

            // Inject project state as a KnownFact so it's visible in the LLM context
            if (!string.IsNullOrWhiteSpace(projectStateContext))
            {
                // Remove any previous project state fact to keep context fresh
                model.KnownFacts.RemoveAll(f => f.Key == "ProjectState");
                model.KnownFacts.Add(new EngineeringFact { Key = "ProjectState", Value = projectStateContext });
            }

            if (_capabilityService is not null)
            {
                try
                {
                    model.KnownFacts.RemoveAll(f => f.Key == "EngineeringContext");
                    model.KnownFacts.Add(new EngineeringFact
                    {
                        Key = "EngineeringContext",
                        Value = _capabilityService.GetContext().ToPromptText()
                    });
                }
                catch
                {
                    // Context projection is additive; a projection failure must not break chat.
                }
            }

            // 2) Remember: persist updated working memory before making recommendations
            await _repository.UpdateAsync(model).ConfigureAwait(false);

            if (_capabilityService is not null)
            {
                try
                {
                    var capability = await _capabilityService.TryHandleAsync(message).ConfigureAwait(false);
                    if (capability is not null)
                    {
                        model.KnownFacts.RemoveAll(f => f.Key == "LastCapabilityAction");
                        model.KnownFacts.Add(new EngineeringFact
                        {
                            Key = "LastCapabilityAction",
                            Value = capability.Capability
                        });
                        model.Conversation.Add(new ConversationEntry
                        {
                            Speaker = "EngineOS",
                            Message = capability.Reply,
                            TimestampUtc = DateTime.UtcNow
                        });
                        await _repository.UpdateAsync(model).ConfigureAwait(false);
                        return capability.Reply;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ED-CAP] Capability handling failed for session {sessionId}: {ex}");
                }
            }

            // --- Conversational Project State Establishment ---
            var stateEstablishmentReply = TryHandleStateEstablishment(model, intents, message);
            if (stateEstablishmentReply != null)
            {
                // State establishment handled the reply; persist and return
                model.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = stateEstablishmentReply, TimestampUtc = DateTime.UtcNow });
                await _repository.UpdateAsync(model).ConfigureAwait(false);
                return stateEstablishmentReply;
            }

            // --- Next Action / Engineering Prompt Generation ---
            var nextActionReply = TryHandleNextActionRequest(model, intents);
            if (nextActionReply != null)
            {
                model.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = nextActionReply, TimestampUtc = DateTime.UtcNow });
                await _repository.UpdateAsync(model).ConfigureAwait(false);
                return nextActionReply;
            }

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
                    // For spike: if configured to use Luna conversation service, forward raw user text via Luna
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
                reply = ComposeFallbackReply(message, recommendation, coordination, GetUserFacingContext(projectStateContext));
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

        public async Task<StructuredConversationResult> SendStructuredReadOnlyMessageAsync(
            Guid sessionId,
            string message,
            CancellationToken cancellationToken = default)
        {
            var classification = ConversationRequestClassifier.Classify(message);
            var existingReply = await SendReadOnlyMessageAsync(sessionId, message, cancellationToken).ConfigureAwait(false);
            var reply = string.IsNullOrWhiteSpace(classification.Reply)
                ? existingReply
                : classification.Reply;

            // The compatibility read-only path records the exchange and never dispatches
            // operations. Replace only its textual boundary reply when the structured
            // classifier has a more useful proposal/unsupported explanation.
            if (!string.Equals(reply, existingReply, StringComparison.Ordinal))
            {
                await ReplaceLatestEngineReplyAsync(sessionId, reply).ConfigureAwait(false);
            }

            return new StructuredConversationResult(
                classification.Kind,
                reply,
                classification.Capability ?? string.Empty,
                classification.ProposedAction,
                classification.Reason,
                classification.Operation);
        }

        private async Task ReplaceLatestEngineReplyAsync(Guid sessionId, string reply)
        {
            var model = await _repository.GetAsync(sessionId).ConfigureAwait(false);
            var latest = model?.Conversation.LastOrDefault(entry =>
                string.Equals(entry.Speaker, "EngineOS", StringComparison.OrdinalIgnoreCase));
            if (latest is null || model is null) return;

            latest.Message = reply;
            await _repository.UpdateAsync(model).ConfigureAwait(false);
        }

        public async Task<string> SendReadOnlyMessageAsync(Guid sessionId, string message, CancellationToken cancellationToken = default)
        {
            var model = await _repository.GetAsync(sessionId).ConfigureAwait(false);
            Debug.WriteLine($"[ED-EP-READONLY] SendReadOnlyMessageAsync called for session {sessionId}. User message: {message}");

            if (model == null)
            {
                return "I couldn't locate the session. Please try starting a new conversation.";
            }

            model.Conversation.Add(new ConversationEntry
            {
                Speaker = "User",
                Message = message,
                TimestampUtc = DateTime.UtcNow
            });

            var projectStateContext = string.Empty;
            if (_stateQuery is not null)
            {
                try
                {
                    projectStateContext = _stateQuery.GenerateStatusSummary();
                }
                catch
                {
                    // A missing projection must not cause the read-only conversation to fabricate context.
                }
            }

            if (!string.IsNullOrWhiteSpace(projectStateContext))
            {
                model.KnownFacts.RemoveAll(f => f.Key == "ProjectState");
                model.KnownFacts.Add(new EngineeringFact { Key = "ProjectState", Value = projectStateContext });
            }

            if (_capabilityService is not null)
            {
                try
                {
                    model.KnownFacts.RemoveAll(f => f.Key == "EngineeringContext");
                    model.KnownFacts.Add(new EngineeringFact
                    {
                        Key = "EngineeringContext",
                        Value = _capabilityService.GetContext().ToPromptText()
                    });
                }
                catch
                {
                    // Keep the transcript usable even when a context projection is unavailable.
                }
            }

            await _repository.UpdateAsync(model).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            EngineeringCapabilityResult? capability = null;
            if (_capabilityService is not null)
            {
                try
                {
                    capability = await _capabilityService.TryHandleReadOnlyAsync(message, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ED-CAP-READONLY] Read-only capability handling failed for session {sessionId}: {ex}");
                }
            }

            var reply = capability?.Reply;
            if (string.IsNullOrWhiteSpace(reply))
            {
                reply = string.IsNullOrWhiteSpace(projectStateContext)
                    ? "I can answer from recorded engineering context, but no context projection is currently available. I will not change state or run tools from this Conversation surface."
                    : $"Current Engineering State: {GetUserFacingContext(projectStateContext)}\n\nThis Conversation surface is read-only for now; no state or engineering operation was changed or run.";
            }

            if (capability is not null)
            {
                model.KnownFacts.RemoveAll(f => f.Key == "LastCapabilityAction");
                model.KnownFacts.Add(new EngineeringFact
                {
                    Key = "LastCapabilityAction",
                    Value = capability.Capability
                });
            }

            model.Conversation.Add(new ConversationEntry
            {
                Speaker = "EngineOS",
                Message = reply,
                TimestampUtc = DateTime.UtcNow
            });
            await _repository.UpdateAsync(model).ConfigureAwait(false);
            return reply;
        }

        private static string ComposeContextualGreeting(EngineeringConversationContext context)
        {
            if (context.Repository == "No repository loaded." && context.CurrentDirection == "No current direction established.")
            {
                return "I'm EngineOS. No repository or current direction is established yet. Tell me what you want to understand or change.";
            }

            return $"I'm EngineOS. The current direction is {context.CurrentDirection}. " +
                   $"The active work is {context.CurrentRound.ToLowerInvariant()}, and the next handoff is {context.NextHandoff.ToLowerInvariant()}. " +
                   "What would you like to examine or change?";
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

            // Project state establishment detection
            if ((m.Contains("we're building") || m.Contains("we are building") || m.Contains("this project is") ||
                 m.Contains("the project is") || m.Contains("our product") || m.Contains("we're developing") ||
                 m.Contains("we are developing") || m.Contains("the product is") || m.Contains("building a") ||
                 m.Contains("developing a") || m.Contains("creating a")) && m.Length > 30)
                intents.Add("EstablishProjectState");

            // Lifecycle/phase detection
            if ((m.Contains("active development") || m.Contains("we're in") || m.Contains("we are in") ||
                 m.Contains("currently focused on") || m.Contains("working on") || m.Contains("current focus") ||
                 m.Contains("mvp") || m.Contains("stabiliz") || m.Contains("maintenance") || m.Contains("paused")) &&
                (m.Contains("phase") || m.Contains("focused") || m.Contains("working on") || m.Contains("status") ||
                 m.Contains("currently") || m.Contains("right now") || m.Contains("active development") ||
                 m.Contains("mvp") || m.Contains("stabiliz") || m.Contains("maintenance")))
                intents.Add("EstablishLifecycle");

            // Confirmation detection (only relevant when pending proposal exists)
            if (m.Length < 80 && (m == "yes" || m == "yes." || m.StartsWith("yes,") || m.StartsWith("yes ") ||
                m.Contains("that's right") || m.Contains("thats right") || m.Contains("correct") ||
                m.Contains("record it") || m.Contains("that is correct") || m.Contains("go ahead") ||
                m.Contains("please record") || m.Contains("sounds right") || m.Contains("that's correct")))
                intents.Add("ConfirmProposal");

            // "Set next focus" — human explicitly specifying an engineering objective to work on next.
            // NOTE: "i want to work on" was removed because it's ambiguous between project selection
            // and engineering focus. Project selection is handled by the workspace/project layer.
            if ((m.Contains("let's work on") || m.Contains("lets work on") || m.Contains("the next task is") ||
                 m.Contains("next we should") || m.Contains("let's focus on") || m.Contains("lets focus on") ||
                 m.Contains("work on this next")) && m.Length > 20)
                intents.Add("SetNextFocus");

            // Next-action / engineering prompt request
            if (m.Contains("what should we do next") || m.Contains("what's the next step") ||
                m.Contains("what should i work on") || m.Contains("prepare the next task") ||
                m.Contains("give me the next prompt") || m.Contains("next engineering task") ||
                m.Contains("what should i send to") || m.Contains("what's next") ||
                m.Contains("generate the next prompt") || m.Contains("next action") ||
                (m.Contains("next") && (m.Contains("step") || m.Contains("task") || m.Contains("prompt") || m.Contains("work"))))
                intents.Add("RequestNextAction");

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

        private string GetUserFacingContext(string fallback)
        {
            if (_capabilityService is null) return fallback;
            try { return _capabilityService.GetContext().ToPromptText(); }
            catch { return fallback; }
        }

        private string ComposeFallbackReply(string message, string recommendation, string coordination, string projectStateContext = "")
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("I have recorded your message and updated my understanding.");
            if (!string.IsNullOrWhiteSpace(projectStateContext))
            {
                sb.Append("\n\nCurrent Engineering State: ");
                sb.Append(projectStateContext);
            }
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

        // Compose an initial repository understanding from available model evidence.
        // This produces a concise, user-friendly summary using InvestigationSummary when available.
        private string ComposeRepositoryUnderstanding(EngineeringModel model)
        {
            // Try to get the investigation from workspace state for a richer summary
            var investigation = _stateQuery?.GetActiveInvestigation();
            if (investigation != null)
            {
                return ComposeRichRepositoryUnderstanding(model, investigation);
            }

            // Fallback: basic summary from known facts only
            return ComposeBasicRepositoryUnderstanding(model);
        }

        private string ComposeRichRepositoryUnderstanding(EngineeringModel model, Domain.Investigation.Investigation investigation)
        {
            // The visual ProjectUnderstanding component now displays the detailed investigation
            // results. The conversation message should be concise — acknowledge the import and
            // direct the user's attention to the understanding section above.
            var folderName = string.Empty;
            var repoPath = model.KnownFacts.LastOrDefault(f => string.Equals(f.Key, "RepositoryPath", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(repoPath))
            {
                try { folderName = System.IO.Path.GetFileName(repoPath.TrimEnd('\\', '/')); } catch { folderName = repoPath; }
            }
            if (string.IsNullOrWhiteSpace(folderName))
            {
                var summary = Domain.Investigation.InvestigationSummary.CreateFrom(investigation);
                folderName = summary.RepositoryName;
            }
            if (string.IsNullOrWhiteSpace(folderName)) folderName = "the repository";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"I've inspected **{folderName}** and built an engineering model of the codebase.");
            sb.AppendLine();
            sb.AppendLine("I've shared what I learned above. What would you like to work on?");
            return sb.ToString();
        }

        private string ComposeBasicRepositoryUnderstanding(EngineeringModel model)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"I've finished understanding the repository.");
            sb.AppendLine();

            var repo = model.KnownFacts.LastOrDefault(f => string.Equals(f.Key, "RepositoryPath", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(repo))
            {
                sb.AppendLine($"My current understanding is based on the repository at: {repo}.");
            }

            sb.AppendLine();
            sb.AppendLine("What would you like to work on?");
            return sb.ToString();
        }

        private static System.Collections.Generic.List<string> DetectTechnologies(Domain.Investigation.Investigation investigation, EngineeringModel model)
        {
            var techs = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // From KnownFacts with Tech: prefix
            foreach (var fact in model.KnownFacts.Where(f => f.Key.StartsWith("Tech:", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(fact.Value)) techs.Add(fact.Value);
            }

            // From type observations: look for project SDK hints
            if (investigation.TypeObservations != null)
            {
                var projects = investigation.TypeObservations
                    .Where(t => !string.IsNullOrWhiteSpace(t.Project))
                    .Select(t => t.Project)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var project in projects)
                {
                    var lower = project.ToLowerInvariant();
                    if (lower.Contains("web")) techs.Add("Blazor Server");
                    if (lower.Contains("api")) techs.Add("ASP.NET API");
                    if (lower.Contains("test") || lower.Contains("tests")) techs.Add("xUnit");
                    if (lower.Contains("wpf")) techs.Add("WPF");
                    if (lower.Contains("desktop")) techs.Add("Desktop");
                }
            }

            // Infer .NET from any type observations existing
            if (investigation.TypeObservations != null && investigation.TypeObservations.Count > 0)
            {
                techs.Add(".NET");
            }

            return techs.ToList();
        }

        private static System.Collections.Generic.List<string> DetectArchitectureLayers(Domain.Investigation.Investigation investigation)
        {
            var layers = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (investigation.TypeObservations != null)
            {
                var projects = investigation.TypeObservations
                    .Where(t => !string.IsNullOrWhiteSpace(t.Project))
                    .Select(t => t.Project)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var project in projects)
                {
                    var lower = project.ToLowerInvariant();
                    if (lower.Contains("core") || lower.Contains("domain")) layers.Add("Core domain layer");
                    else if (lower.Contains("web")) layers.Add("Web layer");
                    else if (lower.Contains("api")) layers.Add("API layer");
                    else if (lower.Contains("test") || lower.Contains("tests")) layers.Add("Test layer");
                    else if (lower.Contains("wpf") || lower.Contains("desktop")) layers.Add("Desktop layer");
                }
            }

            // Also check namespace observations for layer groupings
            if (investigation.NamespaceObservations != null)
            {
                var projectsFromNs = investigation.NamespaceObservations
                    .Where(n => !string.IsNullOrWhiteSpace(n.Project))
                    .Select(n => n.Project)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var project in projectsFromNs)
                {
                    var lower = project.ToLowerInvariant();
                    if (lower.Contains("core") || lower.Contains("domain")) layers.Add("Core domain layer");
                    else if (lower.Contains("web")) layers.Add("Web layer");
                    else if (lower.Contains("api")) layers.Add("API layer");
                    else if (lower.Contains("test") || lower.Contains("tests")) layers.Add("Test layer");
                    else if (lower.Contains("wpf") || lower.Contains("desktop")) layers.Add("Desktop layer");
                }
            }

            return layers.ToList();
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

        // ─── Next Action / Engineering Prompt Generation ────────────────────────────

        /// <summary>
        /// Handles "what should we do next?" and "let's work on X" requests by determining the next action
        /// from project state and generating a worker-neutral implementation prompt.
        /// </summary>
        private string? TryHandleNextActionRequest(EngineeringModel model, string[] intents)
        {
            if (!HasIntent(intents, "RequestNextAction") && !HasIntent(intents, "SetNextFocus")) return null;
            if (_stateQuery == null) return null;

            var projectState = _stateQuery.GetProjectIdentity() != null
                ? (_projectStateService?.GetCurrentState())
                : null;
            var wsContext = _stateQuery.GetWorkspaceContext();

            // ─── Handle explicit "let's work on X" ──────────────────────────────────
            if (HasIntent(intents, "SetNextFocus"))
            {
                var focusText = ExtractExplicitFocus(model.KnownFacts.LastOrDefault(f => f.Key == "LastUserMessage")?.Value ?? string.Empty);
                if (string.IsNullOrWhiteSpace(focusText)) return null;

                if (_projectStateService != null && projectState?.Lifecycle != null)
                {
                    try
                    {
                        _projectStateService.UpdateLifecyclePhase(projectState.Lifecycle.Phase, focusText);
                        projectState = _projectStateService.GetCurrentState();
                    }
                    catch { }
                }

                var (rec, rat, ho) = EngineeringPromptGenerator.DetermineActionForFocus(focusText, projectState);
                return FormatNextActionResponse(rec, rat, ho, projectState);
            }

            // ─── Decision tree: determine what EngineOS knows and what it needs ─────

            // 1. Do we know what project this is?
            bool hasIdentity = projectState?.Identity != null &&
                               !string.IsNullOrWhiteSpace(projectState.Identity.Name) &&
                               projectState.Identity.Name != "Unnamed";

            if (!hasIdentity)
            {
                // Is there a repository connected? If so, use that as context.
                if (wsContext != null && wsContext.HasRepository && wsContext.HasInvestigation && wsContext.DiscoveredTypeCount > 0)
                {
                    return $"I've analyzed the repository ({wsContext.RepositoryName}) and found {wsContext.DiscoveredTypeCount} types across {wsContext.DiscoveredNamespaceCount} namespaces.\n\nBut I don't yet know the bigger picture — what is this project trying to accomplish? A short description will help me recommend engineering work rather than just reporting structure.";
                }
                else if (wsContext != null && wsContext.HasRepository)
                {
                    return $"I see a repository is connected ({wsContext.RepositoryName}), but I haven't discovered its structure yet and I don't know what the project is trying to accomplish.\n\nWould you like me to analyze the repository, or would you prefer to tell me about the project first?";
                }
                else
                {
                    return "I'd like to help figure out what to do next, but I need to understand what we're working on first.\n\nWhat are we building? A short description will let me maintain durable context and recommend engineering work going forward.";
                }
            }

            // 2. Do we have enough engineering context to recommend concrete work?
            bool hasLifecycle = projectState!.Lifecycle != null &&
                                projectState.Lifecycle.Phase != LifecyclePhase.Inception;
            bool hasFocus = hasLifecycle && !string.IsNullOrWhiteSpace(projectState.Lifecycle!.CurrentFocus);

            if (!hasFocus)
            {
                var name = projectState.Identity!.Name;

                // If we have repository/investigation context, mention what we know
                if (wsContext != null && wsContext.HasInvestigation && wsContext.DiscoveredTypeCount > 0)
                {
                    return $"I know this is {name} and I've examined the codebase ({wsContext.DiscoveredTypeCount} types, {wsContext.DiscoveredNamespaceCount} namespaces).\n\nWhat are you trying to accomplish right now? Tell me what you'd like to work on and I'll prepare an implementation task.";
                }
                else
                {
                    return $"I know this is {name}, but I need to understand what you're trying to accomplish right now.\n\nWhat's the current goal — what should we be working on? Once I know, I can generate a specific engineering task.";
                }
            }

            // 3. Full context exists — determine next action and generate prompt
            var (recommendation, rationale, handoff) = EngineeringPromptGenerator.DetermineNextAction(projectState);
            return FormatNextActionResponse(recommendation, rationale, handoff, projectState);
        }

        /// <summary>
        /// Formats the next-action response with recommendation and optional worker-ready prompt.
        /// </summary>
        private static string FormatNextActionResponse(string recommendation, string rationale, HandoffState? handoff, ProjectState? projectState)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## Recommended Next Action");
            sb.AppendLine();
            sb.AppendLine($"**{recommendation}**");
            sb.AppendLine();
            sb.AppendLine($"**Why:** {rationale}");

            if (handoff != null)
            {
                var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, projectState);
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("## Worker-Ready Engineering Prompt");
                sb.AppendLine();
                sb.AppendLine("This prompt is prepared for the external CLI coding-agent workflow. EngineOS has not executed a coding agent.");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(prompt.TrimEnd());
                sb.AppendLine("```");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Extracts the explicit focus from a "let's work on X" or "the next task is X" message.
        /// </summary>
        private static string ExtractExplicitFocus(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return string.Empty;
            var lower = message.ToLowerInvariant();

            var patterns = new[] {
                "let's work on ", "lets work on ", "the next task is ", "next we should ",
                "let's focus on ", "lets focus on ", "work on this next: "
            };

            foreach (var pattern in patterns)
            {
                var idx = lower.IndexOf(pattern);
                if (idx >= 0)
                {
                    var after = message.Substring(idx + pattern.Length).Trim();
                    // Take until end of sentence or full message
                    var endIdx = after.IndexOfAny(new[] { '.', '!', '\n' });
                    var result = endIdx > 0 ? after.Substring(0, endIdx).Trim() : after.Trim();
                    if (result.Length > 5) return result;
                }
            }

            return string.Empty;
        }

        // ─── Conversational State Establishment ─────────────────────────────────────

        private static bool HasIntent(string[] intents, string intent) =>
            intents != null && Array.IndexOf(intents, intent) >= 0;

        /// <summary>
        /// Handles the proposal/confirmation flow for establishing durable ProjectState from conversation.
        /// Returns a reply string if this method handled the message; null if normal flow should continue.
        /// </summary>
        private string? TryHandleStateEstablishment(EngineeringModel model, string[] intents, string message)
        {
            if (_projectStateService == null || _stateQuery == null) return null;

            // --- Handle confirmation of a pending proposal ---
            if (HasIntent(intents, "ConfirmProposal"))
            {
                // Check for pending project identity proposal
                var pendingIdentity = model.KnownFacts.Find(f => f.Key == "ProposedProjectIdentity");
                if (pendingIdentity != null)
                {
                    // Parse and persist
                    var identity = ParseProposedIdentity(pendingIdentity.Value);
                    if (identity != null)
                    {
                        try
                        {
                            _projectStateService.SetProjectIdentity(identity);
                            model.KnownFacts.RemoveAll(f => f.Key == "ProposedProjectIdentity");
                            return $"Recorded. The project identity is now established as: {identity.Name}. {identity.Description}";
                        }
                        catch (Exception ex)
                        {
                            return $"I wasn't able to record the project identity: {ex.Message}";
                        }
                    }
                }

                // Check for pending lifecycle proposal
                var pendingLifecycle = model.KnownFacts.Find(f => f.Key == "ProposedLifecyclePhase");
                if (pendingLifecycle != null)
                {
                    var (phase, focus) = ParseProposedLifecycle(pendingLifecycle.Value);
                    try
                    {
                        _projectStateService.UpdateLifecyclePhase(phase, focus);
                        model.KnownFacts.RemoveAll(f => f.Key == "ProposedLifecyclePhase");
                        return $"Recorded. The project lifecycle is now: {phase}, focused on: {focus}";
                    }
                    catch (Exception ex)
                    {
                        return $"I wasn't able to record the lifecycle phase: {ex.Message}";
                    }
                }

                // No pending proposal — confirmation intent is irrelevant, fall through to normal flow
                return null;
            }

            // --- Handle project identity establishment ---
            if (HasIntent(intents, "EstablishProjectState"))
            {
                // Only propose if ProjectIdentity doesn't already exist
                var existingIdentity = _stateQuery.GetProjectIdentity();
                if (existingIdentity == null || string.IsNullOrWhiteSpace(existingIdentity.Name))
                {
                    // Don't propose if there's already a pending proposal
                    if (model.KnownFacts.Exists(f => f.Key == "ProposedProjectIdentity")) return null;

                    var proposed = ExtractProjectIdentityFromMessage(message);
                    if (proposed != null)
                    {
                        // Stage the proposal as a KnownFact (delimited representation)
                        var proposalValue = $"{proposed.Name}|||{proposed.Description}|||{proposed.ProductVision}";
                        model.KnownFacts.Add(new EngineeringFact { Key = "ProposedProjectIdentity", Value = proposalValue });

                        var summary = !string.IsNullOrWhiteSpace(proposed.Name) && proposed.Name != "Unnamed"
                            ? $"I understand the project as: \"{proposed.Name}\" — {proposed.Description}"
                            : $"I understand the project as: {proposed.Description}";

                        return $"{summary}\n\nShould I record this as the project identity?";
                    }
                }
            }

            // --- Handle lifecycle establishment ---
            if (HasIntent(intents, "EstablishLifecycle") && !HasIntent(intents, "RequestNextAction") && !HasIntent(intents, "SetNextFocus"))
            {
                // Only propose if no pending lifecycle proposal exists
                if (model.KnownFacts.Exists(f => f.Key == "ProposedLifecyclePhase")) return null;

                var (phase, focus) = ExtractLifecycleFromMessage(message);
                if (phase != LifecyclePhase.Inception || !string.IsNullOrWhiteSpace(focus))
                {
                    var proposalValue = $"{phase}|||{focus}";
                    model.KnownFacts.Add(new EngineeringFact { Key = "ProposedLifecyclePhase", Value = proposalValue });

                    return $"Should I record the project lifecycle as {phase}" +
                           (string.IsNullOrWhiteSpace(focus) ? "?" : $", focused on: {focus}?");
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts a proposed ProjectIdentity from a user's descriptive message.
        /// Uses simple heuristics (not LLM-dependent).
        /// </summary>
        private ProjectIdentity? ExtractProjectIdentityFromMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Length < 20) return null;

            var identity = new ProjectIdentity();

            // Try to extract a project name — look for patterns like "building X" or "X is"
            var m = message.Trim();
            var lower = m.ToLowerInvariant();

            // Pattern: "We're building [ProjectName] as/to/that..."
            string? name = null;
            var buildingPatterns = new[] { "we're building ", "we are building ", "building a ", "developing a ", "creating a " };
            foreach (var pattern in buildingPatterns)
            {
                var idx = lower.IndexOf(pattern);
                if (idx >= 0)
                {
                    var after = m.Substring(idx + pattern.Length).Trim();
                    // Take until a common delimiter: "as", "to", "that", "which", comma, period
                    var endIdx = FindNameEnd(after.ToLowerInvariant());
                    if (endIdx > 0 && endIdx <= 60)
                    {
                        name = after.Substring(0, endIdx).Trim().TrimEnd(',', '.', ';');
                    }
                    else if (after.Length <= 60)
                    {
                        name = after.TrimEnd(',', '.', ';');
                    }
                    break;
                }
            }

            // Pattern: "This project is [name]" or "The project is [name]"
            if (name == null)
            {
                var projectIsPatterns = new[] { "this project is ", "the project is ", "our product is ", "the product is " };
                foreach (var pattern in projectIsPatterns)
                {
                    var idx = lower.IndexOf(pattern);
                    if (idx >= 0)
                    {
                        var after = m.Substring(idx + pattern.Length).Trim();
                        var endIdx = FindNameEnd(after.ToLowerInvariant());
                        if (endIdx > 0 && endIdx <= 60)
                        {
                            name = after.Substring(0, endIdx).Trim().TrimEnd(',', '.', ';');
                        }
                        else if (after.Length <= 60)
                        {
                            name = after.TrimEnd(',', '.', ';');
                        }
                        break;
                    }
                }
            }

            // Use the extracted name or mark as unnamed
            if (!string.IsNullOrWhiteSpace(name))
            {
                // If the name is very long, it's likely a description, not a name
                if (name.Length > 40)
                {
                    identity.Name = "Unnamed";
                    identity.Description = name;
                }
                else
                {
                    identity.Name = name;
                    // Use the full message as description
                    identity.Description = m;
                }
            }
            else
            {
                identity.Name = "Unnamed";
                identity.Description = m;
            }

            // Use the full message as ProductVision if it contains vision-like language
            if (lower.Contains("coordinate") || lower.Contains("help") || lower.Contains("enable") ||
                lower.Contains("provide") || lower.Contains("allow") || lower.Contains("understand"))
            {
                identity.ProductVision = m;
            }
            else
            {
                identity.ProductVision = string.Empty;
            }

            return identity;
        }

        private static int FindNameEnd(string lower)
        {
            var delimiters = new[] { " as ", " to ", " that ", " which ", " for ", ". ", ", " };
            var minIdx = int.MaxValue;
            foreach (var d in delimiters)
            {
                var idx = lower.IndexOf(d);
                if (idx >= 0 && idx < minIdx) minIdx = idx;
            }
            return minIdx == int.MaxValue ? -1 : minIdx;
        }

        /// <summary>
        /// Extracts lifecycle phase and focus from a user message.
        /// </summary>
        private (LifecyclePhase phase, string focus) ExtractLifecycleFromMessage(string message)
        {
            var lower = message.ToLowerInvariant();
            var phase = LifecyclePhase.Inception;
            var focus = string.Empty;

            // Detect phase
            if (lower.Contains("active development") || lower.Contains("actively developing"))
                phase = LifecyclePhase.ActiveDevelopment;
            else if (lower.Contains("stabiliz") || lower.Contains("mvp+") || lower.Contains("polish"))
                phase = LifecyclePhase.Stabilization;
            else if (lower.Contains("maintenance") || lower.Contains("maintaining"))
                phase = LifecyclePhase.Maintenance;
            else if (lower.Contains("paused") || lower.Contains("on hold"))
                phase = LifecyclePhase.Paused;
            else if (lower.Contains("mvp") && !lower.Contains("mvp+"))
                phase = LifecyclePhase.ActiveDevelopment; // MVP implies active dev
            else if (lower.Contains("inception") || lower.Contains("starting") || lower.Contains("beginning"))
                phase = LifecyclePhase.Inception;

            // Extract focus — look for "focused on", "working on", "current focus"
            var focusPatterns = new[] { "focused on ", "working on ", "current focus is ", "focus on ", "focus: " };
            foreach (var pattern in focusPatterns)
            {
                var idx = lower.IndexOf(pattern);
                if (idx >= 0)
                {
                    var after = message.Substring(idx + pattern.Length).Trim();
                    // Take until end of sentence
                    var endIdx = after.IndexOfAny(new[] { '.', '!', '\n' });
                    focus = endIdx > 0 ? after.Substring(0, endIdx).Trim() : after.Trim();
                    break;
                }
            }

            // If no explicit focus pattern found, try to use the descriptive part after the phase keyword
            if (string.IsNullOrWhiteSpace(focus))
            {
                var afterPhrase = new[] { "active development ", "stabilizing ", "maintaining " };
                foreach (var pattern in afterPhrase)
                {
                    var idx = lower.IndexOf(pattern);
                    if (idx >= 0)
                    {
                        var after = message.Substring(idx + pattern.Length).Trim();
                        var endIdx = after.IndexOfAny(new[] { '.', '!', '\n' });
                        var candidate = endIdx > 0 ? after.Substring(0, endIdx).Trim() : after.Trim();
                        if (candidate.Length > 3 && candidate.Length < 200)
                        {
                            focus = candidate;
                        }
                        break;
                    }
                }
            }

            return (phase, focus);
        }

        private ProjectIdentity? ParseProposedIdentity(string proposalValue)
        {
            if (string.IsNullOrWhiteSpace(proposalValue)) return null;
            var parts = proposalValue.Split(new[] { "|||" }, StringSplitOptions.None);
            if (parts.Length < 2) return null;
            return new ProjectIdentity
            {
                Name = parts[0],
                Description = parts.Length > 1 ? parts[1] : string.Empty,
                ProductVision = parts.Length > 2 ? parts[2] : string.Empty
            };
        }

        private (LifecyclePhase phase, string focus) ParseProposedLifecycle(string proposalValue)
        {
            if (string.IsNullOrWhiteSpace(proposalValue)) return (LifecyclePhase.Inception, string.Empty);
            var parts = proposalValue.Split(new[] { "|||" }, StringSplitOptions.None);
            var phase = LifecyclePhase.Inception;
            if (parts.Length > 0 && Enum.TryParse<LifecyclePhase>(parts[0], out var parsed))
            {
                phase = parsed;
            }
            var focus = parts.Length > 1 ? parts[1] : string.Empty;
            return (phase, focus);
        }
    }
}
