using System;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Marker interface for presentation-tier services that provide UI-oriented features
    /// (e.g., recommendation, insight, advisor). These implementations belong in presentation
    /// projects and should not be referenced by Core.
    ///
    /// This file intentionally contains no members; presentation services are defined in
    /// the presentation projects and registered there. The marker exists to document
    /// the boundary and provide host wiring guidance.
    /// </summary>
    public interface IPresentationService
    {
    }
}
