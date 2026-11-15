using LoopSort0.Core;
using UnityEngine;

namespace LoopSort0
{
    public sealed partial class GameController
    {
        // The multiplier is the original pace state; BeltSteering.Speed already
        // includes ConveyorConfig.Speed * PaceConfig.ConveyorSpeedMultiplier.
        float NativeConveyorSpeed=>BeltSteering.Speed*(State?.ConveyorSpeedScale??1f);

        void TickNativeRules(float delta)
        {
            if(State==null||Paused||IsEnteringLevel||State.Status!=RunStatus.Playing)return;
            State.Tick(delta,Input.GetMouseButton(0));
        }
    }
}
