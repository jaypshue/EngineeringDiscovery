using System;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Core.Services
{
    public interface IEngineeringPartner
    {
        // Start a new engineering partner session with an initial user statement; returns the working memory model
        Task<EngineeringModel> StartSessionAsync(string openingStatement);

        // Send a user message to an existing session and return the partner's textual reply (may be empty when no AI service configured)
        Task<string> SendMessageAsync(Guid sessionId, string message);

        // Retrieve the current working memory for inspection
        Task<EngineeringModel?> GetWorkingMemoryAsync(Guid sessionId);

        // Accept external engineering evidence into the working memory
        Task AcceptEvidenceAsync(Guid sessionId, EngineeringFact fact);
    }
}
