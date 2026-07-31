using System;

namespace EngineeringDiscovery.Core.Models
{
    public sealed class ProjectObservation
    {
        public string Project { get; set; } = string.Empty;

        public int NamespaceCount { get; set; }

        public int TypeCount { get; set; }

        public int ClassCount { get; set; }

        public int InterfaceCount { get; set; }

        public int RecordCount { get; set; }

        public int StructCount { get; set; }

        public int EnumCount { get; set; }

        public int DelegateCount { get; set; }

        public int MemberCount { get; set; }
    }
}
