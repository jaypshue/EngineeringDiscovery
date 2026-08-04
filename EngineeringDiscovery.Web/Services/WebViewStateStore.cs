using System.Collections.Concurrent;
using EngineeringDiscovery.Core.Services;

namespace EngineeringDiscovery.Web.Services
{
    /// <summary>
    /// Simple in-memory view-state store for Blazor presentation.
    /// Scoped lifetime recommended for Blazor Server to isolate per-circuit state.
    /// </summary>
    public sealed class WebViewStateStore : IViewStateStore
    {
        private readonly ConcurrentDictionary<string, object?> _store = new();

        public object? Get(string key)
        {
            return _store.TryGetValue(key, out var v) ? v : null;
        }

        public void Set(string key, object? value)
        {
            if (value is null) _store.TryRemove(key, out _);
            else _store[key] = value;
        }
    }
}
