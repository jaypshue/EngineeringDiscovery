using System;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Abstraction for hosting-specific view-state storage. Presentation projects implement this to
    /// provide UI-only state (graph view state, layout, selection) without placing it in Core.
    /// Core should not depend on presentation types; keep this interface object-typed by design.
    /// </summary>
    public interface IViewStateStore
    {
        /// <summary>
        /// Get a named view-state object previously stored by the host, or null if not present.
        /// </summary>
        object? Get(string key);

        /// <summary>
        /// Store a named view-state object managed by the host.
        /// </summary>
        void Set(string key, object? value);
    }
}
