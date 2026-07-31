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

        public string? ReturnType { get; init; }

        public int ParameterCount { get; init; }

        public int ApproximateSourceLines { get; init; }
    }
}
