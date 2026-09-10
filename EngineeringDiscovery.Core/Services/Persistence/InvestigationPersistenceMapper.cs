using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Core.Services.Persistence
{
    /// <summary>
    /// Maps between the domain Investigation aggregate and the persistence DTO.
    /// Domain → DTO (ToDto): reads all public properties and private-field-backed accessors.
    /// DTO → Domain (FromDto): reconstructs the domain object using its factory + mutation methods.
    /// </summary>
    public static class InvestigationPersistenceMapper
    {
        public static InvestigationPersistenceDto ToDto(Investigation investigation)
        {
            if (investigation is null) throw new ArgumentNullException(nameof(investigation));

            var dto = new InvestigationPersistenceDto
            {
                Id = investigation.Id,
                RepositoryPath = investigation.RepositoryPath ?? string.Empty,
                Status = investigation.Status.ToString(),
                StartedAt = investigation.StartedAt,
                CompletedAt = investigation.CompletedAt,
                Goal = investigation.Goal ?? string.Empty,
                Owner = investigation.Owner ?? string.Empty,
                Target = investigation.Target ?? string.Empty,
                ArchitectureStatus = investigation.ArchitectureStatus.ToString(),
                PlanningStatus = investigation.PlanningStatus.ToString(),
                DevelopmentStatus = investigation.DevelopmentStatus.ToString(),
                VerificationStatus = investigation.VerificationStatus.ToString(),
            };

            // Findings
            if (investigation.Findings != null)
            {
                foreach (var f in investigation.Findings)
                {
                    dto.Findings.Add(new FindingDto
                    {
                        Id = f.Id,
                        Type = f.Type.ToString(),
                        Description = f.Description ?? string.Empty
                    });
                }
            }

            // Artifacts
            if (investigation.Artifacts != null)
            {
                foreach (var a in investigation.Artifacts)
                {
                    dto.Artifacts.Add(new ArtifactDto
                    {
                        Id = a.Id,
                        Title = a.Title ?? string.Empty,
                        Description = a.Description ?? string.Empty,
                        Type = a.Type.ToString(),
                        CreatedOn = a.CreatedOn
                    });
                }
            }

            // Observations
            if (investigation.Observations != null)
            {
                foreach (var o in investigation.Observations)
                {
                    dto.Observations.Add(new DiscoveryObservationDto
                    {
                        Kind = o.Kind.ToString(),
                        Project = o.Project ?? string.Empty,
                        Namespace = o.Namespace,
                        Type = o.Type,
                        Member = o.Member,
                        Description = o.Description ?? string.Empty
                    });
                }
            }

            // TypeObservations
            if (investigation.TypeObservations != null)
            {
                foreach (var t in investigation.TypeObservations)
                {
                    dto.TypeObservations.Add(new TypeObservationDto
                    {
                        Project = t.Project ?? string.Empty,
                        Namespace = t.Namespace ?? string.Empty,
                        TypeName = t.TypeName ?? string.Empty,
                        QualifiedName = t.QualifiedName,
                        Kind = t.Kind.ToString(),
                        Accessibility = t.Accessibility ?? string.Empty,
                        IsAbstract = t.IsAbstract,
                        IsStatic = t.IsStatic,
                        IsSealed = t.IsSealed,
                        IsPartial = t.IsPartial,
                        IsGeneric = t.IsGeneric,
                        GenericParameterCount = t.GenericParameterCount,
                        BaseType = t.BaseType,
                        ImplementedInterfaceCount = t.ImplementedInterfaceCount,
                        MethodCount = t.MethodCount,
                        ConstructorCount = t.ConstructorCount,
                        PropertyCount = t.PropertyCount,
                        FieldCount = t.FieldCount,
                        EventCount = t.EventCount,
                        PublicMemberCount = t.PublicMemberCount,
                        PrivateMemberCount = t.PrivateMemberCount,
                        MemberCount = t.MemberCount,
                        IsRootType = t.IsRootType,
                        IsLeafType = t.IsLeafType,
                        DerivedTypeCount = t.DerivedTypeCount,
                        IncomingDependencyCount = t.IncomingDependencyCount,
                        OutgoingDependencyCount = t.OutgoingDependencyCount,
                        IsDependencyHub = t.IsDependencyHub,
                        IsDependencyLeaf = t.IsDependencyLeaf,
                    });
                }
            }

            // NamespaceObservations
            if (investigation.NamespaceObservations != null)
            {
                foreach (var n in investigation.NamespaceObservations)
                {
                    dto.NamespaceObservations.Add(new NamespaceObservationDto
                    {
                        Project = n.Project ?? string.Empty,
                        NamespaceName = n.NamespaceName ?? string.Empty,
                        TypeCount = n.TypeCount,
                        ClassCount = n.ClassCount,
                        InterfaceCount = n.InterfaceCount,
                        RecordCount = n.RecordCount,
                        StructCount = n.StructCount,
                        EnumCount = n.EnumCount,
                        DelegateCount = n.DelegateCount,
                        PublicTypeCount = n.PublicTypeCount,
                        InternalTypeCount = n.InternalTypeCount,
                        AbstractTypeCount = n.AbstractTypeCount,
                        StaticTypeCount = n.StaticTypeCount,
                    });
                }
            }

            // MemberObservations
            if (investigation.MemberObservations != null)
            {
                foreach (var m in investigation.MemberObservations)
                {
                    dto.MemberObservations.Add(new MemberObservationDto
                    {
                        Project = m.Project ?? string.Empty,
                        Namespace = m.Namespace,
                        Type = m.Type,
                        MemberName = m.MemberName ?? string.Empty,
                        Visibility = m.Visibility.ToString(),
                        IsStatic = m.IsStatic,
                        IsAbstract = m.IsAbstract,
                        IsSealed = m.IsSealed,
                        IsAsync = m.IsAsync,
                        ReturnType = m.ReturnType,
                        ParameterCount = m.ParameterCount,
                        ApproximateSourceLines = m.ApproximateSourceLines,
                        SourceFilePath = m.SourceFilePath,
                    });
                }
            }

            // RelationshipObservations
            if (investigation.RelationshipObservations != null)
            {
                foreach (var r in investigation.RelationshipObservations)
                {
                    dto.RelationshipObservations.Add(new RelationshipObservationDto
                    {
                        SourceProject = r.SourceProject ?? string.Empty,
                        SourceNamespace = r.SourceNamespace ?? string.Empty,
                        SourceType = r.SourceType ?? string.Empty,
                        SourceQualifiedName = r.SourceQualifiedName ?? string.Empty,
                        TargetDisplayName = r.TargetDisplayName ?? string.Empty,
                        TargetQualifiedName = r.TargetQualifiedName ?? string.Empty,
                        Kind = r.Kind.ToString(),
                        IsExternal = r.IsExternal,
                        Evidence = r.Evidence ?? string.Empty,
                    });
                }
            }

            // ProjectObservation
            if (investigation.ProjectObservation != null)
            {
                var po = investigation.ProjectObservation;
                dto.ProjectObservation = new ProjectObservationDto
                {
                    Project = po.Project ?? string.Empty,
                    NamespaceCount = po.NamespaceCount,
                    TypeCount = po.TypeCount,
                    ClassCount = po.ClassCount,
                    InterfaceCount = po.InterfaceCount,
                    RecordCount = po.RecordCount,
                    StructCount = po.StructCount,
                    EnumCount = po.EnumCount,
                    DelegateCount = po.DelegateCount,
                    MemberCount = po.MemberCount,
                };
            }

            // RelationshipGraph — flatten to edge list
            if (investigation.RelationshipGraph != null)
            {
                var graphDto = new RelationshipGraphDto();
                graphDto.ExternalDependencyCandidateDiscardCount = investigation.RelationshipGraph.ExternalDependencyCandidateDiscardCount;

                foreach (var relType in new[] { RelationshipType.Inheritance, RelationshipType.Dependency, RelationshipType.Implementation, RelationshipType.Containment })
                {
                    foreach (var (source, target) in investigation.RelationshipGraph.GetRelationships(relType))
                    {
                        graphDto.Edges.Add(new GraphEdgeDto
                        {
                            Source = source,
                            Target = target,
                            Type = relType.ToString()
                        });
                    }
                }

                dto.RelationshipGraph = graphDto;
            }

            // RepositoryMetrics
            if (investigation.RepositoryMetrics != null)
            {
                var rm = investigation.RepositoryMetrics;
                dto.RepositoryMetrics = new RepositoryMetricsDto
                {
                    TotalProjects = rm.TotalProjects,
                    TotalNamespaces = rm.TotalNamespaces,
                    TotalTypes = rm.TotalTypes,
                    TotalRelationships = rm.TotalRelationships,
                    RootTypeCount = rm.RootTypeCount,
                    LeafTypeCount = rm.LeafTypeCount,
                    IsolatedTypeCount = rm.IsolatedTypeCount,
                };
            }

            return dto;
        }

        public static Investigation FromDto(InvestigationPersistenceDto dto)
        {
            if (dto is null) throw new ArgumentNullException(nameof(dto));

            // Parse enums with safe fallbacks
            var status = ParseEnum<InvestigationStatus>(dto.Status, InvestigationStatus.Created);
            var archStatus = ParseEnum<EngineeringStageStatus>(dto.ArchitectureStatus, EngineeringStageStatus.NotStarted);
            var planStatus = ParseEnum<EngineeringStageStatus>(dto.PlanningStatus, EngineeringStageStatus.NotStarted);
            var devStatus = ParseEnum<EngineeringStageStatus>(dto.DevelopmentStatus, EngineeringStageStatus.NotStarted);
            var verStatus = ParseEnum<EngineeringStageStatus>(dto.VerificationStatus, EngineeringStageStatus.NotStarted);

            // Use the factory to create with proper Id and path
            var id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
            var repoPath = string.IsNullOrWhiteSpace(dto.RepositoryPath) ? "/" : dto.RepositoryPath;

            var inv = Investigation.Create(id, repoPath, dto.Goal ?? string.Empty, dto.Owner ?? string.Empty, dto.Target ?? string.Empty,
                archStatus, planStatus, devStatus, verStatus);

            // Transition to Started so we can add observations/findings
            inv.Start();

            // Findings
            foreach (var f in dto.Findings ?? Enumerable.Empty<FindingDto>())
            {
                var findingType = ParseEnum<FindingType>(f.Type, FindingType.Observation);
                var findingId = f.Id == Guid.Empty ? Guid.NewGuid() : f.Id;
                inv.AddFinding(new Finding(findingId, findingType, f.Description ?? string.Empty));
            }

            // Artifacts
            foreach (var a in dto.Artifacts ?? Enumerable.Empty<ArtifactDto>())
            {
                var artifactType = ParseEnum<ArtifactType>(a.Type, ArtifactType.LayerViolation);
                var artifactId = a.Id == Guid.Empty ? Guid.NewGuid() : a.Id;
                inv.Artifacts.Add(new InvestigationArtifact(artifactId, a.Title ?? string.Empty, a.Description ?? string.Empty, artifactType));
            }

            // Observations
            foreach (var o in dto.Observations ?? Enumerable.Empty<DiscoveryObservationDto>())
            {
                var kind = ParseEnum<ObservationKind>(o.Kind, ObservationKind.Solution);
                inv.AddObservation(new DiscoveryObservation
                {
                    Kind = kind,
                    Project = o.Project ?? string.Empty,
                    Namespace = o.Namespace,
                    Type = o.Type,
                    Member = o.Member,
                    Description = o.Description ?? string.Empty
                });
            }

            // TypeObservations
            foreach (var t in dto.TypeObservations ?? Enumerable.Empty<TypeObservationDto>())
            {
                var kind = ParseEnum<TypeKind>(t.Kind, TypeKind.Unknown);
                inv.AddTypeObservation(new TypeObservation
                {
                    Project = t.Project ?? string.Empty,
                    Namespace = t.Namespace ?? string.Empty,
                    TypeName = t.TypeName ?? string.Empty,
                    QualifiedName = t.QualifiedName,
                    Kind = kind,
                    Accessibility = t.Accessibility ?? string.Empty,
                    IsAbstract = t.IsAbstract,
                    IsStatic = t.IsStatic,
                    IsSealed = t.IsSealed,
                    IsPartial = t.IsPartial,
                    IsGeneric = t.IsGeneric,
                    GenericParameterCount = t.GenericParameterCount,
                    BaseType = t.BaseType,
                    ImplementedInterfaceCount = t.ImplementedInterfaceCount,
                    MethodCount = t.MethodCount,
                    ConstructorCount = t.ConstructorCount,
                    PropertyCount = t.PropertyCount,
                    FieldCount = t.FieldCount,
                    EventCount = t.EventCount,
                    PublicMemberCount = t.PublicMemberCount,
                    PrivateMemberCount = t.PrivateMemberCount,
                    MemberCount = t.MemberCount,
                    IsRootType = t.IsRootType,
                    IsLeafType = t.IsLeafType,
                    DerivedTypeCount = t.DerivedTypeCount,
                    IncomingDependencyCount = t.IncomingDependencyCount,
                    OutgoingDependencyCount = t.OutgoingDependencyCount,
                    IsDependencyHub = t.IsDependencyHub,
                    IsDependencyLeaf = t.IsDependencyLeaf,
                });
            }

            // NamespaceObservations
            foreach (var n in dto.NamespaceObservations ?? Enumerable.Empty<NamespaceObservationDto>())
            {
                inv.AddNamespaceObservation(new NamespaceObservation
                {
                    Project = n.Project ?? string.Empty,
                    NamespaceName = n.NamespaceName ?? string.Empty,
                    TypeCount = n.TypeCount,
                    ClassCount = n.ClassCount,
                    InterfaceCount = n.InterfaceCount,
                    RecordCount = n.RecordCount,
                    StructCount = n.StructCount,
                    EnumCount = n.EnumCount,
                    DelegateCount = n.DelegateCount,
                    PublicTypeCount = n.PublicTypeCount,
                    InternalTypeCount = n.InternalTypeCount,
                    AbstractTypeCount = n.AbstractTypeCount,
                    StaticTypeCount = n.StaticTypeCount,
                });
            }

            // MemberObservations
            foreach (var m in dto.MemberObservations ?? Enumerable.Empty<MemberObservationDto>())
            {
                var visibility = ParseEnum<Visibility>(m.Visibility, Visibility.Unknown);
                inv.AddMemberObservation(new MemberObservation
                {
                    Project = m.Project ?? string.Empty,
                    Namespace = m.Namespace,
                    Type = m.Type,
                    MemberName = m.MemberName ?? string.Empty,
                    Visibility = visibility,
                    IsStatic = m.IsStatic,
                    IsAbstract = m.IsAbstract,
                    IsSealed = m.IsSealed,
                    IsAsync = m.IsAsync,
                    ReturnType = m.ReturnType,
                    ParameterCount = m.ParameterCount,
                    ApproximateSourceLines = m.ApproximateSourceLines,
                    SourceFilePath = m.SourceFilePath,
                });
            }

            // RelationshipObservations
            foreach (var r in dto.RelationshipObservations ?? Enumerable.Empty<RelationshipObservationDto>())
            {
                var kind = ParseEnum<RelationshipKind>(r.Kind, RelationshipKind.Unknown);
                inv.AddRelationshipObservation(new RelationshipObservation
                {
                    SourceProject = r.SourceProject ?? string.Empty,
                    SourceNamespace = r.SourceNamespace ?? string.Empty,
                    SourceType = r.SourceType ?? string.Empty,
                    SourceQualifiedName = r.SourceQualifiedName ?? string.Empty,
                    TargetDisplayName = r.TargetDisplayName ?? string.Empty,
                    TargetQualifiedName = r.TargetQualifiedName ?? string.Empty,
                    Kind = kind,
                    IsExternal = r.IsExternal,
                    Evidence = r.Evidence ?? string.Empty,
                });
            }

            // ProjectObservation
            if (dto.ProjectObservation != null)
            {
                var po = dto.ProjectObservation;
                inv.SetProjectObservation(new ProjectObservation
                {
                    Project = po.Project ?? string.Empty,
                    NamespaceCount = po.NamespaceCount,
                    TypeCount = po.TypeCount,
                    ClassCount = po.ClassCount,
                    InterfaceCount = po.InterfaceCount,
                    RecordCount = po.RecordCount,
                    StructCount = po.StructCount,
                    EnumCount = po.EnumCount,
                    DelegateCount = po.DelegateCount,
                    MemberCount = po.MemberCount,
                });
            }

            // RelationshipGraph — rebuild from flat edge list
            if (dto.RelationshipGraph != null && dto.RelationshipGraph.Edges.Count > 0)
            {
                var graph = new RepositoryRelationshipGraph();
                foreach (var edge in dto.RelationshipGraph.Edges)
                {
                    var relType = ParseEnum<RelationshipType>(edge.Type, RelationshipType.Unknown);
                    if (!string.IsNullOrWhiteSpace(edge.Source) && !string.IsNullOrWhiteSpace(edge.Target))
                    {
                        graph.AddRelationship(edge.Source, edge.Target, relType);
                    }
                }
                // Restore telemetry counter
                for (int i = 0; i < dto.RelationshipGraph.ExternalDependencyCandidateDiscardCount; i++)
                {
                    graph.IncrementExternalDependencyDiscardCount();
                }
                inv.SetRelationshipGraph(graph);
            }

            // RepositoryMetrics
            if (dto.RepositoryMetrics != null)
            {
                var rm = dto.RepositoryMetrics;
                inv.SetRepositoryMetrics(new RepositoryMetrics
                {
                    TotalProjects = rm.TotalProjects,
                    TotalNamespaces = rm.TotalNamespaces,
                    TotalTypes = rm.TotalTypes,
                    TotalRelationships = rm.TotalRelationships,
                    RootTypeCount = rm.RootTypeCount,
                    LeafTypeCount = rm.LeafTypeCount,
                    IsolatedTypeCount = rm.IsolatedTypeCount,
                });
            }

            // Transition to the correct final status
            if (status == InvestigationStatus.Completed)
            {
                inv.Complete();
            }
            // If status was Created, the investigation was never started — but we already called Start()
            // to add observations. For Created investigations (which shouldn't have data), this is acceptable.
            // The domain status is now at minimum Started.

            return inv;
        }

        private static T ParseEnum<T>(string? value, T defaultValue) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            return Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : defaultValue;
        }
    }
}
