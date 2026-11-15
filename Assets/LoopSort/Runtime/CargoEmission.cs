using UnityEngine;

namespace LoopSort0
{
    public static class CargoEmission
    {
        public static float AnimationMultiplier=>HotUpdate.RuntimeTuning.Get("emission.animation_multiplier",.5f);
        public static float Duration=>HotUpdate.RuntimeTuning.Get("emission.duration",.35f);
        public static float SlotElementOffset=>HotUpdate.RuntimeTuning.Get("emission.slot_offset",.5f);
        public static float SlotCollisionDistance=>HotUpdate.RuntimeTuning.Get("emission.collision_distance",.25f);
        public static float SecondsBetweenPieces=>SlotCollisionDistance/Mathf.Max(.01f,BeltSteering.Speed)*AnimationMultiplier;
        public static Vector3 OutletPosition(Transform carrier)
            =>carrier.TransformPoint(new Vector3(0,1.7f,-2.7f));
        public static Vector3 LandingPoint(TrackGeometry track,Vector3 projectPoint,int cube)
        {
            Vector3 position=track.Nearest(projectPoint,out Vector3 tangent,out _);
            Vector3 right=Vector3.Cross(Vector3.up,tangent).normalized;
            // ConveyorSlot.AddBlock alternates right/left offsets by lateral index.
            return position+Vector3.up*(SlotElementOffset*.5f)+right*((cube&1)==0?-SlotElementOffset*.5f:SlotElementOffset*.5f);
        }
        public static float Progress(float normalizedTime)
            =>(1-Mathf.Cos(Mathf.PI*Mathf.Clamp01(normalizedTime)))*.5f;
        internal static float JumpHeight=>2*AnimationMultiplier*Mathf.PI;
        public static Vector3 JumpPosition(Vector3 from,Vector3 to,float t)=>JumpPosition(from,to,t,JumpHeight);
        internal static Vector3 JumpPosition(Vector3 from,Vector3 to,float t,float height)
        {
            float inverse=1-t;
            float outQuint=1-inverse*inverse*inverse*inverse*inverse;
            return Vector3.Lerp(from,to,t)+Vector3.up*(Mathf.Sin(Mathf.PI*t)*outQuint*height);
        }
    }
}
