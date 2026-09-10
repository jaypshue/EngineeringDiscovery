using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Services.Persistence;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class InvestigationPersistenceRoundTripTests : IDisposable
    {
        private readonly string _tempDir;

        public InvestigationPersistenceRoundTripTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "EngineOS_PersistenceTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private Investigation CreateRepresentativeInvestigation()
        {
            var inv = Investigation.Create(
                Guid.NewGuid(),
                @"C:\projects\TestProject",
                goal: "Assess for maintainability",
                owner: "test@example.com",
                target: "TestProject",
                architectureStatus: EngineeringStageStatus.InProgress,
                planningStatus: EngineeringStageStatus.Complete,
                developmentStatus: EngineeringStageStatus.NotStarted,
                verificationStatus: EngineeringStageStatus.NotStarted);

            inv.Start();

            // Findings
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Architecture, "Solution contains 3 projects."));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, "Core appears to be the Core domain layer."));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Dependency, "Web layer depends on Core domain layer."));

            // Artifacts
            inv.Artifacts.Add(new InvestigationArtifact(Guid.NewGuid(), "LargeService", "ServiceA has 42 methods", ArtifactType.LargeType));
            inv.Artifacts.Add(new InvestigationArtifact(Guid.NewGuid(), "LayerViolation1", "Tests depends on Infrastructure", ArtifactType.LayerViolation));

            // TypeObservations
            inv.AddTypeObservation(new TypeObservation
            {
                Project = "TestProject.Core",
                Namespace = "TestProject.Core.Services",
                TypeName = "UserService",
                QualifiedName = "TestProject.Core:TestProject.Core.Services.UserService",
                Kind = TypeKind.Class,
                Accessibility = "public",
                IsAbstract = false,
                IsStatic = false,
                MethodCount = 12,
                PropertyCount = 3,
                MemberCount = 15,
                PublicMemberCount = 10,
                IsRootType = false,
                IsLeafType = true,
                DerivedTypeCount = 0,
                IncomingDependencyCount = 5,
                OutgoingDependencyCount = 2,
                IsDependencyHub = true,
            });
            inv.AddTypeObservation(new TypeObservation
            {
                Project = "TestProject.Web",
                Namespace = "TestProject.Web.Controllers",
                TypeName = "UsersController",
                QualifiedName = "TestProject.Web:TestProject.Web.Controllers.UsersController",
                Kind = TypeKind.Class,
                Accessibility = "public",
                MethodCount = 5,
                MemberCount = 5,
            });

            // NamespaceObservations
            inv.AddNamespaceObservation(new NamespaceObservation
            {
                Project = "TestProject.Core",
                NamespaceName = "TestProject.Core.Services",
                TypeCount = 8,
                ClassCount = 6,
                InterfaceCount = 2,
                PublicTypeCount = 7,
                InternalTypeCount = 1,
            });

            // MemberObservations
            inv.AddMemberObservation(new MemberObservation
            {
                Project = "TestProject.Core",
                Namespace = "TestProject.Core.Services",
                Type = "UserService",
                MemberName = "GetUserAsync",
                Visibility = Visibility.Public,
                IsAsync = true,
                ParameterCount = 1,
                ApproximateSourceLines = 25,
                ReturnType = "Task<User>",
            });

            // DiscoveryObservations
            inv.AddObservation(new DiscoveryObservation
            {
                Kind = ObservationKind.Project,
                Project = "TestProject.Core",
                Description = "Core domain project with 8 types."
            });

            // RelationshipObservations
            inv.AddRelationshipObservation(new RelationshipObservation
            {
                SourceProject = "TestProject.Web",
                SourceNamespace = "TestProject.Web.Controllers",
                SourceType = "UsersController",
                SourceQualifiedName = "TestProject.Web:TestProject.Web.Controllers.UsersController",
                TargetDisplayName = "IUserService",
                TargetQualifiedName = "TestProject.Core:TestProject.Core.Services.IUserService",
                Kind = RelationshipKind.Implements,
                IsExternal = false,
                Evidence = "Constructor injection"
            });

            // ProjectObservation
            inv.SetProjectObservation(new ProjectObservation
            {
                Project = "TestProject",
                NamespaceCount = 12,
                TypeCount = 45,
                ClassCount = 30,
                InterfaceCount = 10,
                RecordCount = 3,
                MemberCount = 280,
            });

            // RelationshipGraph
            var graph = new RepositoryRelationshipGraph();
            graph.AddRelationship(
                "TestProject.Core:TestProject.Core.Services.UserService",
                "TestProject.Core:TestProject.Core.Services.IUserService",
                RelationshipType.Implementation);
            graph.AddRelationship(
                "TestProject.Web:TestProject.Web.Controllers.UsersController",
                "TestProject.Core:TestProject.Core.Services.UserService",
                RelationshipType.Dependency);
            graph.AddInheritance(
                "TestProject.Core:TestProject.Core.Services.AdminService",
                "TestProject.Core:TestProject.Core.Services.UserService");
            graph.IncrementExternalDependencyDiscardCount();
            graph.IncrementExternalDependencyDiscardCount();
            inv.SetRelationshipGraph(graph);

            // RepositoryMetrics
            inv.SetRepositoryMetrics(new RepositoryMetrics
            {
                TotalProjects = 3,
                TotalNamespaces = 12,
                TotalTypes = 45,
                TotalRelationships = 3,
                RootTypeCount = 5,
                LeafTypeCount = 20,
                IsolatedTypeCount = 8,
            });

            inv.Complete();
            return inv;
        }

        [Fact]
        public void Mapper_RoundTrip_PreservesAllData()
        {
            var original = CreateRepresentativeInvestigation();

            var dto = InvestigationPersistenceMapper.ToDto(original);
            var restored = InvestigationPersistenceMapper.FromDto(dto);

            AssertInvestigationsEquivalent(original, restored);
        }

        [Fact]
        public async Task FileWorkspacePersistence_RoundTrip_PreservesInvestigation()
        {
            var persistence = new FileWorkspacePersistence(_tempDir);
            var original = CreateRepresentativeInvestigation();

            var workspace = new Workspace();
            workspace.Investigation = original;
            workspace.ImportedRepositories.Add(new ImportedRepository
            {
                RepositoryPath = @"C:\projects\TestProject",
                Investigation = original
            });

            await persistence.SaveAsync(workspace);
            var loaded = await persistence.LoadAsync();

            Assert.NotNull(loaded);
            Assert.NotNull(loaded!.Investigation);
            AssertInvestigationsEquivalent(original, loaded.Investigation!);

            // Also verify the ImportedRepository investigation
            Assert.Single(loaded.ImportedRepositories);
            Assert.NotNull(loaded.ImportedRepositories[0].Investigation);
            AssertInvestigationsEquivalent(original, loaded.ImportedRepositories[0].Investigation!);
        }

        [Fact]
        public async Task FileWorkspacePersistence_LoadsOldFormat_WithoutCrashing()
        {
            // Simulate a workspace persisted by an older version (no investigation DTO, just basic fields)
            var oldJson = """
            {
                "Id": "00000000-0000-0000-0000-000000000001",
                "SchemaVersion": "1",
                "RepositoryPath": "C:\\old\\repo",
                "Investigation": null,
                "ImportedRepositories": []
            }
            """;
            var filePath = Path.Combine(_tempDir, "workspace.json");
            await File.WriteAllTextAsync(filePath, oldJson);

            var persistence = new FileWorkspacePersistence(_tempDir);
            var loaded = await persistence.LoadAsync();

            Assert.NotNull(loaded);
            Assert.Null(loaded!.Investigation);
        }

        [Fact]
        public void Mapper_TypeObservations_PreserveCounts()
        {
            var original = CreateRepresentativeInvestigation();
            var dto = InvestigationPersistenceMapper.ToDto(original);
            var restored = InvestigationPersistenceMapper.FromDto(dto);

            Assert.Equal(original.TypeObservations.Count, restored.TypeObservations.Count);
            var originalType = original.TypeObservations.First(t => t.TypeName == "UserService");
            var restoredType = restored.TypeObservations.First(t => t.TypeName == "UserService");

            Assert.Equal(originalType.QualifiedName, restoredType.QualifiedName);
            Assert.Equal(originalType.Kind, restoredType.Kind);
            Assert.Equal(originalType.MethodCount, restoredType.MethodCount);
            Assert.Equal(originalType.MemberCount, restoredType.MemberCount);
            Assert.Equal(originalType.IncomingDependencyCount, restoredType.IncomingDependencyCount);
            Assert.Equal(originalType.IsDependencyHub, restoredType.IsDependencyHub);
            Assert.Equal(originalType.IsLeafType, restoredType.IsLeafType);
        }

        [Fact]
        public void Mapper_Findings_PreserveTypeAndDescription()
        {
            var original = CreateRepresentativeInvestigation();
            var dto = InvestigationPersistenceMapper.ToDto(original);
            var restored = InvestigationPersistenceMapper.FromDto(dto);

            Assert.Equal(original.Findings.Count, restored.Findings.Count);
            for (int i = 0; i < original.Findings.Count; i++)
            {
                Assert.Equal(original.Findings[i].Type, restored.Findings[i].Type);
                Assert.Equal(original.Findings[i].Description, restored.Findings[i].Description);
            }
        }

        [Fact]
        public void Mapper_Artifacts_PreserveAllFields()
        {
            var original = CreateRepresentativeInvestigation();
            var dto = InvestigationPersistenceMapper.ToDto(original);
            var restored = InvestigationPersistenceMapper.FromDto(dto);

            Assert.Equal(original.Artifacts.Count, restored.Artifacts.Count);
            for (int i = 0; i < original.Artifacts.Count; i++)
            {
                Assert.Equal(original.Artifacts[i].Title, restored.Artifacts[i].Title);
                Assert.Equal(original.Artifacts[i].Description, restored.Artifacts[i].Description);
                Assert.Equal(original.Artifacts[i].Type, restored.Artifacts[i].Type);
            }
        }

        [Fact]
        public void Mapper_RelationshipGraph_PreservesEdgesAndTypes()
        {
            var original = CreateRepresentativeInvestigation();
            var dto = InvestigationPersistenceMapper.ToDto(original);
            var restored = InvestigationPersistenceMapper.FromDto(dto);

            Assert.NotNull(restored.RelationshipGraph);

            // Count edges by type
            var origInheritance = original.RelationshipGraph!.GetRelationships(RelationshipType.Inheritance).Count();
            var origDependency = original.RelationshipGraph.GetRelationships(RelationshipType.Dependency).Count();
            var origImplementation = original.RelationshipGraph.GetRelationships(RelationshipType.Implementation).Count();

            var restoredInheritance = restored.RelationshipGraph!.GetRelationships(RelationshipType.Inheritance).Count();
            var restoredDependency = restored.RelationshipGraph.GetRelationships(RelationshipType.Dependency).Count();
            var restoredImplementation = restored.RelationshipGraph.GetRelationships(RelationshipType.Implementation).Count();

            Assert.Equal(origInheritance, restoredInheritance);
            Assert.Equal(origDependency, restoredDependency);
            Assert.Equal(origImplementation, restoredImplementation);

            // Verify specific edges exist
            var deps = restored.RelationshipGraph.GetRelationships(RelationshipType.Dependency).ToList();
            Assert.Contains(deps, e => e.Source.Contains("UsersController") && e.Target.Contains("UserService"));

            // Telemetry counter
            Assert.Equal(
                original.RelationshipGraph.ExternalDependencyCandidateDiscardCount,
                restored.RelationshipGraph.ExternalDependencyCandidateDiscardCount);
        }

        [Fact]
        public void Mapper_RepositoryMetrics_PreservesCounts()
        {
            var original = CreateRepresentativeInvestigation();
            var dto = InvestigationPersistenceMapper.ToDto(original);
            var restored = InvestigationPersistenceMapper.FromDto(dto);

            Assert.NotNull(restored.RepositoryMetrics);
            Assert.Equal(original.RepositoryMetrics!.TotalProjects, restored.RepositoryMetrics!.TotalProjects);
            Assert.Equal(original.RepositoryMetrics.TotalTypes, restored.RepositoryMetrics.TotalTypes);
            Assert.Equal(original.RepositoryMetrics.TotalRelationships, restored.RepositoryMetrics.TotalRelationships);
            Assert.Equal(original.RepositoryMetrics.RootTypeCount, restored.RepositoryMetrics.RootTypeCount);
            Assert.Equal(original.RepositoryMetrics.LeafTypeCount, restored.RepositoryMetrics.LeafTypeCount);
        }

        [Fact]
        public void Mapper_NamespaceObservations_Preserved()
        {
            var original = CreateRepresentativeInvestigation();
            var dto = InvestigationPersistenceMapper.ToDto(original);
            var restored = InvestigationPersistenceMapper.FromDto(dto);

            Assert.Equal(original.NamespaceObservations.Count, restored.NamespaceObservations.Count);
            var origNs = original.NamespaceObservations[0];
            var restoredNs = restored.NamespaceObservations[0];
            Assert.Equal(origNs.NamespaceName, restoredNs.NamespaceName);
            Assert.Equal(origNs.TypeCount, restoredNs.TypeCount);
            Assert.Equal(origNs.ClassCount, restoredNs.ClassCount);
            Assert.Equal(origNs.InterfaceCount, restoredNs.InterfaceCount);
        }

        [Fact]
        public void Mapper_MemberObservations_Preserved()
        {
            var original = CreateRepresentativeInvestigation();
            var dto = InvestigationPersistenceMapper.ToDto(original);
            var restored = InvestigationPersistenceMapper.FromDto(dto);

            Assert.Equal(original.MemberObservations.Count, restored.MemberObservations.Count);
            var origMem = original.MemberObservations[0];
            var restoredMem = restored.MemberObservations[0];
            Assert.Equal(origMem.MemberName, restoredMem.MemberName);
            Assert.Equal(origMem.Visibility, restoredMem.Visibility);
            Assert.Equal(origMem.IsAsync, restoredMem.IsAsync);
            Assert.Equal(origMem.ParameterCount, restoredMem.ParameterCount);
            Assert.Equal(origMem.ApproximateSourceLines, restoredMem.ApproximateSourceLines);
        }

        [Fact]
        public void Mapper_ProjectObservation_Preserved()
        {
            var original = CreateRepresentativeInvestigation();
            var dto = InvestigationPersistenceMapper.ToDto(original);
            var restored = InvestigationPersistenceMapper.FromDto(dto);

            Assert.NotNull(restored.ProjectObservation);
            Assert.Equal(original.ProjectObservation!.Project, restored.ProjectObservation!.Project);
            Assert.Equal(original.ProjectObservation.TypeCount, restored.ProjectObservation.TypeCount);
            Assert.Equal(original.ProjectObservation.NamespaceCount, restored.ProjectObservation.NamespaceCount);
            Assert.Equal(original.ProjectObservation.MemberCount, restored.ProjectObservation.MemberCount);
        }

        [Fact]
        public void Mapper_InvestigationStatus_Preserved()
        {
            var original = CreateRepresentativeInvestigation();
            var dto = InvestigationPersistenceMapper.ToDto(original);
            var restored = InvestigationPersistenceMapper.FromDto(dto);

            Assert.Equal(InvestigationStatus.Completed, restored.Status);
            Assert.Equal(original.RepositoryPath, restored.RepositoryPath);
            Assert.Equal(original.Target, restored.Target);
            Assert.Equal(original.Goal, restored.Goal);
        }

        [Fact]
        public void Mapper_StageStatuses_Preserved()
        {
            var original = CreateRepresentativeInvestigation();
            var dto = InvestigationPersistenceMapper.ToDto(original);
            var restored = InvestigationPersistenceMapper.FromDto(dto);

            Assert.Equal(EngineeringStageStatus.InProgress, restored.ArchitectureStatus);
            Assert.Equal(EngineeringStageStatus.Complete, restored.PlanningStatus);
            Assert.Equal(EngineeringStageStatus.NotStarted, restored.DevelopmentStatus);
            Assert.Equal(EngineeringStageStatus.NotStarted, restored.VerificationStatus);
        }

        [Fact]
        public async Task FullWorkspaceRoundTrip_InvestigationSummaryProducesData()
        {
            var persistence = new FileWorkspacePersistence(_tempDir);
            var original = CreateRepresentativeInvestigation();

            var workspace = new Workspace();
            workspace.Investigation = original;

            await persistence.SaveAsync(workspace);
            var loaded = await persistence.LoadAsync();

            Assert.NotNull(loaded?.Investigation);

            // Verify InvestigationSummary produces meaningful data from persisted investigation
            var summary = InvestigationSummary.CreateFrom(loaded!.Investigation!);
            Assert.True(summary.TypeCount > 0, "TypeCount should be > 0 after persistence round-trip");
            Assert.True(summary.NamespaceCount > 0, "NamespaceCount should be > 0");
            Assert.True(summary.TotalArtifacts > 0, "TotalArtifacts should be > 0");
            Assert.Equal("TestProject", summary.RepositoryName);
        }

        private static void AssertInvestigationsEquivalent(Investigation original, Investigation restored)
        {
            // Identity
            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(original.RepositoryPath, restored.RepositoryPath);
            Assert.Equal(original.Target, restored.Target);
            Assert.Equal(original.Goal, restored.Goal);
            Assert.Equal(original.Owner, restored.Owner);

            // Status
            Assert.Equal(original.Status, restored.Status);

            // Stage statuses
            Assert.Equal(original.ArchitectureStatus, restored.ArchitectureStatus);
            Assert.Equal(original.PlanningStatus, restored.PlanningStatus);
            Assert.Equal(original.DevelopmentStatus, restored.DevelopmentStatus);
            Assert.Equal(original.VerificationStatus, restored.VerificationStatus);

            // Collection counts
            Assert.Equal(original.Findings.Count, restored.Findings.Count);
            Assert.Equal(original.Artifacts.Count, restored.Artifacts.Count);
            Assert.Equal(original.Observations.Count, restored.Observations.Count);
            Assert.Equal(original.TypeObservations.Count, restored.TypeObservations.Count);
            Assert.Equal(original.NamespaceObservations.Count, restored.NamespaceObservations.Count);
            Assert.Equal(original.MemberObservations.Count, restored.MemberObservations.Count);
            Assert.Equal(original.RelationshipObservations.Count, restored.RelationshipObservations.Count);

            // ProjectObservation
            Assert.Equal(original.ProjectObservation != null, restored.ProjectObservation != null);

            // RelationshipGraph
            Assert.Equal(original.RelationshipGraph != null, restored.RelationshipGraph != null);
            if (original.RelationshipGraph != null)
            {
                var origTotal = original.RelationshipGraph.GetRelationships(RelationshipType.Inheritance).Count()
                    + original.RelationshipGraph.GetRelationships(RelationshipType.Dependency).Count()
                    + original.RelationshipGraph.GetRelationships(RelationshipType.Implementation).Count();
                var restoredTotal = restored.RelationshipGraph!.GetRelationships(RelationshipType.Inheritance).Count()
                    + restored.RelationshipGraph.GetRelationships(RelationshipType.Dependency).Count()
                    + restored.RelationshipGraph.GetRelationships(RelationshipType.Implementation).Count();
                Assert.Equal(origTotal, restoredTotal);
            }

            // RepositoryMetrics
            Assert.Equal(original.RepositoryMetrics != null, restored.RepositoryMetrics != null);
        }

        [Fact]
        public async Task PersistedWorkspace_RestoresFreshnessMetadata()
        {
            var persistence = new FileWorkspacePersistence(_tempDir);
            var investigation = CreateRepresentativeInvestigation();

            var workspace = new Workspace();
            workspace.Investigation = investigation;
            workspace.ImportedRepositories.Add(new ImportedRepository
            {
                RepositoryPath = @"C:\projects\TestProject",
                Investigation = investigation
            });
            workspace.SetFreshness(new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), "test-fingerprint:abc123");

            await persistence.SaveAsync(workspace);
            var loaded = await persistence.LoadAsync();

            Assert.NotNull(loaded);
            Assert.NotNull(loaded!.LastBuiltUtc);
            Assert.Equal(new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), loaded.LastBuiltUtc);
            Assert.Equal("test-fingerprint:abc123", loaded.RepositoryFingerprint);
        }

        [Fact]
        public async Task PersistedWorkspace_HasWorkspaceReturnsTrue_WhenInvestigationPresent()
        {
            var persistence = new FileWorkspacePersistence(_tempDir);
            var investigation = CreateRepresentativeInvestigation();

            var workspace = new Workspace();
            workspace.Investigation = investigation;
            workspace.ImportedRepositories.Add(new ImportedRepository
            {
                RepositoryPath = @"C:\projects\TestProject",
                Investigation = investigation
            });

            await persistence.SaveAsync(workspace);
            var loaded = await persistence.LoadAsync();

            Assert.NotNull(loaded);
            Assert.False(loaded!.IsEmpty());
            Assert.NotNull(loaded.Investigation);
            Assert.NotEmpty(loaded.ImportedRepositories);
        }

        [Fact]
        public async Task PersistedWorkspace_ActiveProject_RestoredCorrectly()
        {
            var persistence = new FileWorkspacePersistence(_tempDir);
            var investigation = CreateRepresentativeInvestigation();

            var workspace = new Workspace();
            workspace.Investigation = investigation;
            workspace.ImportedRepositories.Add(new ImportedRepository
            {
                RepositoryPath = @"C:\projects\TestProject",
                Investigation = investigation
            });

            var project = new Project();
            project.State.Identity = new EngineeringDiscovery.Core.Domain.ProjectState.ProjectIdentity
            {
                Name = "TestProject",
                Description = "Imported from C:\\projects\\TestProject"
            };
            project.RepositoryPaths.Add(@"C:\projects\TestProject");
            workspace.Projects.Add(project);
            workspace.ActiveProjectId = project.Id;

            await persistence.SaveAsync(workspace);
            var loaded = await persistence.LoadAsync();

            Assert.NotNull(loaded);
            Assert.NotNull(loaded!.ActiveProjectId);
            Assert.Equal(project.Id, loaded.ActiveProjectId);
            Assert.NotNull(loaded.ActiveProject);
            Assert.Equal("TestProject", loaded.ActiveProject!.Name);
            Assert.Contains(@"C:\projects\TestProject", loaded.ActiveProject.RepositoryPaths);
        }
    }
}
