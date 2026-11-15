using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LoopSort0.Editor
{
    public static class LuaAndroidBuild
    {
        [MenuItem("LoopSort/热更/4. 构建 Lua Android APK")]
        public static void BuildMenu()=>Build(Path.GetFullPath("Builds/Android/LoopSort-Lua-arm64.apk"));

        // Used by a separate batch-mode checkout so an open editor need not close.
        public static void BuildFromCommandLine()
        {
            string[] args=Environment.GetCommandLineArgs();int option=Array.IndexOf(args,"-loopSortOutput");
            string output=option>=0&&option+1<args.Length?args[option+1]:Path.GetFullPath("Builds/Android/LoopSort-Lua-arm64.apk");
            Build(output);
        }
        public static void RefreshBridgeAndBuildFromCommandLine()
        {
            HotUpdateBuilder.GenerateBindings();
            BuildFromCommandLine();
        }
        public static void BuildGm(string output)=>Build(output,true);
        static void Build(string output)=>Build(output,false);
        static void Build(string output,bool gm)
        {
            if(EditorApplication.isPlaying)throw new BuildFailedException("Exit Play Mode before building an APK.");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion=(AndroidSdkVersions)36;
            PlayerSettings.Android.forceInternetPermission=true;
            PlayerSettings.Android.bundleVersionCode=Math.Max(PlayerSettings.Android.bundleVersionCode,(int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds()/10));
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            string identifier=PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            if(string.IsNullOrEmpty(identifier)||identifier=="com.Company.ProductName")
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,"com.LoopSort0.LoopSort");
            string[] scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            if(scenes.Length==0)throw new BuildFailedException("No enabled build scenes.");
            output=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(output));
            AssetDatabase.SaveAssets();
            // Export with Unity's compiler, then package using the current ads
            // SDK's JDK/Gradle requirements without replacing Unity's bundled tools.
            string export=Path.Combine(Path.GetDirectoryName(output),"Gradle");
            bool previousExport=EditorUserBuildSettings.exportAsGoogleAndroidProject;
            try
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject=true;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=scenes,locationPathName=export,target=BuildTarget.Android,
                    options=(gm?BuildOptions.Development:BuildOptions.None)|BuildOptions.AcceptExternalModificationsToPlayer
                });
                if(report==null||report.summary.result!=BuildResult.Succeeded)
                    throw new BuildFailedException("Unity Android export failed: "+(report==null?"no report; see the Editor log":report.summary.result.ToString()));
                AndroidGradleCompatibility.Package(export,output,gm);
            }
            finally{EditorUserBuildSettings.exportAsGoogleAndroidProject=previousExport;}
            Debug.Log("LUA_APK_BUILD_SUCCEEDED "+output+" bytes="+new FileInfo(output).Length);
        }
    }
}
