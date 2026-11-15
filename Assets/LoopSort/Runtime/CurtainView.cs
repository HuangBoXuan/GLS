using UnityEngine;

namespace LoopSort0
{
    public sealed class CurtainView:MonoBehaviour
    {
        Transform cloth,lidHinge;SourceMorphView morph;Material material;bool targetOpen=true;
        public bool IsCovering=>cloth!=null&&(!targetOpen||MotionRunner.For(gameObject).Busy);
        public void Build(int color,Transform visualRoot)
        {
            if(color<=0)return;
            material=GameAssets.NewMaterial(GameAssets.Color(color));
            var go=new GameObject("Colored curtain");go.transform.SetParent(visualRoot,false);
            morph=SourceMorphView.Attach(go,395,new[]{material});cloth=go.transform;
            cloth.localPosition=new Vector3(0,.181f,-2.7f);cloth.localRotation=Quaternion.Euler(-90,0,0);
            lidHinge=VolcanoArt.AddLid(visualRoot,color);
            material.shader=Resources.Load<Shader>("LoopSort/Shaders/VolcanoSourceHidden");
            SetRevealed(false,true);
        }
        public void SetRevealed(bool open,bool immediate=false)
        {
            if(cloth==null)return;
            if(!immediate&&targetOpen==open)return;targetOpen=open;
            var runner=MotionRunner.For(gameObject);runner.Stop("curtain");float duration=morph.CurtainDuration;
            cloth.gameObject.SetActive(true);
            if(immediate){Sample(open?1:0,duration);return;}
            runner.Play("curtain",duration,t=>Sample(open?t:1-t,duration));
        }
        void Sample(float progress,float duration)
        {
            morph.SampleCurtain(progress*duration);
            lidHinge.localRotation=Quaternion.Euler(0,0,Mathf.SmoothStep(0,90,progress));
            lidHinge.gameObject.SetActive(progress<1f);
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
