using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using XLua;
using Object=UnityEngine.Object;

namespace LoopSort0.Editor
{
    // Explicit project-local requests only. No gameplay starts without a
    // capture request, and installation never publishes or creates an APK.
    [InitializeOnLoad]
    public static class VolcanoArtSetup
    {
        const string Theme="Assets/LoopSort/Resources/LoopSort/Theme/";
        const string Data="Assets/LoopSort/Resources/LoopSort/Data/";
        const string Request="Design/MechanismUI/volcano-request.json";
        const string Result="Design/MechanismUI/volcano-result.json";
        static readonly string[] Ids={"cave","barrier","crust","gate","blanket","lid","tag","hopper","ore"};
        static bool busy;
        static double next;
        static VolcanoArtSetup(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(SessionState.GetBool("VolcanoArt.ExitBatch",false)&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {SessionState.EraseBool("VolcanoArt.ExitBatch");EditorApplication.Exit(0);return;}
            if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<next||!File.Exists(Request))return;
            next=EditorApplication.timeSinceStartup+.4;
            try
            {
                var request=JObject.Parse(File.ReadAllText(Request));string action=(string)request["action"];
                if(action=="install")
                {
                    if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
                    busy=true;Install();File.Delete(Request);
                }
                else if(action=="capture")Capture(request);
                else if(action=="check")
                {
                    var errors=ShaderErrors();File.WriteAllText(Result,JsonConvert.SerializeObject(new{status="checked",shaderErrors=errors},Formatting.Indented));File.Delete(Request);
                }
                else throw new InvalidOperationException("Unknown volcano art action "+action);
            }
            catch(Exception e){File.WriteAllText(Result,JsonConvert.SerializeObject(new{status="failed",error=e.ToString()},Formatting.Indented));if(File.Exists(Request))File.Delete(Request);Debug.LogException(e);}
            finally{busy=false;}
        }
        [MenuItem("LoopSort/Art/Install volcanic mine art")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Art installation requires Edit Mode");
            var bounds=new Dictionary<string,float[]>();var sizes=new List<object>();
            var atlas=JsonConvert.DeserializeObject<Dictionary<string,float[][]>>(File.ReadAllText(Theme+"atlas.json"));
            foreach(string id in Ids)
            {
                string path=Theme+"mine-"+id+".png";if(!File.Exists(path))throw new FileNotFoundException("Missing final art",path);
                var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
                if(!image.LoadImage(File.ReadAllBytes(path)))throw new Exception("Invalid PNG "+path);
                int minX=image.width,minY=image.height,maxX=-1,maxY=-1;var pixels=image.GetPixels32();
                for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)if(pixels[y*image.width+x].a>32)
                {minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
                if(maxX<0)throw new Exception("Empty art "+id);
                minX=Math.Max(0,minX-1);minY=Math.Max(0,minY-1);maxX=Math.Min(image.width-1,maxX+1);maxY=Math.Min(image.height-1,maxY+1);
                float[] rect={(float)minX/image.width,(float)minY/image.height,(float)(maxX-minX+1)/image.width,(float)(maxY-minY+1)/image.height};
                bounds[id]=rect;atlas["mine-"+id]=new[]{rect};sizes.Add(new{id,path,width=image.width,height=image.height,alphaCorner=pixels[0].a});Object.DestroyImmediate(image);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=id!="ore";importer.mipmapEnabled=false;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=1024;importer.filterMode=FilterMode.Bilinear;
                importer.wrapMode=id=="ore"?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            WriteJson(Data+"volcano-art.json",bounds);WriteJson(Theme+"atlas.json",atlas);
            AssetDatabase.ImportAsset(Data+"volcano-art.json",ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(Theme+"atlas.json",ImportAssetOptions.ForceSynchronousImport);
            VolcanoArt.Release();
            var cargo=AssetDatabase.LoadAssetAtPath<Material>("Assets/LoopSort/Resources/LoopSort/Shaders/CargoInstanced.mat");
            cargo.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Theme+"mine-ore.png");EditorUtility.SetDirty(cargo);
            RewriteGuideCopy();
            int syntax=0;
            using(var lua=new LuaEnv())foreach(string path in Directory.GetFiles("Assets/StreamingAssets/LoopSort/lua","*.lua",SearchOption.AllDirectories))
            {using(var chunk=lua.LoadString(File.ReadAllText(path),"@"+path)){}syntax++;}
            HotUpdateBuilder.WriteEmbeddedManifest();
            AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var native=PlatformAssetBuilder.UpdateVolcanoSkins("loopsort0");
            var shaders=ShaderErrors();if(shaders.Length>0)throw new Exception(string.Join("\n",shaders));
            WriteJson(Result,new{status="installed",assets=sizes,luaSyntaxFiles=syntax,shaderErrors=shaders,nativeIndex=native.IndexPath,gameplayRulesChanged=false,apkBuilt=false,published=false});
            File.Copy(Result,"Design/MechanismUI/volcano-install-result.json",true);
            Debug.Log("Volcanic mine artwork installed in both scene and native module assets.");
        }
        static string[] ShaderErrors()
        {
            return new[]{"VolcanoDecal","VolcanoSourceHidden","InstancedCargo","HiddenCargo"}.SelectMany(id=>
                ShaderUtil.GetShaderMessages(AssetDatabase.LoadAssetAtPath<Shader>("Assets/LoopSort/Resources/LoopSort/Shaders/"+id+".shader"))
                .Where(m=>m.severity.ToString()=="Error").Select(m=>id+": "+m.message)).ToArray();
        }
        static void RewriteGuideCopy()
        {
            var guides=JObject.Parse(File.ReadAllText(Data+"mechanic-guides.json"));
            var names=new Dictionary<int,string>{{20,"传送洞"},{22,"隐藏块"},{21,"彩色盖"},{2,"单向闸"},{28,"连接栓"},{4,"熔岩壳"},{23,"钥匙闸"},{24,"专色车"},{25,"束缚链"},{26,"覆盖毯"},{27,"补给仓"}};
            foreach(JObject guide in guides["guides"])
            {
                guide["title"]=names[(int)guide["id"]];
                string text=(string)guide["description"];
                foreach(var pair in new[]{Tuple.Create("传送门","矿洞"),Tuple.Create("货车","矿车"),Tuple.Create("方块","矿石"),Tuple.Create("幕布","封盖"),Tuple.Create("冰冻","岩壳封住"),Tuple.Create("破冰","击碎岩壳"),Tuple.Create("绳索","铁链"),Tuple.Create("地毯","隔热毯"),Tuple.Create("螺丝","连接栓"),Tuple.Create("卷帘门","钥匙闸"),Tuple.Create("生成器","出料仓")})text=text.Replace(pair.Item1,pair.Item2);
                guide["description"]=text;
            }
            WriteJson(Data+"mechanic-guides.json",guides);
        }
        static void Capture(JObject request)
        {
            if(!EditorApplication.isPlaying){EditorApplication.isPlaying=true;return;}
            var game=GameController.Instance;if(game==null||game.State==null||game.Hud==null)return;
            if(Application.isBatchMode)game.IsTestRun=true;
            int level=(int?)request["level"]??4;
            if((bool?)request["loaded"]!=true)
            {
                game.LoadLevel(level);game.ClearMenuSelection();game.Hud.ShowGameplay();game.SetPaused(false);
                request["loaded"]=true;request["readyAt"]=EditorApplication.timeSinceStartup+4;WriteJson(Request,request);return;
            }
            if(EditorApplication.timeSinceStartup<((double?)request["readyAt"]??0)||game.IsEnteringLevel)return;
            if((bool?)request["settled"]!=true)
            {
                game.ClearMenuSelection();game.Hud.ShowGameplay();game.SetPaused(false);
                request["settled"]=true;request["readyAt"]=EditorApplication.timeSinceStartup+2;WriteJson(Request,request);return;
            }
            game.ClearMenuSelection();game.Hud.ShowGameplay();game.SetPaused(true);
            string directory=Path.GetFullPath((string)request["directory"]??"Design/MechanismUI/Applied");Directory.CreateDirectory(directory);
            int width=(int?)request["width"]??1080,height=(int?)request["height"]??1920;
            string path=GameSetup.Capture(Path.Combine(directory,"level-"+level+".png"),width,height);
            var art=Object.FindObjectsOfType<MeshRenderer>().Where(r=>r.name==VolcanoArt.ArtName).ToArray();
            var receipt=JObject.FromObject(new{status="captured",level,path,worldArtCount=art.Length,artMaterials=art.Select(r=>r.sharedMaterial.name).Distinct().ToArray(),state=GameSetup.InspectRun()});
            if((bool?)request["captureHome"]==true)
            {
                game.Hud.Home(game.Progress,level);
                receipt["homePath"]=GameSetup.Capture(Path.Combine(directory,"home.png"),width,height);
                request["captureHome"]=false;game.Hud.ShowGameplay();
            }
            int guideId=(int?)request["captureGuide"]??0;
            if(guideId>0)
            {
                game.Hud.FeatureTutorial(MechanicGuide.All.First(g=>g.id==guideId),()=>{},false);
                receipt["guidePath"]=GameSetup.Capture(Path.Combine(directory,"guide-"+guideId+".png"),width,height);
                request["captureGuide"]=0;game.Hud.ShowGameplay();
            }
            if((bool?)request["captureSignin"]==true)
            {
                DailySigninPreview.Capture(game,directory,width,height);
                request["captureSignin"]=false;
            }
            WriteJson(Result,receipt);
            string historyPath=Path.Combine(directory,"captures.json");
            var history=File.Exists(historyPath)?JArray.Parse(File.ReadAllText(historyPath)):new JArray();history.Add(receipt);WriteJson(historyPath,history);
            var queue=request["queue"] as JArray;
            if(queue!=null&&queue.Count>0)
            {
                request["level"]=queue[0];queue.RemoveAt(0);request["loaded"]=false;request["settled"]=false;WriteJson(Request,request);game.SetPaused(false);return;
            }
            File.Delete(Request);
            if((bool?)request["quitAfter"]==true){SessionState.SetBool("VolcanoArt.ExitBatch",true);EditorApplication.isPlaying=false;}
            else game.SetPaused(false);
        }
        public static void BatchBackgroundPreview()
        {
            try
            {
                if(!Application.isBatchMode)throw new InvalidOperationException("Background preview requires an isolated batch editor");
                PlayerSettings.companyName="CodexArtPreview";PlayerSettings.productName="LoopSortBackgroundPreview";
                bool portrait750=Environment.GetCommandLineArgs().Contains("-portrait750");
                bool homeArt=Environment.GetCommandLineArgs().Contains("-homeMinecarts");
                bool signin=Environment.GetCommandLineArgs().Contains("-dailySignin");
                int width=portrait750?750:1080,height=portrait750?1650:1920;
                string directory=portrait750?"Design/MechanismUI/Background750x1650":"Design/MechanismUI/VolcanicBackground";
                if(homeArt)directory="Design/MechanismUI/HomeMinecarts/"+(portrait750?"750x1650":"1080x1920");
                if(signin){directory="Design/MechanismUI/DailySignin/"+(portrait750?"750x1650":"1080x1920");DailySigninPreview.Check();}
                if(homeArt)VolcanoModelPreview.UpdateArtworkBounds();
                AssetDatabase.ImportAsset(Theme+"candy-factory-board.png",ImportAssetOptions.ForceSynchronousImport);
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/LoopSort/Scenes/Gameplay.unity");
                WriteJson(Request,new{action="capture",level=3,queue=homeArt||signin?Array.Empty<int>():new[]{201},captureHome=true,captureGuide=homeArt?24:0,captureSignin=signin,directory,width,height,quitAfter=true});
                EditorApplication.isPlaying=true;
            }
            catch(Exception e){WriteJson(Result,new{status="failed",error=e.ToString()});Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BatchInstallAndPreview()
        {
            try
            {
                if(!Application.isBatchMode)throw new InvalidOperationException("Batch preview requires an isolated batch editor");
                PlayerSettings.companyName="CodexArtPreview";PlayerSettings.productName="LoopSortVolcanoPreview";
                Install();
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/LoopSort/Scenes/Gameplay.unity");
                WriteJson(Request,new{action="capture",level=4,queue=new[]{7,20,60,101,158,201,250,300,334,349},quitAfter=true});
                EditorApplication.isPlaying=true;
            }
            catch(Exception e){WriteJson(Result,new{status="failed",error=e.ToString()});Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void WriteJson(string path,object value){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonConvert.SerializeObject(value,Formatting.Indented)+"\n",new System.Text.UTF8Encoding(false));}
    }
}
