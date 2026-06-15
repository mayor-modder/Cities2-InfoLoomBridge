using System.Collections.Generic;
using InfoLoomBridge.Export;
using InfoLoomBridge.InfoLoom;
using InfoLoomBridge.Runtime;
using Xunit;

namespace InfoLoomBridge.Tests.Export
{
    public sealed class BridgeSnapshotTests
    {
        [Fact]
        public void Copy_clones_nested_panel_payloads()
        {
            var snapshot = new BridgeSnapshot
            {
                InfoLoomBuild = new InfoLoomBuildFingerprint(
                    @"C:\Mods\InfoLoomTwo.dll",
                    new System.Version(1, 0, 0, 0),
                    "1.20.1+fe12d359004d3ea64449b297b5d8b9ec2591ef23",
                    "fe12d359004d3ea64449b297b5d8b9ec2591ef23",
                    System.DateTimeOffset.Parse("2026-02-21T16:24:00Z")),
                Panels = new BridgePanelsSnapshot
                {
                    Demographics = new Dictionary<string, object?>
                    {
                        ["totals"] = new Dictionary<string, object?>
                        {
                            ["total_population"] = 1234
                        }
                    }
                }
            };

            BridgeSnapshot copy = snapshot.Copy();

            Assert.NotSame(snapshot.InfoLoomBuild, copy.InfoLoomBuild);
            Assert.Equal(snapshot.InfoLoomBuild!.GitCommit, copy.InfoLoomBuild!.GitCommit);

            var demographics = Assert.IsAssignableFrom<IDictionary<string, object?>>(copy.Panels.Demographics);
            var totals = Assert.IsAssignableFrom<IDictionary<string, object?>>(demographics["totals"]);
            totals["total_population"] = 9999;

            var originalDemographics = Assert.IsAssignableFrom<IDictionary<string, object?>>(snapshot.Panels.Demographics);
            var originalTotals = Assert.IsAssignableFrom<IDictionary<string, object?>>(originalDemographics["totals"]);
            Assert.Equal(1234, originalTotals["total_population"]);
        }

        [Fact]
        public void Copy_clones_bridge_extensions_and_nested_commute_rows()
        {
            var snapshot = new BridgeSnapshot
            {
                BridgeExtensions = new BridgeExtensionsSnapshot
                {
                    CommuteDestinations = new CommuteDestinationsExtensionSnapshot
                    {
                        Status = "ok",
                        ProviderRowsTotal = 5,
                        ProviderRowsExported = 2,
                        ProviderRowsTruncated = true,
                        ByDistrict = new[]
                        {
                            new CommuteDestinationDistrictRow
                            {
                                DistrictEntity = 101,
                                DistrictName = "Industrial Park",
                                ProviderCount = 3,
                                JobsTotal = 90,
                                JobsFilled = 80,
                                JobsOpen = 10,
                                CommuterEmployees = 44,
                                LocalEmployees = 36,
                                SectorCommuterEmployees = new SectorIntValueSet
                                {
                                    Industrial = 40,
                                    Office = 4
                                }
                            }
                        },
                        TopWorkProviders = new[]
                        {
                            new CommuteDestinationProviderRow
                            {
                                ProviderEntity = 5001,
                                BuildingEntity = 4100,
                                DistrictEntity = 101,
                                DistrictName = "Industrial Park",
                                Sector = "industrial",
                                JobsTotal = 30,
                                JobsFilled = 28,
                                JobsOpen = 2,
                                CommuterEmployees = 21,
                                LocalEmployees = 7
                            }
                        }
                    }
                }
            };

            BridgeSnapshot copy = snapshot.Copy();

            Assert.NotSame(snapshot.BridgeExtensions, copy.BridgeExtensions);
            BridgeExtensionsSnapshot copyExtensions = Assert.IsType<BridgeExtensionsSnapshot>(copy.BridgeExtensions);
            CommuteDestinationsExtensionSnapshot copyCommute = Assert.IsType<CommuteDestinationsExtensionSnapshot>(copyExtensions.CommuteDestinations);
            Assert.NotSame(snapshot.BridgeExtensions.CommuteDestinations, copyCommute);
            Assert.NotSame(snapshot.BridgeExtensions.CommuteDestinations.ByDistrict, copyCommute.ByDistrict);
            Assert.NotSame(snapshot.BridgeExtensions.CommuteDestinations.TopWorkProviders, copyCommute.TopWorkProviders);
            Assert.Equal(44, copyCommute.ByDistrict[0].CommuterEmployees);
            Assert.Equal(21, copyCommute.TopWorkProviders[0].CommuterEmployees);
        }
    }
}
