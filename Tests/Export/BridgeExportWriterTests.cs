using System;
using System.Linq;
using System.Text.Json;
using InfoLoomBridge.Export;
using InfoLoomBridge.InfoLoom;
using InfoLoomBridge.Runtime;
using Xunit;

namespace InfoLoomBridge.Tests.Export
{
    public sealed class BridgeExportWriterTests
    {
        [Fact]
        public void Production_bridge_assembly_does_not_reference_system_text_json()
        {
            string[] references = typeof(BridgeExportWriter)
                .Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name ?? string.Empty)
                .ToArray();

            Assert.DoesNotContain("System.Text.Json", references);
        }

        [Fact]
        public void Serialize_emits_the_approved_bridge_export_contract()
        {
            var snapshot = new BridgeSnapshot
            {
                ExportVersion = "1.0.0",
                GeneratedAt = DateTimeOffset.Parse("2026-03-28T12:34:56Z"),
                BridgeVersion = "bridge-0.1.0",
                InfoLoomVersion = "infoloom-0.1.0",
                InfoLoomBuild = new InfoLoomBuildFingerprint(
                    @"C:\Mods\InfoLoomTwo.dll",
                    new Version(1, 0, 0, 0),
                    "1.20.1+fe12d359004d3ea64449b297b5d8b9ec2591ef23",
                    "fe12d359004d3ea64449b297b5d8b9ec2591ef23",
                    DateTimeOffset.Parse("2026-02-21T16:24:00Z")),
                Status = "healthy",
                Panels = new BridgePanelsSnapshot
                {
                    Demographics = null,
                    Workforce = null,
                    Workplaces = null
                },
                BridgeExtensions = new BridgeExtensionsSnapshot
                {
                    CommuteDestinations = new CommuteDestinationsExtensionSnapshot
                    {
                        Status = "ok",
                        SourceComponent = "ecs.commute_destinations:Game.Companies.WorkProvider",
                        Notes = new[] { "outside-connection origin attribution is not included in this export" },
                        ByDistrict = new[]
                        {
                            new CommuteDestinationDistrictRow
                            {
                                DistrictEntity = 101,
                                DistrictName = "Industrial Park",
                                ProviderCount = 2,
                                JobsTotal = 60,
                                JobsFilled = 50,
                                JobsOpen = 10,
                                CommuterEmployees = 20,
                                LocalEmployees = 30,
                                SectorCommuterEmployees = new SectorIntValueSet
                                {
                                    Industrial = 20
                                }
                            }
                        },
                        TopWorkProviders = Array.Empty<CommuteDestinationProviderRow>(),
                        ProviderRowsTotal = 8,
                        ProviderRowsExported = 0,
                        ProviderRowsTruncated = true
                    }
                }
            };

            string json = BridgeExportWriter.Serialize(snapshot);
            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement root = document.RootElement;
            Assert.Equal(JsonValueKind.Object, root.ValueKind);
            Assert.Equal(8, CountProperties(root));

            Assert.Equal("1.0.0", root.GetProperty("export_version").GetString());
            Assert.Equal(DateTimeOffset.Parse("2026-03-28T12:34:56Z"), root.GetProperty("generated_at").GetDateTimeOffset());
            Assert.Equal("bridge-0.1.0", root.GetProperty("bridge_version").GetString());
            Assert.Equal("infoloom-0.1.0", root.GetProperty("infoloom_version").GetString());
            Assert.Equal("1.20.1+fe12d359004d3ea64449b297b5d8b9ec2591ef23", root.GetProperty("infoloom_build").GetProperty("product_version").GetString());
            Assert.Equal("fe12d359004d3ea64449b297b5d8b9ec2591ef23", root.GetProperty("infoloom_build").GetProperty("git_commit").GetString());
            Assert.Equal("healthy", root.GetProperty("status").GetString());

            JsonElement panels = root.GetProperty("panels");
            Assert.Equal(JsonValueKind.Object, panels.ValueKind);
            Assert.Equal(3, CountProperties(panels));
            Assert.Equal(JsonValueKind.Null, panels.GetProperty("demographics").ValueKind);
            Assert.Equal(JsonValueKind.Null, panels.GetProperty("workforce").ValueKind);
            Assert.Equal(JsonValueKind.Null, panels.GetProperty("workplaces").ValueKind);

            JsonElement extensions = root.GetProperty("bridge_extensions");
            Assert.Equal(JsonValueKind.Object, extensions.ValueKind);

            JsonElement commuteDestinations = extensions.GetProperty("commute_destinations");
            Assert.Equal("ok", commuteDestinations.GetProperty("status").GetString());
            Assert.Equal(8, commuteDestinations.GetProperty("provider_rows_total").GetInt32());
            Assert.True(commuteDestinations.GetProperty("provider_rows_truncated").GetBoolean());
            Assert.Equal(
                20,
                commuteDestinations.GetProperty("by_district")[0]
                    .GetProperty("sector_commuter_employees")
                    .GetProperty("industrial")
                    .GetInt32());
        }

        private static int CountProperties(JsonElement element)
        {
            int count = 0;
            foreach (JsonProperty _ in element.EnumerateObject())
            {
                count++;
            }

            return count;
        }
    }
}
