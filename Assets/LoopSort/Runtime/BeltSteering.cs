using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopSort0
{
    // Ported from the supplied 4.6.0 BlockPhysicsSystem; see NativeMovement.md.
    public static class BeltSteering
    {
        public const int BakeSampleCount=256;
        public const float CellSize=.33f;
        public static float Force=>HotUpdate.RuntimeTuning.Get("belt.force",12);
        public static float InitialImpulse=>HotUpdate.RuntimeTuning.Get("belt.initial_impulse",20);
        public static float MaxSpeed=>HotUpdate.RuntimeTuning.Get("belt.max_speed",20);
        public static float Speed=>HotUpdate.RuntimeTuning.Get("belt.speed",5.5f);
        public static float FixedStep=>HotUpdate.RuntimeTuning.Get("physics.fixed_step",.0333f);
        public static float MaximumStep=>HotUpdate.RuntimeTuning.Get("physics.maximum_step",.05f);
        public const float HalfLane=.4f,MaxHeight=.55f,ResetSide=.8f,ResetHeight=1.1f;
        public struct RouteSample
        {
            public Vector3 position,target,forward,up,side;
            public float distance;
        }
        public struct ConstraintResult
        {
            public Vector3 position;
            public bool lateralClamped,heightClamped,reset;
        }
        public sealed class BakedRoute
        {
            readonly RouteSample[] samples=new RouteSample[BakeSampleCount];
            readonly Dictionary<long,Neighborhood> neighborhoods=new Dictionary<long,Neighborhood>();
            readonly struct Neighborhood
            {
                public readonly int radius;
                public readonly List<int> indices;
                public Neighborhood(int radius){this.radius=radius;indices=new List<int>(4);}
            }
            static long Key(int x,int y,int z)
                =>((long)(x&0x1fffff)<<42)|((long)(y&0x1fffff)<<21)|(uint)(z&0x1fffff);
            public BakedRoute(TrackGeometry track)
            {
                var cells=new Dictionary<Vector3Int,List<int>>(BakeSampleCount);
                for(int i=0;i<samples.Length;i++)
                {
                    double percent=i/(double)(samples.Length-1);
                    track.Spline.Evaluate(percent,out Vector3 position,out Vector3 forward,out Vector3 up,out float distance);
                    if(forward.sqrMagnitude<.00001f)forward=Vector3.forward;
                    // Closed sources use TravelUnclamped; open sources stop at the end.
                    samples[i]=new RouteSample {position=position,target=track.At(distance+1f),
                        forward=forward,up=up,side=Vector3.Cross(forward,up),distance=distance};
                    var cell=new Vector3Int(Mathf.FloorToInt(position.x/CellSize),Mathf.FloorToInt(position.y/CellSize),Mathf.FloorToInt(position.z/CellSize));
                    if(!cells.TryGetValue(cell,out var bucket)){bucket=new List<int>(4);cells.Add(cell,bucket);}
                    bucket.Add(i);
                }
                var orderedCells=new Vector3Int[cells.Count];cells.Keys.CopyTo(orderedCells,0);
                Array.Sort(orderedCells,(a,b)=>
                {
                    int order=a.x.CompareTo(b.x);if(order!=0)return order;
                    order=a.y.CompareTo(b.y);return order!=0?order:a.z.CompareTo(b.z);
                });
                // Precompute the first occupied shell in the original search order.
                // Every fragment in a cell can reuse it without changing tie breaks.
                for(int radius=1;radius<=2;radius++)
                    foreach(var cell in orderedCells)
                    {
                        var bucket=cells[cell];
                        for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++)for(int dz=-radius;dz<=radius;dz++)
                        {
                            if(radius==2&&Mathf.Abs(dx)<radius&&Mathf.Abs(dy)<radius&&Mathf.Abs(dz)<radius)continue;
                            long key=Key(cell.x-dx,cell.y-dy,cell.z-dz);
                            if(!neighborhoods.TryGetValue(key,out var near))
                            {
                                near=new Neighborhood(radius);neighborhoods.Add(key,near);
                            }
                            if(near.radius==radius)near.indices.AddRange(bucket);
                        }
                    }
            }
            public RouteSample GetSample(Vector3 position)
            {
                int x=Mathf.FloorToInt(position.x/CellSize),y=Mathf.FloorToInt(position.y/CellSize),z=Mathf.FloorToInt(position.z/CellSize);
                int best=-1;float error=float.MaxValue;
                if(neighborhoods.TryGetValue(Key(x,y,z),out var near))
                {
                    for(int i=0;i<near.indices.Count;i++)
                    {
                        int index=near.indices[i];float candidate=(position-samples[index].position).sqrMagnitude;
                        if(candidate<error){error=candidate;best=index;}
                    }
                    return samples[best];
                }
                // The APK falls back to all baked samples if its spatial hash misses.
                for(int i=0;i<samples.Length;i++)
                {
                    float candidate=(position-samples[i].position).sqrMagnitude;
                    if(candidate<error){error=candidate;best=i;}
                }
                return samples[best];
            }
        }
        public static ConstraintResult Constrain(Vector3 position,RouteSample sample)
        {
            Vector3 delta=position-sample.position;
            float lateral=Vector3.Dot(delta,sample.side),height=Vector3.Dot(delta,sample.up);
            float clampedSide=Mathf.Clamp(lateral,-HalfLane,HalfLane),clampedHeight=Mathf.Clamp(height,0,MaxHeight);
            bool sideChanged=clampedSide!=lateral,heightChanged=clampedHeight!=height;
            var result=new ConstraintResult {position=position,lateralClamped=sideChanged,heightClamped=heightChanged,
                reset=height>ResetHeight||Mathf.Abs(lateral)>ResetSide};
            if(sideChanged||heightChanged||result.reset)
                result.position=sample.position+sample.forward*Vector3.Dot(delta,sample.forward)+sample.side*clampedSide+sample.up*clampedHeight;
            return result;
        }
        public static Vector3 ForceDirection(Vector3 position,Vector3 target)
        {
            Vector3 delta=target-position;float length=delta.magnitude;
            return length>.00001f?delta/length:Vector3.zero;
        }
        public static Vector3 Step(Rigidbody body,Vector3 position,RouteSample sample,float speed=-1)
            =>StepWithForce(body,position,sample,Force*(speed<0?Speed:speed));
        internal static Vector3 StepWithForce(Rigidbody body,Vector3 position,RouteSample sample,float force)
        {
            var constraint=Constrain(position,sample);
            if(constraint.reset)
            {
                body.position=constraint.position;body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;
                return constraint.position;
            }
            if(constraint.lateralClamped||constraint.heightClamped)
            {
                body.position=constraint.position;Vector3 velocity=body.velocity;
                if(constraint.lateralClamped)velocity-=sample.side*Vector3.Dot(velocity,sample.side);
                if(constraint.heightClamped)velocity-=sample.up*Vector3.Dot(velocity,sample.up);
                body.velocity=velocity;
            }
            // Native force targets a baked point one unit ahead, without velocity
            // feedback or the moving-neighbor separation added by our earlier code.
            body.AddForce(ForceDirection(position,sample.target)*force,ForceMode.Force);
            return constraint.position;
        }
    }
}
