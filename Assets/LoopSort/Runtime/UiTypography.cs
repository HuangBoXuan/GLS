using UnityEngine;
using UnityEngine.UI;

namespace LoopSort0
{
    public static class UiTypography
    {
        public static void Apply(Text label,bool white,bool level)
        {
            label.color=white?Color.white:new Color(.35f,.34f,.49f,1);
            // Both bundled typefaces already have a bold master; avoid synthetic
            // bold which closes small counters and makes the reference feel heavy.
            label.fontStyle=FontStyle.Normal;
            var outline=label.GetComponent<Outline>();
            if(level)
            {
                if(outline==null)outline=label.gameObject.AddComponent<Outline>();
                outline.enabled=true;outline.effectColor=new Color(.29f,.20f,.44f,1);
                outline.effectDistance=new Vector2(3,-3);
            }
            else if(outline!=null)outline.enabled=false;
            Shadow shadow=null;
            foreach(var effect in label.GetComponents<Shadow>())if(!(effect is Outline)){shadow=effect;break;}
            if(white&&!level)
            {
                if(shadow==null)shadow=label.gameObject.AddComponent<Shadow>();
                shadow.enabled=true;shadow.effectColor=new Color(.25f,.18f,.39f,.65f);
                shadow.effectDistance=new Vector2(0,-2);shadow.useGraphicAlpha=true;
            }
            else if(shadow!=null)shadow.enabled=false;
        }
    }
}
