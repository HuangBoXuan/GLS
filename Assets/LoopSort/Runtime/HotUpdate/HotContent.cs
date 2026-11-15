using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace LoopSort0.HotUpdate
{
    // Complete immutable releases. Only the pointer changes after every file is verified.
    public static partial class HotContent
    {
        public const int BridgeApi=1;
        static readonly Dictionary<string,byte[]> embeddedLua=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        static readonly Dictionary<string,ContentFile> activeFiles=new Dictionary<string,ContentFile>(StringComparer.Ordinal);
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly Dictionary<string,AssetBundle> bundles=new Dictionary<string,AssetBundle>();
        static readonly Dictionary<string,string> bundleAssets=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string,string> assetNames=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        public static string Status {get;private set;}="正在准备游戏…";
        public static float PreparationProgress {get;private set;}
        public static bool Busy {get;private set;}
        public static bool UpdateDownloaded {get;private set;}
        public static ContentManifest Active {get;private set;}
        public static UpdateSettings Settings {get;private set;}
        static string releasePath;
        static int installedVersion;
        static string Root=>Path.Combine(Application.persistentDataPath,"LoopSortHotUpdate");
        static string Pointer=>Path.Combine(Root,"active.json");
        static string Previous=>Path.Combine(Root,"previous.json");
        [Serializable] sealed class Installed {public string directory,manifestHash;}
        static string Platform=>Application.platform==RuntimePlatform.Android?"android":Application.platform==RuntimePlatform.IPhonePlayer?"ios":"windows";

        public static IEnumerator Prepare()
        {
            Reset();
            var settingsAsset=Resources.Load<TextAsset>("LoopSort/HotUpdateConfig");
            Settings=settingsAsset!=null?JsonConvert.DeserializeObject<UpdateSettings>(settingsAsset.text):null;
            Settings=Settings??new UpdateSettings();
            if(string.IsNullOrWhiteSpace(Settings.channel))Settings.channel="production";
            ContentManifest packaged;
            using(var manifestRequest=UnityWebRequest.Get(StreamingUrl("manifest.json")))
            {
                yield return manifestRequest.SendWebRequest();
                if(manifestRequest.result!=UnityWebRequest.Result.Success)throw new IOException("Cannot load packaged Lua manifest: "+manifestRequest.error);
                packaged=JsonConvert.DeserializeObject<ContentManifest>(manifestRequest.downloadHandler.text);
            }
            if(packaged==null||packaged.files==null)throw new IOException("Missing packaged Lua catalog");
            PreparationProgress=.05f;
            foreach(var file in packaged.files)
            {
                if(file==null||!SafePath(file.path)||!file.path.StartsWith("lua/",StringComparison.Ordinal)||embeddedLua.ContainsKey(file.path))throw new IOException("Invalid packaged Lua path");
                using(var request=UnityWebRequest.Get(StreamingUrl(file.path)))
                {
                    yield return request.SendWebRequest();
                    if(request.result!=UnityWebRequest.Result.Success)throw new IOException("Packaged Lua missing: "+file.path);
                    byte[] bytes=request.downloadHandler.data;
                    if(!Valid(bytes,file))throw new IOException("Packaged Lua hash mismatch: "+file.path);
                    embeddedLua[file.path]=bytes;
                    PreparationProgress=.05f+.65f*embeddedLua.Count/Math.Max(1,packaged.files.Length);
                }
            }
            try {LoadPointer(Pointer);}catch(Exception error)
            {
                Debug.LogWarning("Discarding incomplete hot content: "+error.Message);
                RestorePrevious();
            }
            PreparationProgress=.72f;
            yield return Check(false);
            PreparationProgress=.97f;
            try {LoadBundles();}catch(Exception error){Debug.LogWarning("Asset update rolled back: "+error.Message);Rollback();}
            Status="准备完成";PreparationProgress=1;
        }
        static string StreamingUrl(string path)
        {
            string location=Application.streamingAssetsPath+"/LoopSort/"+path;
            return location.Contains("://")?location:new Uri(location).AbsoluteUri;
        }
        public static IEnumerator Check(bool deferActivation)
        {
            if(Busy)yield break;Busy=true;UpdateDownloaded=false;
            var work=Download(deferActivation);string failure=null;
            try
            {
                while(true)
                {
                    bool more;
                    try {more=work.MoveNext();}catch(Exception error){failure=error.Message;break;}
                    if(!more)break;yield return work.Current;
                }
            }
            finally {try {(work as IDisposable)?.Dispose();}finally {Busy=false;}}
            if(failure!=null){Debug.LogWarning("Hot update retained current content: "+failure);Status="暂时无法获取更新，继续使用当前内容";}
        }
        static IEnumerator Download(bool deferActivation)
        {
            if(string.IsNullOrWhiteSpace(Settings.manifestUrl)){Status=deferActivation?"暂未开放在线更新":"准备完成";yield break;}
            var manifestUri=new Uri(Settings.manifestUrl);
            bool localDevelopment=Application.isEditor&&(manifestUri.IsLoopback||manifestUri.IsFile);
            if(manifestUri.Scheme!="https"&&!localDevelopment)throw new IOException("Remote hot content requires HTTPS");
            Status="正在检查更新…";
            byte[] manifestBytes;
            using(var request=UnityWebRequest.Get(manifestUri.AbsoluteUri))
            {
                request.timeout=Math.Max(1,Settings.timeoutSeconds);yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success)throw new IOException(request.error);
                manifestBytes=request.downloadHandler.data;
            }
            if(manifestBytes.Length>1024*1024)throw new IOException("Manifest too large");
            var manifest=JsonConvert.DeserializeObject<ContentManifest>(System.Text.Encoding.UTF8.GetString(manifestBytes));
            ValidateManifest(manifest);
            if(manifest.versionCode<=Math.Max(Active?.versionCode??0,installedVersion))
            {Status=installedVersion>(Active?.versionCode??0)?"更新已下载，重新启动游戏后生效":"当前已是最新版本";yield break;}
            string stage=Path.Combine(Root,"releases","stage-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);bool committed=false;
            try
            {
                long downloaded=0,total=manifest.files.Sum(f=>f.size);
                foreach(var file in manifest.files)
                {
                    string relative=string.Join("/",file.path.Split('/').Select(Uri.EscapeDataString));
                    using(var request=UnityWebRequest.Get(new Uri(manifestUri,relative).AbsoluteUri))
                    {
                        request.timeout=Math.Max(1,Settings.timeoutSeconds);var pending=request.SendWebRequest();
                        while(!pending.isDone)
                        {
                            if(request.downloadedBytes>(ulong)file.size)throw new IOException("File larger than declared: "+file.path);
                            Status="正在下载更新 "+Mathf.FloorToInt((downloaded+Math.Min((long)request.downloadedBytes,file.size))*100f/Math.Max(1,total))+"%";
                            if(!deferActivation)PreparationProgress=.75f+.20f*(downloaded+Math.Min((long)request.downloadedBytes,file.size))/Math.Max(1,total);
                            yield return null;
                        }
                        if(request.result!=UnityWebRequest.Result.Success)throw new IOException(file.path+": "+request.error);
                        byte[] bytes=request.downloadHandler.data;
                        if(!Valid(bytes,file))throw new IOException("Content hash/size mismatch: "+file.path);
                        string destination=Path.Combine(stage,file.path.Replace('/',Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));File.WriteAllBytes(destination,bytes);downloaded+=file.size;
                    }
                }
                File.WriteAllBytes(Path.Combine(stage,"manifest.json"),manifestBytes);
                string directory="release-"+manifest.versionCode+"-"+Guid.NewGuid().ToString("N");
                string installed=Path.Combine(Root,"releases",directory);Directory.Move(stage,installed);
                var pointer=new Installed {directory=directory,manifestHash=Hash(manifestBytes)};
                Directory.CreateDirectory(Root);string temporary=Pointer+".new";
                File.WriteAllText(temporary,JsonConvert.SerializeObject(pointer));
                if(File.Exists(Pointer))File.Replace(temporary,Pointer,deferActivation&&installedVersion>(Active?.versionCode??0)?null:Previous);else File.Move(temporary,Pointer);
                committed=true;UpdateDownloaded=true;installedVersion=manifest.versionCode;
                if(!deferActivation)Activate(installed,manifest);
                Status=deferActivation?"更新已下载，重新启动游戏后生效":"更新完成";
            }
            finally
            {
                if(!committed&&Directory.Exists(stage))
                    try {DeleteOwnDirectory(stage);}catch(Exception error){Debug.LogWarning("Cannot clean update staging directory: "+error.Message);}
            }
        }
        static void ValidateManifest(ContentManifest manifest)
        {
            if(manifest==null||manifest.schema!=1||manifest.bridgeApi!=BridgeApi||manifest.binaryVersion!=Application.version||manifest.channel!=Settings.channel)
                throw new IOException("Content incompatible with installed player");
            string binding=JsonConvert.DeserializeObject<Dictionary<string,string>>(Resources.Load<TextAsset>("LoopSort/BindingId").text)["id"];
            if(manifest.bindingId!=binding)throw new IOException("Lua bridge bindings do not match this player");
            if(manifest.platform!="any"&&manifest.platform!=Platform)throw new IOException("Asset bundle platform mismatch");
            if(manifest.versionCode<1||manifest.files==null||manifest.files.Length==0||manifest.files.Length>2048)throw new IOException("Invalid content manifest");
            var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
            foreach(var file in manifest.files)
            {
                if(file==null||!SafePath(file.path)||!paths.Add(file.path)||file.size<0||file.sha256==null||file.sha256.Length!=64)
                    throw new IOException("Invalid content file");
                total=checked(total+file.size);
            }
            if(total>Math.Max(1,Settings.maxDownloadMegabytes)*1024L*1024)throw new IOException("Content exceeds configured size limit");
            foreach(string module in embeddedLua.Keys)if(!paths.Contains(module))throw new IOException("Update must include all packaged Lua modules: "+module);
        }
        static void LoadPointer(string pointerPath)
        {
            if(!File.Exists(pointerPath)){ClearActive();return;}
            var installed=JsonConvert.DeserializeObject<Installed>(File.ReadAllText(pointerPath));
            if(installed==null||!System.Text.RegularExpressions.Regex.IsMatch(installed.directory??"","^release-[0-9]+-[a-f0-9]{32}$"))throw new IOException("Invalid release pointer");
            string path=Path.Combine(Root,"releases",installed.directory);byte[] bytes=File.ReadAllBytes(Path.Combine(path,"manifest.json"));
            if(Hash(bytes)!=installed.manifestHash)throw new IOException("Manifest checksum mismatch");
            var manifest=JsonConvert.DeserializeObject<ContentManifest>(System.Text.Encoding.UTF8.GetString(bytes));ValidateManifest(manifest);
            foreach(var file in manifest.files)if(!Valid(File.ReadAllBytes(Path.Combine(path,file.path.Replace('/',Path.DirectorySeparatorChar))),file))throw new IOException("Installed content damaged");
            Activate(path,manifest);
        }
        static void Activate(string path,ContentManifest manifest)
        {
            activeFiles.Clear();releasePath=path;Active=manifest;installedVersion=manifest.versionCode;
            foreach(var file in manifest.files)activeFiles[file.path]=file;
        }
        static void ClearActive(){Active=null;releasePath=null;installedVersion=0;activeFiles.Clear();}
        public static void Rollback()
        {
            ReleaseResources();RestorePrevious();
            try {LoadBundles();}catch(Exception error)
            {
                Debug.LogWarning("Previous asset release also failed: "+error.Message);
                ReleaseResources();ClearActive();if(File.Exists(Pointer))File.Delete(Pointer);
            }
        }
        static void RestorePrevious()
        {
            try {LoadPointer(Previous);if(Active!=null)File.Copy(Previous,Pointer,true);else if(File.Exists(Pointer))File.Delete(Pointer);}
            catch {ClearActive();if(File.Exists(Pointer))File.Delete(Pointer);}
            // Consume the fallback so a second startup failure reaches embedded content.
            finally {if(File.Exists(Previous))File.Delete(Previous);}
        }
        internal static void Reset()
        {
            ReleaseResources();ClearActive();embeddedLua.Clear();
            Busy=false;UpdateDownloaded=false;Settings=null;Status="正在准备游戏…";PreparationProgress=0;
        }
        public static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        static bool Valid(byte[] bytes,ContentFile file)=>bytes!=null&&bytes.LongLength==file.size&&Hash(bytes)==file.sha256;
        public static bool SafePath(string path)
        {
            if(string.IsNullOrEmpty(path)||path.Contains("..")||path.Contains('\\')||path.Contains(':')||path.StartsWith("/")||path.Contains("//"))return false;
            return path.StartsWith("lua/")&&path.EndsWith(".lua")||path.StartsWith("data/")&&path.EndsWith(".json")||
                path.StartsWith("images/")&&path.EndsWith(".png")||path.StartsWith("bundles/")&&path.EndsWith(".bundle");
        }
        static void DeleteOwnDirectory(string directory)
        {
            string root=Path.GetFullPath(Path.Combine(Root,"releases"))+Path.DirectorySeparatorChar;
            string resolved=Path.GetFullPath(directory);
            if(!resolved.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("Cache path escaped release directory");
            Directory.Delete(resolved,true);
        }
    }
}
