using UnityEngine;

namespace LoopSort0
{
    internal sealed class TruckBackMotion : MonoBehaviour
    {
        SourceMorphView top,rear;
        bool initialized;
        internal static TruckBackMotion For(GameObject owner)
        {
            var value=owner.GetComponent<TruckBackMotion>()??owner.AddComponent<TruckBackMotion>();
            value.Initialize();return value;
        }
        void Initialize()
        {
            if(initialized)return;initialized=true;
            top=transform.Find("Pivot/TopPart1")?.GetComponent<SourceMorphView>();
            rear=transform.Find("Pivot/TopPart2V2")?.GetComponent<SourceMorphView>();
        }
        internal void SetOpen(bool open,bool immediate)
        {
            if(top==null||rear==null)return;
            var motion=MotionRunner.For(gameObject);motion.Stop("truck-back");
            top.ResetWeights();rear.ResetWeights();
            top.gameObject.SetActive(true);rear.gameObject.SetActive(true);
            if(immediate)
            {
                top.Weight(0,100);rear.Weight(0,100);
                top.gameObject.SetActive(!open);rear.gameObject.SetActive(!open);return;
            }
            // Native Carrier.ApplyOpen/CloseBackMotion: the short rear hinge
            // overshoot runs alongside its fold, followed by the long roof fold.
            if(open)
            {
                top.Weight(0,100);rear.Weight(0,100);rear.Weight(2,100);
                motion.Play("truck-back",.85f,t=>
                {
                    float time=t*.85f;
                    rear.Weight(0,100*(1-Mathf.Clamp01(time/.3f)));
                    rear.Weight(2,time<.2f?100*Mathf.Clamp01(time/.2f):100*(1-Mathf.Clamp01((time-.2f)/.05f)));
                    top.Weight(0,100*(1-Mathf.Clamp01((time-.25f)/.6f)));
                },()=>{top.gameObject.SetActive(false);rear.gameObject.SetActive(false);});
            }
            else
            {
                rear.gameObject.SetActive(false);
                motion.Play("truck-back",.95f,t=>
                {
                    float time=t*.95f;top.Weight(0,100*Mathf.Clamp01(time/.6f));
                    if(time<.6f)return;
                    rear.gameObject.SetActive(true);
                    rear.Weight(2,time<.65f?100*Mathf.Clamp01((time-.6f)/.05f):100*(1-Mathf.Clamp01((time-.65f)/.3f)));
                    rear.Weight(0,100*Mathf.Clamp01((time-.65f)/.2f));
                },()=>{top.Weight(0,100);rear.Weight(0,100);rear.Weight(2,0);});
            }
        }
    }
}
