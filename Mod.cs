using System;
using Colossal.Logging;
using Game;
using Game.Modding;
using InfoLoomBridge.Export;
using InfoLoomBridge.InfoLoom;
using InfoLoomBridge.Runtime;

namespace InfoLoomBridge
{
    public sealed partial class Mod : IMod
    {
        public static ILog Log { get; } = LogManager.GetLogger("InfoLoomBridge").SetShowsErrorsInUI(false);

        private static Mod? s_instance;
        private IBridgeExportCoordinator? _exportCoordinator;

        internal static Mod? TryGetInstance()
        {
            return s_instance;
        }

        void IMod.OnLoad(UpdateSystem updateSystem)
        {
            s_instance = this;
            Log.Info(nameof(IMod.OnLoad));
            InfoLoomCompatibilityRuntime.CompatibilityLogger = Log.Info;
            InfoLoomCompatibilityRuntime.LoadCurrentCompatibility();
            _exportCoordinator = new BridgeExportCoordinator(
                new InfoLoomAdapter(),
                () => InfoLoomCompatibilityRuntime.CurrentCompatibility,
                new LiveCommuteDestinationsCollector(),
                Log.Info);
            RegisterBridgeExportRuntimeSystem(updateSystem);
        }

        void IMod.OnDispose()
        {
            Log.Info(nameof(IMod.OnDispose));
            _exportCoordinator?.Dispose();
            _exportCoordinator = null;
            s_instance = null;
        }

        internal void OnUpdate(DateTimeOffset nowUtc)
        {
            ModRuntimeWiring.PublishDueSnapshot(_exportCoordinator, nowUtc);
        }

        private static void RegisterBridgeExportRuntimeSystem(UpdateSystem updateSystem)
        {
            ModRuntimeWiring.RegisterBridgeExportRuntimeSystem(
                phaseName =>
                {
                    if (phaseName == "PostSimulation")
                    {
                        updateSystem.UpdateAt<BridgeExportRuntimeSystem>(SystemUpdatePhase.PostSimulation);
                    }
                });
        }
    }
}
