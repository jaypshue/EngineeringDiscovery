using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
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

            // Seed default questions (simple deterministic flow)
            model.OpenQuestions.Add(new EngineeringQuestion { Question = "Who will use this product?", Reason = "Identify target users", Priority = 1 });
            model.OpenQuestions.Add(new EngineeringQuestion { Question = "What problem are they trying to solve?", Reason = "Clarify user problem", Priority = 2 });
            model.OpenQuestions.Add(new EngineeringQuestion { Question = "What outcome are they expecting?", Reason = "Clarify desired outcome", Priority = 3 });
            model.OpenQuestions.Add(new EngineeringQuestion { Question = "When will they use it?", Reason = "Understand context of use", Priority = 4 });
            model.OpenQuestions.Add(new EngineeringQuestion { Question = "Why is the current approach insufficient?", Reason = "Identify constraints and gaps", Priority = 5 });

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

        public async Task<EngineeringQuestion?> GetNextQuestionAsync(Guid modelId)
        {
            var model = await _repository.GetAsync(modelId).ConfigureAwait(false);
            if (model == null) return null;

            // Determine discovery state
            var state = await GetDiscoveryStateAsync(modelId).ConfigureAwait(false);
            if (state == DiscoveryState.Ready)
            {
                // No more questions
                return null;
            }

            if (state == DiscoveryState.Coaching)
            {
                return new EngineeringQuestion { Question = "It seems you're unsure — would you like a brief coaching tip to help clarify this area? (yes/no)", Reason = "Coaching", Priority = int.MaxValue };
            }

            // If an external conversation service is available, request a question from it.
            if (_conversationService != null)
            {
                var augmented = CreateAugmentedModelWithFocus(model);
                var questionText = await _conversationService.GetNextQuestionAsync(augmented).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(questionText))
                {
                    return new EngineeringQuestion { Question = questionText, Reason = "AI generated", Priority = int.MaxValue };
                }
            }

            // Fallback deterministic rule: return the highest priority open question (lowest Priority value)
            var q = model.OpenQuestions.OrderBy(qi => qi.Priority).FirstOrDefault();
            return q;
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
                var focus = model.OpenQuestions.OrderBy(qi => qi.Priority).FirstOrDefault();
                if (focus != null)
                {
                    focusMsg = $"Focus: {focus.Question} (reason: {focus.Reason}). KnownFacts: {model.KnownFacts.Count}. Confidence: {model.Confidence:0.##}";
                }
                else
                {
                    focusMsg = $"Focus: general clarification. KnownFacts: {model.KnownFacts.Count}. Confidence: {model.Confidence:0.##}";
                }
            }

            copy.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = focusMsg });

            return copy;
        }

        public async Task SubmitAnswerAsync(Guid modelId, Guid questionId, string answer)
        {
            var model = await _repository.GetAsync(modelId).ConfigureAwait(false);
            if (model == null) throw new InvalidOperationException("Model not found");

            model.Conversation.Add(new ConversationEntry { Speaker = "Engineer", Message = answer });

            var question = model.OpenQuestions.FirstOrDefault(q => q.Id == questionId);
            if (question != null)
            {
                model.KnownFacts.Add(new EngineeringFact { Key = question.Question, Value = answer });
                model.OpenQuestions.Remove(question);
            }

            model.Confidence = Math.Min(1.0, model.Confidence + 0.2);

            // Re-evaluate discovery categories and overall readiness after each answer
            ReevaluateDiscovery(model);

            // Decide whether discovery should continue: mark ready if IsDiscoveryReady
            if (await IsDiscoveryReadyAsync(modelId).ConfigureAwait(false))
            {
                model.Status = EngineeringStatus.EngineeringModelReady;
            }

            await _repository.UpdateAsync(model).ConfigureAwait(false);
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
                    cat.Confidence = 0.0;
                }
                else
                {
                    cat.Confidence = model.Confidence;
                    cat.Status = model.Confidence >= 0.6 ? DiscoveryStatus.Complete : DiscoveryStatus.Partial;
                }
            }

            var total = model.DiscoveryCategories.Count;
            var complete = model.DiscoveryCategories.Count(c => c.Status == DiscoveryStatus.Complete);
            model.OverallDiscoveryReadiness = total == 0 ? 0.0 : (double)complete / total * 100.0;
        }

        public async Task<bool> IsDiscoveryReadyAsync(Guid modelId)
        {
            var model = await _repository.GetAsync(modelId).ConfigureAwait(false);
            if (model == null) return false;

            var required = new[] { "Primary User", "Problem Statement", "Desired Outcome", "Success Criteria" };
            var categories = model.DiscoveryCategories ?? new List<DiscoveryCategory>();

            var requiredComplete = required.All(r => categories.Any(c => c.Name.Equals(r, StringComparison.OrdinalIgnoreCase) && c.Status == DiscoveryStatus.Complete));

            var readinessOk = model.OverallDiscoveryReadiness >= 75.0 || model.Confidence >= 0.8;

            return requiredComplete && readinessOk;
        }

        public async Task<DiscoveryState> GetDiscoveryStateAsync(Guid modelId)
        {
            var model = await _repository.GetAsync(modelId).ConfigureAwait(false);
            if (model == null) return DiscoveryState.Discovering;

            if (await IsDiscoveryReadyAsync(modelId).ConfigureAwait(false))
            {
                return DiscoveryState.Ready;
            }

            // Detect coaching triggers from recent engineer messages
            var lastEngineer = model.Conversation?.Where(c => c.Speaker == "Engineer").OrderByDescending(c => c.TimestampUtc).FirstOrDefault();
            if (lastEngineer != null)
            {
                var txt = lastEngineer.Message ?? string.Empty;
                var lowered = txt.ToLowerInvariant();
                var triggers = new[] { "i don't know", "i don't", "not sure", "i'm not sure", "that's what i'm asking", "what should i do", "i need help", "uncertain" };
                if (triggers.Any(t => lowered.Contains(t)))
                {
                    return DiscoveryState.Coaching;
                }
            }

            return DiscoveryState.Discovering;
        }

        public async Task<string> GetReadinessSummaryAsync(Guid modelId)
        {
            var model = await _repository.GetAsync(modelId).ConfigureAwait(false);
            if (model == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("Discovery Readiness Summary:");
            sb.AppendLine($"Overall readiness: {model.OverallDiscoveryReadiness:0.##}%");
            sb.AppendLine($"Confidence: {model.Confidence:0.##}");
            sb.AppendLine();

            foreach (var c in model.DiscoveryCategories)
            {
                sb.AppendLine($"{c.Name} ...... {c.Status} ({c.Confidence:0.##})");
            }

            return sb.ToString();
        }
    }
}
