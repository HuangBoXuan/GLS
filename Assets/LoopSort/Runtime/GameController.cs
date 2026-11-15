using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LoopSort0.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LoopSort0
{
    public sealed partial class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }
        public GameState State { get; private set; }
        public LevelData Level { get; private set; }
        public LevelCatalog Catalog { get; private set; }
        public PlayerProgress Progress { get; private set; }
        public bool Paused { get; private set; }
        public bool SelectingShuffle { get; private set; }
        public GameHud Hud { get; private set; }
        public bool IsTestRun;
        string bootError;
        public Camera Camera { get; private set; }
        public IReadOnlyDictionary<int,PieceView> PieceViews => pieces;
        public IReadOnlyList<Transform> TruckViews => carriers;
        public float BeltPieceSize=>HotUpdate.RuntimeTuning.Get("piece.belt_size",.51f);
        readonly Dictionary<int,PieceView> pieces = new Dictionary<int,PieceView>();
        readonly List<PieceView> beltViews = new List<PieceView>();
        BeltSteering.BakedRoute beltRoute;
        readonly List<Transform> carriers = new List<Transform>(), cargoRoots = new List<Transform>();
        sealed class CargoDisplay
        {
            public readonly GameObject[] groups;
            public readonly MeshRenderer[] renderers;
            public readonly bool[] queuedGroups;
            public readonly int[] launched,queuedColors,queuedBlocks;
            public readonly bool[] queuedHidden;
            public CargoDisplay(int capacity)
            {
                groups=new GameObject[capacity];renderers=new MeshRenderer[capacity];queuedGroups=new bool[capacity];
                launched=new int[capacity];queuedColors=new int[capacity];queuedBlocks=new int[capacity];queuedHidden=new bool[capacity];
            }
            public readonly GameObject[] remainders=new GameObject[2];
            public readonly MeshRenderer[] remainderRenderers=new MeshRenderer[2];
            public GameObject hull;public CargoHighlight highlight;
            public Vector3 frontCenter;
            public float packingDepthDirection;
            public bool cargoDirty=true,outlineDirty=true,curtainOpen,packed,generator;
            public int firstDirtyGroup=int.MaxValue,lastDirtyGroup=-1;
        }
        readonly List<CargoDisplay> cargoDisplays=new List<CargoDisplay>();
        int displayedFreeGroups=-1;
        Mesh cargoMesh,fragmentMesh,cargoCellMesh;Vector3 cargoMeshSize,cargoMeshCenter,cargoMeshMin;
        Vector3 CargoCellSize=>new Vector3(cargoMeshSize.x/State.Fragments.Columns,cargoMeshSize.y/State.Fragments.Rows,cargoMeshSize.z/State.Fragments.Depth);
        readonly List<GameObject> completionMarks=new List<GameObject>();
        readonly List<CurtainView> curtains=new List<CurtainView>();
        readonly List<CarrierFeatureView> featureViews=new List<CarrierFeatureView>();
        readonly List<CarrierGroupFeatureView> groupFeatures=new List<CarrierGroupFeatureView>();
        readonly Dictionary<int,Material> truckColors=new Dictionary<int,Material>();
        int displayedFeatures=-1;
        int shownKeyCollections;
        public string MechanicHint { get; private set; }
        readonly List<Vector3> carrierOrigins=new List<Vector3>();
        readonly List<Vector3> gates = new List<Vector3>();
        readonly List<CarrierData> placements = new List<CarrierData>();
        TrackGeometry track; Transform board;ToyBackdrop backdrop;
        Material purple, dark, black, metal, hidden, outline,truckBody,truckDeck,truckBase;
        PhysicMaterial transportMaterial;
        AudioSource effects, music;
        SourceVisual sourceVisual,longVisual;
        CargoBatchRenderer cargoBatch;
        Vector3 sourcePivot; Bounds boardBounds,visibleBoardBounds;
        int run; bool resultShown; Vector2 screen;
        float hiddenFlowTime;
        bool readyForGuides;
        bool bootComplete;

        public int UndoDepth => history.Count;

        object LuaAction(string action,params object[] arguments)
        {
            if(Hud!=null&&Hud.BoosterPresentationBusy&&(action=="undo"||action=="belt"||action=="truck"||action=="shuffle"||action=="shuffle_select"))return false;
            var values=new object[arguments.Length+1];values[0]=this;Array.Copy(arguments,0,values,1,arguments.Length);
            return HotUpdate.LuaGameRuntime.Call("game.flow",action,values);
        }
        public void ClearMenuSelection(){LuaAction("drop_tutorials");SetShuffleSelection(false);}
        public void PrepareHome(){FlushAdGameplay(true);ClearMenuSelection();SetPaused(true);}
        public void SetShuffleSelection(bool value){SelectingShuffle=value;Hud?.ShowShuffleSelection(value);}
        void ShowHome()=>LuaAction("home");
        void CloseMenu()=>LuaAction("cancel");
        public void ShowNewMechanics(){if(!IsEnteringLevel)LuaAction("show_new_mechanics");}
        public void LoadLevel(int id)
        {
            var level=Catalog.levels.FirstOrDefault(x=>x.id==id);
            if(level==null)return;
            IsDailyRun=false;DailyDate="";LoadLevelData(level);
        }
        void LoadLevelData(LevelData level)
        {
            var work=BuildLevelData(level,false);
            try {while(work.MoveNext()){} }finally {(work as IDisposable)?.Dispose();}
        }
        IEnumerator BuildLevelData(LevelData level,bool incremental)
        {
            FlushAdGameplay(true);
            if(Hud!=null)Hud.CancelBoosterPresentation(false);
            CancelUpdateCheck();
            // The initial level is loaded by Start itself. Cancelling it here
            // prevents startup from completing and leaves the loading overlay up.
            if(bootComplete)StopAllCoroutines();
            run++;resultShown=false;SetShuffleSelection(false);Paused=false;history.Clear();LuaAction("drop_tutorials");
            if(board!=null){board.gameObject.SetActive(false);Destroy(board.gameObject);}track?.Dispose();
            ClearCargoReveals();cargoFlights.Clear();
            pieces.Clear();beltViews.Clear();carriers.Clear();cargoRoots.Clear();cargoDisplays.Clear();gates.Clear();placements.Clear();completionMarks.Clear();curtains.Clear();featureViews.Clear();groupFeatures.Clear();carrierOrigins.Clear();displayedFeatures=-1;
            State?.Dispose();Level=level;track=new TrackGeometry(level);level.splineLength=track.Length;
            State=new GameState(level,allowBasicRulesFallback:true);board=new GameObject("Level "+level.id+" [APK 4.6.0]").transform;
            cargoBatch=board.gameObject.AddComponent<CargoBatchRenderer>();cargoBatch.Initialize(cargoCellMesh,fragmentMesh,GameAssets.CargoMaterial,Camera);
            shownKeyCollections=0;
            if(beltViews.Capacity<State.TotalPieces)beltViews.Capacity=State.TotalPieces;
            if(incremental){startupLoading?.SetProgress(.95f);yield return null;}
            track.Build(board,dark);
            beltRoute=new BeltSteering.BakedRoute(track);
            var budget=new StartupFrameBudget();
            for(int i=0;i<level.carriers.Length;i++)
            {
                CreateTruck(level.carriers[i]);
                if(incremental&&budget.ShouldYield()){startupLoading?.SetProgress(.95f+.02f*(i+1)/level.carriers.Length);yield return null;budget.Resume();}
            }
            groupFeatures.AddRange(CarrierGroupFeatureView.Build(board,level,carriers,sourcePivot,State));
            for(int i=0;i<carriers.Count;i++)
            {
                carrierOrigins[i]=carriers[i].position;
                gates[i]=track.Nearest(CarrierOutlet(i),out _,out _);
            }
            budget=new StartupFrameBudget(64);
            for(int i=0;i<State.Pieces.Count;i++)
            {
                CreatePiece(State.Pieces[i]);
                if(incremental&&budget.ShouldYield()){startupLoading?.SetProgress(.97f+.02f*(i+1)/State.Pieces.Count);yield return null;budget.Resume();}
            }
            MechanicHint=Hint(level);
            RefreshCargo();UpdateBoardBounds();FrameBoard();OnPeripheralLevelLoaded();BeginAdAttempt();Hud.ShowGameplay();Hud.Refresh(this);BeginLevelEntrance();
            Hud.Toast(level.IsSupported?MechanicHint:"本关仍有未接入机制，部分规则为简化实现");
            if(!IsDailyRun){Progress.currentLevel=level.id;Progress.highestUnlocked=Mathf.Max(Progress.highestUnlocked,level.id);}SaveProgress();
#if UNITY_ASSERTIONS
            State.AssertConservation();
#endif
            if(readyForGuides&&!IsEnteringLevel)ShowNewMechanics();
        }
        static string Hint(LevelData level)=>(string)HotUpdate.LuaGameRuntime.Call("game.flow","hint",level);

        void CreateTruck(CarrierData data)
        {
            int index=carriers.Count;
            bool elongated=data.capacityGroups>4;
            SourceVisual visual=elongated?longVisual:sourceVisual;
            int bodyColor=elongated?data.targetColor:data.tagColor;
            float lengthDelta=elongated?State.Carriers[index].capacityGroups-12:0;
            var root=new GameObject("Truck "+data.id).transform;root.SetParent(board,false);root.rotation=Quaternion.Euler(0,data.yaw,0);
            root.position=BoardPresentation.CarrierPosition(new Vector3(data.x,data.y,data.z),root.rotation,sourcePivot);
            carrierOrigins.Add(root.position);
            var nodes=new Dictionary<string,Transform>{{"Carrier",root}};
            foreach(var node in visual.nodes.OrderBy(x=>x.path.Count(c=>c=='/')))
            {
                int slash=node.path.LastIndexOf('/');string parent=node.path.Substring(0,slash);
                if(!nodes.TryGetValue(parent,out Transform parentTransform))continue;
                var child=new GameObject(node.path.Substring(slash+1)).transform;child.SetParent(parentTransform,false);
                child.localPosition=node.transform.m_LocalPosition.Value;child.localRotation=node.transform.m_LocalRotation.Value;child.localScale=node.transform.m_LocalScale.Value;nodes[node.path]=child;
            }
            foreach(var part in visual.parts)
            {
                if(data.generator)continue;
                if(!nodes.TryGetValue(part.path,out Transform target))continue;
                Mesh mesh=GameAssets.CompleteMesh(part.mesh);if(mesh==null)continue;
                var materials=new Material[Mathf.Max(mesh.subMeshCount,part.materialIds.Length)];
                for(int i=0;i<materials.Length;i++)
                {
                    int material=i<part.materialIds.Length?part.materialIds[i]:0;
                    materials[i]=part.wheel?(i==0?black:metal):bodyColor>0
                        ?TruckColor(bodyColor,material==31||material==32?0:2)
                        :material==31||material==32?truckBase:truckBody;
                }
                if(SourceMorphView.Supports(part.mesh))SourceMorphView.Attach(target.gameObject,part.mesh,materials);
                else
                {
                    target.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                    target.gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials;
                }
            }
            if(elongated)ResizeLongTruck(root,nodes,visual,lengthDelta);
            // The visible floor uses the same material as the truck's surrounding frame.
            if(!data.generator&&!elongated)GameAssets.Model("Cargo deck",GameAssets.Mesh(382),data.tagColor>0?TruckColor(data.tagColor,0):truckBase,root,new Vector3(0,.91f,-.2f),new Vector3(3.6f,.15f,8f));
            if(!data.generator)
            {
                VolcanoArt.Apply(root.gameObject,elongated?"long-carrier":"carrier",carrierLengthDelta:lengthDelta);
                if(bodyColor>0)VolcanoArt.TintCarrier(root,GameAssets.Color(bodyColor));
            }
            var collider=root.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.2f,0);collider.size=new Vector3(2.15f,2.8f,4.8f);
            collider.isTrigger=true;
            if(data.curtainColor>0){collider.center=new Vector3(0,1.5f,.1f);collider.size=new Vector3(2.85f,3.1f,5.65f);}
            if(elongated){collider.center=visual.colliderCenter.Value+Vector3.forward*(lengthDelta*.5f);collider.size=visual.colliderSize.Value+Vector3.forward*lengthDelta;}
            root.gameObject.AddComponent<CarrierClickTarget>().Index=index;
            var cargo=new GameObject("Cargo groups").transform;cargo.SetParent(root,false);
            var badge=new GameObject("Completed check",typeof(SpriteRenderer));badge.transform.SetParent(root,false);badge.transform.localPosition=new Vector3(0,3.1f,-.15f);
            var badgeRenderer=badge.GetComponent<SpriteRenderer>();badgeRenderer.sprite=GameAssets.Sprite("check");badgeRenderer.sortingOrder=0;
            if(elongated)badge.transform.localPosition=new Vector3(0,3.1f,collider.center.z);
            if(badgeRenderer.sprite!=null)badge.transform.localScale=Vector3.one*(1.1f/badgeRenderer.sprite.bounds.size.x);
            badge.AddComponent<CompletionMotion>().Initialize();badge.SetActive(false);completionMarks.Add(badge);
            var features=root.gameObject.AddComponent<CarrierFeatureView>();features.Build(State.Carriers[index]);featureViews.Add(features);
            var curtain=root.gameObject.AddComponent<CurtainView>();curtain.Build(data.curtainColor,data.curtainColor>0?features.VisualRoot:null);curtains.Add(curtain);
            if(data.generator){collider.center=features.GeneratorColliderCenter;collider.size=features.GeneratorColliderSize;}
            carriers.Add(root);cargoRoots.Add(cargo);placements.Add(data);
            var display=new CargoDisplay(Mathf.Max(4,State.Carriers[index].capacityGroups)) {generator=data.generator,frontCenter=data.generator?features.GeneratorCargoCenter:new Vector3(0,1.69f,1.29f+(elongated?State.Carriers[index].capacityGroups-4:0))};
            for(int group=0;group<display.groups.Length;group++)
            {
                var model=GameAssets.Model("Color group "+group,cargoMesh,purple,cargo,new Vector3(0,1.69f,1.29f-group),Vector3.one);
                display.groups[group]=model;display.renderers[group]=model.GetComponent<MeshRenderer>();model.SetActive(false);
            }
            // Only the block currently feeding the outlet needs the two extra solid
            // remainders. Reuse them across all four groups instead of spawning pieces.
            for(int part=0;part<display.remainders.Length;part++)
            {
                var model=GameAssets.Model("Cargo peel remainder "+part,cargoMesh,purple,cargo,new Vector3(0,1.69f,1.29f),Vector3.one);
                display.remainders[part]=model;display.remainderRenderers[part]=model.GetComponent<MeshRenderer>();model.SetActive(false);
            }
            display.hull=GameAssets.Model("Front cargo outline",cargoMesh,outline,cargo,Vector3.zero,Vector3.one);
            display.highlight=display.hull.AddComponent<CargoHighlight>();display.hull.SetActive(false);cargoDisplays.Add(display);
            Vector3 mouth=CarrierOutlet(index);
            // Use the actual opening, rather than treating a named local axis as
            // the truck's tail. Increasing packing depth must approach this end.
            display.packingDepthDirection=root.InverseTransformPoint(mouth).z<display.frontCenter.z?-1f:1f;
            gates.Add(track.Nearest(mouth,out _,out _));
        }
        Vector3 CarrierOutlet(int carrier)=>State.Carriers[carrier].generator?featureViews[carrier].GeneratorOutlet:
            placements[carrier].capacityGroups>4?carriers[carrier].TransformPoint(longVisual.outlet.Value):CargoEmission.OutletPosition(carriers[carrier]);

        void CreatePiece(PieceState piece)
        {
            var go=GameAssets.Model("Cube "+piece.id,cargoCellMesh,GameAssets.CargoMaterial,board,Vector3.zero,CargoCellSize);
            var view=go.AddComponent<PieceView>();view.Id=piece.id;view.Renderer=go.GetComponent<MeshRenderer>();view.Filter=go.GetComponent<MeshFilter>();
            view.Batch=cargoBatch;view.Tint=GameAssets.Color(piece.color);
            SetCargoAppearance(view.Renderer,piece);
            view.PhysicsMaterial=transportMaterial;pieces[piece.id]=view;go.SetActive(false);
        }

        Vector3 CargoPosition(int carrier,int slot)
            =>carriers[carrier].TransformPoint(CargoLocalPosition(slot,carrier));
        Vector3 CargoLocalPosition(int slot,int carrier)
        {
            bool generator=State.Carriers[carrier].generator;
            int group=generator?0:slot/State.GroupSize,within=slot%State.GroupSize;var f=State.Fragments;
            Vector3 center=cargoDisplays[carrier].frontCenter;
            // Fill the innermost vertical face bottom-to-top, then approach the
            // opening one depth plane at a time. Native GetBlockCoordinate also
            // alternates the horizontal direction of adjacent height rows.
            int faceSize=f.Columns*f.Rows;
            int depth=within/faceSize,row=(within/f.Columns)%f.Rows,column=within%f.Columns;
            if((row&1)!=0)column=f.Columns-1-column;
            Vector3 cell=CargoCellSize;
            return center+cargoMeshCenter+new Vector3((column-(f.Columns-1)*.5f)*cell.x,
                (row-(f.Rows-1)*.5f)*cell.y,
                -group+(depth-(f.Depth-1)*.5f)*cell.z*cargoDisplays[carrier].packingDepthDirection);
        }

        void RefreshCargo()
        {
            for(int c=0;c<carriers.Count;c++)
            {
                RefreshCargo(c);HighlightFront(c);
            }
            displayedFreeGroups=State.FreeBeltSlots;
        }
        static void SetVisible(GameObject target,bool visible)
        {
            if(target.activeSelf!=visible)target.SetActive(visible);
        }
        internal void MarkCargoDirty(int carrier)
        {
            var display=cargoDisplays[carrier];display.cargoDirty=true;display.outlineDirty=true;
        }
        void MarkCargoGroupDirty(int carrier,int group)
        {
            var display=cargoDisplays[carrier];
            display.firstDirtyGroup=Mathf.Min(display.firstDirtyGroup,group);
            display.lastDirtyGroup=Mathf.Max(display.lastDirtyGroup,group);
            display.outlineDirty=true;
        }
        internal void SetGeneratorCargoLift(int carrier,float offset)
        {
            cargoRoots[carrier].localPosition=Vector3.up*offset;
        }
        void LateUpdate()
        {
            if(!bootComplete||State==null)return;
            TickAdCadence();
            while(!levelEntering&&shownKeyCollections<State.KeyCollections.Count)
            {
                var key=State.KeyCollections[shownKeyCollections++];
                Vector3 from=carriers[key.carrier].TransformPoint(cargoDisplays[key.carrier].frontCenter+Vector3.up*.96f-Vector3.forward*(key.slot/State.GroupSize));
                bool used=false;foreach(var group in groupFeatures)used|=group.ReceiveKey(key.color,from,carriers[key.carrier].rotation);
                if(!used)
                {
                    var pickup=CarrierFeatureView.MakeKey(board,key.color);pickup.transform.position=from;pickup.AddComponent<TransientEffect>();
                    MotionRunner.For(pickup).Play("pickup",.5f,t=>
                    {pickup.transform.position=from+Vector3.up*t;pickup.transform.localScale=Vector3.one*(1-t);},()=>Destroy(pickup));
                }
            }
            // A solid outline only represents whole groups. A single received
            // fragment cannot change another truck's outline until a full group fits.
            int freeGroups=State.FreeBeltSlots;
            bool budgetChanged=displayedFreeGroups!=freeGroups;
            bool featuresChanged=displayedFeatures!=State.FeatureRevision;displayedFeatures=State.FeatureRevision;
            if(featuresChanged)foreach(var feature in groupFeatures)feature.Refresh(State);
            displayedFreeGroups=freeGroups;
            for(int c=0;c<cargoDisplays.Count;c++)
            {
                var display=cargoDisplays[c];var state=State.Carriers[c];
                if(budgetChanged)display.outlineDirty=true;
                bool fullRefresh=featuresChanged||display.cargoDirty||
                    display.curtainOpen!=state.curtainOpen||display.packed!=state.packed;
                if(fullRefresh||display.lastDirtyGroup>=0)RefreshCargo(c,fullRefresh);
                if(display.outlineDirty)HighlightFront(c);
            }
        }
        Material TruckColor(int color,int shade)
        {
            int key=color*3+shade;
            if(!truckColors.TryGetValue(key,out Material material))
                truckColors[key]=material=GameAssets.NewMaterial(GameAssets.Color(color)*(shade==0?.62f:shade==1?.82f:1));
            return material;
        }
        void RefreshCargo(int carrier,bool fullRefresh=true)
        {
            var display=cargoDisplays[carrier];var state=State.Carriers[carrier];var cargo=state.cargo;
            fullRefresh|=state.generator;
            featureViews[carrier].Refresh(State,carrier);
            bool open=state.curtainOpen;
            bool crustCovered=featureViews[carrier].CrustConcealsCargo;
            bool hasPartialRelease=false;
            int generatorFront=(cargo.Count-1)/State.GroupSize;
            if(state.generator)for(int g=display.queuedGroups.Length-1;g>=0;g--)
                if(display.queuedGroups[g]){generatorFront=g;break;}
            if(display.curtainOpen!=open)curtains[carrier].SetRevealed(open);
            SetVisible(cargoRoots[carrier].gameObject,open&&!crustCovered&&(!state.generator||!levelEntering));
            for(int group=0;group<display.groups.Length;group++)
            {
                // The outgoing solid remainder may stay visible while the next
                // generator group waits for the cover animation to reveal it.
                bool shown=open&&!crustCovered&&(!state.generator||(group==generatorFront&&(display.queuedGroups[group]||!featureViews[carrier].GeneratorConcealsCargo)));
                if(shown&&display.queuedGroups[group]&&display.launched[group]>0&&display.launched[group]<State.GroupSize)
                    hasPartialRelease=true;
                if(!fullRefresh&&(group<display.firstDirtyGroup||group>display.lastDirtyGroup))continue;
                if(display.queuedGroups[group])
                {
                    display.groups[group].GetComponent<MotionRunner>()?.StopAll();display.groups[group].transform.localRotation=Quaternion.identity;
                    if(shown)RefreshQueuedGroup(display,group);
                    else SetVisible(display.groups[group],false);
                    continue;
                }
                int start=group*State.GroupSize,end=Mathf.Min(start+State.GroupSize,cargo.Count);
                bool whole=shown&&end-start==State.GroupSize;
                if(whole)
                {
                    int color=State.Pieces[cargo[start]].color;
                    for(int slot=start;slot<end;slot++)
                    {
                        var piece=State.Pieces[cargo[slot]];
                        if(piece.color!=color||(piece.location!=PieceLocation.Cargo&&piece.location!=PieceLocation.Packed)){whole=false;break;}
                    }
                }
                if(whole)
                {
                    var first=State.Pieces[cargo[start]];
                    SetCargoAppearance(display.renderers[group],first);
                    display.groups[group].transform.localScale=Vector3.one;
                    display.groups[group].transform.localPosition=display.frontCenter-Vector3.forward*(state.generator?0:group);
                }
                SetVisible(display.groups[group],whole);
                for(int slot=start;slot<end;slot++)
                {
                    var piece=State.Pieces[cargo[slot]];
                    if(piece.location!=PieceLocation.Cargo&&piece.location!=PieceLocation.Packed)continue;
                    var view=pieces[piece.id];
                    if(!shown||whole){SetVisible(view.gameObject,false);continue;}
                    if(!view.gameObject.activeSelf||view.CargoCarrier!=carrier||view.CargoSlot!=slot)
                    {
                        SetCargoPieceSize(view);view.SetAnimatedPose(CargoPosition(carrier,slot),carriers[carrier].rotation);
                        view.CargoCarrier=carrier;view.CargoSlot=slot;
                    }
                    SetCargoAppearance(view.Renderer,piece);
                    SetVisible(view.gameObject,true);
                }
            }
            if(!hasPartialRelease)for(int part=0;part<display.remainders.Length;part++)SetVisible(display.remainders[part],false);
            completionMarks[carrier].GetComponent<CompletionMotion>().SetComplete(state.packed);
            display.curtainOpen=open;display.packed=state.packed;display.cargoDirty=false;display.outlineDirty=true;
            display.firstDirtyGroup=int.MaxValue;display.lastDirtyGroup=-1;
        }
        void RefreshQueuedGroup(CargoDisplay display,int group)
        {
            int launched=display.launched[group],color=display.queuedColors[group];bool covered=display.queuedHidden[group];
            if(launched==0)
            {
                ShowSolidRemainder(display,display.groups[group],display.renderers[group],group,color,covered,Vector3.zero,Vector3.one);
                return;
            }
            var f=State.Fragments;int plane=f.Columns*f.Rows;
            int depth=launched/plane,row=f.Rows-1-(launched%plane)/f.Columns,column=launched%f.Columns;
            float z0=depth/(float)f.Depth,z1=(depth+1)/(float)f.Depth;
            // The unlaunched volume consists of deeper layers, lower rows of the
            // current layer, and the remaining columns of its current row.
            ShowSolidRemainder(display,display.groups[group],display.renderers[group],group,color,covered,
                new Vector3(0,0,z1),Vector3.one);
            ShowSolidRemainder(display,display.remainders[0],display.remainderRenderers[0],group,color,covered,
                new Vector3(0,0,z0),new Vector3(1,row/(float)f.Rows,z1));
            ShowSolidRemainder(display,display.remainders[1],display.remainderRenderers[1],group,color,covered,
                new Vector3(column/(float)f.Columns,row/(float)f.Rows,z0),new Vector3(1,(row+1)/(float)f.Rows,z1));
        }
        void ShowSolidRemainder(CargoDisplay display,GameObject model,MeshRenderer renderer,int group,int color,bool covered,Vector3 min,Vector3 max)
        {
            Vector3 scale=max-min;
            if(scale.x<=0||scale.y<=0||scale.z<=0){SetVisible(model,false);return;}
            Vector3 center=cargoMeshMin+Vector3.Scale(cargoMeshSize,(min+max)*.5f);
            model.transform.localScale=scale;
            model.transform.localPosition=display.frontCenter-Vector3.forward*(display.generator?0:group)+center-Vector3.Scale(cargoMeshCenter,scale);
            SetCargoAppearance(renderer,display.queuedBlocks[group],color,covered);
            SetVisible(model,true);
        }
        int EmissionSlot(int order,int carrier)
        {
            var f=State.Fragments;int plane=f.Columns*f.Rows;
            int depth=order/plane,row=f.Rows-1-(order%plane)/f.Columns,column=order%f.Columns;
            // Preserve the existing physical peel order, but encode the cell
            // using the same depth/height/column layout as incoming fragments.
            int packingDepth=cargoDisplays[carrier].packingDepthDirection<0?f.Depth-1-depth:depth;
            int packingColumn=(row&1)==0?column:f.Columns-1-column;
            return packingDepth*plane+row*f.Columns+packingColumn;
        }
        void ClearQueuedCargo()
        {
            for(int c=0;c<cargoDisplays.Count;c++)
            {
                var display=cargoDisplays[c];
                Array.Clear(display.queuedGroups,0,display.queuedGroups.Length);
                Array.Clear(display.launched,0,display.launched.Length);
                Array.Clear(display.queuedColors,0,display.queuedColors.Length);
                Array.Clear(display.queuedBlocks,0,display.queuedBlocks.Length);
                Array.Clear(display.queuedHidden,0,display.queuedHidden.Length);
                for(int part=0;part<display.remainders.Length;part++)SetVisible(display.remainders[part],false);
            }
        }
        void HighlightFront(int carrier)
        {
            var display=cargoDisplays[carrier];display.outlineDirty=false;
            if(!cargoRoots[carrier].gameObject.activeInHierarchy||featureViews[carrier].GeneratorConcealsCargo)
            {SetVisible(display.hull,false);return;}
            var cargo=State.Carriers[carrier].cargo;
            if(cargo.Count==0||cargo.Count%State.GroupSize!=0)
            {SetVisible(display.hull,false);return;}
            int releasable=State.ReleasableCount(carrier);
            // The outline is a solid hull for complete blocks. During assembly,
            // its back faces would cover the empty cells between arriving pieces.
            if(releasable==0||releasable%State.GroupSize!=0)
            {SetVisible(display.hull,false);return;}
            var front=State.Pieces[cargo[cargo.Count-1]];if(front.hidden){SetVisible(display.hull,false);return;}
            int first=cargo.Count-releasable;
            int count=0;Bounds bounds=new Bounds();
            for(int slot=cargo.Count-1;slot>=first;slot--)
            {
                var piece=State.Pieces[cargo[slot]];
                if(piece.color!=front.color||piece.hidden||piece.location!=PieceLocation.Cargo)
                {SetVisible(display.hull,false);return;}
                Vector3 local=CargoLocalPosition(slot,carrier);
                var cube=new Bounds(local,CargoCellSize);if(count==0)bounds=cube;else bounds.Encapsulate(cube);count++;
            }
            if(count!=releasable){SetVisible(display.hull,false);return;}
            display.hull.transform.localPosition=bounds.center;
            display.hull.transform.localScale=new Vector3(bounds.size.x/cargoMeshSize.x,bounds.size.y/cargoMeshSize.y,bounds.size.z/cargoMeshSize.z);
            display.highlight.SetCount(count);SetVisible(display.hull,true);
        }

        void Update()
        {
            HotUpdate.LuaGameRuntime.Tick();
            if(!bootComplete||State==null)return;
            TickPeripheral();UpdateCargoFlights();
            if(!Paused&&!IsEnteringLevel)track.AnimateArrows(Time.deltaTime);
            if(!Paused){hiddenFlowTime+=Time.deltaTime;hidden.SetFloat("_FlowTime",hiddenFlowTime);}
            UpdateCargoReveals();
            if(screen!=new Vector2(Screen.width,Screen.height)){screen=new Vector2(Screen.width,Screen.height);FrameBoard();}
            if(!CommerceBusy&&!Hud.BoosterPresentationBusy&&Input.GetKeyDown(KeyCode.Escape))
            {
                if(SelectingShuffle){SetShuffleSelection(false);Hud.Toast("");}
                else if(Hud.HasDialog)CloseMenu();
                else if(Hud.IsHome){Application.Quit();return;}
                else {SetPaused(true);Hud.Pause(Progress);}
            }
            if(!Paused&&Input.GetMouseButtonDown(0)&&!(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject()))
            {
                Ray ray=Camera.ScreenPointToRay(Input.mousePosition);
                if(Physics.Raycast(ray,out RaycastHit hit,200))
                {
                    var target=hit.collider.GetComponentInParent<CarrierClickTarget>();if(target!=null)TryRelease(target.Index);
                }
            }
            TickNativeRules(Time.deltaTime);Hud.Refresh(this);
            if(!Hud.BoosterPresentationBusy&&!resultShown&&State.Status!=RunStatus.Playing)
            {
                resultShown=true;
                if(State.Status==RunStatus.Won){RecordAdRound(true);LuaAction("won");}
                StartCoroutine(ShowResult(run));
            }
        }
        IEnumerator ShowResult(int token)
        {
            bool won=State.Status==RunStatus.Won;
            yield return new WaitForSecondsRealtime(won?.1f:.6f);
            while(token==run&&!Hud.IsHome&&(Hud.BoosterPresentationBusy||(won?completionMarks.Any(mark=>mark.GetComponent<CompletionMotion>().Animating):MechanicAnimationsRunning())))yield return null;
            if(token==run&&!Hud.IsHome&&State.Status!=RunStatus.Playing)
            {
                SetPaused(true);
                if(State.Status==RunStatus.Won)PresentSettlement(true);
                else HotUpdate.LuaUi.Render("revive_offer",Hud);
            }
        }

        bool MechanicAnimationsRunning()
        {
            if(IsEnteringLevel)return true;
            if(SourceParticleEffect.ActiveCount>0)return true;
            if(cargoFlights.Count>0)return true;
            if(revealingCargo.Count>0)return true;
            if(groupFeatures.Any(g=>g.Busy))return true;
            foreach(var motion in board.GetComponentsInChildren<MotionRunner>())if(motion.Busy)return true;
            return false;
        }
        bool VisualTransferBlocked(int carrier)
        {
            if(!State.Carriers[carrier].Accessible)return false;
            if(curtains[carrier].IsCovering||featureViews[carrier].BlocksTransfer)return true;
            foreach(var group in groupFeatures)if(group.Blocks(carrier))return true;
            return false;
        }

        void SetPieceSize(PieceView view,float size)
        {
            view.Filter.sharedMesh=fragmentMesh;
            view.transform.localScale=Vector3.one*(size/fragmentMesh.bounds.size.x);
        }
        void SetCargoPieceSize(PieceView view)
        {
            // Flat mating faces occupy the entire cell; belt bevels would leave
            // visible grooves even if their bounding boxes touched exactly.
            view.Filter.sharedMesh=cargoCellMesh;view.transform.localScale=CargoCellSize;
        }
        static Mesh CreateCargoCellMesh()
        {
            var vertices=new Vector3[24];var normals=new Vector3[24];var triangles=new int[36];
            var faces=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            for(int face=0;face<faces.Length;face++)
            {
                Vector3 normal=faces[face],u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up,v=Vector3.Cross(normal,u);
                int first=face*4,t=face*6;
                vertices[first]=(normal-u-v)*.5f;vertices[first+1]=(normal+u-v)*.5f;
                vertices[first+2]=(normal+u+v)*.5f;vertices[first+3]=(normal-u+v)*.5f;
                for(int i=0;i<4;i++)normals[first+i]=normal;
                triangles[t]=first;triangles[t+1]=first+1;triangles[t+2]=first+2;
                triangles[t+3]=first;triangles[t+4]=first+2;triangles[t+5]=first+3;
            }
            var mesh=new Mesh {name="Joined cargo cell",vertices=vertices,normals=normals,triangles=triangles};
            mesh.RecalculateBounds();return mesh;
        }
        public bool Undo()=>Convert.ToBoolean(LuaAction("undo"));
        public void ExpandBelt()=>LuaAction("belt");
        public void AddTruck()=>LuaAction("truck");
        public void AfterBeltExpansion(bool revive)
        {
            if(Hud.BoosterPresentationBusy)return;
            if(revive)Hud.Close();
            Hud.PlayBoosterUse("belt",()=>
            {
                State.ExpandBelt();PreserveBoosterHistory();
                if(revive){resultShown=false;Hud.Close();SetPaused(false);}
            });
        }
        public bool IsCarrierPlaced(string id)=>placements.Any(p=>p.id==id);
        public void AddTruckView(CarrierData spare)
        {
            if(Hud.BoosterPresentationBusy)return;
            var target=BoardPresentation.CarrierPosition(new Vector3(spare.x,spare.y,spare.z),Quaternion.Euler(0,spare.yaw,0),sourcePivot);
            Hud.PlayBoosterUse("truck",()=>
            {
                PlaceSpareTruck(spare);
            },target);
        }
        void PlaceSpareTruck(CarrierData spare)
        {
            State.AddCarrierFromData(spare);CreateTruck(spare);
            int index=carriers.Count-1;
            carrierOrigins[index]=carriers[index].position;
            gates[index]=track.Nearest(CarrierOutlet(index),out _,out _);
            PreserveBoosterHistory();RefreshCargo();UpdateBoardBounds();FrameBoard();
        }
        void Shuffle(int carrier)=>LuaAction("shuffle",carrier);
        public bool ShuffleView(int carrier)
        {
            if(Hud.BoosterPresentationBusy)return false;
            if(!CanShuffleCarrier(carrier))return false;
            Hud.PlayBoosterUse("shuffle",()=>
            {
                var oldSlots=new Dictionary<int,int>();var cargo=State.Carriers[carrier].cargo;
                for(int slot=0;slot<cargo.Count;slot++)oldSlots[cargo[slot]]=slot;
                if(!State.Shuffle(carrier,new System.Random()))return;
                // A successful shuffle starts a new undo boundary. Only truck
                // releases made after this point may become undoable again.
                history.Clear();
                MarkCargoDirty(carrier);RefreshCargo();StartCoroutine(ShuffleMotion(carrier,oldSlots));
            },carriers[carrier].position+Vector3.up*.5f);
            return true;
        }
        public void SetPaused(bool value)
        {
            if(value)FlushPeripheralProgress();
            if(Paused==value){RefreshMusic();return;}Paused=value;
            foreach(var view in beltViews)
            {
                if(value){view.PausedVelocity=view.Body.velocity;view.PausedAngularVelocity=view.Body.angularVelocity;view.SetAnimatedPose(view.transform.position,view.transform.rotation);}
                else {view.StartPhysics(view.transform.position,view.transform.rotation,view.PausedVelocity);view.Body.angularVelocity=view.PausedAngularVelocity;}
            }
            RefreshMusic();
        }
        public void RefreshMusic()
        {
            if(music==null)return;
            // Gameplay/UI pauses do not interrupt the soundtrack.
            if(IsTestRun||Progress==null||!Progress.music||adApplicationPaused||CommerceBusy){music.Pause();return;}
            if(!music.isPlaying){music.UnPause();if(!music.isPlaying&&music.clip!=null)music.Play();}
        }
        public void NextLevel()=>LuaAction("next_level");
        public void SaveProgress() { if(!IsTestRun)Progress.Save(); }
        public void PlaySound(string clip)=>Play(clip);
        IEnumerator updateCheck;
        void CancelUpdateCheck()
        {
            var pending=updateCheck;updateCheck=null;(pending as IDisposable)?.Dispose();
        }
        IEnumerator CheckUpdates()
        {
            if(!Hud.IsHome){Hud.Toast("请返回主页后检查更新");yield break;}
            if(HotUpdate.HotContent.Busy){Hud.Toast("正在检查更新，请稍候");yield break;}
            updateCheck=HotUpdate.HotContent.Check(true);
            try {while(updateCheck.MoveNext())yield return updateCheck.Current;}
            finally {CancelUpdateCheck();}
            Hud.Toast(HotUpdate.HotContent.Status);
        }
        void OnGUI()
        {
            if(!bootComplete)return;
            if(bootComplete&&!HotUpdate.HotContent.Busy&&bootError==null)return;
            var style=new GUIStyle(GUI.skin.label) {alignment=TextAnchor.MiddleCenter,fontSize=Mathf.Max(18,Screen.height/55),font=GameAssets.Font};
            style.normal.textColor=ArtTheme.Cream;
            GUI.Label(new Rect(20,Screen.height*.42f,Screen.width-40,Screen.height*.16f),bootError??HotUpdate.HotContent.Status,style);
        }
        void Play(string clip)
        {
            if(IsTestRun)return;
            if(Progress.sound){var audio=GameAssets.Audio(clip);if(audio!=null)effects.PlayOneShot(audio,.55f);}
#if UNITY_ANDROID || UNITY_IOS
            if(Progress.haptics&&clip=="complete")Handheld.Vibrate();
#endif
        }
        public void Reframe() => FrameBoard();
        void UpdateBoardBounds()
        {
            boardBounds=BoardPresentation.BoundsFor(track,carriers,sourcePivot);
            visibleBoardBounds=BoardPresentation.VisibleBoundsFor(track,carriers.Concat(groupFeatures.Select(group=>group.transform)));

        }
        void FrameBoard()
        {
            if(Level==null||Camera==null)return;BoardPresentation.Frame(Camera,boardBounds,Level.cameraOffset,visibleBoardBounds,Hud==null?0:Hud.BannerInsetPixels/Mathf.Max(1,Screen.height));
            backdrop?.Resize(Hud==null?0:Hud.BannerInsetPixels);
            foreach(var badge in completionMarks)badge.transform.rotation=Camera.transform.rotation;
        }
    }
}
