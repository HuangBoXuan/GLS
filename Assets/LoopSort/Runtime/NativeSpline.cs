using System;
using System.Collections.Generic;
using LoopSort0.Core;
using UnityEngine;

namespace LoopSort0
{
    // Same-build Dreamteck BSpline, sampleRate=8, default sample mode.
    // See NativeMovement.md for native addresses and serialized resource evidence.
    public sealed class NativeSpline
    {
        readonly Vector3[] controls;
        readonly Vector3[] positions,forwards;
        readonly float[] distances;
        public bool Closed { get; }
        public int SampleCount=>positions.Length;
        public float Length=>distances[distances.Length-1];
        public NativeSpline(TrackPoint[] source,bool closed,int subdivide)
        {
            if(source==null||source.Length<2)throw new ArgumentException("A conveyor requires at least two control points.");
            Closed=closed;
            var points=new List<Vector3>(source.Length);
            foreach(var point in source)points.Add(new Vector3(point.x,TrackGeometry.Surface+point.y,point.z));
            for(int pass=0;pass<subdivide;pass++)
            {
                var expanded=new List<Vector3>(points.Count*2);
                for(int i=0;i<points.Count;i++)
                {
                    expanded.Add(points[i]);
                    if(closed||i+1<points.Count)expanded.Add((points[i]+points[(i+1)%points.Count])*.5f);
                }
                points=expanded;
            }
            controls=points.ToArray();
            int intervals=7*(closed?controls.Length:controls.Length-1);
            positions=new Vector3[intervals+1];forwards=new Vector3[intervals+1];distances=new float[intervals+1];
            double step=1.0/intervals;
            for(int i=0;i<=intervals;i++)
            {
                double percent=i/(double)intervals;
                Vector3 position=Position(percent),forward;
                if(closed)forward=Position(Wrap(percent+step))-Position(Wrap(percent-step));
                else if(i>0&&i<intervals)forward=Position(percent+step)-Position(percent-step);
                else
                {
                    Vector3 previous=position-Position(percent-step),next=Position(percent+step)-position;
                    // Spline.Evaluate's open-end BSpline tangent branch.
                    forward=Vector3.Slerp(next,previous,next.magnitude>0?previous.magnitude/next.magnitude:float.PositiveInfinity);
                }
                positions[i]=position;forwards[i]=forward.normalized;
                if(i>0)distances[i]=distances[i-1]+Vector3.Distance(positions[i-1],position);
            }
        }
        static double Wrap(double value)=>value-Math.Floor(value);
        Vector3 Control(int index)
        {
            int count=controls.Length;
            if(Closed)return controls[(index%count+count)%count];
            if(index<0)return controls[0]+(controls[1]-controls[0])*index;
            if(index>=count)return controls[count-1]+(controls[count-1]-controls[count-2])*(index-count+1);
            return controls[index];
        }
        Vector3 Position(double percent)
        {
            double scaled=Math.Max(0,Math.Min(1,percent))*(Closed?controls.Length:controls.Length-1);
            int segment=(int)Math.Floor(scaled);float t=(float)(scaled-segment),t2=t*t,t3=t2*t;
            return (Control(segment-1)*(1-3*t+3*t2-t3)+Control(segment)*(4-6*t2+3*t3)
                +Control(segment+1)*(1+3*t+3*t2-3*t3)+Control(segment+2)*t3)/6f;
        }
        public void Evaluate(double percent,out Vector3 position,out Vector3 forward,out Vector3 up,out float distance)
        {
            double scaled=Math.Max(0,Math.Min(1,percent))*(positions.Length-1);
            int index=Math.Min((int)Math.Floor(scaled),positions.Length-2);float t=(float)(scaled-index);
            position=Vector3.Lerp(positions[index],positions[index+1],t);
            forward=Vector3.Slerp(forwards[index],forwards[index+1],t);
            up=Vector3.up;distance=Mathf.Lerp(distances[index],distances[index+1],t);
        }
        public float DistanceAtPercent(double percent)
        {
            Evaluate(percent,out _,out _,out _,out float distance);return distance;
        }
        public Vector3 AtDistance(float distance)
        {
            distance=Closed?Mathf.Repeat(distance,Length):Mathf.Clamp(distance,0,Length);
            int first=1,last=distances.Length-1;
            while(first<last){int middle=(first+last)/2;if(distances[middle]<distance)first=middle+1;else last=middle;}
            float t=Mathf.InverseLerp(distances[first-1],distances[first],distance);
            return Vector3.Lerp(positions[first-1],positions[first],t);
        }
    }
}
