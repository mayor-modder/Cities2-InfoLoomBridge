using System;
using InfoLoomBridge.Export;

namespace InfoLoomBridge.InfoLoom
{
    public static class InfoLoomCompatibilityRuntime
    {
        private static BridgeSnapshot _currentExportSnapshot = BridgeSnapshot.FromCompatibility(InfoLoomCompatibilityReport.Missing());

        public static InfoLoomCompatibilityReport CurrentCompatibility { get; private set; } = InfoLoomCompatibilityReport.Missing();
        public static BridgeSnapshot CurrentExportSnapshot
        {
            get { return _currentExportSnapshot.Copy(); }
            private set { _currentExportSnapshot = value; }
        }

        public static Func<InfoLoomCompatibilityReport> CompatibilityDetector { get; set; } = InfoLoomCompatibility.Detect;

        public static Action<string> CompatibilityLogger { get; set; } = _ => { };

        public static void ApplyCompatibility(InfoLoomCompatibilityReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            CurrentCompatibility = report;
            CurrentExportSnapshot = BridgeSnapshot.FromCompatibility(report);
            CompatibilityLogger(report.LogMessage);
        }

        public static void LoadCurrentCompatibility()
        {
            ApplyCompatibility(CompatibilityDetector());
        }

        public static BridgeSnapshot CreateExportSnapshot()
        {
            return CurrentExportSnapshot;
        }
    }
}
