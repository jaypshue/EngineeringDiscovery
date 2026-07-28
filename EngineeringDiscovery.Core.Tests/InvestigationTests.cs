using System;
using Xunit;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Core.Tests
{
    public class InvestigationTests
    {
        [Fact]
        public void Create_SetsIdAndCreatedStatus()
        {
            var id = Guid.NewGuid();
            var inv = Investigation.Create(id, "repo/path");

            Assert.Equal(id, inv.Id);
            Assert.Equal(InvestigationStatus.Created, inv.Status);
        }

        [Fact]
        public void Start_FromCreated_Succeeds()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "repo");
            inv.Start();

            Assert.Equal(InvestigationStatus.Started, inv.Status);
            Assert.NotNull(inv.StartedAt);
        }

        [Fact]
        public void Start_FromNonCreated_Throws()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "repo");
            inv.Start();

            Assert.Throws<InvalidOperationException>(() => inv.Start());
        }

        [Fact]
        public void Complete_FromStarted_Succeeds()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "repo");
            inv.Start();
            inv.Complete();

            Assert.Equal(InvestigationStatus.Completed, inv.Status);
            Assert.NotNull(inv.CompletedAt);
        }

        [Fact]
        public void Complete_FromNonStarted_Throws()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "repo");

            Assert.Throws<InvalidOperationException>(() => inv.Complete());
        }

        [Fact]
        public void Reopen_FromCompleted_Succeeds()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "repo");
            inv.Start();
            inv.Complete();
            inv.Reopen();

            Assert.Equal(InvestigationStatus.Started, inv.Status);
            Assert.Null(inv.CompletedAt);
        }

        [Fact]
        public void AddFinding_WhenStarted_AddsFinding()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "repo");
            inv.Start();

            var f = new Finding(Guid.NewGuid(), "desc");
            inv.AddFinding(f);

            Assert.Contains(f, inv.Findings);
        }

        [Fact]
        public void AddFinding_WhenNotStarted_Throws()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "repo");
            var f = new Finding(Guid.NewGuid(), "desc");

            Assert.Throws<InvalidOperationException>(() => inv.AddFinding(f));
        }
    }
}
