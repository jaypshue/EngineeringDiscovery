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

        public TypeKind Kind { get; set; } = TypeKind.Unknown;

        public string Accessibility { get; set; } = string.Empty; // e.g., public, internal

        public bool IsAbstract { get; set; }

        public bool IsStatic { get; set; }

        public bool IsPartial { get; set; }

        public bool IsGeneric { get; set; }

        public int GenericParameterCount { get; set; }

        public string? BaseType { get; set; }

        public int ImplementedInterfaceCount { get; set; }

        public int MethodCount { get; set; }

        public int ConstructorCount { get; set; }

        public int PropertyCount { get; set; }

        public int FieldCount { get; set; }

        public int EventCount { get; set; }

        public int PublicMemberCount { get; set; }

        public int PrivateMemberCount { get; set; }

        public int MemberCount { get; set; }
    }
}
