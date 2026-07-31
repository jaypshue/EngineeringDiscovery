using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Models
{
    /// <summary>
    /// Repository-wide relationship graph built from existing TypeObservations.
    /// Supports parent (base) type, derived types and direct inheritance relationships.
    /// </summary>
    public sealed class RepositoryRelationshipGraph
    {
        private readonly Dictionary<string, string> _parentMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _derivedMap = new(StringComparer.OrdinalIgnoreCase);

        public RepositoryRelationshipGraph()
        {
        }

        public IReadOnlyDictionary<string, string> ParentMap => _parentMap;

        public IReadOnlyDictionary<string, HashSet<string>> DerivedMap => _derivedMap;

        public void AddInheritance(string childTypeName, string parentTypeName)
        {
            if (string.IsNullOrWhiteSpace(childTypeName)) throw new ArgumentNullException(nameof(childTypeName));
            if (string.IsNullOrWhiteSpace(parentTypeName)) throw new ArgumentNullException(nameof(parentTypeName));

            _parentMap[childTypeName] = parentTypeName;

            if (!_derivedMap.TryGetValue(parentTypeName, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _derivedMap[parentTypeName] = set;
            }

            set.Add(childTypeName);
        }

        public bool TryGetParent(string typeName, out string parentTypeName)
        {
            return _parentMap.TryGetValue(typeName ?? string.Empty, out parentTypeName!);
        }

        public IEnumerable<string> GetDerivedTypes(string parentTypeName)
        {
            if (string.IsNullOrWhiteSpace(parentTypeName)) yield break;
            if (_derivedMap.TryGetValue(parentTypeName, out var set))
            {
                foreach (var t in set) yield return t;
            }
        }
    }
}
