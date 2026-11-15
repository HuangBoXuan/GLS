using UnityEngine;
using UnityEngine.UI;

namespace LoopSort0
{
    // Lua selects the artwork; Unity handles atlas sprites and nine-sliced panels.
    public sealed class ReferenceImage : Image
    {
        string artwork;
        bool panel;
        RectTransform content;
        Rect lastRect;Sprite lastSprite;bool lastAspect;
        bool ToastPanel=>artwork=="toastPanel";
        public override Texture mainTexture=>ToastPanel?(ArtTheme.WhiteSprite?.texture??Texture2D.whiteTexture):base.mainTexture;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if(ToastPanel)
            {
                mesh.Clear();Rect bounds=GetPixelAdjustedRect();
                const float toastEdge=1.5f;
                float corner=Mathf.Min(bounds.height*.22f,16);
                var white=ArtTheme.WhiteSprite;
                Vector2 uv=white!=null?new Vector2(white.rect.center.x/white.texture.width,white.rect.center.y/white.texture.height):Vector2.zero;
                Plate(mesh,bounds,corner,ArtTheme.Hex(0xBA9EE3),uv);
                bounds.xMin+=toastEdge;bounds.xMax-=toastEdge;bounds.yMin+=toastEdge;bounds.yMax-=toastEdge;
                Plate(mesh,bounds,corner-toastEdge,ArtTheme.Hex(0xF6F0FF),uv);
                return;
            }
            base.OnPopulateMesh(mesh);
        }
        void Plate(VertexHelper mesh,Rect r,float radius,Color tint,Vector2 uv)
        {
            if(r.width<=0||r.height<=0)return;
            radius=Mathf.Clamp(radius,0,Mathf.Min(r.width,r.height)*.5f);
            int first=mesh.currentVertCount;const int steps=8,count=4*(steps+1);
            mesh.AddVert(r.center,tint*color,uv);
            for(int corner=0;corner<4;corner++)
            {
                float x=corner==0||corner==3?r.xMax-radius:r.xMin+radius;
                float y=corner<2?r.yMax-radius:r.yMin+radius;
                for(int i=0;i<=steps;i++)
                {
                    float angle=(corner*90+i*90f/steps)*Mathf.Deg2Rad;
                    var p=new Vector2(x+Mathf.Cos(angle)*radius,y+Mathf.Sin(angle)*radius);
                    mesh.AddVert(p,tint*color,uv);
                }
            }
            for(int i=0;i<count;i++)mesh.AddTriangle(first,first+1+i,first+1+(i+1)%count);
        }
        public void SetArtwork(string id)
        {
            if(artwork==id&&sprite!=null)return;
            artwork=id;sprite=GameAssets.Sprite(id);panel=ArtTheme.IsPanel(id);
            material=id=="signinCard"?HotUpdate.HotContent.Load<Material>("LoopSort/Shaders/SigninCard"):null;
            ConfigureGeometry();UpdateContent();
        }
        internal RectTransform ContentRoot
        {
            get
            {
                if(sprite==null||color.a<=.001f)return rectTransform;
                if(content==null)
                {
                    content=new GameObject("Artwork content",typeof(RectTransform)).GetComponent<RectTransform>();
                    content.SetParent(rectTransform,false);
                }
                ConfigureGeometry();UpdateContent();return content;
            }
        }
        internal void ConfigureGeometry()
        {
            var desired=panel&&!preserveAspect?Type.Sliced:Type.Simple;
            if(type!=desired)type=desired;
            float multiplier=1;var r=rectTransform.rect;
            if(sprite!=null&&desired==Type.Sliced&&r.width>0&&r.height>0)
            {
                var b=sprite.border;float ppu=Mathf.Max(.01f,pixelsPerUnit);
                // Keep the center visible even on short level/price buttons.
                bool scalesWithHeight=artwork.Contains("Button")||artwork=="button"||artwork=="levelTile"||artwork=="candyHomeChip";
                float minimum=1;
                if(ArtTheme.ResponsivePanel(artwork))
                {
                    float scale=artwork=="frame"?.20f:artwork=="button"||artwork=="levelTile"||artwork=="yellowButton"?.88f:1f;
                    minimum=sprite.rect.height/(Mathf.Max(1,Mathf.Min(r.width,r.height))*scale*ppu);
                }
                if(ArtTheme.StandalonePanel(artwork))
                {
                    minimum=scalesWithHeight?sprite.rect.height/(r.height*ppu):artwork=="frame"?.35f:artwork=="settingsInner"||artwork=="paper"||artwork=="menuRow"?.6f:.4f;
                }
                float edgeLimit=scalesWithHeight?.98f:.70f;
                multiplier=Mathf.Max(minimum,(b.x+b.z)/(ppu*r.width*edgeLimit),(b.y+b.w)/(ppu*r.height*edgeLimit));
            }
            if(!Mathf.Approximately(pixelsPerUnitMultiplier,multiplier))pixelsPerUnitMultiplier=multiplier;
        }
        void LateUpdate()
        {
            if(lastRect!=rectTransform.rect||lastSprite!=sprite||lastAspect!=preserveAspect)
            {ConfigureGeometry();UpdateContent();}
        }
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();ConfigureGeometry();UpdateContent();
        }
        void UpdateContent()
        {
            var r=rectTransform.rect;lastRect=r;lastSprite=sprite;lastAspect=preserveAspect;
            if(content==null||sprite==null||r.width<=0||r.height<=0)return;
            if(ToastPanel)
            {
                content.anchorMin=new Vector2(.04f,.07f);content.anchorMax=new Vector2(.96f,.96f);
                content.offsetMin=content.offsetMax=Vector2.zero;return;
            }
            Rect drawn=r;var size=sprite.rect.size;
            if(type==Type.Simple&&preserveAspect)
            {
                float scale=Mathf.Min(r.width/size.x,r.height/size.y);
                drawn=new Rect(r.center-size*scale*.5f,size*scale);
            }
            var face=ArtTheme.ContentBounds(artwork);var border=sprite.border;
            float ppu=Mathf.Max(.01f,pixelsPerUnit*pixelsPerUnitMultiplier);
            bool sliced=type==Type.Sliced&&hasBorder;
            float x0=Map(face.xMin,size.x,border.x,border.z,drawn.width,ppu,sliced);
            float x1=Map(face.xMax,size.x,border.x,border.z,drawn.width,ppu,sliced);
            float y0=Map(face.yMin,size.y,border.y,border.w,drawn.height,ppu,sliced);
            float y1=Map(face.yMax,size.y,border.y,border.w,drawn.height,ppu,sliced);
            content.anchorMin=new Vector2((drawn.xMin+x0-r.xMin)/r.width,(drawn.yMin+y0-r.yMin)/r.height);
            content.anchorMax=new Vector2((drawn.xMin+x1-r.xMin)/r.width,(drawn.yMin+y1-r.yMin)/r.height);
            content.offsetMin=content.offsetMax=Vector2.zero;
        }
        static float Map(float point,float source,float low,float high,float target,float ppu,bool sliced)
        {
            if(!sliced)return point*target;
            float pixel=point*source;
            if(pixel<=low)return pixel/ppu;
            if(pixel>=source-high)return target-(source-pixel)/ppu;
            return low/ppu+(pixel-low)/(source-low-high)*(target-(low+high)/ppu);
        }
    }
}
