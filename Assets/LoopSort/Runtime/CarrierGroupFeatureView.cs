using System;
using System.Collections.Generic;
using System.Linq;
using LoopSort0.Core;
using UnityEngine;

namespace LoopSort0
{
    // These source prefabs span a row of carriers, rather than decorating each cart.
    public sealed class CarrierGroupFeatureView : MonoBehaviour
    {
        string kind,key;int color;
        int[] members;
        IReadOnlyList<Transform> carriers;
        Vector3 pivot;
        Transform model,left,right,width,door,padlock,roll,leftRoll,gateArt;
        Vector3 gateArtSize,gateArtPosition;
        int coveredCount=-1;
        CarrierFeatureView.GroupLayout layout;
        Vector3 fromPosition,toPosition,fromLeft,toLeft,fromRight,toRight;
        float fromWidth,toWidth,progress=1,unlockTime=-1,ropeTime=-1,entranceTime=-1,rollRemovalTime=-1;
        bool carpetRemoving;
        readonly HashSet<int> carpetRevealing=new HashSet<int>();
        SourceMorphView frontMorph,topMorph;
        GameObject flyingKey;Vector3 keyOrigin;Quaternion keyOriginRotation;
        bool closed=true;
        public bool Busy=>entranceTime>=0||unlockTime>=0&&unlockTime<2.18f||ropeTime>=0&&ropeTime<.25f||progress<1||rollRemovalTime>=0;
        public bool Blocks(int carrier)=>members.Contains(carrier)&&
            (kind=="lock"&&unlockTime>=0&&unlockTime<2.18f||kind=="rope"&&ropeTime>=0&&ropeTime<.25f||kind=="carpet"&&progress<1&&carpetRevealing.Contains(carrier));
        public Bounds ClosedBounds {get;private set;}
        internal IReadOnlyList<int> Members=>members;
        internal void TranslateLayout(Vector3 offset)
        {
            transform.position+=offset;fromPosition+=offset;toPosition+=offset;
            var bounds=ClosedBounds;bounds.center+=offset;ClosedBounds=bounds;
        }

        public static List<CarrierGroupFeatureView> Build(Transform board,LevelData level,
            IReadOnlyList<Transform> carriers,Vector3 pivot,GameState state)
        {
            var result=new List<CarrierGroupFeatureView>();
            foreach(int color in level.carriers.SelectMany(c=>c.lockColors??Array.Empty<int>()).Distinct())
                Add("lock",null,color,level.carriers.Select((c,i)=>new{c,i})
                    .Where(x=>(x.c.lockColors??Array.Empty<int>()).Contains(color)).Select(x=>x.i).ToArray());
            foreach(string key in level.carriers.SelectMany(c=>c.ropeLocks??Array.Empty<string>()).Distinct())
                Add("rope",key,0,level.carriers.Select((c,i)=>new{c,i})
                    .Where(x=>(x.c.ropeLocks??Array.Empty<string>()).Contains(key)).Select(x=>x.i).ToArray());
            foreach(string key in level.carriers.SelectMany(c=>c.carpetLocks??Array.Empty<string>()).Distinct())
                Add("carpet",key,0,level.carriers.Select((c,i)=>new{c,i})
                    .Where(x=>(x.c.carpetLocks??Array.Empty<string>()).Contains(key)).Select(x=>x.i)
                    .OrderBy(i=>level.carriers[i].carpetOrder).ToArray());
            return result;

            void Add(string kind,string key,int color,int[] members)
            {
                var root=new GameObject("Group "+kind+" "+(key??color.ToString()));root.transform.SetParent(board,false);
                var view=root.AddComponent<CarrierGroupFeatureView>();
                view.kind=kind;view.key=key;view.color=color;view.members=members;
                view.carriers=carriers;view.pivot=pivot;view.Initialize();view.Refresh(state,true);result.Add(view);
            }
        }

        void Initialize()
        {
            model=CarrierFeatureView.CreateFeature(kind,transform).transform;
            layout=CarrierFeatureView.Layout(kind);
            left=model.Find("Left");right=model.Find("Right");
            width=model.Find(kind=="lock"?"Center":"Scale");
            door=model.Find("Door");padlock=model.Find("Lock Pivot");roll=model.Find("Right/Roll");
            gateArt=door!=null?door.Find(VolcanoArt.ArtName):null;
            if(gateArt!=null){gateArtSize=gateArt.localScale;gateArtPosition=gateArt.localPosition;}
            if(kind=="lock")
            {
                CarrierFeatureView.TintLock(model.gameObject,color);
                frontMorph=door.Find("BlendBottom").GetComponent<SourceMorphView>();
                topMorph=door.Find("BlendTop").GetComponent<SourceMorphView>();
            }
            if(kind=="carpet")
            {
                leftRoll=left.Find(VolcanoArt.ArtName);
                SetCarpetSpan(members,true);
            }
            else
            {
                if(kind=="rope")
                {
                    // Native ConnectedCarrierGroup.Initialize sorts by pivot X+Z,
                    // then chooses its two ends using the first pivot's right axis.
                    // An unlock dependency may be in another row; it must not
                    // enlarge the visible enclosure to include that row.
                    members=members.OrderBy(i=>Pivot(i).x+Pivot(i).z).ToArray();
                    if(Vector3.Dot(Pivot(members[members.Length-1])-Pivot(members[0]),carriers[members[0]].right)<0)
                        Array.Reverse(members);
                }
                else
                {
                    var axis=carriers[members[0]].right;
                    members=members.OrderBy(i=>Vector3.Dot(Pivot(i),axis)).ToArray();
                }
                Vector3 a=Pivot(members[0]),b=Pivot(members[members.Length-1]);
                transform.SetPositionAndRotation((a+b)*.5f,carriers[members[0]].rotation);
                left.position=a;right.position=b;
                float distance=Vector3.Distance(a,b);
                width.localScale=new Vector3(distance+(kind=="lock"?layout.centerScaleOffset:layout.widthOffset),1,1);
                if(kind=="lock")
                {
                    door.localScale=new Vector3(distance+layout.doorScaleOffset,1,1);
                    transform.position+=transform.forward*layout.forwardOffset;
                    if(Vector3.Angle(carriers[members[0]].forward,Vector3.forward)>=91)
                        transform.Rotate(0,180,0,Space.Self);
                }
                else {Tile(model.Find("Scale/RopeMid"),new Vector2(1,members.Length+1));VolcanoArt.FitChain(model,new[]{carriers[members[0]],carriers[members[members.Length-1]]});}
            }
            bool first=true;Bounds bounds=default;
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
            }
            ClosedBounds=bounds;
        }

        Vector3 Pivot(int index)=>carriers[index].TransformPoint(pivot);
        static void Tile(Transform part,Vector2 repeats)
        {
            var renderer=part?.GetComponent<MeshRenderer>();if(renderer==null)return;
            var properties=new MaterialPropertyBlock();properties.SetVector("_MainTex_ST",new Vector4(repeats.x,repeats.y,0,0));
            renderer.SetPropertyBlock(properties);
        }

        public void Refresh(GameState state,bool immediate=false)
        {
            if(immediate){entranceTime=-1;rollRemovalTime=-1;}
            if(kind=="carpet")
            {
                int[] remaining=members.Where(i=>state.Carriers[i].carpetLocks.Contains(key)).ToArray();
                if(immediate)
                {
                    carpetRemoving=false;carpetRevealing.Clear();progress=1;
                    gameObject.SetActive(remaining.Length>0);
                    if(remaining.Length>0){roll.localScale=Vector3.one;SetCarpetSpan(remaining,true);}return;
                }
                if(remaining.Length==0)
                {
                    if(carpetRemoving)return;carpetRemoving=true;gameObject.SetActive(true);
                    foreach(int i in members)carpetRevealing.Add(i);
                    coveredCount=0;fromPosition=transform.position;toPosition=transform.position;
                    fromWidth=width.localScale.x;toWidth=0;fromLeft=left.localPosition;toLeft=fromLeft;
                    fromRight=right.localPosition;toRight=fromLeft;progress=0;return;
                }
                if(!immediate&&gameObject.activeSelf&&remaining.Length==coveredCount)return;
                foreach(int i in members)if(!remaining.Contains(i))carpetRevealing.Add(i);
                gameObject.SetActive(true);SetCarpetSpan(remaining,immediate);return;
            }
            bool locked=members.Any(i=>kind=="lock"?state.Carriers[i].lockColors.Contains(color):state.Carriers[i].ropeLocks.Contains(key));
            if(kind=="rope")
            {
                if(immediate){closed=locked;ropeTime=-1;transform.localScale=Vector3.one;gameObject.SetActive(locked);return;}
                if(closed&&!locked){ropeTime=0;gameObject.SetActive(true);}closed=locked;return;
            }
            if(immediate)
            {
                closed=locked;unlockTime=-1;ResetGate();gameObject.SetActive(locked);return;
            }
            if(closed&&!locked&&unlockTime<0)StartUnlock(transform.position+Vector3.up*4);
            closed=locked;
        }

        public bool ReceiveKey(int keyColor,Vector3 from,Quaternion fromRotation)
        {
            if(kind!="lock"||color!=keyColor)return false;StartUnlock(from,fromRotation);return true;
        }
        void ResetGate()
        {
            if(flyingKey!=null)Destroy(flyingKey);flyingKey=null;
            transform.localScale=Vector3.one;model.localScale=Vector3.one;width.localScale=new Vector3(width.localScale.x,1,1);
            door.localScale=new Vector3(door.localScale.x,1,1);door.localPosition=Vector3.zero;
            padlock.localScale=Vector3.one;padlock.localRotation=Quaternion.identity;
            frontMorph.ResetWeights();topMorph.ResetWeights();
            GateArt(0);
        }
        void StartUnlock(Vector3 from)=>StartUnlock(from,transform.rotation);
        void StartUnlock(Vector3 from,Quaternion fromRotation)
        {
            if(unlockTime>=0&&unlockTime<2.18f)return;
            entranceTime=-1;ResetGate();closed=false;unlockTime=0;keyOrigin=from;gameObject.SetActive(true);
            flyingKey=CarrierFeatureView.MakeKey(transform.parent,color);flyingKey.AddComponent<TransientEffect>();
            flyingKey.transform.SetPositionAndRotation(from,fromRotation);
            keyOriginRotation=flyingKey.transform.rotation;
        }

        internal void PrepareLevelEntrance()
        {
            model.gameObject.SetActive(false);
        }
        internal void CompleteLevelEntrance(bool immediate)
        {
            if(!immediate&&kind=="lock"&&gameObject.activeSelf)
            {
                entranceTime=0;ApplyEntrance();
            }
            model.gameObject.SetActive(true);
        }
        void ApplyEntrance()
        {
            // Native OnLevelAnimationEnd: frame, roof, seven shutter strips,
            // then the padlock. These are the inverse of the unlock morphs.
            float time=entranceTime;
            GateArt(1-Mathf.Clamp01((time-.25f)/.7f));
            transform.localScale=new Vector3(1,1,Mathf.Clamp01(time/.25f));
            topMorph.Weight(0,100*(1-Mathf.Clamp01((time-.25f)/.45f)));
            for(int i=0;i<frontMorph.Channels;i++)
                frontMorph.Weight(i,100*(1-Mathf.Clamp01((time-.70f-i*.04f)/.04f)));
            padlock.localScale=Vector3.one*Mathf.Clamp01((time-.98f)/.1f);
        }

        void SetCarpetSpan(int[] covered,bool immediate)
        {
            int first=covered[0],last=covered[covered.Length-1];
            coveredCount=covered.Length;
            Transform carrier=carriers[first];
            float angle=Vector3.Angle(carrier.forward,Vector3.forward);
            float offset=angle>=90?layout.backOffset:layout.forwardOffset;
            Quaternion rotation=carrier.rotation*Quaternion.Euler(0,angle>=90?180:0,0);
            Vector3 a=Pivot(first)+carrier.forward*offset,b=Pivot(last)+carriers[last].forward*offset;
            Vector3 position=(a+b)*.5f;
            transform.rotation=rotation;
            fromPosition=transform.position;toPosition=position;
            fromWidth=width.localScale.x;toWidth=Vector3.Distance(a,b)+layout.widthOffset;
            fromLeft=left.localPosition;fromRight=right.localPosition;
            // Express both endpoints in the target frame so the border and roll
            // remain full-sized while only the inner carpet stretches.
            toLeft=Quaternion.Inverse(rotation)*(a-position);
            toRight=Quaternion.Inverse(rotation)*(b-position);
            progress=immediate?1:0;
            ApplyCarpet();
        }

        void Update()
        {
            if(GameController.Instance==null||GameController.Instance.Paused||!model.gameObject.activeSelf)return;
            if(entranceTime>=0)
            {
                entranceTime=Mathf.Min(1.08f,entranceTime+Time.deltaTime);ApplyEntrance();
                if(entranceTime>=1.08f)entranceTime=-1;
            }
            if(kind=="lock")
            {
                if(unlockTime>=0&&unlockTime<2.18f){unlockTime=Mathf.Min(2.18f,unlockTime+Time.deltaTime);ApplyDoor();}
            }
            else if(kind=="rope"&&ropeTime>=0&&ropeTime<.25f)
            {
                ropeTime=Mathf.Min(.25f,ropeTime+Time.deltaTime);transform.localScale=new Vector3(1,1-ropeTime/.25f,1);
                if(ropeTime>=.25f)gameObject.SetActive(false);
            }
            else if(kind=="carpet"&&progress<1)
            {
                progress=Mathf.Min(1,progress+Time.deltaTime*2);ApplyCarpet();
                if(progress>=1){carpetRevealing.Clear();if(carpetRemoving)rollRemovalTime=0;}
            }
            else if(kind=="carpet"&&rollRemovalTime>=0)
            {
                rollRemovalTime=Mathf.Min(.25f,rollRemovalTime+Time.deltaTime);
                float t=rollRemovalTime/.25f,ease=t*t*(2.70158f*t-1.70158f);
                roll.localScale=Vector3.one*(1-ease);
                if(rollRemovalTime>=.25f){rollRemovalTime=-1;gameObject.SetActive(false);}
            }
        }
        void ApplyDoor()
        {
            GateArt(Mathf.Clamp01((unlockTime-1.2f)/.7f));
            float time=unlockTime;var point=model.Find("Lock Pivot/Lock/Unlock Point");
            Vector3 target=point!=null?point.position:padlock.position;
            if(flyingKey!=null)
            {
                float fly=Mathf.Clamp01(time/.7f),turn=MotionRunner.OutBack(Mathf.Clamp01((time-.8f)/.2f));
                // Recovered Unlock callback: linear XYZ interpolation plus
                // t * 5pi * sin(pi*t) on Y, and rotation toward Unlock Point.
                flyingKey.transform.position=Vector3.Lerp(keyOrigin,target,fly)+Vector3.up*(fly*15.7079639f*Mathf.Sin(fly*Mathf.PI));
                Quaternion socket=point!=null?point.rotation:padlock.rotation;
                flyingKey.transform.rotation=fly<1?Quaternion.Lerp(keyOriginRotation,socket,fly):Quaternion.LerpUnclamped(socket,socket*Quaternion.AngleAxis(90,Vector3.up),turn);
                float scale=Mathf.Lerp(1,1.83f,fly)*(1-Mathf.Clamp01((time-1)/.1f));
                flyingKey.transform.localScale=Vector3.one*scale;
                if(time>=1.1f){Destroy(flyingKey);flyingKey=null;}
            }
            padlock.localScale=Vector3.one*(1-Mathf.Clamp01((time-1.1f)/.1f));
            for(int i=0;i<frontMorph.Channels;i++)
                frontMorph.Weight(i,100*Mathf.Clamp01((time-1.2f-(frontMorph.Channels-1-i)*.04f)/.04f));
            topMorph.Weight(0,100*Mathf.Clamp01((time-1.48f)/.45f));
            // Fold toward the centre of the cart row at the same board height.
            model.localScale=new Vector3(1,1,1-Mathf.Clamp01((time-1.93f)/.25f));
            if(time>=2.18f)gameObject.SetActive(false);
        }
        void GateArt(float progress)
        {
            if(gateArt==null)return;
            gateArt.localScale=new Vector3(gateArtSize.x,gateArtSize.y*(1-progress),gateArtSize.z);
            gateArt.localPosition=gateArtPosition+Vector3.forward*(gateArtSize.y*progress*.5f);
            gateArt.gameObject.SetActive(progress<.999f);
        }
        void ApplyCarpet()
        {
            float t=progress;
            if(roll!=null)roll.gameObject.SetActive(leftRoll!=null||t<1||carpetRemoving||rollRemovalTime>=0);
            transform.position=Vector3.Lerp(fromPosition,toPosition,t);
            width.localScale=new Vector3(Mathf.Lerp(fromWidth,toWidth,t),1,1);
            if(leftRoll!=null)leftRoll.gameObject.SetActive(width.localScale.x>.1f);
            left.localPosition=Vector3.Lerp(fromLeft,toLeft,t);right.localPosition=Vector3.Lerp(fromRight,toRight,t);
            if(roll!=null)roll.localRotation=Quaternion.Euler(0,0,360*t);
        }
    }
}
