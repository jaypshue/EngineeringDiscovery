using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Core.Services
{
    public interface IEnginerringConversationOrchestrator
    {
        Task<EngineeringModel> CreateModelAsync(string idea);

        Task<EngineeringModel?> GetModelAsync(Guid id);

        Task<EngineeringQuestion?> GetNextQuestionAsync(Guid modelId);

        Task SubmitAnswerAsync(Guid modelId, Guid questionId, string answer);
    }
}
