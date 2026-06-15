using System;
using InfoLoomBridge.Export;

namespace InfoLoomBridge
{
    internal static class ModRuntimeWiring
    {
        internal static void RegisterBridgeExportRuntimeSystem(Action<string> registerPhaseName)
        {
            if (registerPhaseName == null)
            {
                throw new ArgumentNullException(nameof(registerPhaseName));
            }

            registerPhaseName("PostSimulation");
        }

        internal static void PublishDueSnapshot(IBridgeExportCoordinator? coordinator, DateTimeOffset nowUtc)
        {
            coordinator?.PublishIfDue(nowUtc);
        }
    }
}
