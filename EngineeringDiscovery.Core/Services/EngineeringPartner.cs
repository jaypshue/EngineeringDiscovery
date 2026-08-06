using System;
using System.Threading.Tasks;
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
            model.OriginalIdea = openingStatement ?? string.Empty;
            model.Status = EngineeringStatus.Discovering;

            // Seed the working memory by recording the opening statement as a KnownFact
            model.KnownFacts.Add(new EngineeringFact { Key = "OpeningStatement", Value = openingStatement ?? string.Empty });

            // Persist a working copy for the session
            await _repository.CreateAsync(model).ConfigureAwait(false);
            return model;
        }

        public async Task<string> SendMessageAsync(Guid sessionId, string message)
        {
            var model = await _repository.GetAsync(sessionId).ConfigureAwait(false);
            if (model == null) return string.Empty;

            model.Conversation.Add(new ConversationEntry { Speaker = "User", Message = message, TimestampUtc = DateTime.UtcNow });

            // Forward a focused prompt to the conversation service if available
            if (_conversationService != null)
            {
                var reply = await _conversationService.GetNextQuestionAsync(model).ConfigureAwait(false);
                // Record EngineOS reply as conversation entry
                if (!string.IsNullOrWhiteSpace(reply))
                {
                    model.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = reply, TimestampUtc = DateTime.UtcNow });
                }

                // Persist changes to working memory
                await _repository.UpdateAsync(model).ConfigureAwait(false);
                return reply ?? string.Empty;
            }

            // No conversation service: persist and return empty reply
            await _repository.UpdateAsync(model).ConfigureAwait(false);
            return string.Empty;
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
