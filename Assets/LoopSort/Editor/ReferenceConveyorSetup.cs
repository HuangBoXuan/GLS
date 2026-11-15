using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LoopSort0.Editor
{
    public static class ReferenceConveyorSetup
    {
        public static void BatchPreview()=>Run(false);
        public static void BatchInstallAndPreview()=>Run(true);
        static void Run(bool install)
        {
            try
            {
                if(!Application.isBatchMode)throw new InvalidOperationException("Run the preview in an isolated batch project");
                PlayerSettings.companyName="CodexArtPreview";PlayerSettings.productName="LoopSortConveyorPreview";
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/LoopSort/Resources/LoopSort/Shaders/ReferenceConveyor.shader");
                var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").Select(m=>m.message).ToArray();
                if(errors.Length>0)throw new Exception(string.Join("\n",errors));
                var before=CollisionHashes();
                if(install)PlatformAssetBuilder.UpdateReferenceTracks("loopsort0");
                var after=CollisionHashes();
                if(!before.SequenceEqual(after))throw new Exception("Track collision assets changed");
                Directory.CreateDirectory("Design/MechanismUI/TrackApplied");
                File.WriteAllText("Design/MechanismUI/TrackApplied/installation.json",JsonConvert.SerializeObject(new{
                    status=install?"installed":"previewed",shaderErrors=errors,unchangedCollisionAssets=before.Length,
                    nativeTracksUpdated=install,ordinaryCargo="original colour palette, no ore texture",apkBuilt=false},Formatting.Indented));
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/LoopSort/Scenes/Gameplay.unity");
                File.WriteAllText("Design/MechanismUI/volcano-request.json",JsonConvert.SerializeObject(new{
                    action="capture",level=3,queue=new[]{4,81,349},directory="Design/MechanismUI/TrackApplied",quitAfter=true}));
                EditorApplication.isPlaying=true;
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        static string[] CollisionHashes()
        {
            return Directory.GetFiles("Assets/LoopSort/PlatformAssets/Tracks","*-collision.asset").OrderBy(p=>p,StringComparer.Ordinal)
                .Select(path=>{using(var sha=SHA256.Create())return path+":"+Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}).ToArray();
        }
    }
}
