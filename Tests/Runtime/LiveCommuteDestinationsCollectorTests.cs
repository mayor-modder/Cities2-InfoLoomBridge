using System;
using InfoLoomBridge.Runtime;
using Xunit;

namespace InfoLoomBridge.Tests.Runtime
{
    public sealed class LiveCommuteDestinationsCollectorTests
    {
        [Fact]
        public void Collect_returns_failure_when_entity_manager_is_unavailable()
        {
            var collector = new LiveCommuteDestinationsCollector(() => null, _ => null, topProviderLimit: 10);

            CommuteDestinationsCollectionResult result = collector.Collect();

            Assert.False(result.IsSuccess);
            Assert.Contains("entity manager", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        }
    }
}
