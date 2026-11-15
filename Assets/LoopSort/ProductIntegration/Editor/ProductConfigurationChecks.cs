using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LoopSort.ProductIntegration.Editor
{
    public sealed class ProductConfigurationChecks : IPreprocessBuildWithReport
    {
        [Serializable] sealed class OfflineMarker { public bool localOnly; }
        public int callbackOrder => -1000;
        static string LocalPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../ProjectSettings/LoopSortProduct.local.json"));
        public void OnPreprocessBuild(BuildReport report)
        {
            // The explicitly offline shell has its own immutable local-only gate.
            string offlinePath = Path.Combine(Application.dataPath, "Resources/PlatformLocal/configuration.json");
            if (File.Exists(offlinePath) && JsonUtility.FromJson<OfflineMarker>(File.ReadAllText(offlinePath))?.localOnly == true) return;
            if ((report.summary.options & BuildOptions.Development) != 0) return;
            if (!File.Exists(LocalPath)) throw new BuildFailedException("Release product configuration is missing: ProjectSettings/LoopSortProduct.local.json");
            var config = JsonUtility.FromJson<ProductConfiguration>(File.ReadAllText(LocalPath));
            if (config == null) throw new BuildFailedException("Invalid product configuration JSON.");
            var missing = config.MissingReleaseFields();
            if (PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android) != config.androidPackageName)
                missing.Add("Android package mismatch");
            if (config.maxEnabled && (int)PlayerSettings.Android.minSdkVersion < 24) missing.Add("Android minimum API 24");
            if ((int)PlayerSettings.Android.targetSdkVersion < 36) missing.Add("Google Play target API 36");
            if(!PlayerSettings.Android.useCustomKeystore||!File.Exists(PlayerSettings.Android.keystoreName)||string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
                missing.Add("release signing keystore and alias");
            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0) missing.Add("Android ARM64");
            foreach(var scene in EditorBuildSettings.scenes)
                if(scene.enabled&&scene.path=="Assets/LoopSort/Scenes/Gameplay.unity")
                    missing.Add("production authenticated host scene (Gameplay is the standalone validation entry)");
            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP) missing.Add("Android IL2CPP");
            if (missing.Count != 0) throw new BuildFailedException("Release product configuration incomplete: " + string.Join(", ", missing));
            ProductBuildInputs.ApplyLocal();
        }
        [MenuItem("LoopSort/平台迁移/查看正式配置缺项（不运行）")]
        public static void ShowMissing()
        {
            if (!File.Exists(LocalPath)) { Debug.Log("正式配置待填写：ProjectSettings/LoopSortProduct.local.json。请复制 Configuration/Products/loopsort0-development.template.json 后填写。"); return; }
            var config = JsonUtility.FromJson<ProductConfiguration>(File.ReadAllText(LocalPath));
            if (config == null) throw new InvalidDataException("Invalid product configuration JSON.");
            var missing = config.MissingReleaseFields();
            Debug.Log(missing.Count == 0 ? "配置字段完整；仍需真实设备和后台验收。" : "正式配置缺项：" + string.Join(", ", missing));
        }
    }
}
