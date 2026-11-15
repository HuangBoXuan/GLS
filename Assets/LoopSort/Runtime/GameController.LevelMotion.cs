using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LoopSort0
{
    public sealed partial class GameController
    {
        [Serializable] sealed class EntranceCameraPadding {public float top,bottom,left,right;}
        sealed class EnteringTruck
        {
            public Transform root,leftWheel,rightWheel;
            public Vector3 from,to,forward,right,direction;
            public Quaternion rotation,leftRotation,rightRotation,arrivalRotation;
            public float duration,rightDot;
            public bool forwardMove,arrived;
            public TruckBackMotion back;
        }
        bool levelEntering;
        public bool IsEnteringLevel=>levelEntering;

        void BeginLevelEntrance()
        {
            levelEntering=true;
            track.SetEntranceClip(0);track.SetEntranceDetails(0);
            foreach(var feature in featureViews)feature.PrepareLevelEntrance();
            foreach(var group in groupFeatures)group.PrepareLevelEntrance();
            for(int i=0;i<carriers.Count;i++)
                if(State.Carriers[i].generator)SetVisible(cargoRoots[i].gameObject,false);
            var padding=GameAssets.Data<EntranceCameraPadding>("presentation");
            var plane=new Plane(Vector3.up,boardBounds.center);
            Vector3 Project(float x,float y)
            {
                var ray=Camera.ViewportPointToRay(new Vector3(x,y,0));
                return plane.Raycast(ray,out float distance)?ray.GetPoint(distance):boardBounds.center;
            }
            Vector3 center=Project(.5f,.5f);
            float width=Vector3.Distance(Project(0,.5f),Project(1,.5f))*.5f+padding.left+padding.right+5;
            float height=Vector3.Distance(Project(.5f,0),Project(.5f,1))*.5f+padding.top+padding.bottom+1.5f;
            var entries=new List<EnteringTruck>(carriers.Count);
            float arrival=0;
            for(int i=0;i<carriers.Count;i++)
            {
                var root=carriers[i];Vector3 to=root.position,forward=root.forward,right=root.right;
                Vector3 delta=center-to;
                float z=Mathf.Abs(forward.z),x=Mathf.Abs(forward.x);
                float outside=z<x?width:height;
                outside*=Vector3.Dot(delta,forward)<0?1:-1;
                float lateral=Vector3.Dot(delta,right);
                Vector3 from=to+forward*outside-right*(lateral*(z*lateral<x?1:3));
                Vector3 direction=(to-from).normalized;
                var entry=new EnteringTruck {
                    root=root,from=from,to=to,rotation=root.rotation,forward=forward,right=right,direction=direction,
                    duration=Mathf.Max(.01f,Vector3.Distance(from,to)*.04f),rightDot=Vector3.Dot(direction,right),
                    forwardMove=Vector3.Dot(direction,forward)>0,back=TruckBackMotion.For(root.gameObject),
                    leftWheel=root.Find("Pivot/Model Root/Wheels/Wheel L F"),
                    rightWheel=root.Find("Pivot/Model Root/Wheels/Wheel R F")
                };
                if(entry.leftWheel!=null)entry.leftRotation=entry.leftWheel.localRotation;
                if(entry.rightWheel!=null)entry.rightRotation=entry.rightWheel.localRotation;
                root.position=from;entry.back.SetOpen(false,true);
                entries.Add(entry);arrival=Mathf.Max(arrival,entry.duration);
            }
            StartCoroutine(PlayLevelEntrance(entries,arrival,run));
        }

        IEnumerator PlayLevelEntrance(List<EnteringTruck> entries,float arrival,int token)
        {
            float revealDuration=Mathf.Max(.01f,track.Length*.0275f);
            float settled=arrival+Mathf.Max(revealDuration,.6f);
            float total=track.Closed?arrival+revealDuration+.6f:settled+.6f,time=0;
            SourceParticleEffect trail=null;bool beganTrack=false;
            while(token==run&&time<total)
            {
                if(Paused){yield return null;continue;}
                time=Mathf.Min(total,time+Time.deltaTime);
                foreach(var entry in entries)PoseEnteringTruck(entry,time);
                if(time>=arrival)
                {
                    float progress=Mathf.Clamp01((time-arrival)/revealDuration);
                    track.SetEntranceClip(progress);
                    track.Spline.Evaluate(progress,out Vector3 head,out _,out _,out _);
                    if(!beganTrack)
                    {
                        beganTrack=true;
                        trail=SourceParticleEffect.Spawn("trail",board,head+Vector3.down*.5f,Quaternion.identity);
                    }
                    if(trail!=null)trail.transform.position=head+Vector3.down*.5f;
                    if(progress>=1)
                    {
                        if(trail!=null){trail.Stop();trail=null;}
                        if(time>=settled)track.SetEntranceDetails(time-settled);
                    }
                }
                yield return null;
            }
            while(token==run&&Paused)yield return null;
            if(token!=run)yield break;
            foreach(var entry in entries)
            {
                entry.root.SetPositionAndRotation(entry.to,entry.rotation);
                ResetEntranceWheels(entry);
            }
            track.SetEntranceClip(1);track.SetEntranceDetails(.6f);
            RevealEntranceMechanisms(false);
            if(readyForGuides)ShowNewMechanics();
        }

        void RevealEntranceMechanisms(bool immediate)
        {
            levelEntering=false;
            for(int i=0;i<featureViews.Count;i++)
            {
                featureViews[i].CompleteLevelEntrance();
                if(State.Carriers[i].generator)RefreshCargo(i);
            }
            foreach(var group in groupFeatures)group.CompleteLevelEntrance(immediate);
        }

        static void PoseEnteringTruck(EnteringTruck entry,float time)
        {
            if(time<entry.duration)
            {
                float t=Mathf.Clamp01(time/entry.duration);
                Vector3 position=Vector3.Lerp(entry.from,entry.to,t)+entry.right*(1.5f*Mathf.PI*entry.rightDot*t*Mathf.Sin(Mathf.PI*t));
                Vector3 motion=position-entry.root.position;
                if(motion.sqrMagnitude>.000001f)
                {
                    Vector3 direction=motion.normalized;
                    Vector3 facing=entry.forwardMove?direction:-direction;
                    float turn=Vector3.SignedAngle(entry.forwardMove?entry.root.forward:-entry.root.forward,direction,Vector3.up);
                    float steering=Mathf.Clamp(turn*(entry.forwardMove?-40:40),-40,40);
                    if(entry.leftWheel!=null)entry.leftWheel.localRotation=Quaternion.AngleAxis(steering,Vector3.up)*entry.leftRotation;
                    if(entry.rightWheel!=null)entry.rightWheel.localRotation=Quaternion.AngleAxis(steering,Vector3.up)*entry.rightRotation;
                    entry.root.rotation=Quaternion.LookRotation(facing,Vector3.up);
                }
                entry.root.position=position;return;
            }
            if(!entry.arrived)
            {
                entry.arrived=true;entry.arrivalRotation=entry.root.rotation;
                entry.root.position=entry.to;ResetEntranceWheels(entry);
                entry.back.SetOpen(true,false);
            }
            float elapsed=Mathf.Min(.6f,time-entry.duration);
            // Native post motion: three 0.2s rotation stages and two
            // 0.2s OutQuad position stages, with a 0.3-unit braking overshoot.
            Vector3 travel=entry.forwardMove?entry.forward:-entry.forward;
            Vector3 axis=Vector3.Cross(travel,Vector3.up);
            Quaternion positive=Quaternion.AngleAxis(2,axis)*entry.rotation;
            Quaternion negative=Quaternion.AngleAxis(-2,axis)*entry.rotation;
            if(elapsed<.2f)entry.root.rotation=Quaternion.Lerp(entry.arrivalRotation,positive,elapsed/.2f);
            else if(elapsed<.4f)entry.root.rotation=Quaternion.Lerp(positive,negative,(elapsed-.2f)/.2f);
            else entry.root.rotation=Quaternion.Lerp(negative,entry.rotation,(elapsed-.4f)/.2f);
            float phase=Mathf.Clamp01((elapsed<.2f?elapsed:elapsed-.2f)/.2f);
            float ease=1-(1-phase)*(1-phase);
            entry.root.position=entry.to+travel*(.3f*(elapsed<.2f?ease:1-ease));
        }
        static void ResetEntranceWheels(EnteringTruck entry)
        {
            if(entry.leftWheel!=null)entry.leftWheel.localRotation=entry.leftRotation;
            if(entry.rightWheel!=null)entry.rightWheel.localRotation=entry.rightRotation;
        }
    }
}
