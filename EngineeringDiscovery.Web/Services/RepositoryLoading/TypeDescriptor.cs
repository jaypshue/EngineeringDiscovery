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
        public string Kind { get; set; } = "class"; // e.g., class, interface, enum, struct, delegate
        public string Accessibility { get; set; } = string.Empty; // e.g., public, internal
        public bool IsAbstract { get; set; }
        public bool IsStatic { get; set; }
        public bool IsGeneric { get; set; }
        public int GenericParameterCount { get; set; }
        public int MethodCount { get; set; }
        public int ConstructorCount { get; set; }
        public int PropertyCount { get; set; }
        public int FieldCount { get; set; }
        public int EventCount { get; set; }
        public string? SourceFilePath { get; set; }
    }
}
