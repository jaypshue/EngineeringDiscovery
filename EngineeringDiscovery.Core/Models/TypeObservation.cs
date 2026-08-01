using System;

namespace EngineeringDiscovery.Core.Models
{
    public enum TypeKind
    {
        Class,
        Interface,
        Record,
        Struct,
        Enum,
        Delegate,
        Unknown
    }

    public sealed class TypeObservation
    {
        public string Project { get; set; } = string.Empty;

        public string Namespace { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// Repository-unique canonical identity for the observed type.
        /// Populated during discovery. Examples:
        /// - ProjectName:Namespace.TypeName
        /// - Namespace.TypeName
        /// This value is used by repository-wide graphs and enrichers when
        /// a unique identifier is required. Do not use TypeName for uniqueness.
        /// </summary>
        public string? QualifiedName { get; set; }

        public TypeKind Kind { get; set; } = TypeKind.Unknown;

        public string Accessibility { get; set; } = string.Empty; // e.g., public, internal

        public bool IsAbstract { get; set; }

        public bool IsStatic { get; set; }

        public bool IsPartial { get; set; }

        public bool IsGeneric { get; set; }

        public int GenericParameterCount { get; set; }

        public string? BaseType { get; set; }

        public int ImplementedInterfaceCount { get; set; }

        // Newly captured lists of related type identities (discovery may populate these when available)
        // Stored as display strings and resolved to QualifiedName by enrichment.
        public System.Collections.Generic.List<string> ImplementedInterfaces { get; set; } = new System.Collections.Generic.List<string>();

        public System.Collections.Generic.List<string> ConstructorParameterTypes { get; set; } = new System.Collections.Generic.List<string>();

        public System.Collections.Generic.List<string> MethodParameterTypes { get; set; } = new System.Collections.Generic.List<string>();

        public System.Collections.Generic.List<string> FieldTypes { get; set; } = new System.Collections.Generic.List<string>();

        public System.Collections.Generic.List<string> PropertyTypes { get; set; } = new System.Collections.Generic.List<string>();

        public System.Collections.Generic.List<string> EventTypes { get; set; } = new System.Collections.Generic.List<string>();

        // Generic argument types discovered from members (where practical)
        public System.Collections.Generic.List<string> GenericArgumentTypes { get; set; } = new System.Collections.Generic.List<string>();

        public int MethodCount { get; set; }

        public int ConstructorCount { get; set; }

        public int PropertyCount { get; set; }

        public int FieldCount { get; set; }

        public int EventCount { get; set; }

        public int PublicMemberCount { get; set; }

        public int PrivateMemberCount { get; set; }

        public int MemberCount { get; set; }

        // Relationship metadata (populated by enrichment passes)
        public bool IsRootType { get; set; }

        public bool IsLeafType { get; set; }

        public int DerivedTypeCount { get; set; }

        // Number of interfaces implemented by this type (discovery may populate this when available)
        // Discovery currently may not populate this; enrichment should avoid speculating.
        public int ImplementsInterfaceCount { get; set; }

        // High-level classification hints; enrichment may set if derivable from observations.
        public bool IsFrameworkType { get; set; }

        public bool IsApplicationType { get; set; }

        // Dependency metadata (populated by enrichment passes)
        public int IncomingDependencyCount { get; set; }

        public int OutgoingDependencyCount { get; set; }

        public bool IsDependencyHub { get; set; }

        public bool IsDependencyLeaf { get; set; }
    }
}
