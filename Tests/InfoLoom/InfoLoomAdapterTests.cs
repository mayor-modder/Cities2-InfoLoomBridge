using System;
using System.Collections.Generic;
using InfoLoomBridge.InfoLoom;
using Xunit;

namespace InfoLoomBridge.Tests.InfoLoom
{
    public sealed class InfoLoomAdapterTests
    {
        [Fact]
        public void ReadFirstPanelSlice_uses_the_default_production_adapter_without_callers_providing_system_names()
        {
            var adapter = (IInfoLoomAdapter)Activator.CreateInstance(typeof(InfoLoomAdapter))!;

            InfoLoomFirstPanelSliceResult result = adapter.ReadFirstPanelSlice();

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(InfoLoomCompatibilityFailureKind.MissingSystem, result.Failure!.Kind);
        }

        [Fact]
        public void ReadFirstPanelSlice_returns_a_member_failure_when_world_reflection_is_unavailable()
        {
            var adapter = new InfoLoomAdapter(
                new InfoLoomAdapter.ProductionInfoLoomSystemResolver(
                    new FakeReflectionProbe(
                        hasPopulationType: true,
                        hasWorkforceType: true,
                        hasWorkplacesType: true,
                        hasPopulationTypeV2: false,
                        hasWorkforceTypeV2: false,
                        hasWorkplacesTypeV2: false,
                        defaultWorldFailure: InfoLoomCompatibilityFailure.MissingMember(
                            "Unity.Entities.World",
                            "DefaultGameObjectInjectionWorld"))));

            InfoLoomFirstPanelSliceResult result = adapter.ReadFirstPanelSlice();

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(InfoLoomCompatibilityFailureKind.MissingMember, result.Failure!.Kind);
            Assert.Equal("Unity.Entities.World", result.Failure.TypeName);
            Assert.Equal("DefaultGameObjectInjectionWorld", result.Failure.MemberName);
        }

        [Fact]
        public void ReadFirstPanelSlice_returns_invalid_payload_when_world_reflection_is_unreadable()
        {
            var adapter = new InfoLoomAdapter(
                new InfoLoomAdapter.ProductionInfoLoomSystemResolver(
                    new FakeReflectionProbe(
                        hasPopulationType: true,
                        hasWorkforceType: true,
                        hasWorkplacesType: true,
                        hasPopulationTypeV2: false,
                        hasWorkforceTypeV2: false,
                        hasWorkplacesTypeV2: false,
                        defaultWorldFailure: InfoLoomCompatibilityFailure.InvalidPayload(
                            "Unity.Entities.World",
                            "DefaultGameObjectInjectionWorld getter failed",
                            "DefaultGameObjectInjectionWorld"))));

            InfoLoomFirstPanelSliceResult result = adapter.ReadFirstPanelSlice();

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(InfoLoomCompatibilityFailureKind.InvalidPayload, result.Failure!.Kind);
            Assert.Equal("Unity.Entities.World", result.Failure.TypeName);
            Assert.Equal("DefaultGameObjectInjectionWorld", result.Failure.MemberName);
        }

        [Fact]
        public void ReadFirstPanelSlice_returns_invalid_payload_when_demographics_totals_are_null()
        {
            var adapter = new InfoLoomAdapter(
                new FakeInfoLoomSystemResolver(
                    InfoLoomSystemLookupResult.Success(new FakePopulationStructureUISystem(null, new object())),
                    InfoLoomSystemLookupResult.Success(new FakeWorkforceInfoLoomUISystem(new object())),
                    InfoLoomSystemLookupResult.Success(new FakeWorkplacesInfoLoomUISystem(new object()))));

            InfoLoomFirstPanelSliceResult result = adapter.ReadFirstPanelSlice();

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(InfoLoomCompatibilityFailureKind.InvalidPayload, result.Failure!.Kind);
            Assert.Equal(typeof(FakePopulationStructureUISystem).FullName, result.Failure.TypeName);
            Assert.Equal("m_Totals", result.Failure.MemberName);
        }

        [Fact]
        public void ReadFirstPanelSlice_returns_the_supported_panel_payloads()
        {
            var totals = new { total_population = 1234 };
            var demographicsResults = new { households = 456 };
            var workforceResults = new { workers = 789 };
            var workplacesResults = new { workplaces = 321 };

            var adapter = new InfoLoomAdapter(
                new FakeInfoLoomSystemResolver(
                    InfoLoomSystemLookupResult.Success(new FakePopulationStructureUISystem(totals, demographicsResults)),
                    InfoLoomSystemLookupResult.Success(new FakeWorkforceInfoLoomUISystem(workforceResults)),
                    InfoLoomSystemLookupResult.Success(new FakeWorkplacesInfoLoomUISystem(workplacesResults))));

            InfoLoomFirstPanelSliceResult result = adapter.ReadFirstPanelSlice();

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Panels);

            IReadOnlyDictionary<string, object?> demographics = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Panels!.Demographics);
            IReadOnlyDictionary<string, object?> workforce = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Panels.Workforce);
            IReadOnlyDictionary<string, object?> workplaces = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Panels.Workplaces);

            Assert.Same(totals, demographics["totals"]);
            Assert.Same(demographicsResults, demographics["results"]);
            Assert.Same(workforceResults, workforce["results"]);
            Assert.Same(workplacesResults, workplaces["results"]);
        }

        [Fact]
        public void ReadFirstPanelSlice_supports_InfoLoomTwo_type_names_and_refresh_methods()
        {
            var adapter = new InfoLoomAdapter(
                new InfoLoomAdapter.ProductionInfoLoomSystemResolver(
                    new FakeReflectionProbe(
                        hasPopulationType: false,
                        hasWorkforceType: false,
                        hasWorkplacesType: false,
                        hasPopulationTypeV2: true,
                        hasWorkforceTypeV2: true,
                        hasWorkplacesTypeV2: true,
                        defaultWorldFailure: null)));

            InfoLoomFirstPanelSliceResult result = adapter.ReadFirstPanelSlice();

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Panels);

            IReadOnlyDictionary<string, object?> demographics = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Panels!.Demographics);
            IReadOnlyDictionary<string, object?> workforce = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Panels.Workforce);
            IReadOnlyDictionary<string, object?> workplaces = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Panels.Workplaces);

            Assert.Equal(4321, demographics["totals"]);
            Assert.Equal(8765, demographics["results"]);
            Assert.Equal(111, workforce["results"]);
            Assert.Equal(222, workplaces["results"]);
        }

        [Fact]
        public void ReadFirstPanelSlice_returns_a_structured_failure_when_a_required_member_is_missing()
        {
            var adapter = new InfoLoomAdapter(
                new FakeInfoLoomSystemResolver(
                    InfoLoomSystemLookupResult.Success(new FakePopulationStructureUISystemMissingResults(new object())),
                    InfoLoomSystemLookupResult.Success(new FakeWorkforceInfoLoomUISystem(new object())),
                    InfoLoomSystemLookupResult.Success(new FakeWorkplacesInfoLoomUISystem(new object()))));

            InfoLoomFirstPanelSliceResult result = adapter.ReadFirstPanelSlice();

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(InfoLoomCompatibilityFailureKind.MissingMember, result.Failure!.Kind);
            Assert.Equal("m_Results", result.Failure.MemberName);
            Assert.Equal(typeof(FakePopulationStructureUISystemMissingResults).FullName, result.Failure.TypeName);
        }

        private sealed class FakeInfoLoomSystemResolver : IInfoLoomSystemResolver
        {
            private readonly InfoLoomSystemLookupResult _populationStructure;
            private readonly InfoLoomSystemLookupResult _workforce;
            private readonly InfoLoomSystemLookupResult _workplaces;

            public FakeInfoLoomSystemResolver(
                InfoLoomSystemLookupResult populationStructure,
                InfoLoomSystemLookupResult workforce,
                InfoLoomSystemLookupResult workplaces)
            {
                _populationStructure = populationStructure;
                _workforce = workforce;
                _workplaces = workplaces;
            }

            public InfoLoomSystemLookupResult TryGetPopulationStructureSystem()
            {
                return _populationStructure;
            }

            public InfoLoomSystemLookupResult TryGetWorkforceSystem()
            {
                return _workforce;
            }

            public InfoLoomSystemLookupResult TryGetWorkplacesSystem()
            {
                return _workplaces;
            }
        }

        private sealed class FakeReflectionProbe : IInfoLoomReflectionProbe
        {
            private readonly bool _hasPopulationType;
            private readonly bool _hasWorkforceType;
            private readonly bool _hasWorkplacesType;
            private readonly bool _hasPopulationTypeV2;
            private readonly bool _hasWorkforceTypeV2;
            private readonly bool _hasWorkplacesTypeV2;
            private readonly InfoLoomCompatibilityFailure? _defaultWorldFailure;

            public FakeReflectionProbe(
                bool hasPopulationType,
                bool hasWorkforceType,
                bool hasWorkplacesType,
                bool hasPopulationTypeV2,
                bool hasWorkforceTypeV2,
                bool hasWorkplacesTypeV2,
                InfoLoomCompatibilityFailure? defaultWorldFailure)
            {
                _hasPopulationType = hasPopulationType;
                _hasWorkforceType = hasWorkforceType;
                _hasWorkplacesType = hasWorkplacesType;
                _hasPopulationTypeV2 = hasPopulationTypeV2;
                _hasWorkforceTypeV2 = hasWorkforceTypeV2;
                _hasWorkplacesTypeV2 = hasWorkplacesTypeV2;
                _defaultWorldFailure = defaultWorldFailure;
            }

            public bool TryFindLoadedType(string typeName, out System.Type? type)
            {
                if (typeName == "InfoLoom.Systems.PopulationStructureUISystem" && _hasPopulationType)
                {
                    type = typeof(FakePopulationStructureUISystem);
                    return true;
                }

                if (typeName == "InfoLoom.Systems.WorkforceInfoLoomUISystem" && _hasWorkforceType)
                {
                    type = typeof(FakeWorkforceInfoLoomUISystem);
                    return true;
                }

                if (typeName == "InfoLoom.Systems.WorkplacesInfoLoomUISystem" && _hasWorkplacesType)
                {
                    type = typeof(FakeWorkplacesInfoLoomUISystem);
                    return true;
                }

                if (typeName == "InfoLoomTwo.Systems.DemographicsData.Demographics" && _hasPopulationTypeV2)
                {
                    type = typeof(FakeInfoLoomTwoDemographicsSystem);
                    return true;
                }

                if (typeName == "InfoLoomTwo.Systems.WorkforceData.WorkforceSystem" && _hasWorkforceTypeV2)
                {
                    type = typeof(FakeInfoLoomTwoWorkforceSystem);
                    return true;
                }

                if (typeName == "InfoLoomTwo.Systems.WorkplacesData.WorkplacesSystem" && _hasWorkplacesTypeV2)
                {
                    type = typeof(FakeInfoLoomTwoWorkplacesSystem);
                    return true;
                }

                type = null;
                return false;
            }

            public bool TryGetDefaultWorld(out object? world, out InfoLoomCompatibilityFailure? failure)
            {
                world = new object();
                failure = _defaultWorldFailure;
                return failure == null;
            }

            public bool TryGetExistingSystemManaged(object world, System.Type systemType, out object? system, out InfoLoomCompatibilityFailure? failure)
            {
                system = Activator.CreateInstance(systemType);
                failure = null;
                return system != null;
            }
        }

        internal sealed class FakePopulationStructureUISystem
        {
            private readonly object? m_Totals;
            private readonly object? m_Results;

            public FakePopulationStructureUISystem(object? totals, object? results, bool hasResultsField = true)
            {
                m_Totals = totals;
                if (hasResultsField)
                {
                    m_Results = results;
                }
                else
                {
                    m_Results = null;
                }
            }
        }

        private sealed class FakePopulationStructureUISystemMissingResults
        {
            private readonly object? m_Totals;

            public FakePopulationStructureUISystemMissingResults(object totals)
            {
                m_Totals = totals;
            }
        }

        private sealed class FakeWorkforceInfoLoomUISystem
        {
            private readonly object? m_Results;

            public FakeWorkforceInfoLoomUISystem(object results)
            {
                m_Results = results;
            }
        }

        private sealed class FakeWorkplacesInfoLoomUISystem
        {
            private readonly object? m_Results;

            public FakeWorkplacesInfoLoomUISystem(object results)
            {
                m_Results = results;
            }
        }

        private sealed class FakeInfoLoomTwoDemographicsSystem
        {
            private object? m_Totals;
            private object? m_Results;

            public bool IsPanelVisible { get; set; }

            public void UpdateDemographics()
            {
                if (IsPanelVisible)
                {
                    m_Totals = 4321;
                    m_Results = 8765;
                }
            }
        }

        private sealed class FakeInfoLoomTwoWorkforceSystem
        {
            private object? m_Results;
            private bool _forceUpdateRequested;

            public bool IsPanelVisible { get; set; }

            public void ForceUpdateOnce()
            {
                _forceUpdateRequested = true;
            }

            private void OnUpdate()
            {
                if (IsPanelVisible && _forceUpdateRequested)
                {
                    m_Results = 111;
                }
            }
        }

        private sealed class FakeInfoLoomTwoWorkplacesSystem
        {
            private object? m_Results;
            private bool _forceUpdateRequested;

            public bool IsPanelVisible { get; set; }

            public void ForceUpdateOnce()
            {
                _forceUpdateRequested = true;
            }

            private void OnUpdate()
            {
                if (IsPanelVisible && _forceUpdateRequested)
                {
                    m_Results = 222;
                }
            }
        }
    }
}
