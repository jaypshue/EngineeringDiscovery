using System.Collections.Generic;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    internal enum MemberKind
    {
        Method,
        Constructor,
        Property,
        Field,
        Event,
        Unknown
    }

    // Language-specific provider artifact describing a member. This type is owned by
    // repository providers and intentionally does not reference Core types (no QualifiedName, no TypeReference).
    internal class MemberDescriptor
    {
        public string Project { get; set; } = string.Empty;

        public string Namespace { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        public string MemberName { get; set; } = string.Empty;

        public MemberKind Kind { get; set; } = MemberKind.Unknown;

        public string Visibility { get; set; } = string.Empty;

        public bool IsStatic { get; set; }

        public bool IsAsync { get; set; }

        public string? ReturnTypeDisplay { get; set; }

        public List<string> ParameterTypeDisplays { get; } = new List<string>();

        public List<string> GenericArgumentDisplays { get; } = new List<string>();

        public int LineCount { get; set; }
    }
}
