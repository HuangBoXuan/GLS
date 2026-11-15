using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace LoopSort0
{
    // Solid models follow the original animation nodes. Hidden source meshes
    // retain their animation data and stay invisible across renderer toggles.
    public static class VolcanoArt
    {
        public const string ArtName="Volcano art";
        const string LidHingeName="Lid hinge";
        const string CartRoofPath="Pivot/TopPart1";
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        public static bool Active(GameObject root)=>root!=null&&root.GetComponentsInChildren<Transform>(true).Any(t=>t.name==ArtName);
        public static Material CreateMaterial(string id)
        {
            if(id.StartsWith("surface-",StringComparison.Ordinal))return VolcanoModels.Surface(id.Substring(8));
            if(id=="base-metal")return VolcanoModels.Surface("iron");
            if(id=="blanket")return VolcanoModels.Surface("fabric");
            // Legacy factory keys remain in native module indexes, but no
            // visible model uses a sprite/decal material anymore.
            string shaderPath="LoopSort/Shaders/VolcanoSourceHidden";
            var shader=Application.isPlaying?HotUpdate.HotContent.Load<Shader>(shaderPath):Resources.Load<Shader>(shaderPath);
            if(shader==null)return null;
            var material=new Material(shader){name="Volcano / "+id,enableInstancing=true};
            return material;
        }
        static Material Material(string id)
        {
            if(!materials.TryGetValue(id,out var value)||value==null){value=CreateMaterial(id);if(value!=null)materials[id]=value;}return value;
        }
        public static void Release()
        {
            foreach(var material in materials.Values)if(material!=null){if(Application.isPlaying)Object.Destroy(material);else Object.DestroyImmediate(material);}materials.Clear();VolcanoModels.Release();
        }
        public static void ConfigureHidden(Material material)
        {
            material.SetColor("_Color",ArtTheme.Hex(0x2653C8));
            material.SetColor("_QuestionColor",ArtTheme.Hex(0xFFCAA3));
        }
        static void Hide(GameObject root,Func<string,Material> load)
        {
            var invisible=load("source-hidden");
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer is ParticleSystemRenderer||renderer is SpriteRenderer)continue;
                renderer.forceRenderingOff=true;
                renderer.sharedMaterials=Enumerable.Repeat(invisible,renderer.sharedMaterials.Length).ToArray();
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
        }
        static void FitCartRoof(Transform cart,float originalLength,Material material)
        {
            var root=cart.parent;
            var roof=root.Find(CartRoofPath)?.GetComponent<SkinnedMeshRenderer>();
            if(roof==null||material==null)return;

            Vector3 center=cart.localPosition;
            float length=cart.localScale.y;
            float halfWidth=cart.localScale.x*.36f;
            // Seat the roof just below the rim, with its back edge under the lamp mount.
            float rim=center.y+1.503f;
            float front=center.z-length*.48f+.105f;
            float back=center.z+(.475f-.125f/originalLength)*length;

            // Fit the closed roof to the rim. The legacy flap stays hidden at the outlet.
            var roofScale=new Vector3(halfWidth/.92615f,1,(back-front)/4.05795f);
            roof.transform.localScale=roofScale;
            roof.transform.localPosition=roof.transform.parent.InverseTransformPoint(root.TransformPoint(new Vector3(0,rim,front)))
                -Vector3.Scale(roofScale,new Vector3(0,2.49486f,-2.21503f));
            roof.sharedMaterial=material;
            roof.forceRenderingOff=false;
        }
        public static bool Apply(GameObject root,string kind,Func<string,Material> factory=null,float carrierLengthDelta=0)
        {
            if(root==null||Active(root))return false;
            var load=factory??Material;
            if(kind=="carpet-divider")
            {
                Hide(root,load);Add(root.transform,root.transform,"blanket-clasp",new Vector3(0,3.22f,0),new Vector2(.24f,4.78f),load);return true;
            }
            string id=kind=="carrier"||kind=="long-carrier"?"cart":kind=="portal"?"cave":kind=="ice"?"crust":kind=="rope"?"chain":kind=="carpet"?"blanket":kind=="sticky"?"connector":kind=="generator"?"hopper":kind=="curtain"?"lid":kind;
            if(kind=="carrier"||kind=="long-carrier")
            {
                Hide(root,load);bool longer=kind=="long-carrier";
                float length=longer?13.1f:4.88f;
                var cart=Add(root.transform,root.transform,id,new Vector3(0,.95f,longer?4.12f:.01f),new Vector2(2.4f,length),load);
                cart.localPosition+=Vector3.forward*(carrierLengthDelta*.5f);
                cart.localScale+=Vector3.up*carrierLengthDelta;
                FitCartRoof(cart,length,load("surface-body-dark"));
                return true;
            }
            if(kind=="portal")
            {
                Hide(root,load);
                // Cave art is authored in the vertical XY plane, with its feet
                // at y=0. Its opening faces along the conveyor's local Z axis.
                var arch=Add(root.transform,root.transform,id,new Vector3(0,-.7071f,-.05f),new Vector2(2.412f,1.969f),load);
                arch.localRotation=Quaternion.identity;return true;
            }
            if(kind=="curtain")
            {
                Hide(root,load);CreateLid(root.transform,load);return true;
            }
            if(kind=="lock")
            {
                var door=root.transform.Find("Door");var padlock=root.transform.Find("Lock Pivot");var frame=root.transform.Find("Center");
                if(door==null||padlock==null||frame==null)return false;
                Bounds gate=BoundsIn(root.transform,door),keyhole=BoundsIn(root.transform,padlock);
                var posts=new[]{root.transform.Find("Left"),root.transform.Find("Right")};
                const float frameDepth=4.68f;
                float height=gate.max.y+.07f;
                Hide(root,load);
                Add(root.transform,door,"gate",new Vector3(0,height,0),new Vector2(1,4.34f),load);
                Add(root.transform,frame,"gate-beam",new Vector3(0,height+.06f,0),new Vector2(1,frameDepth),load);
                Add(root.transform,padlock.Find("Lock")??padlock,"lock",new Vector3(keyhole.center.x,keyhole.max.y+.06f,keyhole.center.z),new Vector2(1.25f,1.35f),load);
                for(int i=0;i<posts.Length;i++)if(posts[i]!=null)
                {
                    float side=i==0?-1f:1f;
                    Add(root.transform,posts[i],i==0?"gate-left":"gate-right",new Vector3(side*.88f,height+.08f,0),new Vector2(.46f,frameDepth),load);
                }
                return true;
            }
            if(kind=="generator")
            {
                var left=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Cover1");
                var right=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Cover2");
                if(left==null||right==null)return false;
                // Keep the animation pivots and live stock labels. Replace the
                // old base as well: it otherwise intersects the illustrated rim.
                Hide(root,load);
                // All moving and fixed surfaces share the guide's coordinate
                // system, so the silver frame and door leaves meet closed.
                const float span=2.5f,z=-1.2143f,y=2.48f;
                var frame=Add(root.transform,root.transform,"hopper-frame",new Vector3(0,y,z),new Vector2(span,span),load);
                frame.name="Volcano hopper frame";
                foreach(var pair in new[]{Tuple.Create(left,"hopper-left"),Tuple.Create(right,"hopper-right")})
                {
                    float cx=pair.Item1==left?-.2325f:.2325f;
                    var hinge=new GameObject("Volcano hopper hinge "+(cx<0?"left":"right")).transform;
                    hinge.SetParent(root.transform,false);
                    // Hinge at the top edge: the bevel and underside descend
                    // outward into the housing instead of sweeping into the ore.
                    hinge.localPosition=new Vector3((cx<0?-.465f:.465f)*span,y+.26f,z+.02f*span);
                    Add(root.transform,hinge,pair.Item2,new Vector3(cx*span,y,z+.02f*span),new Vector2(.465f*span,.66f*span),load);
                }
                return true;
            }
            if(kind=="carpet")
            {
                var scale=root.transform.Find("Scale");var left=root.transform.Find("Left");
                var roll=root.transform.Find("Right/Roll");
                if(scale==null||left==null||roll==null)return false;
                Hide(root,load);
                Add(root.transform,scale,"blanket",new Vector3(0,3.22f,0),new Vector2(1,4.78f),load);
                var rollerSize=new Vector2(.44f,4.63f);
                Add(root.transform,left,"blanket-roller",new Vector3(-.87f,3.32f,0),rollerSize,load);
                roll.localPosition=new Vector3(.87f,3.32f,0);
                Add(root.transform,roll,"blanket-roller",roll.localPosition,rollerSize,load);
                return true;
            }
            Transform target=kind=="barrier"?root.transform.Find("Pivot/Model Rock"):kind=="rope"?root.transform.Find("Scale"):root.transform;
            if(target==null)return false;
            Bounds bounds=BoundsIn(root.transform,target);
            var center=new Vector3(bounds.center.x,bounds.max.y+.025f,bounds.center.z);
            var size=new Vector2(Mathf.Max(.3f,bounds.size.x),Mathf.Max(.3f,bounds.size.z));
            if(kind=="barrier")size.y=size.x/3.75f;
            if(kind=="tag"){center=new Vector3(.97f,2.55f,-1.3f);size=new Vector2(.85f,.95f);}
            if(kind=="ice"){center=new Vector3(0,2.5f,.015f);size=new Vector2(2.5f,4.75f);}
            if(kind=="rope")size.y=Mathf.Max(4.8f,size.y);
            Hide(root,load);Add(root.transform,target,id,center,size,load);return true;
        }
        public static Transform AddLid(Transform carrier,int color)
        {
            var hinge=CreateLid(carrier,Material);
            var tint=new MaterialPropertyBlock();
            tint.SetColor("_Color",GameAssets.Color(color));
            hinge.GetChild(0).GetComponent<Renderer>().SetPropertyBlock(tint);
            return hinge;
        }
        static Transform CreateLid(Transform carrier,Func<string,Material> load)
        {
            var size=new Vector2(2.34f,4.74f);
            var center=new Vector3(0,2.68f,.01f);
            var hinge=new GameObject(LidHingeName).transform;
            hinge.SetParent(carrier,false);
            hinge.localPosition=center+Vector3.left*(size.x*.43f);
            Add(carrier,hinge,"lid",center,size,load);
            return hinge;
        }
        public static void TintCarrier(Transform carrier,Color color)
        {
            var art=carrier.Find(ArtName);if(art==null)return;
            var block=new MaterialPropertyBlock();block.SetColor("_Color",color);art.GetComponent<Renderer>().SetPropertyBlock(block);
            carrier.Find(CartRoofPath)?.GetComponent<Renderer>()?.SetPropertyBlock(block);
        }
        public static void FitChain(Transform model,Transform[] carriers=null)
        {
            var span=model.Find("Scale");var art=span!=null?span.Find(ArtName):null;
            if(art==null)return;
            var size=new Vector2(Mathf.Abs(art.localScale.x*span.localScale.x),Mathf.Abs(art.localScale.y));
            if(carriers!=null&&carriers.Length>0)
            {
                bool first=true;Bounds b=default;
                foreach(var carrier in carriers)
                {
                    var box=RopeClearance.Bounds(carrier);
                    for(int i=0;i<8;i++)
                    {
                        var p=model.InverseTransformPoint(box.center+Vector3.Scale(box.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                        if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);
                    }
                }
                size=new Vector2(b.size.x+.38f,b.size.z+.38f);
                art.SetParent(model,false);art.localPosition=new Vector3(b.center.x,b.center.y+.22f,b.center.z);
                art.localRotation=Quaternion.Euler(90,0,0);art.localScale=new Vector3(size.x,size.y,1);art.SetParent(span,true);
            }
            art.GetComponent<MeshFilter>().sharedMesh=VolcanoModels.Get("chain",size).mesh;
        }
        static Transform Add(Transform root,Transform attach,string id,Vector3 center,Vector2 size,Func<string,Material> load)
        {
            var model=VolcanoModels.Get(id,size);
            var go=new GameObject(ArtName,typeof(MeshFilter),typeof(MeshRenderer));go.layer=root.gameObject.layer;
            go.GetComponent<MeshFilter>().sharedMesh=model.mesh;
            go.transform.SetParent(root,false);go.transform.localPosition=center;go.transform.localRotation=Quaternion.Euler(90,0,0);go.transform.localScale=new Vector3(size.x,size.y,1);
            go.transform.SetParent(attach,true);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterials=model.surfaces.Select(s=>load("surface-"+s)).ToArray();renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;return go.transform;
        }
        static Bounds BoundsIn(Transform root,Transform node)
        {
            bool first=true;Bounds result=default;
            foreach(var r in node.GetComponentsInChildren<Renderer>(true))
            {
                if(r is ParticleSystemRenderer)continue;Bounds b=r.localBounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 p=root.InverseTransformPoint(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                    if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);
                }
            }
            return result;
        }
    }
}
