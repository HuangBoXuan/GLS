using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopSort0
{
    // Serialized source clip coefficients, sampled without an Animator controller.
    internal static class SourceTransformClip
    {
        [Serializable] sealed class Clip {public float duration;public Binding[] bindings;}
        [Serializable] sealed class Binding {public string path;public int attribute;public Curve[] curves;}
        [Serializable] sealed class Curve {public Key[] keys;}
        [Serializable] sealed class Key {public float time;public float[] coefficients;}
        static readonly Dictionary<string,Clip> clips=new Dictionary<string,Clip>();
        internal static void ReleaseCaches()=>clips.Clear();

        internal static void Play(GameObject owner,string id,Action finish)
        {
            if(!clips.TryGetValue(id,out var clip))clips[id]=clip=GameAssets.AnimationData<Clip>(id);
            var targets=new Transform[clip.bindings.Length];
            for(int i=0;i<targets.Length;i++)targets[i]=owner.transform.Find(clip.bindings[i].path);
            Action<float> pose=t=>
            {
                float time=t*clip.duration;
                for(int i=0;i<targets.Length;i++)
                {
                    if(targets[i]==null)continue;
                    var binding=clip.bindings[i];var c=binding.curves;
                    var value=new Vector3(Sample(c[0],time),Sample(c[1],time),Sample(c[2],time));
                    if(binding.attribute==1)targets[i].localPosition=value;
                    else if(binding.attribute==3)targets[i].localScale=value;
                    else if(binding.attribute==4)targets[i].localEulerAngles=value;
                    else if(binding.attribute==2)targets[i].localRotation=new Quaternion(value.x,value.y,value.z,Sample(c[3],time));
                }
            };
            pose(0);MotionRunner.For(owner).Play(id,clip.duration,pose,()=>{pose(1);finish?.Invoke();});
        }
        static float Sample(Curve curve,float time)
        {
            var keys=curve.keys;int i=0;
            while(i+1<keys.Length&&keys[i+1].time<=time)i++;
            var key=keys[i];float dt=Mathf.Max(0,time-key.time);var c=key.coefficients;
            return ((c[0]*dt+c[1])*dt+c[2])*dt+c[3];
        }
    }
}
