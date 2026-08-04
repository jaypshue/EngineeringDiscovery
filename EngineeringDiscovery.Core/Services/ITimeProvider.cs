using System;

namespace EngineeringDiscovery.Core.Services
{
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }
}
