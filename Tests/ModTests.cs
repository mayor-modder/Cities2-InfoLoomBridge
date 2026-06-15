using System;
using InfoLoomBridge.Export;
using Xunit;

namespace InfoLoomBridge.Tests
{
    public sealed class ModTests
    {
        [Fact]
        public void RegisterBridgeExportRuntimeSystem_uses_post_simulation_phase()
        {
            string? capturedPhaseName = null;

            ModRuntimeWiring.RegisterBridgeExportRuntimeSystem(phaseName => capturedPhaseName = phaseName);

            Assert.Equal("PostSimulation", capturedPhaseName);
        }

        [Fact]
        public void OnUpdate_publishes_due_snapshot_through_export_coordinator()
        {
            var coordinator = new FakeBridgeExportCoordinator();
            DateTimeOffset nowUtc = DateTimeOffset.Parse("2026-03-28T12:34:56Z");

            ModRuntimeWiring.PublishDueSnapshot(coordinator, nowUtc);

            Assert.Equal(nowUtc, coordinator.LastPublishedAt);
        }

        private sealed class FakeBridgeExportCoordinator : IBridgeExportCoordinator
        {
            public DateTimeOffset? LastPublishedAt { get; private set; }

            public void PublishIfDue(DateTimeOffset nowUtc)
            {
                LastPublishedAt = nowUtc;
            }

            public void Dispose()
            {
            }
        }
    }
}
