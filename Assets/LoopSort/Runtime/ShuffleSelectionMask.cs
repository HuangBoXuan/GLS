using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LoopSort0
{
    // A single UI mesh shades the union's complement. All pointer events stay
    // on this graphic, including transparent holes, until a target is validated.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ShuffleSelectionMask:MaskableGraphic,IPointerClickHandler
    {
        GameController game;int epoch=-1,features=-1,cargoStamp;float refreshAt;Matrix4x4 projection;Vector2 size;
        readonly List<Rect> holes=new List<Rect>();readonly List<float> cuts=new List<float>();
        readonly List<Rect> row=new List<Rect>();
        public void Initialize(GameController value){game=value;color=new Color(.09f,.045f,.12f,.70f);raycastTarget=true;Refresh();}
        void LateUpdate()
        {
            if(game==null||!game.SelectingShuffle)return;
            if(epoch!=game.State.Epoch||features!=game.State.FeatureRevision||cargoStamp!=CargoStamp()||Time.unscaledTime>=refreshAt||projection!=game.Camera.projectionMatrix*game.Camera.worldToCameraMatrix||size!=rectTransform.rect.size)Refresh();
        }
        int CargoStamp()
        {
            unchecked {int stamp=17;foreach(var c in game.State.Carriers)stamp=stamp*31+c.cargo.Count*7+c.incoming.Count*3+c.releasing+(c.CanEmit?1:0);return stamp;}
        }
        void Refresh()
        {
            if(game==null||game.State==null)return;holes.Clear();
            epoch=game.State.Epoch;features=game.State.FeatureRevision;cargoStamp=CargoStamp();refreshAt=Time.unscaledTime+.35f;projection=game.Camera.projectionMatrix*game.Camera.worldToCameraMatrix;size=rectTransform.rect.size;
            for(int index=0;index<game.TruckViews.Count;index++)
            {
                if(!game.CanShuffleCarrier(index))continue;
                var box=game.TruckViews[index].GetComponent<BoxCollider>();if(box==null)continue;
                Vector2 min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue;
                for(int i=0;i<8;i++)
                {
                    var point=box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    var screen=game.Camera.WorldToScreenPoint(point);Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screen,canvas.worldCamera,out local);min=Vector2.Min(min,local);max=Vector2.Max(max,local);
                }
                holes.Add(Rect.MinMaxRect(min.x-7,min.y-7,max.x+7,max.y+7));
            }
            SetVerticesDirty();
        }
        public void OnPointerClick(PointerEventData data){if(game!=null)game.SelectShuffleAt(data.position);}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var area=rectTransform.rect;cuts.Clear();cuts.Add(area.yMin);cuts.Add(area.yMax);
            foreach(var hole in holes){cuts.Add(Mathf.Clamp(hole.yMin,area.yMin,area.yMax));cuts.Add(Mathf.Clamp(hole.yMax,area.yMin,area.yMax));}
            cuts.Sort();
            for(int i=1;i<cuts.Count;i++)
            {
                float bottom=cuts[i-1],top=cuts[i];if(top-bottom<.01f)continue;float mid=(bottom+top)*.5f;
                row.Clear();foreach(var hole in holes)if(hole.yMin<=mid&&hole.yMax>=mid)row.Add(hole);
                row.Sort((a,b)=>a.xMin.CompareTo(b.xMin));float x=area.xMin;
                foreach(var hole in row){float next=Mathf.Clamp(hole.xMin,area.xMin,area.xMax);Quad(vh,x,bottom,next,top);x=Mathf.Max(x,Mathf.Min(area.xMax,hole.xMax));}
                Quad(vh,x,bottom,area.xMax,top);
            }
        }
        void Quad(VertexHelper vh,float left,float bottom,float right,float top)
        {
            if(right<=left)return;int i=vh.currentVertCount;
            vh.AddVert(new Vector3(left,bottom),color,Vector2.zero);vh.AddVert(new Vector3(left,top),color,Vector2.zero);
            vh.AddVert(new Vector3(right,top),color,Vector2.zero);vh.AddVert(new Vector3(right,bottom),color,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
