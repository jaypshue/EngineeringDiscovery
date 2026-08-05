using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Core.Services
{
    public class EnginerringConversationOrchestrator : IEnginerringConversationOrchestrator
    {
        private readonly IEngineeringModelRepository _repository;

        public EnginerringConversationOrchestrator(IEngineeringModelRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
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

            // Deterministic rule: return the highest priority open question (lowest Priority value)
            var q = model.OpenQuestions.OrderBy(qi => qi.Priority).FirstOrDefault();
            return q;
        }

        public async Task SubmitAnswerAsync(Guid modelId, Guid questionId, string answer)
        {
            var model = await _repository.GetAsync(modelId).ConfigureAwait(false);
            if (model == null) throw new InvalidOperationException("Model not found");

            // Record conversation entry
            model.Conversation.Add(new ConversationEntry { Speaker = "Engineer", Message = answer });

            // Promote confirmed information into KnownFacts using a simple mapping
            var question = model.OpenQuestions.FirstOrDefault(q => q.Id == questionId);
            if (question != null)
            {
                model.KnownFacts.Add(new EngineeringFact { Key = question.Question, Value = answer });
                model.OpenQuestions.Remove(question);
            }

            // Update confidence rudimentary: increase by fixed step per answer
            model.Confidence = Math.Min(1.0, model.Confidence + 0.2);

            if (!model.OpenQuestions.Any())
            {
                model.Status = EngineeringStatus.EngineeringModelReady;
            }

            await _repository.UpdateAsync(model).ConfigureAwait(false);
        }
    }
}

