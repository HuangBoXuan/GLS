using System;
using System.IO;
using LoopSort.ProductIntegration;
using LoopSort.ProductIntegration.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LoopSort0.Editor
{
    // Isolated review builds use the same automatic startup as the normal player.
    public static class StandalonePlayerBuild
    {
        public static void Prepare()
        {
            if(!Application.isBatchMode)throw new BuildFailedException("Run Tools/build_android_player.ps1 in an isolated project.");
            ProductBuildInputs.ApplyLocal();
            var config=ProductConfiguration.LoadBundled();var missing=config.MissingAdIntegrationFields();
            if(missing.Count!=0)throw new BuildFailedException("Advertising input missing: "+string.Join(", ",missing));
            PlayerSettings.productName="LoopSort";
            AppLovinSettings.Instance.QualityServiceEnabled=false;AppLovinSettings.Instance.SaveAsync();
            // Android UMP is gathered by the player before MAX initialization.
            var privacy=AppLovinMax.Scripts.IntegrationManager.Editor.AppLovinInternalSettings.Instance;
            privacy.ConsentFlowEnabled=false;
            privacy.ConsentFlowPrivacyPolicyUrl=config.privacyPolicyUrl;privacy.ConsentFlowTermsOfServiceUrl=config.termsUrl;
            privacy.DebugUserGeography=MaxSdkBase.ConsentFlowUserGeography.Unknown;privacy.Save();
            EditorUserBuildSettings.development=true;PlayerSettings.Android.useCustomKeystore=false;
            PlayerSettings.Android.forceInternetPermission=true;PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            ProductAndroidDependencies.Resolve();HotUpdateBuilder.GenerateBindings();AssetDatabase.SaveAssets();
            Debug.Log("LOOPSORT_PLAYER_PREPARED");
        }
        public static void Build()
        {
            if(!Application.isBatchMode)throw new BuildFailedException("Use an isolated batch project.");
            PlayerSettings.productName="LoopSort";
            if(Directory.Exists("Assets/LoopSort/AdIntegrationTest")||Directory.Exists("Assets/Resources/LoopSortAdTest"))
                throw new BuildFailedException("Remove obsolete diagnostic code and resources.");
            if(ProductConfiguration.LoadBundled().MissingAdIntegrationFields().Count!=0)throw new BuildFailedException("Advertising input is incomplete.");
            DailySigninPreview.Check();
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-loopSortOutput");
            if(index<0||index+1>=args.Length)throw new BuildFailedException("Specify immutable Gradle output.");
            string output=Path.GetFullPath(args[index+1]);
            bool reuse=Array.IndexOf(args,"-loopSortReuseGradle")>=0;
            if(File.Exists(output))throw new BuildFailedException("Build output is a file.");
            if(Directory.Exists(output))
            {
                // Explicitly reuse only our private Gradle cache. Published APKs
                // still receive a new filename in the packaging step.
                string privateRoot=Path.GetFullPath("D:/MiniProject/BuildTemp")+Path.DirectorySeparatorChar;
                if(!reuse||!output.StartsWith(privateRoot,StringComparison.OrdinalIgnoreCase)||
                   !Directory.Exists(Path.Combine(output,"unityLibrary/src/main/Il2CppOutputProject")))
                    throw new BuildFailedException("Existing output is not an explicitly selected private Gradle export.");
            }
            EditorUserBuildSettings.development=true;PlayerSettings.Android.useCustomKeystore=false;
            PlayerSettings.Android.bundleVersionCode=checked((int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds()/10));
            EditorUserBuildSettings.exportAsGoogleAndroidProject=true;
            HotUpdateBuilder.GenerateBindings();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/LoopSort/Scenes/Gameplay.unity"},target=BuildTarget.Android,
                locationPathName=output,options=BuildOptions.Development|BuildOptions.AcceptExternalModificationsToPlayer});
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Android player export failed; inspect private build log.");
            File.WriteAllText(output+".json","{\"unityGradleExported\":true,\"debugSigned\":true,\"automaticAdvertising\":true,\"diagnosticPanel\":false,\"productionReady\":false,\"published\":false}\n");
            Debug.Log("LOOPSORT_PLAYER_EXPORTED");
        }
    }
}
