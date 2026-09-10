using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Services.Persistence
{
    /// <summary>
    /// Complete persistence DTO for the Investigation aggregate. Captures all engineering
    /// knowledge produced by the investigation pipeline so it survives serialization round-trips.
    /// Version 1 — increment PersistenceVersion when changing the persisted shape.
    /// </summary>
    public sealed class InvestigationPersistenceDto
    {
        public int PersistenceVersion { get; set; } = 1;

        // Identity and metadata
        public Guid Id { get; set; }
        public string RepositoryPath { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Goal { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;

        // Engineering stage statuses
        public string ArchitectureStatus { get; set; } = string.Empty;
        public string PlanningStatus { get; set; } = string.Empty;
        public string DevelopmentStatus { get; set; } = string.Empty;
        public string VerificationStatus { get; set; } = string.Empty;

        // Collections
        public List<FindingDto> Findings { get; set; } = new();
        public List<ArtifactDto> Artifacts { get; set; } = new();
        public List<DiscoveryObservationDto> Observations { get; set; } = new();
        public List<TypeObservationDto> TypeObservations { get; set; } = new();
        public List<NamespaceObservationDto> NamespaceObservations { get; set; } = new();
        public List<MemberObservationDto> MemberObservations { get; set; } = new();
        public List<RelationshipObservationDto> RelationshipObservations { get; set; } = new();

        // Complex objects
        public ProjectObservationDto? ProjectObservation { get; set; }
        public RelationshipGraphDto? RelationshipGraph { get; set; }
        public RepositoryMetricsDto? RepositoryMetrics { get; set; }
    }

    public sealed class FindingDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class ArtifactDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
    }

    public sealed class DiscoveryObservationDto
    {
        public string Kind { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string? Namespace { get; set; }
        public string? Type { get; set; }
        public string? Member { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public sealed class TypeObservationDto
    {
        public string Project { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string? QualifiedName { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Accessibility { get; set; } = string.Empty;
        public bool IsAbstract { get; set; }
        public bool IsStatic { get; set; }
        public bool IsSealed { get; set; }
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
        public bool IsRootType { get; set; }
        public bool IsLeafType { get; set; }
        public int DerivedTypeCount { get; set; }
        public int IncomingDependencyCount { get; set; }
        public int OutgoingDependencyCount { get; set; }
        public bool IsDependencyHub { get; set; }
        public bool IsDependencyLeaf { get; set; }
    }

    public sealed class NamespaceObservationDto
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

    public sealed class MemberObservationDto
    {
        public string Project { get; set; } = string.Empty;
        public string? Namespace { get; set; }
        public string? Type { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string Visibility { get; set; } = string.Empty;
        public bool IsStatic { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsSealed { get; set; }
        public bool IsAsync { get; set; }
        public string? ReturnType { get; set; }
        public int ParameterCount { get; set; }
        public int ApproximateSourceLines { get; set; }
        public string? SourceFilePath { get; set; }
    }

    public sealed class RelationshipObservationDto
    {
        public string SourceProject { get; set; } = string.Empty;
        public string SourceNamespace { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string SourceQualifiedName { get; set; } = string.Empty;
        public string TargetDisplayName { get; set; } = string.Empty;
        public string TargetQualifiedName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public bool IsExternal { get; set; }
        public string Evidence { get; set; } = string.Empty;
    }

    public sealed class ProjectObservationDto
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

    public sealed class RelationshipGraphDto
    {
        /// <summary>
        /// All edges in the graph, stored as a flat list. The graph can be
        /// reconstructed by calling AddRelationship for each edge.
        /// </summary>
        public List<GraphEdgeDto> Edges { get; set; } = new();
        public int ExternalDependencyCandidateDiscardCount { get; set; }
    }

    public sealed class GraphEdgeDto
    {
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public sealed class RepositoryMetricsDto
    {
        public int TotalProjects { get; set; }
        public int TotalNamespaces { get; set; }
        public int TotalTypes { get; set; }
        public int TotalRelationships { get; set; }
        public int RootTypeCount { get; set; }
        public int LeafTypeCount { get; set; }
        public int IsolatedTypeCount { get; set; }
    }
}
