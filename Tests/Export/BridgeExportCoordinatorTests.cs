using System;
using System.IO;
using System.Text.Json;
using InfoLoomBridge.Export;
using InfoLoomBridge.InfoLoom;
using InfoLoomBridge.Runtime;
using Xunit;

namespace InfoLoomBridge.Tests.Export
{
    public sealed class BridgeExportCoordinatorTests : IDisposable
    {
        private readonly string _tempRoot;

        public BridgeExportCoordinatorTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "InfoLoomBridgeTests", Guid.NewGuid().ToString("N"));
        }

        [Fact]
        public void ResolveOutputRoot_builds_the_bridge_modsdata_path_from_local_appdata()
        {
            string resolved = BridgeExportCoordinator.ResolveOutputRoot(@"C:\Users\TestUser\AppData\Local");

            Assert.Equal(
                Path.Combine(
                    @"C:\Users\TestUser\AppData\LocalLow",
                    "Colossal Order",
                    "Cities Skylines II",
                    "ModsData",
                    "InfoLoomBridge"),
                resolved);
        }

        [Fact]
        public void PublishIfDue_writes_latest_json_with_ok_status_for_supported_panel_data()
        {
            var coordinator = new BridgeExportCoordinator(
                new StubAdapter(
                    InfoLoomFirstPanelSliceResult.CreateSuccess(
                        new InfoLoomPanelSlice(
                            CreatePayload("totals", new { total_population = 1234 }, "results", new { households = 456 }),
                            CreatePayload("results", new { workers = 789 }),
                            CreatePayload("results", new { workplaces = 321 })))),
                () => CreateSupportedReport(),
                new StubCommuteCollector(CommuteDestinationsCollectionResult.CreateFailure("not configured")),
                _ => { },
                outputRootOverride: _tempRoot,
                publishInterval: TimeSpan.FromSeconds(30));

            coordinator.PublishIfDue(DateTimeOffset.Parse("2026-03-28T12:34:56Z"));

            Assert.True(File.Exists(coordinator.LatestSnapshotPath));
            Assert.False(File.Exists(coordinator.LatestSnapshotPath + ".tmp"));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(coordinator.LatestSnapshotPath));
            JsonElement root = document.RootElement;

            Assert.Equal("ok", root.GetProperty("status").GetString());
            Assert.False(root.TryGetProperty("message", out _));
            Assert.Equal("1.2.3", root.GetProperty("infoloom_version").GetString());
            Assert.Equal("1.2.3+abcdef", root.GetProperty("infoloom_build").GetProperty("product_version").GetString());
            Assert.Equal("abcdef", root.GetProperty("infoloom_build").GetProperty("git_commit").GetString());
            Assert.Equal(1234, root.GetProperty("panels").GetProperty("demographics").GetProperty("totals").GetProperty("total_population").GetInt32());
            Assert.Equal(789, root.GetProperty("panels").GetProperty("workforce").GetProperty("results").GetProperty("workers").GetInt32());
            Assert.Equal(321, root.GetProperty("panels").GetProperty("workplaces").GetProperty("results").GetProperty("workplaces").GetInt32());
        }

        [Fact]
        public void PublishIfDue_writes_error_status_and_message_when_dependency_is_missing()
        {
            var adapter = new ThrowingAdapter();
            var coordinator = new BridgeExportCoordinator(
                adapter,
                () => InfoLoomCompatibilityReport.Missing(),
                new StubCommuteCollector(CommuteDestinationsCollectionResult.CreateFailure("not configured")),
                _ => { },
                outputRootOverride: _tempRoot,
                publishInterval: TimeSpan.FromSeconds(30));

            coordinator.PublishIfDue(DateTimeOffset.Parse("2026-03-28T12:34:56Z"));

            Assert.Equal(0, adapter.CallCount);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(coordinator.LatestSnapshotPath));
            JsonElement root = document.RootElement;

            Assert.Equal("error", root.GetProperty("status").GetString());
            Assert.Equal("InfoLoom dependency missing", root.GetProperty("message").GetString());
        }

        [Fact]
        public void PublishIfDue_writes_error_status_and_message_when_adapter_returns_failure()
        {
            var coordinator = new BridgeExportCoordinator(
                new StubAdapter(
                    InfoLoomFirstPanelSliceResult.CreateFailure(
                        InfoLoomCompatibilityFailure.InvalidPayload(
                            "InfoLoom.Systems.WorkforceInfoLoomUISystem",
                            "m_Results resolved to null",
                            "m_Results"))),
                () => CreateSupportedReport(),
                new StubCommuteCollector(CommuteDestinationsCollectionResult.CreateFailure("not configured")),
                _ => { },
                outputRootOverride: _tempRoot,
                publishInterval: TimeSpan.FromSeconds(30));

            coordinator.PublishIfDue(DateTimeOffset.Parse("2026-03-28T12:34:56Z"));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(coordinator.LatestSnapshotPath));
            JsonElement root = document.RootElement;

            Assert.Equal("error", root.GetProperty("status").GetString());
            Assert.Contains("InfoLoom payload is not readable", root.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Contains("m_Results resolved to null", root.GetProperty("message").GetString(), StringComparison.Ordinal);
        }

        [Fact]
        public void PublishIfDue_replaces_latest_json_with_error_snapshot_when_adapter_throws()
        {
            var coordinator = new BridgeExportCoordinator(
                new ThrowingAdapter(),
                () => CreateSupportedReport(),
                new StubCommuteCollector(CommuteDestinationsCollectionResult.CreateFailure("not configured")),
                _ => { },
                outputRootOverride: _tempRoot,
                publishInterval: TimeSpan.FromSeconds(30));

            coordinator.PublishIfDue(DateTimeOffset.Parse("2026-03-28T12:34:56Z"));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(coordinator.LatestSnapshotPath));
            JsonElement root = document.RootElement;

            Assert.Equal("error", root.GetProperty("status").GetString());
            Assert.Contains("InfoLoom bridge export failed", root.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Contains("Adapter should not be called.", root.GetProperty("message").GetString(), StringComparison.Ordinal);
        }

        [Fact]
        public void PublishIfDue_replaces_existing_latest_json_without_leaving_a_temp_file()
        {
            var coordinator = new BridgeExportCoordinator(
                new StubAdapter(
                    InfoLoomFirstPanelSliceResult.CreateSuccess(
                        new InfoLoomPanelSlice(
                            CreatePayload("totals", new { total_population = 100 }, "results", new { households = 10 }),
                            CreatePayload("results", new { workers = 20 }),
                            CreatePayload("results", new { workplaces = 30 })))),
                () => CreateSupportedReport(),
                new StubCommuteCollector(CommuteDestinationsCollectionResult.CreateFailure("not configured")),
                _ => { },
                outputRootOverride: _tempRoot,
                publishInterval: TimeSpan.FromSeconds(30));

            Directory.CreateDirectory(_tempRoot);
            File.WriteAllText(coordinator.LatestSnapshotPath, "{\"status\":\"stale\"}");

            coordinator.PublishIfDue(DateTimeOffset.Parse("2026-03-28T12:34:56Z"));

            string json = File.ReadAllText(coordinator.LatestSnapshotPath);
            Assert.Contains("\"status\":\"ok\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"status\":\"stale\"", json, StringComparison.Ordinal);
            Assert.False(File.Exists(coordinator.LatestSnapshotPath + ".tmp"));
        }

        [Fact]
        public void PublishIfDue_skips_runs_before_the_next_due_time()
        {
            var adapter = new StubAdapter(
                InfoLoomFirstPanelSliceResult.CreateSuccess(
                    new InfoLoomPanelSlice(
                        CreatePayload("totals", new { total_population = 1 }, "results", new { households = 1 }),
                        CreatePayload("results", new { workers = 1 }),
                        CreatePayload("results", new { workplaces = 1 }))));
            var coordinator = new BridgeExportCoordinator(
                adapter,
                () => CreateSupportedReport(),
                new StubCommuteCollector(CommuteDestinationsCollectionResult.CreateFailure("not configured")),
                _ => { },
                outputRootOverride: _tempRoot,
                publishInterval: TimeSpan.FromSeconds(30));

            coordinator.PublishIfDue(DateTimeOffset.Parse("2026-03-28T12:34:56Z"));
            coordinator.PublishIfDue(DateTimeOffset.Parse("2026-03-28T12:35:10Z"));

            Assert.Equal(1, adapter.CallCount);
        }

        [Fact]
        public void PublishIfDue_writes_commute_destination_extension_when_collector_succeeds()
        {
            var coordinator = new BridgeExportCoordinator(
                new StubAdapter(InfoLoomFirstPanelSliceResult.CreateSuccess(CreatePanelSlice())),
                () => CreateSupportedReport(),
                new StubCommuteCollector(
                    CommuteDestinationsCollectionResult.CreateSuccess(
                        new CommuteDestinationsCollectedData
                        {
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
                        })),
                _ => { },
                outputRootOverride: _tempRoot,
                publishInterval: TimeSpan.FromSeconds(30));

            coordinator.PublishIfDue(DateTimeOffset.Parse("2026-03-29T22:10:00Z"));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(coordinator.LatestSnapshotPath));
            JsonElement extension = document.RootElement.GetProperty("bridge_extensions").GetProperty("commute_destinations");

            Assert.Equal("ok", extension.GetProperty("status").GetString());
            Assert.Equal(20, extension.GetProperty("by_district")[0].GetProperty("commuter_employees").GetInt32());
            Assert.Equal(8, extension.GetProperty("provider_rows_total").GetInt32());
        }

        [Fact]
        public void PublishIfDue_keeps_top_level_ok_when_commute_extension_fails()
        {
            var coordinator = new BridgeExportCoordinator(
                new StubAdapter(InfoLoomFirstPanelSliceResult.CreateSuccess(CreatePanelSlice())),
                () => CreateSupportedReport(),
                new StubCommuteCollector(CommuteDestinationsCollectionResult.CreateFailure("district carrier unavailable")),
                _ => { },
                outputRootOverride: _tempRoot,
                publishInterval: TimeSpan.FromSeconds(30));

            coordinator.PublishIfDue(DateTimeOffset.Parse("2026-03-29T22:10:00Z"));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(coordinator.LatestSnapshotPath));
            JsonElement root = document.RootElement;
            JsonElement extension = root.GetProperty("bridge_extensions").GetProperty("commute_destinations");

            Assert.Equal("ok", root.GetProperty("status").GetString());
            Assert.Equal("error", extension.GetProperty("status").GetString());
            Assert.Equal("district carrier unavailable", extension.GetProperty("message").GetString());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }

        private static InfoLoomCompatibilityReport CreateSupportedReport()
        {
            return InfoLoomCompatibilityReport.Supported(
                new InfoLoomDependencyInfo(
                    isLoadable: true,
                    version: new Version(1, 2, 3),
                    location: @"C:\Mods\InfoLoom.dll",
                    buildFingerprint: new InfoLoomBuildFingerprint(
                        @"C:\Mods\InfoLoom.dll",
                        new Version(1, 2, 3),
                        "1.2.3+abcdef",
                        "abcdef",
                        DateTimeOffset.Parse("2026-04-04T12:00:00Z"))));
        }

        private static System.Collections.Generic.IReadOnlyDictionary<string, object?> CreatePayload(string key, object value)
        {
            return new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [key] = value
            };
        }

        private static System.Collections.Generic.IReadOnlyDictionary<string, object?> CreatePayload(string firstKey, object firstValue, string secondKey, object secondValue)
        {
            return new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [firstKey] = firstValue,
                [secondKey] = secondValue
            };
        }

        private static InfoLoomPanelSlice CreatePanelSlice()
        {
            return new InfoLoomPanelSlice(
                CreatePayload("totals", new { total_population = 1234 }, "results", new { households = 456 }),
                CreatePayload("results", new { workers = 789 }),
                CreatePayload("results", new { workplaces = 321 }));
        }

        private sealed class StubAdapter : IInfoLoomAdapter
        {
            private readonly InfoLoomFirstPanelSliceResult _result;

            public StubAdapter(InfoLoomFirstPanelSliceResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public InfoLoomFirstPanelSliceResult ReadFirstPanelSlice()
            {
                CallCount++;
                return _result;
            }
        }

        private sealed class ThrowingAdapter : IInfoLoomAdapter
        {
            public int CallCount { get; private set; }

            public InfoLoomFirstPanelSliceResult ReadFirstPanelSlice()
            {
                CallCount++;
                throw new InvalidOperationException("Adapter should not be called.");
            }
        }

        private sealed class StubCommuteCollector : ICommuteDestinationsCollector
        {
            private readonly CommuteDestinationsCollectionResult _result;

            public StubCommuteCollector(CommuteDestinationsCollectionResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public CommuteDestinationsCollectionResult Collect()
            {
                CallCount++;
                return _result;
            }
        }
    }
}
