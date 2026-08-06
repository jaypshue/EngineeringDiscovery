using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Core.Services
{
    public interface IEngineeringConversationService
    {
        /// <summary>
        /// Given the current EngineeringModel, generates the Engineering Partner's next conversational response as plain text.
        /// The response may be a question, clarification, summary, recommendation, or other conversational reply.
        /// </summary>
        Task<string> RespondAsync(EngineeringModel model);
    }
}
