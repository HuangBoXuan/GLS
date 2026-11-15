using System;
using System.Collections.Generic;
using System.Linq;
using LoopSort0.Core;
using UnityEngine;

namespace LoopSort0
{
    // Native geometry and animation data use the Lua-selected toy materials.
    public sealed class CarrierFeatureView : MonoBehaviour
    {
        [Serializable] sealed class Catalog { public Template[] visuals; public Surface[] materials; }
        [Serializable] sealed class Template
        {
            public string id; public SourceNode[] nodes; public SourcePart[] parts;
            public SourceVector outlet,colliderCenter,colliderSize;
            public GroupLayout layout;
        }
        [Serializable] public sealed class GroupLayout
        {
            public float forwardOffset,backOffset,widthOffset,centerScaleOffset,doorScaleOffset;
        }
        [Serializable] sealed class UV {public float x,y;}
        [Serializable] sealed class Surface
        {
            public int id; public string name;
            public UV textureScale,textureOffset;
        }
        static Catalog catalog;
        static readonly Dictionary<int,Material> materials=new Dictionary<int,Material>();
        static Material keyOutline;
        internal static void ReleaseCaches()
        {
            foreach(var material in materials.Values)if(material!=null)Destroy(material);
            materials.Clear();catalog=null;
            if(keyOutline!=null)Destroy(keyOutline);keyOutline=null;
        }
        GameObject barrier,ice;
        TextMesh[] remaining=Array.Empty<TextMesh>();
        public Vector3 GeneratorOutlet=>transform.TransformPoint(catalog.visuals.First(v=>v.id=="generator").outlet.Value+Vector3.up*BoardPresentation.GeneratorVisualOffset);
        public Vector3 GeneratorColliderCenter=>catalog.visuals.First(v=>v.id=="generator").colliderCenter.Value+Vector3.up*(.96f+BoardPresentation.GeneratorVisualOffset);
        public Vector3 GeneratorColliderSize=>catalog.visuals.First(v=>v.id=="generator").colliderSize.Value+Vector3.up*1.92f;
        // The solid ore mesh extends .75 below its origin. Its bottom clears
        // the illustrated platform's highest bevel at y=2.74.
        public Vector3 GeneratorCargoCenter
        {
            get
            {
                // The guide's rectangular aperture is centered at v=.52.
                // Compensate the raised cube's screen displacement under the
                // board's oblique orthographic camera, for every carrier yaw.
                var center=new Vector3(0,3.50f,GeneratorColliderCenter.z+.05f);
                float pitch=BoardPresentation.CameraPitch*Mathf.Deg2Rad;
                var offset=Vector3.back*((3.50f-2.70f)/Mathf.Tan(pitch));
                return center+transform.InverseTransformDirection(offset)+Vector3.up*BoardPresentation.GeneratorVisualOffset;
            }
        }
        GameObject[] keys=Array.Empty<GameObject>(),screws=Array.Empty<GameObject>();
        int[] keyColors=Array.Empty<int>();
        bool wasFrozen,wasPacked,generatorMoving,generatorAwaitingRelease;
        int generatorCount=-1,displayedRemaining=-1;float generatorPhase;
        Transform barrierModel,visualRoot;Vector3 barrierPosition;Vector3 icePosition;
        bool entranceHidden;
        Renderer uncrustedCart;
        Transform[] covers=Array.Empty<Transform>();
        public bool BlocksTransfer=>generatorMoving;
        internal bool BlocksInteraction=>generatorMoving;
        internal bool CrustConcealsCargo=>wasFrozen;
        public bool GeneratorConcealsCargo=>generatorAwaitingRelease||(generatorMoving&&generatorPhase<.40f);

        public void Build(CarrierState state)
        {
            EnsureCatalog();
            int groups=Mathf.Max(4,state.capacityGroups);
            keys=new GameObject[groups];screws=new GameObject[groups];keyColors=new int[groups];
            if(state.generator)
            {
                var generator=Create("generator");
                generator.transform.localPosition+=Vector3.up*BoardPresentation.GeneratorVisualOffset;
                covers=generator.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Volcano hopper hinge ",StringComparison.Ordinal)).ToArray();
                generatorCount=state.cargo.Count;
                SetCoverAmount(1);
                BuildGeneratorCounts(generator.transform.Find("Pivot"));
            }
            if(state.barrier)
            {
                barrier=Create("barrier");barrierModel=barrier.transform.Find("Pivot/Model Rock")??barrier.transform;
                barrierPosition=barrierModel.localPosition;
            }
            if(state.iceRequires.Length>0)
            {
                ice=Create("ice");icePosition=ice.transform.localPosition;
                uncrustedCart=transform.Find(VolcanoArt.ArtName)?.GetComponent<Renderer>();
            }
            wasFrozen=state.Frozen;wasPacked=state.packed;Visible(ice,state.Frozen);
            ShowCrustedCart(state.Frozen);
        }
        internal void PrepareLevelEntrance()
        {
            entranceHidden=true;
            if(visualRoot!=null)visualRoot.gameObject.SetActive(false);
            ShowCrustedCart(wasFrozen);
        }
        internal void CompleteLevelEntrance()
        {
            entranceHidden=false;
            if(visualRoot!=null)visualRoot.gameObject.SetActive(true);
            ShowCrustedCart(wasFrozen);
        }

        void BuildGeneratorCounts(Transform pivot)
        {
            // Position the live counts on the two black plates in the new rim.
            // The original sloping TextZones were below this model's top surface.
            var root=new GameObject("Generator base counts").transform;root.SetParent(pivot,false);
            root.localPosition=new Vector3(0,2.79f,1.0857f);root.localRotation=Quaternion.Euler(90,0,0);
            var positions=new[]{new Vector3(0,-1.05f,0),new Vector3(0,1.05f,0)};
            var rotations=new[]{Quaternion.identity,Quaternion.Euler(0,0,180)};
            remaining=new TextMesh[positions.Length];
            for(int i=0;i<remaining.Length;i++)
            {
                var label=new GameObject("Remaining groups "+(i+1));label.transform.SetParent(root,false);
                label.transform.localRotation=rotations[i];
                label.transform.localPosition=positions[i]+rotations[i]*Vector3.back*.01f;
                var text=label.AddComponent<TextMesh>();remaining[i]=text;
                text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
                text.fontSize=64;text.characterSize=.29f*10/text.fontSize;
                text.color=ArtTheme.Cream;text.font=GameAssets.Font;text.richText=false;
                if(text.font!=null)label.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
            }
        }

        static void EnsureCatalog(){if(catalog==null)catalog=GameAssets.Data<Catalog>("feature-visuals");}
        internal static GroupLayout Layout(string id){EnsureCatalog();return catalog.visuals.First(x=>x.id==id).layout;}
        internal Transform VisualRoot
        {
            get
            {
                if(visualRoot==null)
                {
                    visualRoot=new GameObject("Carrier mechanisms").transform;
                    visualRoot.SetParent(transform,false);
                    visualRoot.gameObject.SetActive(!entranceHidden);
                }
                return visualRoot;
            }
        }
        GameObject Create(string id)=>CreateFeature(id,VisualRoot);
        internal static GameObject CreateFeature(string id,Transform parentRoot)
        {
            EnsureCatalog();
            var template=catalog.visuals.First(x=>x.id==id);
            var root=new GameObject("APK feature "+id);root.transform.SetParent(parentRoot,false);
            var nodes=new Dictionary<string,Transform>{{"Feature",root.transform}};
            foreach(var node in template.nodes.OrderBy(x=>x.path.Count(c=>c=='/')))
            {
                int slash=node.path.LastIndexOf('/');
                if(slash<0||!nodes.TryGetValue(node.path.Substring(0,slash),out Transform parent))continue;
                var child=new GameObject(node.path.Substring(slash+1)).transform;child.SetParent(parent,false);
                child.localPosition=node.transform.m_LocalPosition.Value;
                child.localRotation=node.transform.m_LocalRotation.Value;child.localScale=node.transform.m_LocalScale.Value;
                nodes[node.path]=child;
            }
            foreach(var part in template.parts)
            {
                if(!nodes.TryGetValue(part.path,out Transform target))continue;
                Mesh mesh=GameAssets.CompleteMesh(part.mesh);if(mesh==null)continue;
                var surfaces=new Material[Mathf.Max(1,mesh.subMeshCount)];
                for(int i=0;i<surfaces.Length;i++)surfaces[i]=Material(part.materialIds[Mathf.Min(i,part.materialIds.Length-1)]);
                if(SourceMorphView.Supports(part.mesh))SourceMorphView.Attach(target.gameObject,part.mesh,surfaces);
                else
                {
                    target.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                    target.gameObject.AddComponent<MeshRenderer>().sharedMaterials=surfaces;
                }
            }
            VolcanoArt.Apply(root,id);
            return root;
        }

        static Material Material(int id)
        {
            if(materials.TryGetValue(id,out Material value)&&value!=null)return value;
            var source=catalog.materials.First(s=>s.id==id);
            value=ArtTheme.FeatureMaterial(id);
            value.name="Toy workshop / "+source.name;
            if(source.textureScale!=null)value.mainTextureScale=new Vector2(source.textureScale.x,source.textureScale.y);
            if(source.textureOffset!=null)value.mainTextureOffset=new Vector2(source.textureOffset.x,source.textureOffset.y);
            materials[id]=value;return value;
        }

        internal static void TintModel(GameObject model,int color)
        {
            if(model==null)return;
            var tint=new MaterialPropertyBlock();tint.SetColor("_Color",GameAssets.Color(color));
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.gameObject.name!="Key black outline")renderer.SetPropertyBlock(tint);
        }
        void Tint(GameObject model,int color)=>TintModel(model,color);
        internal static GameObject MakeKey(Transform parent,int color)
        {
            var root=new GameObject("Collected key");root.transform.SetParent(parent,false);
            var key=CreateFeature("key",root.transform);PlaceBadge(key,Vector3.zero,1.18f);OutlineKey(key);TintModel(key,color);return root;
        }

        internal static void TintLock(GameObject model,int color)
        {
            var body=model.transform.Find("Lock Pivot/Lock")?.GetComponent<MeshRenderer>();if(body==null)return;
            var properties=new MaterialPropertyBlock();properties.SetColor("_Color",GameAssets.Color(color));
            body.SetPropertyBlock(properties,0); // Keep the dark keyhole and purple hanger.
            var art=model.transform.Find("Lock Pivot/Lock/"+VolcanoArt.ArtName)?.GetComponent<MeshRenderer>();
            if(art!=null)art.SetPropertyBlock(properties);
        }

        static void OutlineKey(GameObject model)
        {
            if(VolcanoArt.Active(model))return;
            if(keyOutline==null)
            {
                keyOutline=new Material(HotUpdate.HotContent.Load<Shader>("LoopSort/Shaders/CargoOutline"));
                keyOutline.SetFloat("_Width",.028f);keyOutline.color=Color.black;
            }
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var edge=GameAssets.Model("Key black outline",filter.sharedMesh,keyOutline,filter.transform,Vector3.zero,Vector3.one);
                var renderer=edge.GetComponent<MeshRenderer>();
                var surfaces=new Material[filter.sharedMesh.subMeshCount];
                for(int i=0;i<surfaces.Length;i++)surfaces[i]=keyOutline;
                renderer.sharedMaterials=surfaces;
            }
        }

        static void PlaceBadge(GameObject model,Vector3 position,float width)
        {
            // Cargo keys retain their native orientation and sit above the block.
            Bounds bounds=LocalBounds(model);
            float scale=width/Mathf.Max(.01f,Mathf.Max(bounds.size.x,bounds.size.z));
            model.transform.localScale=Vector3.one*scale;model.transform.localPosition=position-bounds.center*scale;
        }
        static Bounds LocalBounds(GameObject model)
        {
            var renderers=model.GetComponentsInChildren<MeshRenderer>();if(renderers.Length==0)return new Bounds(Vector3.zero,Vector3.one);
            Bounds bounds=renderers[0].localBounds;bool first=true;
            foreach(var renderer in renderers)
            {
                Bounds b=renderer.localBounds;
                for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                {
                    Vector3 corner=new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z);
                    Vector3 p=model.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        static void Visible(GameObject model,bool value)
        {
            if(model!=null&&model.activeSelf!=value)model.SetActive(value);
        }

        public void Refresh(GameState game,int carrier)
        {
            var state=game.Carriers[carrier];
            if(wasFrozen&&!state.Frozen)BreakIce();
            if(!wasPacked&&state.packed)CompleteDecorations();
            wasFrozen=state.Frozen;wasPacked=state.packed;
            if(state.Frozen){Visible(ice,true);ShowCrustedCart(true);}
            if(!state.packed)Visible(barrier,true);
            // Release reserves/removes the old group before its fragments finish
            // leaving. Keep the next stock hidden until the cover's reveal phase.
            generatorAwaitingRelease=state.generator&&state.releasing>0&&generatorCount>state.cargo.Count;
            if(state.generator&&state.releasing==0&&generatorCount!=state.cargo.Count)
            {
                if(generatorCount>state.cargo.Count&&state.cargo.Count>0)MoveCovers(carrier);
                else if(state.cargo.Count==0)SetCoverAmount(1);
                generatorCount=state.cargo.Count;
            }
            if(remaining.Length>0)
            {
                int count=(state.cargo.Count+game.GroupSize-1)/game.GroupSize;
                if(displayedRemaining!=count)
                {
                    displayedRemaining=count;string text=count.ToString();
                    foreach(var label in remaining)label.text=text;
                }
                foreach(var label in remaining)Visible(label.gameObject,state.Accessible);
            }
            for(int group=0;group<keys.Length;group++)
            {
                int slot=group*game.GroupSize;
                var piece=slot<state.cargo.Count?game.Pieces[state.cargo[slot]]:null;
                bool active=state.Accessible&&!state.packed&&piece!=null&&(!state.generator||(!generatorMoving&&!GeneratorConcealsCargo&&group==(state.cargo.Count-1)/game.GroupSize));
                bool key=active&&!piece.hidden&&piece.keyColor>0;
                Vector3 center=state.generator?GeneratorCargoCenter:new Vector3(0,1.69f,1.29f+Mathf.Max(0,state.capacityGroups-4)-group);
                if(key&&keys[group]==null)
                {
                    keys[group]=Create("key");PlaceBadge(keys[group],center+Vector3.up*.96f,1.18f);OutlineKey(keys[group]);
                }
                Visible(keys[group],key);
                if(key&&keyColors[group]!=piece.keyColor)
                {Tint(keys[group],piece.keyColor);keyColors[group]=piece.keyColor;}
                bool screw=active&&piece.sticky;
                if(screw&&screws[group]==null)
                {
                    screws[group]=Create("sticky");screws[group].transform.localPosition=center;
                }
                Visible(screws[group],screw);
            }
        }

        void BreakIce()
        {
            if(ice==null)return;
            // Rules have already unlocked the cart. Reveal it in this refresh;
            // the decorative break particles must not delay visibility or input.
            ice.SetActive(false);ShowCrustedCart(false);
            TransientEffect.IceBreak(transform.parent,transform.TransformPoint(new Vector3(0,.84f,.05f)),transform.rotation);
        }
        void ShowCrustedCart(bool frozen)
        {
            // The guide includes the darkened rim, wheels and rear sill. Keep
            // the regular copper cart hidden until that entire shell breaks.
            if(uncrustedCart!=null)uncrustedCart.forceRenderingOff=frozen&&!entranceHidden;
        }
        void CompleteDecorations()
        {
            if(barrier!=null)
                MotionRunner.For(gameObject).Play("barrier",.5f,t=>
                    barrierModel.localPosition=new Vector3(barrierPosition.x,Mathf.Lerp(barrierPosition.y,-.2f,t),barrierPosition.z),()=>barrier.SetActive(false));
        }
        void MoveCovers(int carrier)
        {
            generatorMoving=true;generatorPhase=0;
            GameController.Instance.SetGeneratorCargoLift(carrier,-1.65f);
            MotionRunner.For(gameObject).Play("generator",.9f,t=>
            {
                bool show=generatorPhase<.40f&&t>=.40f;generatorPhase=t;
                // The current cube has already left. Lower the empty split
                // platform, lift the next cube through it, then close beneath it.
                float opening=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.28f));
                float closing=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.75f)/.25f));
                SetCoverAmount(1-opening+closing);
                float lift=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.40f)/.35f));
                GameController.Instance.SetGeneratorCargoLift(carrier,Mathf.Lerp(-1.65f,0,lift));
                if(show)GameController.Instance.MarkCargoDirty(carrier);
            },()=>
            {
                generatorMoving=false;SetCoverAmount(1);
                GameController.Instance.SetGeneratorCargoLift(carrier,0);
                GameController.Instance.MarkCargoDirty(carrier);
            });
        }
        void SetCoverAmount(float amount)
        {
            for(int i=0;i<covers.Length;i++)
                covers[i].localRotation=Quaternion.Euler(0,0,(covers[i].localPosition.x<0?-90:90)*(1-Mathf.Clamp01(amount)));
        }
        public void ShakeScrews()
        {
            foreach(var screw in screws)
            {
                if(screw==null||!screw.activeSelf)continue;
                var model=screw.transform;Quaternion rotation=model.localRotation;
                MotionRunner.For(screw).Play("jiggle",.325f,t=>model.localRotation=rotation*Quaternion.Euler(0,0,Mathf.Sin(t*10*Mathf.PI)*12*(1-t)),()=>model.localRotation=rotation);
            }
        }
        public void AnimateRelease(int firstGroup)
        {
            for(int group=firstGroup;group<screws.Length;group++)
            {
                if(screws[group]==null||!screws[group].activeSelf)continue;
                var screw=screws[group];screws[group]=null;MotionRunner.For(screw).StopAll();
                screw.transform.SetParent(transform.parent,true);screw.AddComponent<TransientEffect>();
                SourceTransformClip.Play(screw,"sticky-complete",()=>Destroy(screw));
            }
        }
        public void ResetAnimation()
        {
            generatorMoving=false;generatorAwaitingRelease=false;generatorPhase=0;generatorCount=-1;
            var state=GameController.Instance.State.Carriers[GetComponent<CarrierClickTarget>().Index];
            wasFrozen=state.Frozen;wasPacked=state.packed;
            if(ice!=null){ice.transform.localPosition=icePosition;ice.transform.localScale=Vector3.one;Visible(ice,state.Frozen);ShowCrustedCart(state.Frozen);}
            if(barrier!=null){barrierModel.localPosition=barrierPosition;Visible(barrier,!state.packed);}
            if(state.generator)
            {
                generatorCount=state.cargo.Count;SetCoverAmount(1);
                GameController.Instance.SetGeneratorCargoLift(GetComponent<CarrierClickTarget>().Index,0);
            }
        }
    }
}
