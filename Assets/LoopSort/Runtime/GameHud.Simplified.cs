using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LoopSort0
{
    public sealed partial class GameHud
    {
        public void HideDialogClose(){if(dialogClose!=null)dialogClose.gameObject.SetActive(false);}
        public void OpenInformation(string key)
        {
            var allowed=new[]{"privacyPolicyUrl","termsUrl","adPrivacyUrl","accountDeletionUrl"};
            if(Array.IndexOf(allowed,key)<0)return;
            var config=GameAssets.Data<Dictionary<string,object>>("peripheral-services");
            string url=config!=null&&config.TryGetValue(key,out var value)?Convert.ToString(value):"";
            if(Uri.TryCreate(url,UriKind.Absolute,out var address)&&address.Scheme==Uri.UriSchemeHttps)Application.OpenURL(url);
            else Toast("此服务页面尚未配置");
        }
        Coroutine celebrationRoutine;
        RectTransform celebrationLayer;
        public void Celebrate(){StopCelebration();celebrationRoutine=StartCoroutine(Confetti(modal));}
        void StopCelebration()
        {
            if(celebrationRoutine!=null){StopCoroutine(celebrationRoutine);celebrationRoutine=null;}
            if(celebrationLayer!=null){Destroy(celebrationLayer.gameObject);celebrationLayer=null;}
        }
        IEnumerator Confetti(RectTransform target)
        {
            if(target==null)yield break;
            const int count=56;
            var layer=Panel(target,"Celebration confetti",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            layer.SetAsFirstSibling();celebrationLayer=layer;
            var sparks=new RectTransform[count];var images=new UnityEngine.UI.Image[count];
            var starts=new Vector2[count];var velocities=new Vector2[count];
            var delays=new float[count];var phases=new float[count];var lifetimes=new float[count];var spins=new float[count];
            Color[] colors={ArtTheme.Hex(0xFFD166),ArtTheme.Hex(0xF49E4C),ArtTheme.Hex(0xEF7364),
                ArtTheme.Hex(0x69C6B5),ArtTheme.Hex(0xB69BD6),ArtTheme.Hex(0xFFF0CA)};
            for(int i=0;i<count;i++)
            {
                // Local deterministic variation leaves gameplay's random stream untouched.
                float u=((i*37+11)%101)/100f,v=((i*53+7)%97)/96f,side=i%2==0?1:-1;
                starts[i]=new Vector2(side>0?.08f:.92f,.47f);
                velocities[i]=new Vector2(side*(.22f+.26f*u),1+.24f*v);
                delays[i]=(i/14)*.10f+u*.08f;phases[i]=i*2.399963f;lifetimes[i]=1.8f+.5f*v;
                spins[i]=side*(180+280*u);
                var spark=Image(layer,"Confetti "+i,null,Vector2.one*.5f,Vector2.one*.5f);
                spark.color=Color.clear;spark.raycastTarget=false;sparks[i]=spark.rectTransform;images[i]=spark;
                sparks[i].sizeDelta=new Vector2(7+5*u,i%3==0?22:11+7*v);
            }
            float time=0;
            while(target!=null&&layer!=null&&target.gameObject.activeInHierarchy&&overlay.gameObject.activeInHierarchy&&time<3)
            {
                time+=Time.unscaledDeltaTime;
                float width=layer.rect.width,height=layer.rect.height;
                for(int i=0;i<count;i++)
                {
                    float age=time-delays[i];var c=colors[i%colors.Length];
                    if(age<0||age>lifetimes[i]){c.a=0;images[i].color=c;continue;}
                    var pos=starts[i]+velocities[i]*age+Vector2.down*(.8f*age*age);
                    pos.x+=Mathf.Sin(phases[i]+age*4)*.035f*age;
                    sparks[i].anchoredPosition=new Vector2((pos.x-.5f)*width,(pos.y-.5f)*height);
                    sparks[i].localRotation=Quaternion.Euler(0,0,phases[i]*Mathf.Rad2Deg+spins[i]*age);
                    sparks[i].localScale=new Vector3(.15f+.85f*Mathf.Abs(Mathf.Cos(phases[i]+age*8)),1,1);
                    c.a=Mathf.Clamp01(age/.08f)*Mathf.Clamp01((lifetimes[i]-age)/.45f)*Mathf.Clamp01((pos.y-.28f)/.25f);
                    images[i].color=c;
                }
                yield return null;
            }
            if(layer!=null)Destroy(layer.gameObject);
            celebrationLayer=null;celebrationRoutine=null;
        }
    }
}
