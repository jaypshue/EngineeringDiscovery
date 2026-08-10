using System;
using Xunit;
using EngineeringDiscovery.Core.Domain.Iteration;

namespace EngineeringDiscovery.Core.Tests.Iteration
{
    public class EngineeringIterationTests
    {
        [Fact]
        public void CanCreateIterationAndAddSteps()
        {
            var it = new EngineeringIteration();
            it.Goal = "Investigate post-import explorer issue";

            Assert.Equal(0, it.Steps.Count);

            var step1 = new EngineeringIterationStep { Prompt = "Step 1 prompt" };
            it.Steps.Add(step1);

            Assert.Single(it.Steps);
            Assert.Equal("Step 1 prompt", it.Steps[0].Prompt);
        }

        [Fact]
        public void StepsPersistOrderWhenAddingMultiple()
        {
            var it = new EngineeringIteration();
            it.Steps.Add(new EngineeringIterationStep { Prompt = "First" });
            it.Steps.Add(new EngineeringIterationStep { Prompt = "Second" });

            Assert.Equal(2, it.Steps.Count);
            Assert.Equal("First", it.Steps[0].Prompt);
            Assert.Equal("Second", it.Steps[1].Prompt);
        }

        [Fact]
        public void CopilotResponseAndHumanObservationAssignedToCorrectStep()
        {
            var it = new EngineeringIteration();
            var step = new EngineeringIterationStep { Prompt = "Fix X" };
            it.Steps.Add(step);

            step.CopilotResponse = "Use Y approach";
            step.HumanObservation = "Test failed with exception";

            Assert.Equal("Use Y approach", it.Steps[0].CopilotResponse);
            Assert.Equal("Test failed with exception", it.Steps[0].HumanObservation);
        }

        [Fact]
        public void IterationCanRepresentCurrentStep()
        {
            var it = new EngineeringIteration();
            it.Status = IterationStatus.InProgress;
            var s = new EngineeringIterationStep { Prompt = "current" };
            it.Steps.Add(s);

            Assert.Equal(IterationStatus.InProgress, it.Status);
            Assert.Equal("current", it.Steps[^1].Prompt);
        }
    }
}
