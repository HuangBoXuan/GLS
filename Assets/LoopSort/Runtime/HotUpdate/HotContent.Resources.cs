using System;
using System.Linq;
using UnityEngine;
using System.IO;

namespace LoopSort0.HotUpdate
{
    public static partial class HotContent
    {
        static void ReleaseResources()
        {
            GameAssets.ReleaseCaches();
            UnloadBundles();foreach(var image in textures.Values)if(image!=null)UnityEngine.Object.Destroy(image);textures.Clear();
        }
        public static byte[] Lua(string module)
        {
            string path="lua/"+module.Replace('.','/')+".lua";
            if(!SafePath(path))return null;
            if(Active!=null)return activeFiles.ContainsKey(path)?File.ReadAllBytes(Path.Combine(releasePath,path)):null;
            return embeddedLua.TryGetValue(path,out var bytes)?bytes:null;
        }
        public static string Text(string resource)
        {
            string relative=resource.StartsWith("LoopSort/",StringComparison.Ordinal)?resource.Substring(9):resource;
            string path=relative.StartsWith("Theme/",StringComparison.Ordinal)?"data/"+relative+".json":"data/"+(relative.StartsWith("Data/")?relative.Substring(5):relative)+".json";
            if(activeFiles.ContainsKey(path))return File.ReadAllText(Path.Combine(releasePath,path));
            return Load<TextAsset>(resource)?.text;
        }
        public static Texture2D Texture(string resource)
        {
            string relative=resource.StartsWith("LoopSort/",StringComparison.Ordinal)?resource.Substring(9):resource;
            string path="images/"+relative+".png";
            if(!activeFiles.ContainsKey(path))return Load<Texture2D>(resource);
            if(textures.TryGetValue(path,out var texture))return texture;
            texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!texture.LoadImage(File.ReadAllBytes(Path.Combine(releasePath,path)),true)){UnityEngine.Object.Destroy(texture);throw new IOException("Invalid hot texture "+path);}
            texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;textures[path]=texture;return texture;
        }
        internal static bool CanUseThemeAtlas(string atlas)
            =>Active==null||(activeFiles.ContainsKey("data/Theme/atlas.json")&&activeFiles.ContainsKey("images/Theme/"+atlas+".png"));
        public static T Load<T>(string resource) where T:UnityEngine.Object
        {
            if(bundleAssets.TryGetValue(resource,out string bundle))return bundles[bundle].LoadAsset<T>(assetNames[resource]);
            return Resources.Load<T>(resource);
        }
        public static System.Collections.IEnumerator Preload<T>(string resource) where T:UnityEngine.Object
        {
            if(bundleAssets.TryGetValue(resource,out string bundle))yield return bundles[bundle].LoadAssetAsync<T>(assetNames[resource]);
            else yield return Resources.LoadAsync<T>(resource);
        }
        static void LoadBundles()
        {
            foreach(var entry in activeFiles.Values.Where(f=>f.path.StartsWith("bundles/")))
            {
                var bundle=AssetBundle.LoadFromFile(Path.Combine(releasePath,entry.path));
                if(bundle==null)throw new IOException("Cannot load asset bundle "+entry.path);
                bundles[entry.path]=bundle;
                foreach(string name in bundle.GetAllAssetNames())
                {
                    int start=name.IndexOf("/resources/",StringComparison.OrdinalIgnoreCase);if(start<0)continue;
                    string resource=name.Substring(start+11);resource=resource.Substring(0,resource.Length-Path.GetExtension(resource).Length);
                    bundleAssets[resource]=entry.path;assetNames[resource]=name;
                }
            }
        }
        static void UnloadBundles(){foreach(var b in bundles.Values)b.Unload(true);bundles.Clear();bundleAssets.Clear();assetNames.Clear();}
    }
}
