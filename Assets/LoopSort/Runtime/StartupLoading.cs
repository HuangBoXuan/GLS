using UnityEngine;
using UnityEngine.UI;

namespace LoopSort0
{
    // Uses packaged Resources only: this screen must work before hot content,
    // Lua, the gameplay camera and the HUD have been initialized.
    public sealed class StartupLoading : MonoBehaviour
    {
        Text caption;
        LoadingProgressGraphic progressBar;
        string loadingCaption;
        float progress,targetProgress;
        bool failed,captionInitialized;
        public bool Finished=>progress>=1;
        public static StartupLoading Create(Transform owner)
        {
            var go=new GameObject("Startup loading",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(owner,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(750,1650);scaler.matchWidthOrHeight=.5f;
            var screen=go.AddComponent<StartupLoading>();screen.Build();return screen;
        }
        static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        void Build()
        {
            var background=Rect(transform,"Home illustration",Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>();
            background.texture=Resources.Load<Texture2D>("LoopSort/Theme/candy-factory-home");
            background.uvRect=ArtTheme.BackgroundUv(background.texture);
            background.color=background.texture!=null?Color.white:new Color(.26f,.13f,.23f,1);
            GameLogo.Create(transform);
            var shade=Rect(transform,"Loading panel",new Vector2(0,0),new Vector2(1,.125f)).gameObject.AddComponent<Image>();
            shade.color=new Color(.17f,.14f,.36f,.78f);
            progressBar=Rect(transform,"Loading progress",new Vector2(.10f,.068f),new Vector2(.90f,.098f)).gameObject.AddComponent<LoadingProgressGraphic>();
            progressBar.raycastTarget=false;
            caption=Rect(transform,"Loading caption",new Vector2(.06f,.018f),new Vector2(.94f,.057f)).gameObject.AddComponent<Text>();
            caption.font=Resources.Load<Font>("LoopSort/Fonts/NotoSansSC-Bold");caption.fontSize=32;
            caption.resizeTextForBestFit=true;caption.resizeTextMinSize=18;caption.resizeTextMaxSize=32;
            caption.alignment=TextAnchor.MiddleCenter;caption.color=Color.white;caption.raycastTarget=false;
            string language=PlayerPrefs.GetString("LoopSort.Ui.Language","zh-CN");
            loadingCaption=language=="en"?"Loading…":language=="pt-BR"?"Carregando…":language=="hi"?"लोड हो रहा है…":"加载中…";
            caption.text=loadingCaption;
            caption.font=UiLocalization.FontFor(caption.text);caption.text=UiLocalization.Shape(caption.text);
            SetProgress(0);
            DrawProgress();
        }
        void Update()
        {
            if(HotUpdate.HotContent.Busy)SetProgress(.05f+.65f*HotUpdate.HotContent.PreparationProgress);
            if(failed)return;
            float next=Mathf.MoveTowards(progress,targetProgress,Mathf.Min(Time.unscaledDeltaTime,.05f)*1.4f);
            if(!Mathf.Approximately(progress,next)){progress=next;DrawProgress();}
        }
        public void SetProgress(float value)
        {
            if(failed)return;
            targetProgress=Mathf.Max(targetProgress,Mathf.Clamp01(value));
        }
        void DrawProgress()
        {
            if(progressBar!=null)progressBar.SetProgress(progress);
            int percent=Mathf.FloorToInt(progress*100);
            if(caption!=null&&(!captionInitialized||shownPercent!=percent))
            {captionInitialized=true;shownPercent=percent;caption.text=UiLocalization.Shape(loadingCaption)+" "+percent+"%";}
        }
        int shownPercent=-1;
        public void Failed(string message)
        {
            failed=true;
            caption.text=message;caption.font=Resources.Load<Font>("LoopSort/Fonts/NotoSansSC-Bold");
        }
    }

    // A normal determinate progress bar; artwork animation belongs to the background.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoadingProgressGraphic : MaskableGraphic
    {
        float progress;
        public void SetProgress(float value){progress=Mathf.Clamp01(value);SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float w=r.width,h=r.height;
            Rounded(vh,r.x,r.y-3,w,h,h*.5f,new Color(.26f,.19f,.45f,.6f));
            Rounded(vh,r.x,r.y,w,h,h*.5f,new Color(.71f,.62f,.94f));
            Rounded(vh,r.x+4,r.y+4,w-8,h-8,(h-8)*.5f,new Color(.94f,.92f,1));
            float fill=(w-14)*progress;
            if(fill>.01f)
            {
                Rounded(vh,r.x+7,r.y+7,fill,h-14,(h-14)*.5f,new Color(.24f,.73f,.39f));
                Rounded(vh,r.x+7,r.y+h*.53f,fill,h*.24f,h*.12f,new Color(.48f,.86f,.49f));
            }
        }
        static void Rounded(VertexHelper vh,float x,float y,float w,float h,float radius,Color color)
        {
            if(w<=0||h<=0)return;
            radius=Mathf.Min(radius,Mathf.Min(w,h)*.5f);int n=vh.currentVertCount;
            vh.AddVert(new Vector3(x+w*.5f,y+h*.5f),color,Vector2.zero);
            const int steps=5,count=4*(steps+1);
            for(int corner=0;corner<4;corner++)
            {
                float cx=x+(corner==0||corner==3?w-radius:radius),cy=y+(corner<2?h-radius:radius);
                for(int step=0;step<=steps;step++)
                {
                    float angle=(corner*90+step*90f/steps)*Mathf.Deg2Rad;
                    vh.AddVert(new Vector3(cx+Mathf.Cos(angle)*radius,cy+Mathf.Sin(angle)*radius),color,Vector2.zero);
                }
            }
            for(int i=0;i<count;i++)vh.AddTriangle(n,n+1+i,n+1+(i+1)%count);
        }
    }
}
