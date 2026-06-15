using System;
using InfoLoomBridge.Export;
using InfoLoomBridge.InfoLoom;
using Xunit;

namespace InfoLoomBridge.Tests.InfoLoom
{
    public sealed class InfoLoomCompatibilityRuntimeTests
    {
        [Fact]
        public void LoadCurrentCompatibility_caches_and_logs_the_detected_report()
        {
            var report = InfoLoomCompatibilityReport.Incompatible(
                new InfoLoomDependencyInfo(true, new Version(2, 0, 0), @"C:\Mods\InfoLoom"));
            string? loggedMessage = null;
            Func<InfoLoomCompatibilityReport> originalDetector = InfoLoomCompatibilityRuntime.CompatibilityDetector;
            Action<string> originalLogger = InfoLoomCompatibilityRuntime.CompatibilityLogger;

            try
            {
                InfoLoomCompatibilityRuntime.CompatibilityDetector = () => report;
                InfoLoomCompatibilityRuntime.CompatibilityLogger = message => loggedMessage = message;

                InfoLoomCompatibilityRuntime.LoadCurrentCompatibility();
                BridgeSnapshot snapshot = InfoLoomCompatibilityRuntime.CreateExportSnapshot();

                Assert.Same(report, InfoLoomCompatibilityRuntime.CurrentCompatibility);
                Assert.NotSame(InfoLoomCompatibilityRuntime.CurrentExportSnapshot, snapshot);
                Assert.Equal("incompatible", InfoLoomCompatibilityRuntime.CurrentExportSnapshot.Status);
                Assert.Equal("2.0.0", InfoLoomCompatibilityRuntime.CurrentExportSnapshot.InfoLoomVersion);
                Assert.Equal(report.LogMessage, loggedMessage);
                Assert.Equal("incompatible", snapshot.Status);
                Assert.Equal("2.0.0", snapshot.InfoLoomVersion);
                snapshot.Status = "supported";
                snapshot.InfoLoomVersion = "1.2.3";
                Assert.Equal("incompatible", InfoLoomCompatibilityRuntime.CurrentExportSnapshot.Status);
                Assert.Equal("2.0.0", InfoLoomCompatibilityRuntime.CurrentExportSnapshot.InfoLoomVersion);
            }
            finally
            {
                InfoLoomCompatibilityRuntime.CompatibilityDetector = originalDetector;
                InfoLoomCompatibilityRuntime.CompatibilityLogger = _ => { };
                InfoLoomCompatibilityRuntime.ApplyCompatibility(InfoLoomCompatibilityReport.Missing());
                InfoLoomCompatibilityRuntime.CompatibilityLogger = originalLogger;
            }
        }
    }
}
