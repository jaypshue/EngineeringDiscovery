using System;

namespace EngineeringDiscovery.Core.Models
{
    public enum TypeReferenceKind
    {
        Unknown,
        Type, // concrete or interface type
        GenericArgument,
        Array,
        Pointer,
        Other
    }

    public sealed class TypeReference
    {
        /// <summary>
        /// Canonical repository-qualified name for the referenced type. Empty when unresolved/external.
        /// </summary>
        public string QualifiedName { get; init; } = string.Empty;

        /// <summary>
        /// Display-friendly representation as discovered from the compiler (e.g., System.String, List&lt;T&gt;).
        /// </summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>
        /// True when the discovery could not resolve this reference to a repository-local QualifiedName
        /// (e.g., framework or external package types).
        /// </summary>
        public bool IsExternal { get; init; }

        public TypeReferenceKind Kind { get; init; } = TypeReferenceKind.Unknown;
    }
}
