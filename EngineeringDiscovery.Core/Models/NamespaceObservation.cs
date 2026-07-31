using System;

namespace EngineeringDiscovery.Core.Models
{
    public sealed class NamespaceObservation
    {
        public string Project { get; set; } = string.Empty;

        public string NamespaceName { get; set; } = string.Empty;

        public int TypeCount { get; set; }

        public int ClassCount { get; set; }

        public int InterfaceCount { get; set; }

        public int RecordCount { get; set; }

        public int StructCount { get; set; }

        public int EnumCount { get; set; }

        public int DelegateCount { get; set; }

        public int PublicTypeCount { get; set; }

        public int InternalTypeCount { get; set; }

        public int AbstractTypeCount { get; set; }

        public int StaticTypeCount { get; set; }
    }
}
