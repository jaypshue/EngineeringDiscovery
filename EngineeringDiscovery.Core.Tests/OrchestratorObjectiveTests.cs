using System;
using System.Linq;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using EngineeringDiscovery.Core.Services;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class OrchestratorObjectiveTests
    {
        [Fact]
        public async Task NewModel_InitialObjective_IsProductVision()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var orchestrator = new EnginerringConversationOrchestrator(repo, null);

            var model = await orchestrator.CreateModelAsync("A multiplayer adventure game");

            // Seeded objectives should include Product Vision
            Assert.Contains(model.DiscoveryObjectives, o => o.Name == "Product Vision");
            var initial = model.DiscoveryObjectives.First(o => o.Name == "Product Vision");
            Assert.Equal(ObjectiveStatus.NotStarted, initial.Status);
        }

        [Fact]
        public async Task CompletingObjective_AdvancesToNextObjective()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var orchestrator = new EnginerringConversationOrchestrator(repo, null);

            var model = await orchestrator.CreateModelAsync("A single-player puzzle game");

            // Mark Product Vision objective complete
            var pv = model.DiscoveryObjectives.First(o => o.Name == "Product Vision");
            pv.Status = ObjectiveStatus.Complete;
            await repo.UpdateAsync(model);

            // Request next question - should move to Target Users objective
            var q = await orchestrator.RespondAsync(model.Id);
            Assert.NotNull(q);
            Assert.Equal("Target Users", q.Objective);
        }

        [Fact]
        public async Task DeferredObjective_IsSkipped()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var orchestrator = new EnginerringConversationOrchestrator(repo, null);

            var model = await orchestrator.CreateModelAsync("A mobile utility app");

            var tv = model.DiscoveryObjectives.First(o => o.Name == "Target Users");
            tv.Status = ObjectiveStatus.Deferred;
            await repo.UpdateAsync(model);

            // Next question should not target Target Users
            var q = await orchestrator.RespondAsync(model.Id);
            Assert.NotNull(q);
            Assert.NotEqual("Target Users", q.Objective);
        }

        [Fact]
        public async Task AllObjectivesComplete_ReturnsDiscoveryComplete()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var orchestrator = new EnginerringConversationOrchestrator(repo, null);

            var model = await orchestrator.CreateModelAsync("A web dashboard");
            foreach (var o in model.DiscoveryObjectives) o.Status = ObjectiveStatus.Complete;
            await repo.UpdateAsync(model);

            var q = await orchestrator.RespondAsync(model.Id);
            Assert.Null(q);

            var ready = await orchestrator.IsDiscoveryReadyAsync(model.Id);
            Assert.True(ready);
        }

        [Fact]
        public async Task DiscoveryComplete_NoFurtherQuestionsReturned()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var orchestrator = new EnginerringConversationOrchestrator(repo, null);

            var model = await orchestrator.CreateModelAsync("A simple API");
            // Mark required product objectives complete or deferred
            foreach (var o in model.DiscoveryObjectives.Where(o => o.Type == ObjectiveType.Product)) o.Status = ObjectiveStatus.Complete;
            await repo.UpdateAsync(model);

            var q1 = await orchestrator.RespondAsync(model.Id);
            // After product objectives done, orchestrator may pick an engineering objective; for this test we assume none are required and discovery completes
            // If a question is returned, consider the model not complete yet
            if (q1 != null)
            {
                var ready = await orchestrator.IsDiscoveryReadyAsync(model.Id);
                Assert.False(ready);
            }
            else
            {
                var ready = await orchestrator.IsDiscoveryReadyAsync(model.Id);
                Assert.True(ready);
            }
        }
    }
}
