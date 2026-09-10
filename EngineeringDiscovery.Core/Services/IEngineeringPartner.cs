using System;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Core.Services
{
    public interface IEngineeringPartner
    {
        // Start or reuse the default engineering partner session behavior.
        Task<EngineeringModel> StartSessionAsync(string openingStatement);

        // Explicitly control whether an empty opening statement may reuse the prior session.
        // Import workflows use reuseExisting: false so a repository cannot attach to an
        // unrelated conversation.
        Task<EngineeringModel> StartSessionAsync(string openingStatement, bool reuseExisting);

        // Send a user message to an existing session and return the partner's textual reply (may be empty when no AI service configured)
        Task<string> SendMessageAsync(Guid sessionId, string message);

        // Send a question through the read-only Conversation surface. This records the exchange and
        // uses the shared context projection, but does not dispatch mutations or engineering operations.
        Task<string> SendReadOnlyMessageAsync(Guid sessionId, string message, CancellationToken cancellationToken = default);

        // Retrieve the current working memory for inspection
        Task<EngineeringModel?> GetWorkingMemoryAsync(Guid sessionId);

        // Accept external engineering evidence into the working memory
        Task AcceptEvidenceAsync(Guid sessionId, EngineeringFact fact);
    }
}
