using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LoopSort0
{
    public sealed class MotionRunner : MonoBehaviour
    {
        readonly Dictionary<string,Coroutine> tracks=new Dictionary<string,Coroutine>();
        public bool Busy=>tracks.Count>0;
        public static MotionRunner For(GameObject owner)=>owner.GetComponent<MotionRunner>()??owner.AddComponent<MotionRunner>();
        public void Play(string channel,float duration,Action<float> pose,Action finish=null,float delay=0)
        {
            Stop(channel);tracks[channel]=StartCoroutine(Animate(channel,duration,pose,finish,delay));
        }
        IEnumerator Animate(string channel,float duration,Action<float> pose,Action finish,float delay)
        {
            float time=-delay;
            while(time<duration)
            {
                if(GameController.Instance!=null&&!GameController.Instance.Paused)
                {
                    time+=Time.deltaTime;if(time>=0)pose(Mathf.Clamp01(time/Mathf.Max(.001f,duration)));
                }
                yield return null;
            }
            tracks.Remove(channel);finish?.Invoke();
        }
        public void Stop(string channel){if(tracks.TryGetValue(channel,out var routine)){StopCoroutine(routine);tracks.Remove(channel);}}
        public void StopAll(){StopAllCoroutines();tracks.Clear();}
        void OnDisable(){StopAll();}
        public static float OutBack(float t){float x=t-1;return 1+2.70158f*x*x*x+1.70158f*x*x;}
        public static float InOutBack(float t)
        {
            const float c=2.5949095f;
            if(t<.5f){float x=2*t;return x*x*((c+1)*x-c)*.5f;}
            float y=2*t-2;return (y*y*((c+1)*y+c)+2)*.5f;
        }
    }
}
