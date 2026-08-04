using System.Collections.Generic;
using EngineeringDiscovery.Core.Services;

namespace EngineeringDiscovery.Wpf.Services
{
    /// <summary>
    /// Simple WPF view-state store. Singleton lifetime is expected in the WPF host.
    /// Stores UI-only view state objects and optionally persists them to disk in future steps.
    /// </summary>
    public sealed class WpfViewStateStore : IViewStateStore
    {
        private readonly Dictionary<string, object?> _store = new();
        private readonly object _lock = new();

        public object? Get(string key)
        {
            lock (_lock)
            {
                return _store.TryGetValue(key, out var v) ? v : null;
            }
        }

        public void Set(string key, object? value)
        {
            lock (_lock)
            {
                if (value is null) _store.Remove(key);
                else _store[key] = value;
            }
        }
    }
}
