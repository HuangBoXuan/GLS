using System;
using System.Collections;
using System.Linq;
using LoopSort0.Core;
using UnityEngine;

namespace LoopSort0
{
    public sealed partial class GameController
    {
        StartupLoading startupLoading;
        IEnumerator Start()
        {
            if(!IsTestRun)
            {
                startupLoading=StartupLoading.Create(transform);
                yield return null; // Paint before the first synchronous initialization work.
            }
            bool failed=false;
            var startup=StartupWork.Run(InitializeGame(),error=>
            {
                failed=true;bootError="游戏内容加载失败，请重新启动游戏";
                if(startupLoading!=null)startupLoading.Failed(bootError);
                Debug.LogException(error);
            });
            try
            {
                while(true)
                {
                    if(!startup.MoveNext()){bootComplete=!failed;break;}
                    yield return startup.Current;
                }
            }
            finally
            {
                (startup as IDisposable)?.Dispose();
                if(bootComplete&&startupLoading!=null){Destroy(startupLoading.gameObject);startupLoading=null;}
            }
        }
        IEnumerator InitializeGame()
        {
            Instance=this;
            var prepare=HotUpdate.HotContent.Prepare();
            try
            {
                while(prepare.MoveNext())
                {
                    if(startupLoading!=null)startupLoading.SetProgress(.05f+.65f*HotUpdate.HotContent.PreparationProgress);
                    // Unity may yield a nested download enumerator; the loading
                    // component also samples its byte-based progress every frame.
                    yield return prepare.Current;
                }
            }
            finally {(prepare as IDisposable)?.Dispose();}
            if(startupLoading!=null)startupLoading.SetProgress(.70f);
            for(int attempt=0;;attempt++)
            {
                Exception failure=null;
                yield return StartupWork.Run(HotUpdate.LuaGameRuntime.InitializeIncrementally(value=>startupLoading?.SetProgress(.70f+.05f*value)),error=>failure=error);
                if(failure==null)break;
                if(HotUpdate.HotContent.Active==null||attempt>=2)throw failure;
                Debug.LogWarning("Lua startup rolled back: "+failure.Message);HotUpdate.HotContent.Rollback();
            }
            Application.targetFrameRate=60;
            Time.fixedDeltaTime=BeltSteering.FixedStep;Time.maximumDeltaTime=BeltSteering.MaximumStep;
            Physics.gravity=new Vector3(0,-9.81f,0);Physics.defaultSolverIterations=1;Physics.defaultSolverVelocityIterations=1;
            Physics.sleepThreshold=.005f;Physics.defaultContactOffset=.01f;Physics.bounceThreshold=2;
            Physics.defaultMaxDepenetrationVelocity=10;Physics.defaultMaxAngularSpeed=50;
            Physics.autoSyncTransforms=false;Physics.reuseCollisionCallbacks=true;Physics.invokeCollisionCallbacks=false;
            QualitySettings.shadows=ShadowQuality.Disable;
            if(startupLoading!=null)startupLoading.SetProgress(.76f);
            if(!IsTestRun)yield return null;
            Progress=IsTestRun?new PlayerProgress():PlayerProgress.Load();
            yield return HotUpdate.HotContent.Preload<TextAsset>("LoopSort/Data/main");
            string catalogJson=HotUpdate.HotContent.Text("LoopSort/Data/main");
#if UNITY_WEBGL && !UNITY_EDITOR
            Catalog=Newtonsoft.Json.JsonConvert.DeserializeObject<LevelCatalog>(catalogJson);
#else
            // Pure data deserialization: no Unity API is called on the worker.
            var catalogTask=System.Threading.Tasks.Task.Run(()=>Newtonsoft.Json.JsonConvert.DeserializeObject<LevelCatalog>(catalogJson));
            while(!catalogTask.IsCompleted)yield return null;
            Catalog=catalogTask.GetAwaiter().GetResult();
#endif
            if(startupLoading!=null)startupLoading.SetProgress(.80f);
            string[] warmTextures={"candy-factory-home","candy-factory-board","candyloop-logo","candy-v2-panels","candy-buttons","candy-mechanisms","approved-menu","menu-details","reference-ui","belt-capacity","icons","toggleOn","music","sound","haptic","language","close","mine-ore"};
            for(int i=0;i<warmTextures.Length;i++)
            {
                yield return HotUpdate.HotContent.Preload<Texture2D>("LoopSort/Theme/"+warmTextures[i]);
                if(startupLoading!=null)startupLoading.SetProgress(.80f+.04f*(i+1)/warmTextures.Length);
            }
            yield return HotUpdate.HotContent.Preload<AudioClip>("LoopSort/Audio/music");
            yield return HotUpdate.HotContent.Preload<GameObject>("LoopSort/Models/421");
            yield return HotUpdate.HotContent.Preload<GameObject>("LoopSort/Models/401");
            UiLocalization.Select(Progress.language,false);
            sourceVisual=GameAssets.Data<SourceVisual>("carrier-visual");
            longVisual=GameAssets.Data<SourceVisual>("long-carrier-visual");
            sourcePivot=sourceVisual.nodes.First(n=>n.path=="Carrier/Pivot").transform.m_LocalPosition.Value;
            foreach(int id in sourceVisual.parts.Select(p=>p.mesh).Concat(longVisual.parts.Select(p=>p.mesh)).Concat(new[]{382}).Distinct())
            {
                yield return HotUpdate.HotContent.Preload<GameObject>("LoopSort/Models/"+id);
                GameAssets.CompleteMesh(id);
                yield return null;
            }
            yield return HotUpdate.HotContent.Preload<Material>("LoopSort/Shaders/CargoInstanced");
            if(startupLoading!=null)startupLoading.SetProgress(.85f);
            purple=GameAssets.NewMaterial(ArtTheme.Teal);
            dark=ReferenceConveyor.Material();
            black=GameAssets.NewMaterial(ArtTheme.Ink);
            metal=GameAssets.NewMaterial(ArtTheme.Caramel);
            truckBody=GameAssets.NewMaterial(ArtTheme.Cream);truckDeck=GameAssets.NewMaterial(ArtTheme.Caramel);truckBase=GameAssets.NewMaterial(ArtTheme.Plum);
            hidden=new Material(HotUpdate.HotContent.Load<Shader>("LoopSort/Shaders/HiddenCargo"));
            hidden.mainTexture=Resources.Load<Texture2D>("LoopSort/Theme/mine-ore");
            VolcanoArt.ConfigureHidden(hidden);
            outline=new Material(HotUpdate.HotContent.Load<Shader>("LoopSort/Shaders/CargoOutline"));
            cargoMesh=GameAssets.Mesh(421);var cargoBounds=cargoMesh.bounds;
            cargoMeshSize=cargoBounds.size;cargoMeshCenter=cargoBounds.center;cargoMeshMin=cargoBounds.min;
            fragmentMesh=GameAssets.Mesh(401);cargoCellMesh=CreateCargoCellMesh();
            transportMaterial=new PhysicMaterial("APK block physics") { dynamicFriction=0,staticFriction=0,bounciness=.6f,
                frictionCombine=PhysicMaterialCombine.Maximum,bounceCombine=PhysicMaterialCombine.Maximum };
            Camera=UnityEngine.Camera.main;
            if(Camera==null)Camera=new GameObject("Game Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            Camera.tag="MainCamera";Camera.orthographic=true;Camera.clearFlags=CameraClearFlags.SolidColor;
            Camera.depthTextureMode|=DepthTextureMode.Depth;
            Camera.backgroundColor=ArtTheme.Ink;Camera.nearClipPlane=.1f;Camera.farClipPlane=120;
            backdrop=ToyBackdrop.Create(Camera);
            RenderSettings.ambientLight=ArtTheme.Cream;RenderSettings.ambientIntensity=.9f;
            if(startupLoading!=null)startupLoading.SetProgress(.87f);
            if(!IsTestRun)yield return null;
            var sun=FindObjectOfType<Light>();if(sun==null)sun=new GameObject("Soft key light",typeof(Light)).GetComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=1.1f;sun.color=new Color(1,.95f,1);sun.transform.rotation=Quaternion.Euler(48,-30,0);sun.shadows=LightShadows.None;
            effects=gameObject.AddComponent<AudioSource>();music=gameObject.AddComponent<AudioSource>();music.loop=true;music.volume=.60f;music.clip=GameAssets.Audio("music");
            var hudObject=new GameObject("Game UI",typeof(RectTransform));Hud=hudObject.AddComponent<GameHud>();
            yield return Hud.BuildSteps(Camera);BindHud();
            if(startupLoading!=null){startupLoading.SetProgress(.94f);yield return null;}
            var initial=Catalog.levels.FirstOrDefault(l=>l.id==Progress.currentLevel)??Catalog.levels.FirstOrDefault();
            if(initial==null)throw new InvalidOperationException("The level catalog is empty.");
            IsDailyRun=false;DailyDate="";
            yield return BuildLevelData(initial,true);
            readyForGuides=true;
            if(!IsTestRun)ShowHome();
            if(startupLoading!=null)
            {
                startupLoading.SetProgress(1);
                while(!startupLoading.Finished)yield return null;
                yield return null; // Present the completed bar once before removing the cover.
            }
        }

        void BindHud()
        {
            Hud.OnPause=()=>LuaAction("pause");Hud.OnRetry=()=>LuaAction("retry");Hud.OnContinue=()=>LuaAction("next_level");
            Hud.OnCancel=()=>LuaAction("cancel");Hud.OnHome=()=>LuaAction("home");Hud.OnPlay=()=>LuaAction("play");
            Hud.OnHelp=()=>LuaAction("help");Hud.OnLevels=()=>LuaAction("levels");
            Hud.OnLevel=id=>LuaAction("select_level",id);Hud.OnUndo=()=>LuaAction("undo");
            Hud.OnBelt=()=>LuaAction("belt");Hud.OnTruck=()=>LuaAction("truck");Hud.OnShuffle=()=>LuaAction("shuffle_select");
            Hud.OnBuyBooster=id=>Convert.ToBoolean(LuaAction("buy",id));Hud.OnName=name=>LuaAction("name",name);
            Hud.OnArea=area=>LuaAction("area",area);Hud.OnToggle=key=>LuaAction("toggle",key);
            Hud.OnCheckUpdates=()=>StartCoroutine(CheckUpdates());
        }
        void OnDestroy()
        {
            if(startupLoading!=null)Destroy(startupLoading.gameObject);
            FlushAdGameplay(true);
            if(commerceProvider is IPeripheralAdLifecycle lifecycle)lifecycle.AdClosed-=RewardedAdClosed;
            if(Hud!=null)Hud.CancelBoosterPresentation(false);
            CancelUpdateCheck();
            ReleasePeripheralAssets();
            State?.Dispose();
            if(Hud!=null)Hud.ReleaseLuaCallbacks();
            if(Instance==this)Instance=null;
            if(Hud!=null)Destroy(Hud.gameObject);
            if(board!=null)Destroy(board.gameObject);
            track?.Dispose();
            if(transportMaterial!=null)Destroy(transportMaterial);
            if(cargoCellMesh!=null)Destroy(cargoCellMesh);
            if(hidden!=null)Destroy(hidden);if(outline!=null)Destroy(outline);
            if(backdrop!=null)Destroy(backdrop.gameObject);
            foreach(var material in new[]{purple,dark,black,metal,truckBody,truckDeck,truckBase})if(material!=null)Destroy(material);
            foreach(var material in truckColors.Values)if(material!=null)Destroy(material);
            try {HotUpdate.LuaGameRuntime.Shutdown();}finally {HotUpdate.HotContent.Reset();}
        }
    }
}
