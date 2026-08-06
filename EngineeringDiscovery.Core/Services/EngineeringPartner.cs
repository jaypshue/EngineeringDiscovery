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

            // If a conversation service is available, request an initial partner reply and record it
            if (_conversationService != null)
            {
                try
                {
                    var reply = await _conversationService.GetNextQuestionAsync(model).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(reply))
                    {
                        model.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = reply, TimestampUtc = DateTime.UtcNow });
                        await _repository.UpdateAsync(model).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Swallow errors from the external service to keep startup deterministic without AI
                }
            }

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

            model.Conversation.Add(new ConversationEntry { Speaker = "User", Message = message, TimestampUtc = DateTime.UtcNow });

            // Forward a focused prompt to the conversation service if available
            if (_conversationService != null)
            {
                try
                {
                    Debug.WriteLine($"[ED-EP6] Prompt sent to conversation service for session {sessionId}");
                    var reply = await _conversationService.GetNextQuestionAsync(model).ConfigureAwait(false);
                    Debug.WriteLine($"[ED-EP6] LLM response received for session {sessionId}: {(reply ?? "(null)")}");

                    // Ensure the partner always returns a meaningful response
                    if (string.IsNullOrWhiteSpace(reply))
                    {
                        reply = "I wasn't able to generate a response just now.";
                        Debug.WriteLine($"[ED-EP6] LLM returned empty; substituting friendly message for session {sessionId}");
                    }

                    // Record EngineOS reply as conversation entry
                    model.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = reply, TimestampUtc = DateTime.UtcNow });

                    // Persist changes to working memory
                    await _repository.UpdateAsync(model).ConfigureAwait(false);
                    Debug.WriteLine($"[ED-EP6] Engineering Partner reply recorded for session {sessionId}");
                    return reply;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ED-EP6] Exception from conversation service for session {sessionId}: {ex}");
                    var friendly = "I encountered an unexpected error while processing your request.";
                    model.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = friendly, TimestampUtc = DateTime.UtcNow });
                    await _repository.UpdateAsync(model).ConfigureAwait(false);
                    return friendly;
                }
            }

            // No conversation service: provide a friendly fallback response
            var fallback = "I don't currently have enough context to answer that.";
            Debug.WriteLine($"[ED-EP6] No conversation service configured; returning fallback reply for session {sessionId}");
            model.Conversation.Add(new ConversationEntry { Speaker = "EngineOS", Message = fallback, TimestampUtc = DateTime.UtcNow });
            await _repository.UpdateAsync(model).ConfigureAwait(false);
            return fallback;
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
