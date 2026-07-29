using System;
using Xunit;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Domain;

namespace EngineeringDiscovery.Core.Tests
{
    public class InvestigationAggregateTests
    {
        [Fact]
        public void Create_AssignsProperties_And_DefaultsStagesToNotStarted()
        {
            // Arrange
            var id = Guid.NewGuid();
            var repoPath = "/";
            var goal = "Improve maintainability";
            var owner = "owner@example.com";
            var target = "monorepo/component";

            // Act
            var inv = Investigation.Create(id, repoPath, goal, owner, target);

            // Assert
            Assert.Equal(id, inv.Id);
            Assert.Equal(goal, inv.Goal);
            Assert.Equal(owner, inv.Owner);
            Assert.Equal(target, inv.Target);
            Assert.Equal(EngineeringStageStatus.NotStarted, inv.ArchitectureStatus);
            Assert.Equal(EngineeringStageStatus.NotStarted, inv.PlanningStatus);
            Assert.Equal(EngineeringStageStatus.NotStarted, inv.DevelopmentStatus);
            Assert.Equal(EngineeringStageStatus.NotStarted, inv.VerificationStatus);
            Assert.Equal(InvestigationStatus.Created, inv.Status);
        }

        [Fact]
        public void Create_Throws_When_IdEmpty()
        {
            // Arrange
            var id = Guid.Empty;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Investigation.Create(id, "/"));
        }

        [Fact]
        public void Create_Throws_When_TargetMissing()
        {
            // Arrange
            var id = Guid.NewGuid();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Investigation.Create(id, ""));
        }

        [Fact]
        public void Start_FromCreated_Succeeds()
        {
            // Arrange
            var inv = Investigation.Create(Guid.NewGuid(), "/");

            // Act
            inv.Start();

            // Assert
            Assert.Equal(InvestigationStatus.Started, inv.Status);
            Assert.NotNull(inv.StartedAt);
        }

        [Fact]
        public void Start_FromNonCreated_Throws()
        {
            // Arrange
            var inv = Investigation.Create(Guid.NewGuid(), "/");
            inv.Start();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => inv.Start());
        }

        [Fact]
        public void Complete_FromStarted_Succeeds()
        {
            // Arrange
            var inv = Investigation.Create(Guid.NewGuid(), "/");
            inv.Start();

            // Act
            inv.Complete();

            // Assert
            Assert.Equal(InvestigationStatus.Completed, inv.Status);
            Assert.NotNull(inv.CompletedAt);
        }

        [Fact]
        public void Complete_FromNonStarted_Throws()
        {
            // Arrange
            var inv = Investigation.Create(Guid.NewGuid(), "/");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => inv.Complete());
        }

        [Fact]
        public void Reopen_FromCompleted_Succeeds()
        {
            // Arrange
            var inv = Investigation.Create(Guid.NewGuid(), "/");
            inv.Start();
            inv.Complete();

            // Act
            inv.Reopen();

            // Assert
            Assert.Equal(InvestigationStatus.Started, inv.Status);
            Assert.Null(inv.CompletedAt);
        }

        [Fact]
        public void AddFinding_WhenStarted_AddsFinding()
        {
            // Arrange
            var inv = Investigation.Create(Guid.NewGuid(), "/");
            inv.Start();
            var f = new Finding(Guid.NewGuid(), "desc");

            // Act
            inv.AddFinding(f);

            // Assert
            Assert.Contains(f, inv.Findings);
        }

        [Fact]
        public void AddFinding_WhenNotStarted_Throws()
        {
            // Arrange
            var inv = Investigation.Create(Guid.NewGuid(), "/");
            var f = new Finding(Guid.NewGuid(), "desc");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => inv.AddFinding(f));
        }

        [Fact]
        public void SetArchitectureStatus_AllowsTransitions()
        {
            // Arrange
            var inv = Investigation.Create(Guid.NewGuid(), "/");

            // Act
            inv.SetArchitectureStatus(EngineeringStageStatus.InProgress);

            // Assert
            Assert.Equal(EngineeringStageStatus.InProgress, inv.ArchitectureStatus);

            // Act
            inv.SetArchitectureStatus(EngineeringStageStatus.Complete);

            // Assert
            Assert.Equal(EngineeringStageStatus.Complete, inv.ArchitectureStatus);
        }

        [Fact]
        public void SetPlanningStatus_AllowsInProgress()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "/");
            inv.SetPlanningStatus(EngineeringStageStatus.InProgress);
            Assert.Equal(EngineeringStageStatus.InProgress, inv.PlanningStatus);
        }

        [Fact]
        public void SetDevelopmentStatus_AllowsComplete()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "/");
            inv.SetDevelopmentStatus(EngineeringStageStatus.Complete);
            Assert.Equal(EngineeringStageStatus.Complete, inv.DevelopmentStatus);
        }

        [Fact]
        public void SetVerificationStatus_AllowsComplete()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "/");
            inv.SetVerificationStatus(EngineeringStageStatus.Complete);
            Assert.Equal(EngineeringStageStatus.Complete, inv.VerificationStatus);
        }

        [Fact]
        public void StageUpdates_AreIndependent()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "/");
            inv.SetArchitectureStatus(EngineeringStageStatus.InProgress);
            inv.SetPlanningStatus(EngineeringStageStatus.InProgress);

            Assert.Equal(EngineeringStageStatus.InProgress, inv.ArchitectureStatus);
            Assert.Equal(EngineeringStageStatus.InProgress, inv.PlanningStatus);
            Assert.Equal(EngineeringStageStatus.NotStarted, inv.DevelopmentStatus);
            Assert.Equal(EngineeringStageStatus.NotStarted, inv.VerificationStatus);
        }

        // Note: The aggregate does not currently prevent updating stages when Completed.
        // This behaviour is documented here as executable tests assert the current model.
        [Fact]
        public void StageUpdate_AfterComplete_IsAllowed_Currently()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "/");
            inv.Start();
            inv.Complete();

            // act
            inv.SetArchitectureStatus(EngineeringStageStatus.Complete);

            // assert - current behaviour allows this
            Assert.Equal(EngineeringStageStatus.Complete, inv.ArchitectureStatus);
        }

    }
}
