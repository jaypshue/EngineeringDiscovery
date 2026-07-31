using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    internal interface IObservationEnrichmentPass
    {
        void Enrich(Investigation investigation);
    }
}
