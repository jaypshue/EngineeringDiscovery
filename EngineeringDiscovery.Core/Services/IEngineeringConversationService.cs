using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Core.Services
{
    public interface IEngineeringConversationService
    {
        /// <summary>
        /// Given the current EngineeringModel, returns exactly one next engineering question as plain text.
        /// </summary>
        Task<string> GetNextQuestionAsync(EngineeringModel model);
    }
}
