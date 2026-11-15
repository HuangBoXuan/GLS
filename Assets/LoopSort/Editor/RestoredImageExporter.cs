using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LoopSort0.Editor
{
    [InitializeOnLoad]
    public static class RestoredImageExporter
    {
        const string Native="Assets/LoopSort/PlatformAssets/";
        const string ResourcesRoot="Assets/LoopSort/Resources/LoopSort/";
        static string output;
        static JArray images;
        static readonly List<Screen> screens=new List<Screen>();
        static int screenIndex;
        static bool initialized,started,settled;
        static bool uiOnly;
        static bool popupsOnly;
        static RenderTexture popupTarget;
        static double ready;
        sealed class Screen {public string id,label;public Action<GameController> show;}
        static RestoredImageExporter(){EditorApplication.update+=Poll;}
        public static void BatchExportCurrentUI()
        {
            try
            {
                if(!Application.isBatchMode)throw new InvalidOperationException("Use an isolated batch project");
                var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-imageExportOutput");
                if(arg<0||arg+1>=args.Length)throw new ArgumentException("Missing imageExportOutput");
                output=Path.GetFullPath(args[arg+1]);
                if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())
                    throw new IOException("UI staging folder must be empty: "+output);
                Directory.CreateDirectory(output);images=new JArray();uiOnly=true;SessionState.SetBool("RestoredExport.UIOnly",true);
                popupsOnly=args.Contains("-popupExportOnly");SessionState.SetBool("RestoredExport.PopupsOnly",popupsOnly);
                PlayerSettings.companyName="CodexImageExport";PlayerSettings.productName="LoopSortCurrentUIExport";
                WriteManifest(false);SessionState.SetString("RestoredExport.Output",output);
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/LoopSort/Scenes/Gameplay.unity");
                Object.FindObjectOfType<GameController>().IsTestRun=true;
                EditorApplication.isPlaying=true;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BatchRepairExports()
        {
            try
            {
                string[] args=Environment.GetCommandLineArgs();output=Path.GetFullPath(args[Array.IndexOf(args,"-imageExportOutput")+1]);
                images=(JArray)JObject.Parse(File.ReadAllText(Path.Combine(output,"manifest.json")))["images"];
                var index=JObject.Parse(File.ReadAllText(Native+"index.json"));
                foreach(var entry in ((JObject)index["prefabs"]).Properties().Where(p=>p.Name.StartsWith("particle-")))
                {
                    string file="Models/"+entry.Name+".png";foreach(var old in images.Where(i=>(string)i["file"]==file).ToArray())old.Remove();
                    Render(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>((string)entry.Value)),file,"游戏模型",entry.Name,(string)entry.Value,78);
                }
                string source=(string)index["meshes"]["413"];var material=new Material(Resources.Load<Shader>("LoopSort/Shaders/ToySurface")){color=ArtTheme.Hex(0xE8D8C0)};
                try
                {
                    foreach(var old in images.Where(i=>(string)i["file"]=="Source-Meshes/mesh-413.png").ToArray())old.Remove();
                    Render(MeshObject(AssetDatabase.LoadAssetAtPath<Mesh>(source),material,"Source mesh 413"),"Source-Meshes/mesh-413.png","原始网格","413（退化线形网格）",source,60,25);
                }
                finally{Object.DestroyImmediate(material);}
                WriteManifest(true);WriteIndex();EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BatchExport()
        {
            uiOnly=false;SessionState.SetBool("RestoredExport.UIOnly",false);
            popupsOnly=false;SessionState.SetBool("RestoredExport.PopupsOnly",false);
            try
            {
                if(!Application.isBatchMode)throw new InvalidOperationException("Use an isolated batch project");
                string[] args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-imageExportOutput");
                if(arg<0||arg+1>=args.Length)throw new ArgumentException("Missing imageExportOutput");
                output=Path.GetFullPath(args[arg+1]);
                bool reuse=args.Contains("-reuseScreenExports");
                if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any()&&
                    !(reuse&&File.Exists(Path.Combine(output,"manifest.json"))&&(string)JObject.Parse(File.ReadAllText(Path.Combine(output,"manifest.json")))["status"]=="complete"))
                    throw new IOException("Export folder must be new or a completed export from this tool: "+output);
                SessionState.SetBool("RestoredExport.ReuseScreens",reuse);
                Directory.CreateDirectory(output);images=new JArray();
                PlayerSettings.companyName="CodexImageExport";PlayerSettings.productName="LoopSortRestoredImageExport";
                // No preparation/rebuild here: render the restored native assets as they stand.
                var index=JObject.Parse(File.ReadAllText(Native+"index.json"));
                foreach(var entry in ((JObject)index["prefabs"]).Properties())
                {
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>((string)entry.Value);
                    var go=Object.Instantiate(asset);ConfigureGroup(go,entry.Name);
                    foreach(var particle in go.GetComponentsInChildren<ParticleSystem>(true))particle.Simulate(.35f,true,true,true);
                    Render(go,"Models/"+entry.Name+".png","游戏模型",entry.Name,(string)entry.Value,78);
                }
                var neutral=new Material(Resources.Load<Shader>("LoopSort/Shaders/ToySurface")){color=ArtTheme.Hex(0xE8D8C0)};
                try
                {
                    foreach(var entry in ((JObject)index["meshes"]).Properties())
                    {
                        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>((string)entry.Value);var go=MeshObject(mesh,neutral,"Source mesh "+entry.Name);
                        Render(go,"Source-Meshes/mesh-"+entry.Name+".png","原始网格",entry.Name,(string)entry.Value,60,25);
                    }
                }
                finally{Object.DestroyImmediate(neutral);}
                var tracks=((JObject)index["tracks"]).Properties().SelectMany(p=>((JObject)p.Value).Properties().Select(v=>(string)v.Value)).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
                foreach(string path in tracks)
                {
                    string id=Path.GetFileNameWithoutExtension(path);var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    Render(go,"Tracks/track-"+id+".png","轨道布局",id,path,78);
                }
                WriteManifest(false);SessionState.SetString("RestoredExport.Output",output);
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/LoopSort/Scenes/Gameplay.unity");
                EditorApplication.isPlaying=true;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void ConfigureGroup(GameObject go,string id)
        {
            if(id!="lock"&&id!="rope"&&id!="carpet")return;
            var t=go.transform;var left=t.Find("Left");var right=t.Find("Right");
            if(left!=null)left.localPosition=new Vector3(-1.8f,0,0);if(right!=null)right.localPosition=new Vector3(1.8f,0,0);
            var center=t.Find(id=="lock"?"Center":"Scale");if(center!=null)center.localScale=new Vector3(id=="carpet"?5.7f:5,1,1);
            var door=t.Find("Door");if(door!=null)door.localScale=new Vector3(5,1,1);
        }
        static GameObject MeshObject(Mesh mesh,Material material,string name)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.GetComponent<MeshFilter>().sharedMesh=mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials=Enumerable.Repeat(material,Math.Max(1,mesh.subMeshCount)).ToArray();
            return go;
        }
        static void Render(GameObject go,string relative,string category,string label,string source,float pitch,float yaw=0)
        {
            var preview=new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(go);
                foreach(var particle in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particle.Play(false);particle.Simulate(.2f,false,true,true);
                    if(particle.particleCount==0&&particle.emission.rateOverDistance.constantMax>0)
                    {particle.Emit(6);particle.Simulate(.12f,false,false,true);}
                    Debug.Log("Export particle "+particle.name+": "+particle.particleCount);
                }
                var visible=go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&!r.forceRenderingOff&&r.gameObject.activeInHierarchy&&r.sharedMaterials.Any(m=>m!=null&&m.shader.name!="LoopSort0/VolcanoSourceHidden")).ToArray();
                var bounds=visible.Length>0?visible[0].bounds:new Bounds(Vector3.zero,Vector3.one);
                foreach(var r in visible.Skip(1))bounds.Encapsulate(r.bounds);
                if(bounds.size.sqrMagnitude<.0001f)bounds=new Bounds(bounds.center,Vector3.one);
                var cam=preview.camera;cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;
                cam.nearClipPlane=.01f;cam.farClipPlane=2000;cam.allowHDR=false;
                cam.transform.rotation=Quaternion.Euler(pitch,yaw,0);cam.transform.position=bounds.center-cam.transform.forward*Mathf.Max(30,bounds.size.magnitude*2);
                float x=0,y=0;
                for(int i=0;i<8;i++){var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var q=cam.transform.InverseTransformPoint(p);x=Mathf.Max(x,Mathf.Abs(q.x));y=Mathf.Max(y,Mathf.Abs(q.y));}
                cam.orthographicSize=Mathf.Max(x,y)*1.1f;
                preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-30,0);preview.lights[1].intensity=.5f;preview.ambientColor=Color.gray;
                const int size=1024;preview.BeginPreview(new Rect(0,0,size,size),GUIStyle.none);
                var previousTarget=cam.targetTexture;
                var target=RenderTexture.GetTemporary(size,size,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                cam.targetTexture=target;cam.Render();
                var old=RenderTexture.active;RenderTexture.active=target;
                var png=new Texture2D(size,size,TextureFormat.RGBA32,false);
                bool hasPixels=false;
                try{png.ReadPixels(new Rect(0,0,size,size),0,0);png.Apply();hasPixels=png.GetPixels32().Any(p=>p.a>0);if(hasPixels)Save(png,relative);}
                finally{Object.DestroyImmediate(png);RenderTexture.active=old;cam.targetTexture=previousTarget;preview.EndPreview();RenderTexture.ReleaseTemporary(target);}
                if(hasPixels)Add(relative,category,label,source,size,size);
                else Debug.LogWarning("No visible static geometry; omitted empty export: "+source);
            }
            finally{preview.Cleanup();if(go!=null)Object.DestroyImmediate(go);}
        }
        static void Poll()
        {
            if(SessionState.GetBool("RestoredExport.Exit",false)&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.EraseBool("RestoredExport.Exit");EditorApplication.Exit(0);return;}
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            if(string.IsNullOrEmpty(output))output=SessionState.GetString("RestoredExport.Output","");
            if(string.IsNullOrEmpty(output))return;
            try
            {
                var game=GameController.Instance;if(game==null||game.State==null||game.Hud==null)return;game.IsTestRun=true;
                if(!initialized)
                {
                    uiOnly=SessionState.GetBool("RestoredExport.UIOnly",false);
                    popupsOnly=SessionState.GetBool("RestoredExport.PopupsOnly",false);
                    images=(JArray)JObject.Parse(File.ReadAllText(Path.Combine(output,"manifest.json")))["images"];
                    if(popupsOnly)
                    {
                        // Keep layout and capture on the same portrait viewport.
                        // Width-fitted dialogs otherwise size against the Editor's
                        // landscape Game view before the capture changes its aspect.
                        popupTarget=new RenderTexture(1080,1920,24);popupTarget.Create();
                        game.Camera.targetTexture=popupTarget;game.Camera.aspect=1080f/1920;game.Reframe();
                        Canvas.ForceUpdateCanvases();game.Hud.SendMessage("Update");
                        AudioListener.pause=true;
                        game.Progress.language="zh-CN";UiLocalization.Select("zh-CN",false);
                        foreach(var guide in MechanicGuide.All)if(!game.Progress.learnedFeatures.Contains(guide.id))game.Progress.learnedFeatures.Add(guide.id);
                        game.LoadLevel(3);game.ClearMenuSelection();
                    }
                    ExportSprites();if(popupsOnly)ExportAdBreakIcon(game);if(!uiOnly)ExportCargo();BuildScreens(game);initialized=true;
                }
                if(screenIndex>=screens.Count)
                {
                    if(popupTarget!=null){game.Camera.targetTexture=null;popupTarget.Release();Object.DestroyImmediate(popupTarget);popupTarget=null;}
                    WriteManifest(true);WriteIndex();SessionState.EraseString("RestoredExport.Output");SessionState.SetBool("RestoredExport.Exit",true);
                    EditorApplication.isPlaying=false;return;
                }
                var screen=screens[screenIndex];
                if(SessionState.GetBool("RestoredExport.ReuseScreens",false)&&File.Exists(Path.Combine(output,"UI-Screens/"+screen.id+".png")))
                {Add("UI-Screens/"+screen.id+".png","界面截图",screen.label,"实际 GameHud / GameController",1080,1920);screenIndex++;return;}
                if(!started)
                {
                    game.ClearMenuSelection();game.Hud.ShowGameplay();game.SetPaused(false);
                    if(popupsOnly)
                    {
                        game.Progress.language="zh-CN";UiLocalization.Select("zh-CN",false);
                        game.State.Data.status=Core.RunStatus.Playing;game.State.Data.failReason=Core.FailReason.None;
                        typeof(GameController).GetField("resultShown",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(game,true);
                    }
                    screen.show(game);started=true;settled=false;return;
                }
                if(game.IsEnteringLevel)return;
                if(!settled){ready=EditorApplication.timeSinceStartup+1.6;settled=true;return;}
                if(EditorApplication.timeSinceStartup<ready)return;
                game.SetPaused(true);
                if(popupsOnly)
                {
                    var gm=game.Hud.Root.Find("GM");if(gm!=null)gm.gameObject.SetActive(false);
                    var toast=game.Hud.Root.Find("Message plaque");if(toast!=null)toast.gameObject.SetActive(false);
                    game.Hud.SendMessage("Update");
                }
                Canvas.ForceUpdateCanvases();string relative="UI-Screens/"+screen.id+".png";
                string path=Path.Combine(output,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));GameSetup.Capture(path,1080,1920);
                Add(relative,"界面截图",screen.label,"实际 GameHud / GameController",1080,1920);screenIndex++;started=false;
            }
            catch(Exception e){Debug.LogException(e);WriteManifest(false);EditorApplication.Exit(1);}
        }
        static void ExportSprites()
        {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(string path in new[]{"Assets/StreamingAssets/LoopSort/lua/ui/assets.lua","Assets/StreamingAssets/LoopSort/lua/ui/menu_theme.lua","Assets/StreamingAssets/LoopSort/lua/platform/assets.lua"})
                foreach(Match match in Regex.Matches(File.ReadAllText(path),"[A-Za-z][A-Za-z0-9_]*"))names.Add(match.Value);
            var loaded=new Dictionary<string,Texture2D>();
            try
            {
                foreach(string id in names.OrderBy(n=>n,StringComparer.Ordinal))
                {
                    var sprite=ArtTheme.Sprite(id);if(sprite==null)continue;
                    string path=AssetDatabase.GetAssetPath(sprite.texture);if(string.IsNullOrEmpty(path)||!File.Exists(path))continue;
                    if(!loaded.TryGetValue(path,out var original)){original=new Texture2D(2,2,TextureFormat.RGBA32,false);original.LoadImage(File.ReadAllBytes(path));loaded[path]=original;}
                    Rect r=sprite.rect;int x=Mathf.Clamp(Mathf.RoundToInt(r.x/sprite.texture.width*original.width),0,original.width-1),y=Mathf.Clamp(Mathf.RoundToInt(r.y/sprite.texture.height*original.height),0,original.height-1);
                    int width=Mathf.Clamp(Mathf.RoundToInt(r.width/sprite.texture.width*original.width),1,original.width-x),height=Mathf.Clamp(Mathf.RoundToInt(r.height/sprite.texture.height*original.height),1,original.height-y);
                    var cropped=new Texture2D(width,height,TextureFormat.RGBA32,false);
                    try{cropped.SetPixels(original.GetPixels(x,y,width,height));cropped.Apply();Save(cropped,"UI-Elements/"+id+".png");}finally{Object.DestroyImmediate(cropped);}
                    Add("UI-Elements/"+id+".png","独立 UI 图片",id,path,width,height);
                }
            }
            finally{foreach(var texture in loaded.Values)Object.DestroyImmediate(texture);}
        }
        static void ExportCargo()
        {
            var palette=JObject.Parse(File.ReadAllText(ResourcesRoot+"Data/palette.json"));
            foreach(var swatch in palette["colors"])
            {
                int id=(int)swatch["id"];var go=MeshObject(GameAssets.Mesh(421),GameAssets.CargoMaterial,"Cargo "+id);
                var properties=new MaterialPropertyBlock();properties.SetColor("_Color",GameAssets.Color(id));go.GetComponent<Renderer>().SetPropertyBlock(properties);
                Render(go,"Models/cargo-colour-"+id+".png","游戏模型","方块颜色 "+id,ResourcesRoot+"Models/421.obj",78);
            }
        }
        static void ExportAdBreakIcon(GameController game)
        {
            const int size=512;
            var cameraObject=new GameObject("UI icon export camera",typeof(Camera));
            var canvasObject=new GameObject("UI icon export canvas",typeof(RectTransform),typeof(Canvas));
            var target=new RenderTexture(size,size,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;Texture2D png=null;
            try
            {
                var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;
                camera.transform.position=new Vector3(0,0,-10);camera.nearClipPlane=.1f;camera.farClipPlane=100;
                target.Create();camera.targetTexture=target;
                var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera;canvas.planeDistance=5;
                game.Hud.AdBreakArtwork(canvas.transform as RectTransform,Vector2.zero,Vector2.one);
                foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                png=new Texture2D(size,size,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,size,size),0,0);png.Apply();
                var pixels=png.GetPixels32();
                if(!pixels.Any(p=>p.a>0)||!pixels.Any(p=>p.a==0))throw new InvalidOperationException("Pause-banner icon must render with transparency");
                Save(png,"UI-Elements/pause-banner-icon.png");
                Add("UI-Elements/pause-banner-icon.png","独立 UI 图片","暂停横幅图标",ResourcesRoot+"Theme/advert-yellow.png",size,size);
            }
            finally
            {
                RenderTexture.active=previous;if(png!=null)Object.DestroyImmediate(png);
                Object.DestroyImmediate(canvasObject);Object.DestroyImmediate(cameraObject);target.Release();Object.DestroyImmediate(target);
            }
        }
        static void BuildScreens(GameController game)
        {
            if(popupsOnly){BuildPopupScreens();return;}
            foreach(int level in new[]{3,4,7,20,60,81,101,158,201,250,300,334,349})
            {int id=level;screens.Add(new Screen{id="gameplay-"+id,label="游戏关卡 "+id,show=g=>g.LoadLevel(id)});}
            AddScreen("home","主页封面",g=>g.Hud.OnHome?.Invoke());
            AddScreen("settings","设置",g=>g.Hud.Pause(g.Progress));AddScreen("levels","关卡选择",g=>g.Hud.Levels(g.Catalog,3));
            AddScreen("mechanics","机制图鉴",g=>g.Hud.Mechanics());AddScreen("shop","道具商店",g=>g.Hud.Shop());
            AddScreen("areas","地区",g=>g.Hud.Areas());AddScreen("ranking","闯关记录",g=>g.Hud.Ranking());
            AddScreen("teams","队伍页面",g=>g.Hud.Teams());AddScreen("daily","每日练习",g=>g.Hud.DailyPractice());
            AddScreen("result-win","过关弹窗",g=>g.Hud.Result(true));
            AddScreen("daily-signin","七日签到",g=>g.Hud.PeripheralPage("daily_reward"));
            AddScreen("advertising","广告入口",g=>HotUpdate.LuaUi.Render("advertising",g.Hud));
            AddScreen("result-fail","失败弹窗",g=>g.Hud.Result(false));AddScreen("notice","提示弹窗",g=>g.Hud.Notice("图片导出预览"));
            for(int i=0;i<4;i++){int item=i;AddScreen("buy-booster-"+i,"道具购买 "+i,g=>g.Hud.BuyBooster(item));}
            foreach(var guide in MechanicGuide.All){var item=guide;AddScreen("mechanic-"+guide.id,guide.title,g=>g.Hud.FeatureTutorial(item,()=>{},false));}
        }
        static void BuildPopupScreens()
        {
            AddScreen("settings","主页设置",g=>{g.Hud.Home(g.Progress,g.Level.id);g.Hud.Pause(g.Progress);});
            AddScreen("pause","局内暂停",g=>g.Hud.Pause(g.Progress));
            AddScreen("languages","语言选择",g=>HotUpdate.LuaUi.Render("languages",g.Hud));
            foreach(string language in new[]{"en","hi","pt-BR"})
            {
                string code=language;
                AddScreen("settings-"+code,"主页设置 · "+UiLocalization.LanguageName(code),g=>
                {g.Progress.language=code;UiLocalization.Select(code,false);g.Hud.Home(g.Progress,g.Level.id);g.Hud.Pause(g.Progress);});
            }
            AddScreen("revive-offer","看广告复活",g=>{g.State.Data.status=Core.RunStatus.Failed;g.State.Data.failReason=Core.FailReason.ConveyorFull;HotUpdate.LuaUi.Render("revive_offer",g.Hud);});
            AddScreen("result-fail","失败与看广告跳关",g=>{g.State.Data.status=Core.RunStatus.Failed;g.Hud.Result(false);});
            AddScreen("result-win","过关结算",g=>g.Hud.Result(true));
            AddScreen("advertising","暂停横幅广告",g=>HotUpdate.LuaUi.Render("advertising",g.Hud));
            AddScreen("advertising-active","横幅广告暂停中",g=>{g.Progress.bannerPausedUntilUtc=g.UtcSeconds+900;HotUpdate.LuaUi.Render("advertising",g.Hud);});
            foreach(string booster in new[]{"undo","shuffle","belt","truck"})
            {
                string id=booster;AddScreen("booster-reward-"+id,"看广告领取道具 · "+id,g=>HotUpdate.LuaUi.Render("booster_reward",g.Hud,id));
            }
            AddScreen("delete-data","账号与数据删除",g=>HotUpdate.LuaUi.Render("delete_data",g.Hud));
            AddScreen("daily-signin","七日签到",g=>g.Hud.PeripheralPage("daily_reward"));
            AddScreen("daily-signin-claimed","七日签到 · 今日已领取",g=>{g.Progress.lastDailyRewardDay=g.TodayKey;g.Progress.dailyRewardCount=3;g.Hud.PeripheralPage("daily_reward");});
        }
        static void AddScreen(string id,string label,Action<GameController> show)=>screens.Add(new Screen{id=id,label=label,show=show});
        static void Save(Texture2D png,string relative){string path=Path.Combine(output,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,png.EncodeToPNG());}
        static void Add(string file,string category,string label,string source,int width,int height)
        {
            images.Add(JObject.FromObject(new{file,category,label,source,width,height}));
            if(images.Count%25==0)Debug.Log("Restored image export: "+images.Count+" images");
        }
        static void WriteManifest(bool complete)
        {
            File.WriteAllText(Path.Combine(output,"manifest.json"),JsonConvert.SerializeObject(new{status=complete?"complete":"in-progress",images,
                counts=images.GroupBy(i=>(string)i["category"]).ToDictionary(g=>g.Key,g=>g.Count()),apkBuilt=false,sourceArtworkRegenerated=false},Formatting.Indented),new UTF8Encoding(false));
        }
        static void WriteIndex()
        {
            var html=new StringBuilder("<!doctype html><meta charset='utf-8'><title>还原后界面与模型导出</title><style>body{font:15px sans-serif;background:#eee9e1;color:#3d2938;margin:28px}section{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:12px}figure{margin:0;background:white;border-radius:8px;padding:10px;text-align:center}img{width:100%;height:180px;object-fit:contain;background:repeating-conic-gradient(#f1f1f1 0% 25%,#fff 0% 50%) 50%/14px 14px}figcaption{margin-top:8px}a{color:inherit}</style><h1>还原后界面与模型</h1><p>全部来自 Unity 当前资源和界面。点击图片查看原图，名称与来源见 manifest.json。</p>");
            foreach(var group in images.GroupBy(i=>(string)i["category"]))
            {html.Append("<h2>"+group.Key+"（"+group.Count()+"）</h2><section>");foreach(var item in group){string file=((string)item["file"]).Replace('\\','/');html.Append("<figure><a href='"+file+"'><img loading='lazy' src='"+file+"'></a><figcaption>"+(string)item["label"]+"</figcaption></figure>");}html.Append("</section>");}
            File.WriteAllText(Path.Combine(output,"index.html"),html.ToString(),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(output,"README.txt"),"还原后的界面与模型图片导出\n\nUI-Screens：真实界面与代表关卡截图，1080×1920。\nUI-Elements：当前 UI 别名对应的原图精灵切片，保留原始像素和透明通道。\nModels：当前游戏预制体、粒子效果静帧和各颜色方块，1024×1024 透明底。\nSource-Meshes：全部原始模型网格的中性材质几何预览，便于对应底层模型。\nTracks：全部不同轨道布局的透明底预览。\n\nindex.html：图片浏览目录。manifest.json：文件、名称、来源和尺寸。\n未重新生成美术，未构建 APK，未发布。\n",new UTF8Encoding(false));
        }
    }
}
