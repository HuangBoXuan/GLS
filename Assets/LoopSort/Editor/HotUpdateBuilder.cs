using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LoopSort0.HotUpdate;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LoopSort0.Editor
{
    public sealed class HotUpdateBuilder:IPreprocessBuildWithReport
    {
        const string LuaRoot="Assets/StreamingAssets/LoopSort/lua";
        const string EmbeddedManifest="Assets/StreamingAssets/LoopSort/manifest.json";
        const string BindingFile="Assets/LoopSort/Resources/LoopSort/BindingId.json";
        public int callbackOrder=>-100;
        [InitializeOnLoadMethod]
        static void RegisterEditorCatalogRefresh()
        {
            EditorApplication.playModeStateChanged-=RefreshBeforePlay;
            EditorApplication.playModeStateChanged+=RefreshBeforePlay;
        }
        static void RefreshBeforePlay(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.ExitingEditMode)return;
            try {WriteEmbeddedManifest();AssetDatabase.ImportAsset(EmbeddedManifest);}
            catch(Exception error)
            {
                Debug.LogException(error);
                EditorApplication.isPlaying=false;
            }
        }
        [MenuItem("LoopSort/热更/刷新内置 Lua 文件清单")]
        public static void RefreshEmbeddedCatalog()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before refreshing the embedded Lua catalog");
            WriteEmbeddedManifest();
            AssetDatabase.ImportAsset(EmbeddedManifest);
        }
        public static string BindingId()
        {
            string api="SDK="+LuaBindingConfig.SdkVersion+"\nLua="+LuaBindingConfig.LuaVersion
                +"\nMode="+LuaBindingConfig.BindingMode+"\n"+LuaBindingConfig.LinkerContents+"\n"
                +string.Join("\n",LuaBindingConfig.Types.OrderBy(t=>t.FullName,StringComparer.Ordinal).Select(t=>t.FullName+"\n"+
                (t.Namespace!=null&&t.Namespace.StartsWith("LoopSort0",StringComparison.Ordinal)?
                    string.Join("\n",t.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)
                        .Select(m=>m.ToString()+(m is FieldInfo field&&field.IsLiteral?"="+Convert.ToString(field.GetRawConstantValue(),System.Globalization.CultureInfo.InvariantCulture):""))
                        .OrderBy(m=>m,StringComparer.Ordinal)):"")));
            api+="\n"+string.Join("\n",LuaBindingConfig.Delegates.Select(t=>t.ToString()).OrderBy(t=>t,StringComparer.Ordinal));
            return HotContent.Hash(System.Text.Encoding.UTF8.GetBytes(api));
        }
        [MenuItem("LoopSort/热更/1. 刷新独立包桥接清单")]
        public static void GenerateBindings()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before refreshing the standalone bridge manifest");
            // The fixed SDK owns xLua and its static callback bridge. Do not
            // generate partial xLua classes into a different game assembly.
            Directory.CreateDirectory(Path.GetDirectoryName(LuaBindingConfig.LinkerPath));
            File.WriteAllText(LuaBindingConfig.LinkerPath,LuaBindingConfig.LinkerContents);
            File.WriteAllText(BindingFile,JsonConvert.SerializeObject(new {
                id=BindingId(),sdkVersion=LuaBindingConfig.SdkVersion,luaVersion=LuaBindingConfig.LuaVersion,
                bindingMode=LuaBindingConfig.BindingMode,standaloneOnly=true
            },Formatting.Indented));
            WriteEmbeddedManifest();AssetDatabase.Refresh();
            Debug.Log("Standalone reflection bridge manifest updated. Rebuild the APK for this SDK/Lua runtime. This does not register game-specific C# capabilities with the platform host.");
        }
        public static void WriteEmbeddedManifest()
        {
            var files=Directory.GetFiles(LuaRoot,"*.lua",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal)
                .Select(p=>FileInfo(p,"lua/"+Relative(LuaRoot,p))).ToArray();
            if(files.Length==0)throw new BuildFailedException("Lua source directory is empty");
            var manifest=new ContentManifest {version="embedded",versionCode=0,binaryVersion=PlayerSettings.bundleVersion,
                bindingId=ReadBindingId(),channel=ContentChannel(),files=files};
            string text=JsonConvert.SerializeObject(manifest,Formatting.Indented);
            if(!File.Exists(EmbeddedManifest)||File.ReadAllText(EmbeddedManifest)!=text)
                File.WriteAllText(EmbeddedManifest,text);
        }
        [MenuItem("LoopSort/热更/2. 生成 Lua、关卡和图片更新包")]
        public static void BuildContentMenu()=>BuildContent();
        public static string BuildContent()=>Build(false);
        [MenuItem("LoopSort/热更/3. 生成含模型、字体和音频的完整更新包")]
        public static void BuildWithBundlesMenu()=>BuildWithBundles();
        public static string BuildWithBundles()=>Build(true);
        static string Build(bool includeBundles)
        {
            EnsureBindings();WriteEmbeddedManifest();
            int previous=EditorPrefs.GetInt("LoopSort0.HotUpdateVersion",0);
            int code=Math.Max(previous+1,checked((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            EditorPrefs.SetInt("LoopSort0.HotUpdateVersion",code);
            string platform=includeBundles?Platform(EditorUserBuildSettings.activeBuildTarget):"any";
            string output=Path.GetFullPath("Builds/HotUpdate/"+platform+"/"+code);Directory.CreateDirectory(output);
            var files=new List<ContentFile>();
            foreach(string source in Directory.GetFiles(LuaRoot,"*.lua",SearchOption.AllDirectories))
                Copy(source,"lua/"+Relative(LuaRoot,source),output,files);
            string resources="Assets/LoopSort/Resources/LoopSort";
            foreach(string source in Directory.GetFiles(resources,"*.json",SearchOption.AllDirectories))
            {
                string relative=Relative(resources,source);
                if(relative=="HotUpdateConfig.json"||relative=="BindingId.json"||relative=="Data/ui-skin.json")continue;
                string path="data/"+(relative.StartsWith("Data/",StringComparison.Ordinal)?relative.Substring(5):relative);
                Copy(source,path,output,files);
            }
            foreach(string folder in new[]{"Theme","Portraits","Textures"})
                if(Directory.Exists(resources+"/"+folder))foreach(string source in Directory.GetFiles(resources+"/"+folder,"*.png",SearchOption.AllDirectories))
                    Copy(source,"images/"+folder+"/"+Relative(resources+"/"+folder,source),output,files);
            if(includeBundles)
            {
                string destination=Path.Combine(output,"bundles");Directory.CreateDirectory(destination);
                var assets=new List<string>();
                foreach(string dir in new[]{"Models","Fonts","Audio","Shaders"})
                    assets.AddRange(Directory.GetFiles(resources+"/"+dir,"*",SearchOption.AllDirectories)
                        .Where(p=>!p.EndsWith(".meta")&&!p.EndsWith(".txt")&&!p.EndsWith(".md")).Select(p=>p.Replace('\\','/')));
                var definition=new AssetBundleBuild {assetBundleName="resources.bundle",assetNames=assets.ToArray()};
                var result=BuildPipeline.BuildAssetBundles(destination,new[]{definition},BuildAssetBundleOptions.ChunkBasedCompression,EditorUserBuildSettings.activeBuildTarget);
                if(result==null)throw new BuildFailedException("AssetBundle build failed");
                files.Add(FileInfo(destination+"/resources.bundle","bundles/resources.bundle"));
            }
            var manifest=new ContentManifest {version=DateTime.UtcNow.ToString("yyyy.MM.dd.HHmmss"),versionCode=code,
                binaryVersion=PlayerSettings.bundleVersion,bindingId=ReadBindingId(),platform=platform,
                channel=ContentChannel(),files=files.OrderBy(f=>f.path,StringComparer.Ordinal).ToArray()};
            File.WriteAllText(Path.Combine(output,"manifest.json"),JsonConvert.SerializeObject(manifest,Formatting.Indented));
            Debug.Log("Hot update package ready: "+output+". Upload the whole directory and set its manifest HTTPS URL in HotUpdateConfig.json.");
            return output;
        }
        public static void CheckLuaSyntax()
        {
            using(var environment=new XLua.LuaEnv())
                foreach(string source in Directory.GetFiles(LuaRoot,"*.lua",SearchOption.AllDirectories))
                    using(var compiled=environment.LoadString<XLua.LuaFunction>(File.ReadAllText(source),"@"+source)){}
        }
        static void Copy(string source,string relative,string output,List<ContentFile> files)
        {
            if(!HotContent.SafePath(relative))throw new BuildFailedException("Invalid update path "+relative);
            string destination=Path.Combine(output,relative.Replace('/',Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(source,destination,true);files.Add(FileInfo(destination,relative));
        }
        static ContentFile FileInfo(string source,string relative)
        {
            byte[] bytes=File.ReadAllBytes(source);return new ContentFile {path=relative.Replace('\\','/'),size=bytes.LongLength,sha256=HotContent.Hash(bytes)};
        }
        static string Relative(string root,string path)=>path.Substring(root.Length).TrimStart('/','\\').Replace('\\','/');
        static string ReadBindingId()=>File.Exists(BindingFile)?JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(BindingFile))["id"]:"unbound";
        static string ContentChannel()
        {
            var settings=JsonConvert.DeserializeObject<UpdateSettings>(File.ReadAllText("Assets/LoopSort/Resources/LoopSort/HotUpdateConfig.json"));
            return string.IsNullOrWhiteSpace(settings?.channel)?"production":settings.channel;
        }
        static void EnsureBindings()
        {
            if(!File.Exists(LuaBindingConfig.LinkerPath)
                ||File.ReadAllText(LuaBindingConfig.LinkerPath).Replace("\r\n","\n")!=LuaBindingConfig.LinkerContents
                ||ReadBindingId()!=BindingId())
                throw new BuildFailedException("Standalone bridge or SDK runtime changed. Run LoopSort0/热更/1. 刷新独立包桥接清单, then rebuild the APK. Old APKs cannot load content for this bridge.");
        }
        static string Platform(BuildTarget target)=>target==BuildTarget.Android?"android":target==BuildTarget.iOS?"ios":"windows";
        public void OnPreprocessBuild(BuildReport report)
        {
            EnsureBindings();CheckLuaSyntax();WriteEmbeddedManifest();AssetDatabase.Refresh();
            if(report.summary.platform==BuildTarget.Android)PlayerSettings.Android.forceInternetPermission=true;
        }
    }
}
