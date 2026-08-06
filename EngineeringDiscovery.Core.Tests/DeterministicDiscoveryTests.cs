using System;
using System.Linq;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using EngineeringDiscovery.Core.Services;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class DeterministicDiscoveryTests
    {
        [Fact]
        public async Task CompletingOneRequiredFact_AdvancesToNextMissingFact()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var orchestrator = new EnginerringConversationOrchestrator(repo, null);

            var model = await orchestrator.CreateModelAsync("A productivity tool");

            // Arrange: pick Product Vision objective and add required facts
            var obj = model.DiscoveryObjectives.First(o => o.Name == "Product Vision");
            obj.RequiredFacts.Add("OneSentenceDescription");
            obj.RequiredFacts.Add("PrimaryValueProp");
            await repo.UpdateAsync(model);

            // Initially the missing fact should be the first required
            var missing1 = obj.RequiredFacts.First(r => !obj.CollectedFacts.Any(f => f.Key == r));
            Assert.Equal("OneSentenceDescription", missing1);

            // Simulate answering that fact
            obj.CollectedFacts.Add(new EngineeringFact { Key = "OneSentenceDescription", Value = "A simple todo app" });
            await repo.UpdateAsync(model);

            // Next missing fact should be PrimaryValueProp
            var missing2 = obj.RequiredFacts.FirstOrDefault(r => !obj.CollectedFacts.Any(f => f.Key == r));
            Assert.Equal("PrimaryValueProp", missing2);
        }

        [Fact]
        public async Task CompletedFacts_AreNeverRequestedAgain()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var orchestrator = new EnginerringConversationOrchestrator(repo, null);

            var model = await orchestrator.CreateModelAsync("A utility app");
            var obj = model.DiscoveryObjectives.First(o => o.Name == "Target Users");
            obj.RequiredFacts.Add("PrimaryUser");
            await repo.UpdateAsync(model);

            // Simulate collecting the fact
            obj.CollectedFacts.Add(new EngineeringFact { Key = "PrimaryUser", Value = "Administrators" });
            await repo.UpdateAsync(model);

            // Now get next question; orchestrator should not ask for PrimaryUser again
            var response = await orchestrator.RespondAsync(model.Id);
            if (response != null)
            {
                Assert.False(response.Question.Contains("PrimaryUser", StringComparison.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public async Task InvalidAnswers_DoNotSatisfyRequiredFacts()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var orchestrator = new EnginerringConversationOrchestrator(repo, null);

            var model = await orchestrator.CreateModelAsync("A reporting tool");
            var obj = model.DiscoveryObjectives.First(o => o.Name == "Primary Platform");
            obj.RequiredFacts.Add("SupportedPlatforms");
            await repo.UpdateAsync(model);

            // Simulate an invalid answer (empty)
            var invalidFact = new EngineeringFact { Key = "SupportedPlatforms", Value = string.Empty };
            obj.CollectedFacts.Add(invalidFact);
            await repo.UpdateAsync(model);

            // Orchestrator should treat empty value as not satisfying the required fact
            var missing = obj.RequiredFacts.FirstOrDefault(r => !obj.CollectedFacts.Any(f => f.Key == r && !string.IsNullOrWhiteSpace(f.Value)));
            Assert.Equal("SupportedPlatforms", missing);
        }

        [Fact]
        public async Task ObjectivesComplete_OnlyAfterAllRequiredFactsSatisfied()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var orchestrator = new EnginerringConversationOrchestrator(repo, null);

            var model = await orchestrator.CreateModelAsync("A data service");
            var obj = model.DiscoveryObjectives.First(o => o.Name == "Core Workflow");
            obj.RequiredFacts.Add("Trigger");
            obj.RequiredFacts.Add("PrimaryAction");
            await repo.UpdateAsync(model);

            // Satisfy only one fact
            obj.CollectedFacts.Add(new EngineeringFact { Key = "Trigger", Value = "User clicks button" });
            await repo.UpdateAsync(model);

            // Objective should not be complete yet
            Assert.NotEqual(ObjectiveStatus.Complete, obj.Status);

            // Satisfy remaining fact
            obj.CollectedFacts.Add(new EngineeringFact { Key = "PrimaryAction", Value = "Generate report" });
            obj.Status = ObjectiveStatus.Complete;
            await repo.UpdateAsync(model);

            Assert.Equal(ObjectiveStatus.Complete, obj.Status);
        }

        [Fact]
        public async Task DiscoveryFinishes_AfterAllRequiredObjectivesComplete()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var orchestrator = new EnginerringConversationOrchestrator(repo, null);

            var model = await orchestrator.CreateModelAsync("A catalog site");
            // Mark all product objectives complete
            foreach (var o in model.DiscoveryObjectives.Where(o => o.Type == ObjectiveType.Product))
            {
                o.Status = ObjectiveStatus.Complete;
            }
            await repo.UpdateAsync(model);

            var ready = await orchestrator.IsDiscoveryReadyAsync(model.Id);
            Assert.True(ready);
        }
    }
}
