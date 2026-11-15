using UnityEngine;

namespace LoopSort0
{
    // Undo and level cleanup remove this marker together with detached key/screw effects.
    public sealed class TransientEffect : MonoBehaviour
    {
        public static void Firework(Transform parent,Vector3 point)=>SourceParticleEffect.Spawn("complete",parent,point,Quaternion.identity);
        public static void IceBreak(Transform parent,Vector3 point,Quaternion rotation)=>SourceParticleEffect.Spawn("ice",parent,point,rotation);
        public static void HiddenReveal(Transform parent,Vector3 point,Quaternion rotation)=>SourceParticleEffect.Spawn("hidden",parent,point,rotation);
        public static void CarrierSpawn(Transform parent,Vector3 point,Quaternion rotation)=>SourceParticleEffect.Spawn("spawn",parent,point,rotation);
    }
}
