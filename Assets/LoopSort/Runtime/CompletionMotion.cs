using UnityEngine;

namespace LoopSort0
{
    public sealed class CompletionMotion : MonoBehaviour
    {
        Vector3 size;bool completed,applied;float angle;
        internal bool Animating {get;private set;}
        public void Initialize(){size=transform.localScale;}
        public void SetComplete(bool value,bool immediate=false)
        {
            if(!applied){applied=true;immediate=true;}
            if(!immediate&&completed==value)return;completed=value;
            var motion=MotionRunner.For(transform.parent.gameObject);motion.Stop("completion");
            MotionRunner.For(gameObject).StopAll();angle=0;Animating=false;
            var back=TruckBackMotion.For(transform.parent.gameObject);
            back.SetOpen(!value,immediate||!value);
            gameObject.SetActive(value&&immediate);transform.localScale=size;
            if(!value||immediate)return;
            Animating=true;gameObject.SetActive(true);transform.localScale=size*2;angle=10;
            TransientEffect.Firework(transform.parent,transform.position);
            // Show the check as soon as the last block settles, alongside the
            // roof closure. Keep the result waiting for the .95-second closure,
            // while the original check pop itself finishes in .5 seconds.
            motion.Play("completion",.95f,t=>
            {
                float ease=MotionRunner.InOutBack(Mathf.Clamp01(t*.95f/.5f));transform.localScale=size*Mathf.LerpUnclamped(2,1,ease);
                angle=Mathf.LerpUnclamped(10,0,ease);
            },()=>{transform.localScale=size;angle=0;Animating=false;});
        }
        void LateUpdate()
        {
            if(GameController.Instance?.Camera!=null)transform.rotation=GameController.Instance.Camera.transform.rotation*Quaternion.Euler(0,0,angle);
        }
    }
}
