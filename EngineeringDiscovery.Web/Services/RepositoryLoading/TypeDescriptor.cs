namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    internal class TypeDescriptor
    {
        public string Namespace { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string QualifiedName { get; set; } = string.Empty;
        public string? BaseType { get; set; }
        public string Kind { get; set; } = "class";
        public string Accessibility { get; set; } = string.Empty;
        public bool IsAbstract { get; set; }
        public bool IsStatic { get; set; }
        public bool IsPartial { get; set; }
        public bool IsGeneric { get; set; }
        public int GenericParameterCount { get; set; }
        public int MethodCount { get; set; }
        public int ConstructorCount { get; set; }
        public int PropertyCount { get; set; }
        public int FieldCount { get; set; }
        public int EventCount { get; set; }
        public string? SourceFilePath { get; set; }
        public string ProjectName { get; set; } = string.Empty;
    }
}
