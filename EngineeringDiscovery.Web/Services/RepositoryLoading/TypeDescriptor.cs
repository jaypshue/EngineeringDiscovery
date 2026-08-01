namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    internal class TypeDescriptor
    {
        // Language-neutral type descriptor used by Discovery. Avoid exposing compiler-specific
        // concepts (no Roslyn or MSBuild types). Keep only engineering semantics.
        public string Namespace { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string QualifiedName { get; set; } = string.Empty; // canonical repository-unique identity
        public string? BaseType { get; set; } // display form of base type

        // Canonical references (if the repository provider can resolve them to repository-local QualifiedName)
        // Discovery is responsible for converting display strings to TypeReference objects.
        public EngineeringDiscovery.Core.Models.TypeReference? BaseTypeReference { get; set; }
        public EngineeringTypeKind Kind { get; set; } = EngineeringTypeKind.Class;
        public EngineeringAccessibility Accessibility { get; set; } = EngineeringAccessibility.Unknown;
        public bool IsAbstract { get; set; }
        public bool IsStatic { get; set; }
        public bool IsGeneric { get; set; }
        public int GenericParameterCount { get; set; }
        public int GenericConstraintCount { get; set; }
        public bool IsSealed { get; set; }
        public int ImplementedInterfaceCount { get; set; }
        // Newly captured engineering facts (member-level)
        public List<string> ImplementedInterfaces { get; set; } = new List<string>();

        // Constructor parameter types (display strings)
        public List<string> ConstructorParameterTypes { get; set; } = new List<string>();

        // Method parameter types (display strings)
        public List<string> MethodParameterTypes { get; set; } = new List<string>();

        // Field types
        public List<string> FieldTypes { get; set; } = new List<string>();

        // Property types
        public List<string> PropertyTypes { get; set; } = new List<string>();

        // Event types
        public List<string> EventTypes { get; set; } = new List<string>();

        // Generic argument types discovered from members (where practical)
        public List<string> GenericArgumentTypes { get; set; } = new List<string>();
        public int AttributeCount { get; set; }
        public int NestedTypeCount { get; set; }
        public int SourceLineCount { get; set; }
        public int DependencyCount { get; set; } // provider-known only heuristic
        public int MethodCount { get; set; }
        public int ConstructorCount { get; set; }
        public int PropertyCount { get; set; }
        public int FieldCount { get; set; }
        public int EventCount { get; set; }
        public string? SourceFilePath { get; set; }
    }
}
