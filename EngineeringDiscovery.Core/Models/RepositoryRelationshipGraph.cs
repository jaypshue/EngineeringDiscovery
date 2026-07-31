using System;
using System.Collections.Generic;
using System.Linq;

namespace EngineeringDiscovery.Core.Models
{
    /// <summary>
    /// Repository-wide canonical relationship graph.
    /// Supports multiple relationship types (Inheritance, Dependency, Implementation, Containment).
    /// Nodes are identified by repository-wide QualifiedName strings.
    /// </summary>
    public enum RelationshipType
    {
        Unknown,
        Inheritance,
        Dependency,
        Implementation,
        Containment
    }

    public sealed class RepositoryRelationshipGraph
    {
        // Outgoing relationships: source -> (relationshipType -> set of targets)
        private readonly Dictionary<string, Dictionary<RelationshipType, HashSet<string>>> _outgoing = new(StringComparer.OrdinalIgnoreCase);

        // Incoming relationships: target -> (relationshipType -> set of sources)
        private readonly Dictionary<string, Dictionary<RelationshipType, HashSet<string>>> _incoming = new(StringComparer.OrdinalIgnoreCase);

        // Backwards-compatible convenience maps for inheritance
        private readonly Dictionary<string, string> _parentMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _derivedMap = new(StringComparer.OrdinalIgnoreCase);

        public RepositoryRelationshipGraph()
        {
        }

        // Backwards-compatible accessors (inheritance-only)
        public IReadOnlyDictionary<string, string> ParentMap => _parentMap;
        public IReadOnlyDictionary<string, HashSet<string>> DerivedMap => _derivedMap;

        public void AddNode(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName)) return;
            if (!_outgoing.ContainsKey(qualifiedName)) _outgoing[qualifiedName] = new Dictionary<RelationshipType, HashSet<string>>();
            if (!_incoming.ContainsKey(qualifiedName)) _incoming[qualifiedName] = new Dictionary<RelationshipType, HashSet<string>>();
        }

        public void AddRelationship(string sourceQualifiedName, string targetQualifiedName, RelationshipType relationshipType)
        {
            if (string.IsNullOrWhiteSpace(sourceQualifiedName)) throw new ArgumentNullException(nameof(sourceQualifiedName));
            if (string.IsNullOrWhiteSpace(targetQualifiedName)) throw new ArgumentNullException(nameof(targetQualifiedName));

            AddNode(sourceQualifiedName);
            AddNode(targetQualifiedName);

            // Outgoing
            var outMap = _outgoing[sourceQualifiedName];
            if (!outMap.TryGetValue(relationshipType, out var outSet))
            {
                outSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                outMap[relationshipType] = outSet;
            }
            var added = outSet.Add(targetQualifiedName);

            // Incoming
            var inMap = _incoming[targetQualifiedName];
            if (!inMap.TryGetValue(relationshipType, out var inSet))
            {
                inSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                inMap[relationshipType] = inSet;
            }
            inSet.Add(sourceQualifiedName);

            // If inheritance edge, maintain parent/derived convenience maps (single parent semantics)
            if (relationshipType == RelationshipType.Inheritance && added)
            {
                _parentMap[sourceQualifiedName] = targetQualifiedName;
                if (!_derivedMap.TryGetValue(targetQualifiedName, out var dset))
                {
                    dset = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _derivedMap[targetQualifiedName] = dset;
                }
                dset.Add(sourceQualifiedName);
            }
        }

        // Backwards-compatible helper for existing callers
        public void AddInheritance(string childTypeName, string parentTypeName)
        {
            AddRelationship(childTypeName, parentTypeName, RelationshipType.Inheritance);
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

        public IEnumerable<(RelationshipType Type, string Target)> GetOutgoingRelationships(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName)) yield break;
            if (!_outgoing.TryGetValue(qualifiedName, out var map)) yield break;
            foreach (var kv in map)
            {
                foreach (var target in kv.Value) yield return (kv.Key, target);
            }
        }

        public IEnumerable<(RelationshipType Type, string Source)> GetIncomingRelationships(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName)) yield break;
            if (!_incoming.TryGetValue(qualifiedName, out var map)) yield break;
            foreach (var kv in map)
            {
                foreach (var src in kv.Value) yield return (kv.Key, src);
            }
        }

        public IEnumerable<(string Source, string Target)> GetRelationships(RelationshipType type)
        {
            foreach (var src in _outgoing.Keys)
            {
                var map = _outgoing[src];
                if (map.TryGetValue(type, out var set))
                {
                    foreach (var t in set) yield return (src, t);
                }
            }
        }

        public IEnumerable<string> GetParents(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName)) yield break;
            if (_incoming.TryGetValue(qualifiedName, out var map) && map.TryGetValue(RelationshipType.Inheritance, out var set))
            {
                foreach (var p in set) yield return p;
            }
        }

        public IEnumerable<string> GetChildren(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName)) yield break;
            if (_outgoing.TryGetValue(qualifiedName, out var map) && map.TryGetValue(RelationshipType.Inheritance, out var set))
            {
                foreach (var c in set) yield return c;
            }
        }

        public IEnumerable<string> GetDependencies(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName)) yield break;
            if (_outgoing.TryGetValue(qualifiedName, out var map) && map.TryGetValue(RelationshipType.Dependency, out var set))
            {
                foreach (var d in set) yield return d;
            }
        }

        public IEnumerable<string> GetDependents(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName)) yield break;
            if (_incoming.TryGetValue(qualifiedName, out var map) && map.TryGetValue(RelationshipType.Dependency, out var set))
            {
                foreach (var s in set) yield return s;
            }
        }
    }
}
