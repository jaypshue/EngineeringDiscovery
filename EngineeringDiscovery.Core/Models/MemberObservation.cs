using System;

namespace EngineeringDiscovery.Core.Models
{
    public enum Visibility
    {
        Public,
        Protected,
        Internal,
        Private,
        Unknown
    }

    public sealed class MemberObservation
    {
        public string Project { get; init; } = string.Empty;

        public string? Namespace { get; init; }

        public string? Type { get; init; }

        public string MemberName { get; init; } = string.Empty;

        public Visibility Visibility { get; init; } = Visibility.Unknown;

        public bool IsStatic { get; init; }

        public bool IsAsync { get; init; }

        // Display form of the return type (presentation compatibility)
        public string? ReturnType { get; set; }

        // Canonical reference for the return type produced by Discovery (ED-182)
        public TypeReference? ReturnTypeReference { get; set; }

        // Canonical references for parameter types produced by Discovery (ED-182)
        public System.Collections.Generic.List<TypeReference> ParameterTypeReferences { get; set; } = new System.Collections.Generic.List<TypeReference>();

        public int ParameterCount { get; init; }

        public int ApproximateSourceLines { get; init; }
    }
}
