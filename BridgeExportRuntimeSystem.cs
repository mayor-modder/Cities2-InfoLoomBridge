using System;
using Game;

namespace InfoLoomBridge
{
    public sealed partial class BridgeExportRuntimeSystem : GameSystemBase
    {
        protected override void OnUpdate()
        {
            Mod? mod = Mod.TryGetInstance();
            if (mod == null)
            {
                return;
            }

            mod.OnUpdate(DateTimeOffset.UtcNow);
        }
    }
}
