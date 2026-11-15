using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LoopSort.ProductIntegration.Editor
{
    public static class ProductAndroidDependencies
    {
        [Serializable] sealed class Receipt
        {
            public bool resolved, gameExecuted, testsRun;
            public string unityVersion, completedAtUtc;
        }
        [MenuItem("LoopSort/平台迁移/准备广告 Android 依赖（不运行游戏）")]
        public static void Resolve()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            string previous = Environment.GetEnvironmentVariable("JAVA_HOME");
            string[] candidates = { previous,
                Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/OpenJDK"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/UnityAndroid", Application.unityVersion, "OpenJDK") };
            string jdk = candidates.FirstOrDefault(p => !string.IsNullOrEmpty(p) &&
                (File.Exists(Path.Combine(p, "bin/java.exe")) || File.Exists(Path.Combine(p, "bin/java"))));
            if (jdk == null) throw new InvalidOperationException("Configure a valid Unity JDK before resolving product dependencies.");
            var receipt = new Receipt { unityVersion = Application.unityVersion };
            try
            {
                Environment.SetEnvironmentVariable("JAVA_HOME", jdk);
                Directory.CreateDirectory("Assets/Plugins/Android");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new InvalidOperationException("Android Build Support is required.");
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                string[] resolvers = Directory.GetFiles("Assets/ExternalDependencyManager", "Google.JarResolver.dll", SearchOption.AllDirectories);
                if (resolvers.Length != 1) throw new InvalidOperationException("Install exactly one pinned EDM4U Android resolver.");
                Type type = Assembly.LoadFrom(Path.GetFullPath(resolvers[0])).GetType("GooglePlayServices.PlayServicesResolver", true);
                MethodInfo resolve = type.GetMethod("ResolveSync", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
                if (resolve == null) throw new InvalidOperationException("Pinned EDM4U ResolveSync API is unavailable.");
                receipt.resolved = (bool)resolve.Invoke(null, new object[] { true });
                if (!receipt.resolved) throw new InvalidOperationException("Native dependency resolution failed; inspect the Unity Editor log.");
                Debug.Log("Product Android dependencies resolved. No game, tests or APK executed.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("JAVA_HOME", previous);
                receipt.completedAtUtc = DateTime.UtcNow.ToString("o");
                string directory = Path.GetFullPath("Builds/ProductIntegration");
                Directory.CreateDirectory(directory);
                string file = Path.Combine(directory, "native-dependencies-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
                File.WriteAllText(file, JsonUtility.ToJson(receipt, true));
            }
        }
    }
}
