using System;

namespace EngineeringDiscovery.Core.Models
{
    public enum RelationshipKind
    {
        Unknown,
        Extends,
        Implements
    }

    public sealed class RelationshipObservation
    {
        public string SourceProject { get; init; } = string.Empty;

        public string SourceNamespace { get; init; } = string.Empty;

        public string SourceType { get; init; } = string.Empty;

        public string SourceQualifiedName { get; init; } = string.Empty;

        public string TargetDisplayName { get; init; } = string.Empty;

        public string TargetQualifiedName { get; init; } = string.Empty;

        public RelationshipKind Kind { get; init; } = RelationshipKind.Unknown;

        public bool IsExternal { get; init; }

        public string Evidence { get; init; } = string.Empty;
    }
}
