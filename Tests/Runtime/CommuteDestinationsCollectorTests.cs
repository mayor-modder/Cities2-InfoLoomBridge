using InfoLoomBridge.Runtime;
using Xunit;

namespace InfoLoomBridge.Tests.Runtime
{
    public sealed class CommuteDestinationsCollectorTests
    {
        [Fact]
        public void Collect_rolls_provider_rows_up_into_district_totals()
        {
            var collector = new CommuteDestinationsCollector(
                new[]
                {
                    new ProviderSnapshot(5001, 4100, 101, "Industrial Park", null, "industrial", 40, 32, 20, 12),
                    new ProviderSnapshot(5002, 4101, 101, "Industrial Park", null, "office", 20, 15, 5, 10),
                    new ProviderSnapshot(6001, 5100, 202, "Downtown", null, "office", 25, 20, 14, 6)
                },
                topProviderLimit: 2);

            CommuteDestinationsCollectionResult result = collector.Collect();

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value!.ByDistrict.Length);
            Assert.Equal("Industrial Park", result.Value.ByDistrict[0].DistrictName);
            Assert.Equal(60, result.Value.ByDistrict[0].JobsTotal);
            Assert.Equal(25, result.Value.ByDistrict[0].CommuterEmployees);
            Assert.Equal(22, result.Value.ByDistrict[0].LocalEmployees);
            Assert.Equal(20, result.Value.ByDistrict[0].SectorCommuterEmployees.Industrial);
            Assert.Equal(5, result.Value.ByDistrict[0].SectorCommuterEmployees.Office);
        }

        [Fact]
        public void Collect_sorts_and_truncates_top_work_providers_by_commuters()
        {
            var collector = new CommuteDestinationsCollector(
                new[]
                {
                    new ProviderSnapshot(5001, 4100, 101, "Industrial Park", null, "industrial", 40, 32, 20, 12),
                    new ProviderSnapshot(5002, 4101, 101, "Industrial Park", null, "office", 20, 15, 5, 10),
                    new ProviderSnapshot(5003, 4102, 101, "Industrial Park", null, "industrial", 10, 10, 8, 2)
                },
                topProviderLimit: 2);

            CommuteDestinationsCollectionResult result = collector.Collect();

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value!.ProviderRowsTotal);
            Assert.Equal(2, result.Value.ProviderRowsExported);
            Assert.True(result.Value.ProviderRowsTruncated);
            Assert.Equal(5001, result.Value.TopWorkProviders[0].ProviderEntity);
            Assert.Equal(5003, result.Value.TopWorkProviders[1].ProviderEntity);
        }

        [Fact]
        public void Collect_preserves_best_effort_provider_names()
        {
            var collector = new CommuteDestinationsCollector(
                new[]
                {
                    new ProviderSnapshot(
                        providerEntity: 5001,
                        buildingEntity: 4100,
                        districtEntity: 101,
                        districtName: "Industrial Park",
                        buildingName: "North Freight Campus",
                        sector: "industrial",
                        jobsTotal: 40,
                        jobsFilled: 32,
                        commuterEmployees: 20,
                        localEmployees: 12)
                });

            CommuteDestinationsCollectionResult result = collector.Collect();

            Assert.True(result.IsSuccess);
            Assert.Equal("North Freight Campus", result.Value!.TopWorkProviders[0].BuildingName);
            Assert.Null(result.Value.TopWorkProviders[0].CompanyName);
        }
    }
}
