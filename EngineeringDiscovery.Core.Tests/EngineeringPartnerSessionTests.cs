using System;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Services;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public sealed class EngineeringPartnerSessionTests
    {
        [Fact]
        public async Task ImportStyleSessionCreationDoesNotReusePriorEmptySession()
        {
            var partner = new EngineeringPartner(new InMemoryEngineeringModelRepository());

            var prior = await partner.StartSessionAsync(string.Empty);
            var imported = await partner.StartSessionAsync(string.Empty, reuseExisting: false);
            var resumed = await partner.StartSessionAsync(string.Empty);

            Assert.NotEqual(prior.Id, imported.Id);
            Assert.Equal(imported.Id, resumed.Id);
        }
    }
}
