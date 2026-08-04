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
        }
    }
}
