using UnityEngine;
using UnityEngine.UI;

namespace LoopSort0
{
    public sealed partial class GameHud
    {
        bool candyResult;
        RawImage candyBackdrop;
        RenderTexture candySnapshot;
        Material candyBlur;

        void BuildCandyHeader(RectTransform chrome)
        {
            Art(chrome,"Candy header","candyHeader",new Vector2(.014f,.848f),new Vector2(.986f,.998f));
            for(int i=0;i<22;i++)
            {
                float x=.124f+i*.033f;
                var dash=Image(chrome,"Header stitch",null,new Vector2(x,.876f),new Vector2(x+.017f,.880f));
                dash.color=new Color(.93f,.90f,1,.64f);
            }
            foreach(float x in new[]{.088f,.802f})
                Art(chrome,"Header pin","candyHeader",new Vector2(x,.946f),new Vector2(x+.070f,1.027f));
        }

        // Render the scene once when entering a dialog group. The blurred
        // backdrop remains static for the lifetime of that dialog.
        void CaptureCandyBackdrop()
        {
            if(candySnapshot!=null)return;
            var camera=Canvas.worldCamera;
            if(camera==null||Screen.width<=0||Screen.height<=0)return;
            if(candyBlur==null)
            {
                var shader=Resources.Load<Shader>("LoopSort/Shaders/CandyModalBlur");
                if(shader==null||!shader.isSupported)return;
                candyBlur=new Material(shader);
            }
            int height=288,width=Mathf.Max(1,Mathf.RoundToInt(height*(float)Screen.width/Screen.height));
            var source=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            var previous=camera.targetTexture;
            var active=RenderTexture.active;
            bool showOverlay=overlay.gameObject.activeSelf;
            float aspect=camera.aspect;
            try
            {
                overlay.gameObject.SetActive(false);
                camera.targetTexture=source;
                camera.aspect=aspect;
                UnityEngine.Canvas.ForceUpdateCanvases();
                camera.Render();
                candySnapshot=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
                Graphics.Blit(source,candySnapshot,candyBlur);
            }
            finally
            {
                camera.targetTexture=previous;
                camera.aspect=aspect;
                RenderTexture.active=active;
                RenderTexture.ReleaseTemporary(source);
                overlay.gameObject.SetActive(showOverlay);
            }

            if(candyBackdrop==null)
            {
                var rect=Panel(overlayBackground,"Softened board",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
                candyBackdrop=rect.gameObject.AddComponent<RawImage>();
                candyBackdrop.raycastTarget=false;
            }
            candyBackdrop.texture=candySnapshot;
            candyBackdrop.color=new Color(.57f,.55f,.71f,1);
            candyBackdrop.gameObject.SetActive(true);
        }

        void ClearCandyBackdrop()
        {
            if(candyBackdrop!=null)
            {
                candyBackdrop.texture=null;
                candyBackdrop.gameObject.SetActive(false);
            }
            if(candySnapshot==null)return;
            RenderTexture.ReleaseTemporary(candySnapshot);
            candySnapshot=null;
        }

        void OnDestroy()
        {
            ClearCandyBackdrop();
            if(candyBlur!=null)Destroy(candyBlur);
        }
    }
}
