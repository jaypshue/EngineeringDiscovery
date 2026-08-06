using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Core.Services
{
    public class EnginerringConversationOrchestrator : IEnginerringConversationOrchestrator
    {
        private readonly IEngineeringModelRepository _repository;
        private readonly IEngineeringConversationService? _conversationService;

        public EnginerringConversationOrchestrator(IEngineeringModelRepository repository, IEngineeringConversationService? conversationService = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _conversationService = conversationService;
        }

        public async Task<EngineeringModel> CreateModelAsync(string idea)
        {
            var model = new EngineeringModel();
            model.OriginalIdea = idea ?? string.Empty;
            model.Status = EngineeringStatus.Discovering;
            // Seed explicit product discovery objectives (ED-310)
            var productVision = new DiscoveryObjective { Name = "Product Vision", Type = ObjectiveType.Product, IsRequired = true };
            var targetUsers = new DiscoveryObjective { Name = "Target Users", Type = ObjectiveType.Product, IsRequired = true };
            var coreWorkflow = new DiscoveryObjective { Name = "Core Workflow", Type = ObjectiveType.Product, IsRequired = true };
            var primaryPlatform = new DiscoveryObjective { Name = "Primary Platform", Type = ObjectiveType.Product, IsRequired = true };
            var majorConstraints = new DiscoveryObjective { Name = "Major Constraints", Type = ObjectiveType.Product, IsRequired = true };

            model.DiscoveryObjectives.Add(productVision);
            model.DiscoveryObjectives.Add(targetUsers);
            model.DiscoveryObjectives.Add(coreWorkflow);
            model.DiscoveryObjectives.Add(primaryPlatform);
            model.DiscoveryObjectives.Add(majorConstraints);

            // Seed default questions mapped to the objectives
            model.OpenQuestions.Add(new EngineeringQuestion { Question = "Please describe the product in one sentence.", Reason = "Establish product vision", Priority = 1, Objective = productVision.Name });
            model.OpenQuestions.Add(new EngineeringQuestion { Question = "Who are the target users of this product?", Reason = "Identify target users", Priority = 2, Objective = targetUsers.Name });
            model.OpenQuestions.Add(new EngineeringQuestion { Question = "Describe the primary workflow or scenario for the product.", Reason = "Understand core workflow", Priority = 3, Objective = coreWorkflow.Name });
            model.OpenQuestions.Add(new EngineeringQuestion { Question = "Which platforms should this product run on (mobile/desktop/web)?", Reason = "Determine primary platform", Priority = 4, Objective = primaryPlatform.Name });
            model.OpenQuestions.Add(new EngineeringQuestion { Question = "Are there known constraints or limitations we must consider?", Reason = "Identify major constraints", Priority = 5, Objective = majorConstraints.Name });

            // Seed discovery categories projection mapping to the seeded questions where appropriate
            model.DiscoveryCategories.Add(new DiscoveryCategory { Name = "Primary User", ExpectedQuestion = "Who will use this product?" });
            model.DiscoveryCategories.Add(new DiscoveryCategory { Name = "Problem Statement", ExpectedQuestion = "What problem are they trying to solve?" });
            model.DiscoveryCategories.Add(new DiscoveryCategory { Name = "Desired Outcome", ExpectedQuestion = "What outcome are they expecting?" });
            model.DiscoveryCategories.Add(new DiscoveryCategory { Name = "Primary Workflow", ExpectedQuestion = "When will they use it?" });
            model.DiscoveryCategories.Add(new DiscoveryCategory { Name = "Constraints", ExpectedQuestion = "Why is the current approach insufficient?" });
            model.DiscoveryCategories.Add(new DiscoveryCategory { Name = "Inputs", ExpectedQuestion = string.Empty });
            model.DiscoveryCategories.Add(new DiscoveryCategory { Name = "Outputs", ExpectedQuestion = string.Empty });
            model.DiscoveryCategories.Add(new DiscoveryCategory { Name = "External Integrations", ExpectedQuestion = string.Empty });
            model.DiscoveryCategories.Add(new DiscoveryCategory { Name = "Success Criteria", ExpectedQuestion = string.Empty });

            // Evaluate initial readiness (likely 0)
            ReevaluateDiscovery(model);

            await _repository.CreateAsync(model).ConfigureAwait(false);
            return model;
        }

        public Task<EngineeringModel?> GetModelAsync(Guid id)
        {
            return _repository.GetAsync(id);
        }

        public async Task<EngineeringQuestion?> RespondAsync(Guid modelId)
        {
            var callTs = DateTime.UtcNow;
            Debug.WriteLine($"[ED-EP7] Orchestrator.RespondAsync called for model {modelId} at {callTs:o}");
            var model = await _repository.GetAsync(modelId).ConfigureAwait(false);
            if (model == null) return null;
            // Determine discovery state
            var state = await GetDiscoveryStateAsync(modelId).ConfigureAwait(false);
            if (state == DiscoveryState.Ready)
            {
                // No more questions
                return null;
            }

            // Prefer the most incomplete Objective (Product objectives must complete before Engineering objectives are started)
            var productObjectivesIncomplete = model.DiscoveryObjectives.Where(o => o.Type == ObjectiveType.Product && o.Status != ObjectiveStatus.Complete).ToList();
            DiscoveryObjective? activeObjective = null;
            if (productObjectivesIncomplete.Any())
            {
                activeObjective = productObjectivesIncomplete.OrderBy(o => o.Status == ObjectiveStatus.NotStarted ? 0 : (o.Status == ObjectiveStatus.Active ? 1 : 2)).FirstOrDefault();
            }
            else
            {
                // Move to engineering objectives
                var engObjectivesIncomplete = model.DiscoveryObjectives.Where(o => o.Type == ObjectiveType.Engineering && o.Status != ObjectiveStatus.Complete && o.Status != ObjectiveStatus.Deferred).ToList();
                activeObjective = engObjectivesIncomplete.OrderBy(o => o.Status == ObjectiveStatus.NotStarted ? 0 : (o.Status == ObjectiveStatus.Active ? 1 : 2)).FirstOrDefault();
            }

            // If there's no active objective but some are deferred, consider discovery ready if all remaining are deferred
            if (activeObjective == null)
            {
                var remaining = model.DiscoveryObjectives.Count(o => o.Status != ObjectiveStatus.Complete && o.Status != ObjectiveStatus.Deferred);
                if (remaining == 0)
                {
                    // All outstanding objectives are complete or deferred
                    return null;
                }
            }

            // If an external conversation service is available, request a question focused on the active objective and the next missing required fact
            if (_conversationService != null && activeObjective != null)
            {
                // Determine the next missing required fact for this objective and persist focus
                var missingRequiredFact = activeObjective.RequiredFacts?
                    .FirstOrDefault(rf => !model.KnownFacts.Any(kf =>
                        string.Equals(kf.Key, rf, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(kf.Value, rf, StringComparison.OrdinalIgnoreCase)));
                activeObjective.LastAskedFact = missingRequiredFact ?? string.Empty;

                // Build a minimalist augmented model that includes only the active objective context
                var augmented = CreateAugmentedModelWithFocusAndObjective(model, activeObjective);

                // Ask AI for a single candidate question focused strictly on the active objective
                Debug.WriteLine($"[ED-EP7] Orchestrator calling conversationService.RespondAsync for model {modelId}");
                var responseText = await _conversationService.RespondAsync(augmented).ConfigureAwait(false);
                Debug.WriteLine($"[ED-EP7] Orchestrator received response for model {modelId}: '{responseText ?? "(null)"}'");
                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    // Reject if it repeats recently
                    if (IsRecentResponse(model, responseText))
                    {
                        model.Conversation.Add(new ConversationEntry { Speaker = "Orchestrator", Message = $"Rejected AI response as repetitive: {responseText}", TimestampUtc = DateTime.UtcNow });
                    }
                    else
                    {
                        // Accept and construct an EngineeringQuestion associated with the active objective
                        var eq = new EngineeringQuestion { Question = responseText, Reason = "AI generated", Priority = int.MaxValue, Objective = activeObjective.Name, TargetCategory = string.Empty };

                        // Mark objective active and set current focus
                        activeObjective.Status = ObjectiveStatus.Active;
                        model.CurrentFocus = activeObjective.Name;
                        model.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = responseText, TimestampUtc = DateTime.UtcNow });
                        await _repository.UpdateAsync(model).ConfigureAwait(false);
                        return eq;
                    }
                }
            }

            // Fallback deterministic rule: return the highest priority open question (lowest Priority value)
            // Fallback deterministic rule: return the highest priority open question that aligns to the active objective if any
            EngineeringQuestion? fallback = null;
            if (activeObjective != null)
            {
                fallback = model.OpenQuestions.OrderBy(qi => qi.Priority).FirstOrDefault(qi => string.Equals(qi.Objective, activeObjective.Name, StringComparison.OrdinalIgnoreCase));
            }
            if (fallback == null) fallback = model.OpenQuestions.OrderBy(qi => qi.Priority).FirstOrDefault();
            if (fallback == null) fallback = model.OpenQuestions.OrderBy(qi => qi.Priority).FirstOrDefault();

            if (fallback != null)
            {
                model.CurrentFocus = fallback.Objective ?? fallback.Reason;
                // If fallback aligns to an objective, mark that objective active
                var aligned = model.DiscoveryObjectives.FirstOrDefault(o => string.Equals(o.Name, fallback.Objective, StringComparison.OrdinalIgnoreCase));
                if (aligned != null) aligned.Status = ObjectiveStatus.Active;
                await _repository.UpdateAsync(model).ConfigureAwait(false);
            }

            return fallback;
        }

        private static (string objective, string targetCategory) InferObjectiveAndCategory(string text, EngineeringModel model, string? preferredCategory)
        {
            if (string.IsNullOrWhiteSpace(text)) return (string.Empty, string.Empty);
            var s = text.ToLowerInvariant();

            // Look for explicit "objective:" or "objective -" markers
            var idx = s.IndexOf("objective:");
            if (idx >= 0)
            {
                var part = text.Substring(idx + "objective:".Length).Trim();
                var upto = part.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                return (upto, preferredCategory ?? string.Empty);
            }

            // Map simple keywords to objectives
            var objectives = new Dictionary<string, string>
            {
                { "who will use", "Understand target users" },
                { "what problem", "Clarify product problem" },
                { "what outcome", "Discover success criteria" },
                { "when will", "Understand primary workflow" },
                { "why is", "Identify constraints" },
                { "deploy", "Clarify deployment requirements" },
                { "security", "Clarify security requirements" },
                { "performance", "Understand performance expectations" },
                { "integrat", "Understand integrations" },
                { "architecture", "Reduce architecture uncertainty" }
            };

            foreach (var kv in objectives)
            {
                if (s.Contains(kv.Key))
                {
                    // try to map to a category by searching existing category names
                    var cat = model.DiscoveryCategories.FirstOrDefault(c => c.Name.ToLowerInvariant().Contains(kv.Key.Split(' ')[0]));
                    return (kv.Value, cat?.Name ?? preferredCategory ?? string.Empty);
                }
            }

            // Heuristic: prefer the preferredCategory passed in
            if (!string.IsNullOrWhiteSpace(preferredCategory)) return ("Clarify: " + preferredCategory, preferredCategory);

            // Fallback: no objective
            return (string.Empty, string.Empty);
        }

        private static bool QuestionTargetsCompleted(EngineeringModel model, string targetCategory)
        {
            if (string.IsNullOrWhiteSpace(targetCategory)) return false;
            var cat = model.DiscoveryCategories.FirstOrDefault(c => string.Equals(c.Name, targetCategory, StringComparison.OrdinalIgnoreCase));
            if (cat == null) return false;
            return cat.Status == DiscoveryStatus.Complete || cat.Confidence >= 85.0;
        }

        private static bool IsRecentResponse(EngineeringModel model, string text)
        {
            if (model == null) return false;
            var recent = model.Conversation?.Where(c => c.Speaker == "EngineOS" || c.Speaker == "Orchestrator").OrderByDescending(c => c.TimestampUtc).Take(8) ?? Enumerable.Empty<ConversationEntry>();
            var lower = text?.ToLowerInvariant() ?? string.Empty;
            foreach (var r in recent)
            {
                if (string.IsNullOrWhiteSpace(r.Message)) continue;
                var m = r.Message.ToLowerInvariant();
                if (m.Contains(lower) || lower.Contains(m) || GetSimpleSimilarity(m, lower) >= 0.7) return true;
            }
            return false;
        }

        // Very simple similarity: shared token ratio
        private static double GetSimpleSimilarity(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0.0;
            var sa = a.Split(new[] { ' ', '.', ',', '?', '!' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 2).Distinct();
            var sb = b.Split(new[] { ' ', '.', ',', '?', '!' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 2).Distinct();
            var inter = sa.Intersect(sb).Count();
            var total = Math.Max(sa.Count(), sb.Count());
            if (total == 0) return 0.0;
            return (double)inter / total;
        }

        private EngineeringModel CreateAugmentedModelWithFocus(EngineeringModel model)
        {
            var copy = new EngineeringModel
            {
                Id = model.Id,
                OriginalIdea = model.OriginalIdea,
                Status = model.Status,
                Confidence = model.Confidence
            };

            foreach (var f in model.KnownFacts)
            {
                copy.KnownFacts.Add(new EngineeringFact { Key = f.Key, Value = f.Value });
            }

            foreach (var q in model.OpenQuestions)
            {
                copy.OpenQuestions.Add(new EngineeringQuestion { Question = q.Question, Reason = q.Reason, Priority = q.Priority });
            }

            foreach (var c in model.Conversation)
            {
                copy.Conversation.Add(new ConversationEntry { Speaker = c.Speaker, Message = c.Message, TimestampUtc = c.TimestampUtc });
            }

            foreach (var dc in model.DiscoveryCategories)
            {
                copy.DiscoveryCategories.Add(new DiscoveryCategory { Name = dc.Name, ExpectedQuestion = dc.ExpectedQuestion });
            }

            // Copy discovery objectives minimally so AI sees which objective is active
            foreach (var o in model.DiscoveryObjectives)
            {
                copy.DiscoveryObjectives.Add(new DiscoveryObjective { Id = o.Id, Name = o.Name, Status = o.Status, IsRequired = o.IsRequired, Type = o.Type, LastAskedFact = o.LastAskedFact });
            }

            // Prefer the weakest category (Unknown first, then lowest confidence)
            var focusCat = model.DiscoveryCategories
                .OrderBy(c => c.Status == DiscoveryStatus.Unknown ? 0 : (c.Status == DiscoveryStatus.Partial ? 1 : 2))
                .ThenBy(c => c.Confidence)
                .FirstOrDefault();

            string focusMsg;
            if (focusCat != null)
            {
                focusMsg = $"Focus: {focusCat.Name}. Status: {focusCat.Status}. Confidence: {focusCat.Confidence:0.##}. KnownFacts: {model.KnownFacts.Count}.";
            }
            else
            {
                focusMsg = "Focus: General discovery. No single low-confidence category identified.";
            }

            // Add a lightweight orchestrator hint as a conversation entry to guide the AI service
            copy.Conversation.Add(new ConversationEntry { Speaker = "Orchestrator", Message = focusMsg, TimestampUtc = DateTime.UtcNow });

            // Also add a succinct EngineOS message describing the immediate question focus
            var topQuestion = model.OpenQuestions.OrderBy(qi => qi.Priority).FirstOrDefault();
            string engineMsg;
            if (topQuestion != null)
            {
                engineMsg = $"Focus: {topQuestion.Question} (reason: {topQuestion.Reason}). KnownFacts: {model.KnownFacts.Count}. Confidence: {model.Confidence:0.##}";
            }
            else
            {
                engineMsg = $"Focus: general clarification. KnownFacts: {model.KnownFacts.Count}. Confidence: {model.Confidence:0.##}";
            }

            copy.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = engineMsg, TimestampUtc = DateTime.UtcNow });

            return copy;
        }

        private EngineeringModel CreateAugmentedModelWithFocusAndObjective(EngineeringModel model, DiscoveryObjective objective)
        {
            var copy = CreateAugmentedModelWithFocus(model);
            // Add a clear instruction about the active objective and its required facts
            var sb = new StringBuilder();
            sb.AppendLine($"Active Objective: {objective.Name}");
            if (!string.IsNullOrWhiteSpace(objective.LastAskedFact))
            {
                sb.AppendLine($"Missing fact: {objective.LastAskedFact}");
                sb.AppendLine($"Ask specifically about this missing required fact: {objective.LastAskedFact}");
            }
            else if (objective.RequiredFacts != null && objective.RequiredFacts.Count > 0)
            {
                sb.AppendLine("Required facts:");
                foreach (var rf in objective.RequiredFacts)
                {
                    sb.AppendLine($"- {rf}");
                }
            }

            copy.Conversation.Add(new ConversationEntry { Speaker = "Orchestrator", Message = sb.ToString(), TimestampUtc = DateTime.UtcNow });
            return copy;
        }

        public async Task SubmitAnswerAsync(Guid modelId, Guid questionId, string answer)
        {
            var model = await _repository.GetAsync(modelId).ConfigureAwait(false);
            if (model is null) return;

            // Record the user's message
            model.Conversation.Add(new ConversationEntry { Speaker = "User", Message = answer, TimestampUtc = DateTime.UtcNow });

            // Find and remove the answered open question
            var q = model.OpenQuestions.FirstOrDefault(x => x.Id == questionId);
            if (q != null) model.OpenQuestions.Remove(q);

            // Simple classification heuristics
            var classification = ClassifyAnswer(answer);

            switch (classification)
            {
                case "Deferred":
                    // Record a deferred marker as a fact and mark related category low confidence
                    model.KnownFacts.Add(new EngineeringFact { Key = "Deferred", Value = q?.Question ?? "unspecified" });
                    if (q != null)
                    {
                        var cat = model.DiscoveryCategories.FirstOrDefault(c => string.Equals(c.ExpectedQuestion, q.Question, StringComparison.OrdinalIgnoreCase));
                        if (cat != null)
                        {
                            cat.Status = DiscoveryStatus.Partial;
                            cat.Confidence = Math.Min(30.0, cat.Confidence);
                        }
                    }
                    break;
                case "Unknown":
                    // Mark category as unknown/low confidence
                    model.KnownFacts.Add(new EngineeringFact { Key = "Unknown", Value = q?.Question ?? "unspecified" });
                    if (q != null)
                    {
                        var cat = model.DiscoveryCategories.FirstOrDefault(c => string.Equals(c.ExpectedQuestion, q.Question, StringComparison.OrdinalIgnoreCase));
                        if (cat != null)
                        {
                            cat.Status = DiscoveryStatus.Unknown;
                            cat.Confidence = Math.Min(10.0, cat.Confidence);
                        }
                    }
                    break;
                case "UserQuestion":
                    // Treat as a user question; add a conversational placeholder and re-add the original question for clarity
                    model.Conversation.Add(new ConversationEntry { Speaker = "System", Message = "Note: user asked a question; orchestrator will not record as fact.", TimestampUtc = DateTime.UtcNow });
                    if (q != null) model.OpenQuestions.Insert(0, q);
                    break;
                case "Correction":
                    // Add as a corrective fact and increase confidence slightly for the related category
                    model.KnownFacts.Add(new EngineeringFact { Key = "Correction", Value = answer });
                    if (q != null)
                    {
                        var cat = model.DiscoveryCategories.FirstOrDefault(c => string.Equals(c.ExpectedQuestion, q.Question, StringComparison.OrdinalIgnoreCase));
                        if (cat != null)
                        {
                            cat.Confidence = Math.Min(100.0, cat.Confidence + 20.0);
                            cat.Status = cat.Confidence > 70 ? DiscoveryStatus.Complete : DiscoveryStatus.Partial;
                        }
                    }
                    break;
                default:
                    // Treat as Engineering Fact
                    var factKey = q?.Question ?? $"Fact-{model.KnownFacts.Count + 1}";
                    model.KnownFacts.Add(new EngineeringFact { Key = factKey, Value = answer });

                    // Map to discovery category if expected question matches
                    if (q != null)
                    {
                        var cat = model.DiscoveryCategories.FirstOrDefault(c => string.Equals(c.ExpectedQuestion, q.Question, StringComparison.OrdinalIgnoreCase));
                        if (cat != null)
                        {
                            cat.SupportingFacts.Add(new EngineeringFact { Key = factKey, Value = answer });
                            // bump confidence
                            cat.Confidence = Math.Min(100.0, cat.Confidence + 30.0);
                            cat.Status = cat.Confidence > 70 ? DiscoveryStatus.Complete : DiscoveryStatus.Partial;
                        }
                    }
                    break;
            }

            // Update conversation projection and reevaluate readiness
            ReevaluateDiscovery(model);

            // Persist
            await _repository.UpdateAsync(model).ConfigureAwait(false);
        }

        private string ClassifyAnswer(string answer)
        {
            if (string.IsNullOrWhiteSpace(answer)) return "Unknown";
            var s = answer.Trim().ToLowerInvariant();

            // Deferred heuristics
            if ((s.Contains("later") || s.Contains("not now") || s.Contains("i'll") || s.Contains("i will") || s.Contains("i don't know") || s.Contains("dont know")) && s.Length < 120)
            {
                // short negative replies like "I don't know" -> Unknown
                if (s.Contains("don't") || s.Contains("do not") || s.Contains("dont")) return "Unknown";
                return "Deferred";
            }

            // Unknown heuristics
            if (s.Contains("don't know") || s.Contains("dont know") || s.Contains("not sure") || s.Contains("unsure") || s.Contains("i'm not sure")) return "Unknown";

            // User asking a question
            if (s.EndsWith("?") || s.StartsWith("what ") || s.StartsWith("how ") || s.StartsWith("why ") || s.StartsWith("when ") || s.StartsWith("who ")) return "UserQuestion";

            // Correction heuristics
            if (s.StartsWith("actually") || s.StartsWith("no ") || s.Contains("instead") || s.Contains("correction")) return "Correction";

            return "Fact";
        }

        private void ReevaluateDiscovery(EngineeringModel model)
        {
            if (model.DiscoveryCategories == null) return;

            foreach (var cat in model.DiscoveryCategories)
            {
                cat.SupportingFacts.Clear();

                foreach (var kf in model.KnownFacts)
                {
                    if (!string.IsNullOrWhiteSpace(cat.ExpectedQuestion) && kf.Key.Equals(cat.ExpectedQuestion, StringComparison.OrdinalIgnoreCase))
                    {
                        cat.SupportingFacts.Add(kf);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(cat.Name) && kf.Key.IndexOf(cat.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        cat.SupportingFacts.Add(kf);
                        continue;
                    }
                }

                if (cat.SupportingFacts.Count == 0)
                {
                    cat.Status = DiscoveryStatus.Unknown;
                    cat.Confidence = Math.Max(0.0, cat.Confidence * 0.6);
                }
                else
                {
                    // Score based on supporting facts count and length
                    var score = Math.Min(100.0, cat.SupportingFacts.Count * 30.0);
                    cat.Confidence = Math.Max(cat.Confidence * 0.6, score);
                    cat.Status = cat.Confidence >= 70 ? DiscoveryStatus.Complete : DiscoveryStatus.Partial;
                }
            }

            // Overall readiness is average of category confidences
            if (model.DiscoveryCategories.Count > 0)
            {
                model.OverallDiscoveryReadiness = model.DiscoveryCategories.Average(c => c.Confidence);
            }
            else
            {
                model.OverallDiscoveryReadiness = 0.0;
            }

            // Update model status
            if (model.OverallDiscoveryReadiness >= 75.0)
            {
                model.Status = EngineeringStatus.EngineeringModelReady;
            }
            else
            {
                model.Status = EngineeringStatus.Discovering;
            }
        }

        public Task<bool> IsDiscoveryReadyAsync(Guid modelId)
        {
            return Task.Run(async () =>
            {
                var m = await _repository.GetAsync(modelId).ConfigureAwait(false);
                if (m is null) return false;

                // If explicit discovery objectives exist, discovery is ready when all required objectives are Complete or Deferred
                if (m.DiscoveryObjectives != null && m.DiscoveryObjectives.Count > 0)
                {
                    var required = m.DiscoveryObjectives.Where(o => o.IsRequired).ToList();
                    if (required.Count > 0)
                    {
                        return required.All(o => o.Status == ObjectiveStatus.Complete || o.Status == ObjectiveStatus.Deferred);
                    }
                }

                // Fallback to overall readiness score
                return m.OverallDiscoveryReadiness >= 75.0;
            });
        }

        public async Task<DiscoveryState> GetDiscoveryStateAsync(Guid modelId)
        {
            var m = await _repository.GetAsync(modelId).ConfigureAwait(false);
            if (m is null) return DiscoveryState.Discovering;

            if (m.OverallDiscoveryReadiness >= 75.0) return DiscoveryState.Ready;

            // If many categories are unknown, surface that as continued discovery (no coaching mode)
            var unknownCount = m.DiscoveryCategories.Count(c => c.Status == DiscoveryStatus.Unknown);
            if (unknownCount >= Math.Max(1, m.DiscoveryCategories.Count / 4)) return DiscoveryState.Discovering;

            return DiscoveryState.Discovering;
        }

        public async Task<string> GetReadinessSummaryAsync(Guid modelId)
        {
            var m = await _repository.GetAsync(modelId).ConfigureAwait(false);
            if (m is null) return "Model not found.";

            var sb = new StringBuilder();
            sb.AppendLine($"Overall readiness: {m.OverallDiscoveryReadiness:0.##}%");
            foreach (var c in m.DiscoveryCategories)
            {
                sb.AppendLine($"- {c.Name}: {c.Status} ({c.Confidence:0.##}%) - facts: {c.SupportingFacts.Count}");
            }

            return sb.ToString();
        }
    }
}
