namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    // Canonical engineering vocabulary for type kinds. Providers map language-specific
    // constructs to these values before producing TypeDescriptor objects.
    internal enum EngineeringTypeKind
    {
        Unknown,
        Class,
        Interface,
        Record,
        Struct,
        Enum,
        Delegate,
        Annotation,
        TypeAlias,
        Namespace
    }
}
