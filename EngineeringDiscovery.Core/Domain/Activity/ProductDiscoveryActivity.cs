using System;

namespace EngineeringDiscovery.Core.Domain.Activity
{
    public sealed class ProductDiscoveryActivity : EngineeringActivity
    {
        public ProductDiscoveryActivity()
        {
            ActivityType = ActivityType.ProductDiscovery;
            Status = ActivityStatus.Active;
            Title = "Build EngineOS";
            Intent = new System.Collections.Generic.List<string>
            {
                "Build an Engineering Operating System that continuously improves engineering understanding."
            };
            // initial observation
            var obs = new EngineeringObservation
            {
                Description = "I have an idea.",
                Source = ObservationSource.Human,
                ObservationType = ObservationType.Product,
                Confidence = 100
            };
            AddObservation(obs);

            // initial hypothesis
            var h = new EngineeringHypothesis
            {
                Description = "The engineering workflow for this product has not yet been discovered.",
                Status = HypothesisStatus.Active,
                Confidence = 100
            };
            AddHypothesis(h);
        }
    }
}
