using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MiniGamePlatform.HotUpdate;
using MiniGamePlatform.SDK.EditorChecks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace LoopSort.PlatformLocalHost.Editor
{
    // The isolated project deliberately excludes old game assemblies and their
    // preserve-all link.xml files. No original scene or PlayerSettings is changed.
    public static class PlatformLocalPlayerBuilder
    {
        const string RequiredUnity = "2022.3.62f2";
        const string HostSources = "Assets/LoopSort/PlatformHost";
        const string Marker = "platform-local-project.json";
        const string Kind = "loopsort0-local-only-platform-shell";
        const string ScenePath = "Assets/PlatformHost/Scenes/PlatformLocal.unity";
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        [Serializable]
        public sealed class Preparation
        {
            public string kind, unityVersion, projectDirectory, apkPath, logPath, sourceModuleDirectory, manifestSha256, gameId, moduleVersion;
            public bool localOnly, platformRegistered, productionReady;
        }
        [Serializable]
        sealed class FontBuiltinShaderInfo
        {
            public string name, flags, referencePath, resourcePath, handling;
            public int flagsValue, removedAlwaysIncludedReferences;
            public bool explicitlyIncluded;
        }
        [Serializable]
        sealed class Receipt
        {
            public string kind, unityVersion, apkPath, apkSha256, sourceModuleDirectory, manifestSha256, gameId, moduleVersion;
            public long apkBytes;
            public bool buildSucceeded, localOnly, platformRegistered, productionReady, gameplayExecuted, automatedTestsRun;
            public string[] explicitIncludedShaders;
            public FontBuiltinShaderInfo fontBuiltinShader;
            public string nativeContentKeyMode;
        }

        [MenuItem("LoopSort/平台迁移/构建本地离线独立 APK（不运行游戏）")]
        public static void BuildLatestMenu()
        {
            RequireOriginalProject();
            // Isolated batch workers deliver verified modules back to the
            // project here; their short-path build cache need not be present.
            string[] roots = { Path.Combine(ProjectRoot(), "Builds", "PlatformModules"),
                Path.Combine(ProjectRoot(), "Builds", "PlatformDelivery") };
            string manifest = roots.Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateFiles(root, "manifest.json", SearchOption.AllDirectories))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault(path => IsModuleDirectory(Path.GetDirectoryName(path)));
            if (manifest == null) throw new BuildFailedException("No complete, verified Android module was found.");
            string prepared = PreparePrepared(Path.GetDirectoryName(manifest), PlatformLocalBootstrap.HashFile(manifest));
            StartPreparedProject(prepared);
            Debug.Log("Local-only APK build started without running the game. Project: " + prepared);
        }

        // Creates a concrete, immutable build workspace and returns its directory.
        // The marker contains apkPath/logPath for asynchronous callers.
        public static string PreparePrepared(string modulePath, string manifestSha, string outputRoot = null)
        {
            RequireOriginalProject();
            modulePath = Path.GetFullPath(modulePath);
            var manifest = ReadPinnedModule(modulePath, manifestSha);
            string original = ProjectRoot();
            string outputParent = outputRoot == null ? Path.Combine(original, "Builds", "PlatformLocalPlayers") : ValidateOutputRoot(outputRoot);
            string id = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string output = Path.Combine(outputParent, id);
            if (Directory.Exists(output) || File.Exists(output)) throw new BuildFailedException("Local player output already exists.");
            string project = Path.Combine(output, "Project");
            Directory.CreateDirectory(project);
            var preparation = new Preparation {
                kind = Kind, unityVersion = RequiredUnity, localOnly = true, platformRegistered = false, productionReady = false,
                projectDirectory = project, apkPath = Path.Combine(output, "LoopSort0-Platform-LocalOnly-arm64.apk"),
                logPath = Path.Combine(output, "unity-build.log"), sourceModuleDirectory = modulePath,
                manifestSha256 = manifestSha.ToLowerInvariant(), gameId = manifest.gameId, moduleVersion = manifest.version
            };
            CopyTree(Path.Combine(original, HostSources), Path.Combine(project, "Assets", "PlatformHost"));
            CopySdkPackages(original, project);
            Write(project, "ProjectSettings/ProjectVersion.txt", File.ReadAllText(Path.Combine(original, "ProjectSettings", "ProjectVersion.txt"), Utf8));
            Write(project, "ProjectSettings/TagManager.asset", TagManager());
            var config = new LocalPlayerConfiguration {
                gameId = manifest.gameId, moduleVersion = manifest.version, manifestSha256 = preparation.manifestSha256,
                bundledRoot = "PlatformLocal/module", profileId = "local-only:default", localOnly = true
            };
            Write(project, "Assets/Resources/PlatformLocal/configuration.json", JsonUtility.ToJson(config, true));
            string font = Path.Combine(original, "Assets/LoopSort/Resources/LoopSort/Fonts/NotoSansSC-Medium.otf");
            CopyFile(font, Path.Combine(project, "Assets/Resources/PlatformLocal/NotoSansSC-Medium.otf"));
            CopyFile(font + ".meta", Path.Combine(project, "Assets/Resources/PlatformLocal/NotoSansSC-Medium.otf.meta"));
            CopyFile(Path.Combine(original, "Assets/LoopSort/Resources/LoopSort/Fonts/OFL-NotoSansSC.txt"), Path.Combine(project, "Assets/Resources/PlatformLocal/OFL-NotoSansSC.txt"));
            string bundled = Path.Combine(project, "Assets/StreamingAssets/PlatformLocal/module");
            foreach (var file in manifest.files)
                CopyFile(PlatformLocalBootstrap.SafeChild(modulePath, file.path), PlatformLocalBootstrap.SafeChild(bundled, file.path));
            CopyFile(Path.Combine(modulePath, "manifest.json"), Path.Combine(bundled, "manifest.json"));
            if (!ModuleInstaller.IsValid(manifest, bundled)) throw new BuildFailedException("Copied module failed content verification.");
            Write(project, Marker, JsonUtility.ToJson(preparation, true));
            File.WriteAllText(Path.Combine(output, "preparation.json"), JsonUtility.ToJson(preparation, true), Utf8);
            File.WriteAllText(Path.Combine(output, "README.txt"),
                "Local-only independent shell; not a platform release.\n" +
                "This shell contains native SDK/UI/storage infrastructure and the pinned downloadable Lua module.\n" +
                "Its device profile is local-only:default; it does not create a PlatformSession or a login token.\n" +
                "Ads, cloud, leaderboard, teams and payment are unavailable. No network calls are made.\n" +
                "The original project, scenes, SDK packages and PlayerSettings are unchanged.\n" +
                "Build receipt records compilation only; game execution, device validation and automated tests are not performed.\n" +
                "Pinned module SHA-256: " + preparation.manifestSha256 + "\n", Utf8);
            Debug.Log("PLATFORM_LOCAL_PLAYER_PREPARED " + project);
            return project;
        }

        // Synchronous automation entry. Prefer PreparePrepared + StartPreparedProject
        // when the calling editor must remain responsive during IL2CPP compilation.
        public static string BuildPrepared(string modulePath, string manifestSha)
        {
            string project = PreparePrepared(modulePath, manifestSha);
            using var process = StartPreparedProject(project);
            process.WaitForExit();
            var prepared = ReadPreparation(project);
            if (process.ExitCode != 0 || !File.Exists(prepared.apkPath))
                throw new BuildFailedException("Local-only APK build did not complete. Log: " + prepared.logPath);
            return prepared.apkPath;
        }

        // Explicit batch launcher for short output roots on Windows. It only
        // stages the pinned module and starts the isolated APK build process.
        public static void BuildFromCommandLine()
        {
            string project = PrepareCommandLineProject();
            using var process = StartPreparedProject(project);
            Debug.Log("PLATFORM_LOCAL_PLAYER_LAUNCHED project=" + project + " pid=" + process.Id);
        }

        // Allows verified local package references to be configured before UPM
        // starts, instead of interrupting a fresh editor during package import.
        public static void PrepareFromCommandLine() => PrepareCommandLineProject();

        static string PrepareCommandLineProject()
        {
            RequireOriginalProject();
            if (!Application.isBatchMode) throw new BuildFailedException("The explicit local player launcher requires batch mode.");
            string[] arguments = Environment.GetCommandLineArgs();
            string module = UniqueArgument(arguments, "-platformModuleDirectory");
            string hash = UniqueArgument(arguments, "-platformManifestSha256");
            string outputRoot = UniqueArgument(arguments, "-platformLocalOutputRoot");
            RequireAbsolutePath(module, "-platformModuleDirectory");
            outputRoot = ValidateOutputRoot(outputRoot);
            if (hash.Length != 64 || hash.Any(c => !(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f') && !(c >= 'A' && c <= 'F')))
                throw new BuildFailedException("-platformManifestSha256 must be exactly 64 hexadecimal characters.");
            return PreparePrepared(module, hash, outputRoot);
        }

        public static Process StartPreparedProject(string project)
        {
            RequireVersion();
            var prepared = ReadPreparation(project);
            string executable = EditorApplication.applicationPath;
            var arguments = "-batchmode -nographics -quit -projectPath " + Quote(project) +
                " -buildTarget Android -executeMethod LoopSort.PlatformLocalHost.Editor.PlatformLocalPlayerBuilder.BuildIsolatedFromCommandLine" +
                " -logFile " + Quote(prepared.logPath);
            var process = Process.Start(new ProcessStartInfo(executable, arguments) {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = project
            });
            if (process == null) throw new BuildFailedException("Could not start the fixed Unity editor for local APK compilation.");
            Debug.Log("PLATFORM_LOCAL_PLAYER_BUILD_STARTED pid=" + process.Id + " log=" + prepared.logPath);
            return process;
        }

        // Runs only in the generated project, never in the user's working project.
        public static void BuildIsolatedFromCommandLine()
        {
            RequireVersion();
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new BuildFailedException("The isolated player build requires batch mode and must not run the game.");
            string project = ProjectRoot();
            var prepared = ReadPreparation(project);
            string module = Path.Combine(project, "Assets/StreamingAssets/PlatformLocal/module");
            ReadPinnedModule(module, prepared.manifestSha256);
            // This shell packages plaintext only. The official SDK generator
            // must emit its deny-all symbol, never import real/private keys.
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ModuleBuildKeyRing.EnvironmentName)) ||
                Environment.GetEnvironmentVariable("MINIGAME_REQUIRE_MODULE_KEYS") == "1")
                throw new BuildFailedException("The local-only plaintext shell requires empty native key provisioning; no private module key input is accepted.");
            if (Directory.Exists(Path.Combine(project, "Assets/LoopSort/Runtime")) || Directory.Exists(Path.Combine(project, "Assets/LoopSort/Core")))
                throw new BuildFailedException("A local platform shell cannot include legacy game C# assemblies.");

            PlayerSettings.companyName = "LoopSort0 Local Development";
            PlayerSettings.productName = "缤纷方块·本地开发版";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.loopsort0.platform.local");
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            Directory.CreateDirectory(Path.Combine(project, "Assets/PlatformHost/Scenes"));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string[] shaders = IncludeUIShaders(out FontBuiltinShaderInfo fontInfo);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var shell = new GameObject("Platform local-only shell");
            shell.AddComponent<PlatformLocalBootstrap>();
            // Native font dependency only. This object is inactive and never
            // renders; game text uses UGUI's Canvas material, not font.material.
            var fontAnchor = new GameObject("Font dependency (inactive)", typeof(RectTransform));
            fontAnchor.SetActive(false); fontAnchor.transform.SetParent(shell.transform, false);
            var textAnchor = fontAnchor.AddComponent<UnityEngine.UI.Text>();
            textAnchor.font = AssetDatabase.LoadAssetAtPath<Font>(fontInfo.resourcePath);
            textAnchor.text = string.Empty; textAnchor.enabled = false;
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new BuildFailedException("Could not save PlatformLocal scene.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(prepared.apkPath));
            // SDK preprocess generates Android-only mgp_module_key, including
            // the empty-registry deny-all implementation. The wrapper owns
            // cleanup on both successful and failed native builds.
            var result = ModuleHostKeyBuild.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, target = BuildTarget.Android,
                locationPathName = prepared.apkPath, options = BuildOptions.CompressWithLz4HC
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Local-only APK build failed: " + result.summary.result + " (" + result.summary.totalErrors + " errors)");
            var receipt = new Receipt {
                kind = Kind, unityVersion = Application.unityVersion, apkPath = prepared.apkPath,
                apkSha256 = PlatformLocalBootstrap.HashFile(prepared.apkPath), apkBytes = new FileInfo(prepared.apkPath).Length,
                sourceModuleDirectory = prepared.sourceModuleDirectory, manifestSha256 = prepared.manifestSha256,
                gameId = prepared.gameId, moduleVersion = prepared.moduleVersion, buildSucceeded = true, localOnly = true,
                platformRegistered = false, productionReady = false, gameplayExecuted = false, automatedTestsRun = false,
                explicitIncludedShaders = shaders, fontBuiltinShader = fontInfo,
                nativeContentKeyMode = "sdk-generated-empty-keyring-deny-all"
            };
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(prepared.apkPath), "build-receipt.json"), JsonUtility.ToJson(receipt, true), Utf8);
            Debug.Log("PLATFORM_LOCAL_APK_BUILD_SUCCEEDED " + prepared.apkPath + " sha256=" + receipt.apkSha256);
        }

        static ModuleManifest ReadPinnedModule(string directory, string pin)
        {
            string path = Path.Combine(directory, "manifest.json");
            if (string.IsNullOrEmpty(pin) || !string.Equals(PlatformLocalBootstrap.HashFile(path), pin, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("The supplied manifest pin does not match the prepared module.");
            var manifest = ModuleManifest.Parse(File.ReadAllText(path, Utf8));
            if (manifest.platform != "android" || manifest.unityVersion != RequiredUnity || manifest.contentEncryption != null && manifest.contentEncryption != "none" && manifest.contentEncryption != "")
                throw new BuildFailedException("A plaintext Android module built by Unity " + RequiredUnity + " is required for this local shell.");
            if (!ModuleInstaller.IsValid(manifest, directory)) throw new BuildFailedException("Module files do not match the manifest.");
            return manifest;
        }
        static bool IsModuleDirectory(string path)
        {
            try { ReadPinnedModule(path, PlatformLocalBootstrap.HashFile(Path.Combine(path, "manifest.json"))); return true; }
            catch { return false; }
        }
        static Preparation ReadPreparation(string project)
        {
            project = Path.GetFullPath(project);
            string marker = Path.Combine(project, Marker);
            if (!File.Exists(marker)) throw new BuildFailedException("This is not a generated local shell workspace.");
            var prepared = JsonUtility.FromJson<Preparation>(File.ReadAllText(marker, Utf8));
            if (prepared == null || prepared.kind != Kind || !prepared.localOnly || prepared.productionReady || prepared.platformRegistered ||
                prepared.unityVersion != RequiredUnity || !string.Equals(project, Path.GetFullPath(prepared.projectDirectory), StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Invalid local shell preparation marker.");
            string output = Directory.GetParent(project)?.FullName;
            if (output == null || !string.Equals(Path.GetFullPath(prepared.apkPath), Path.Combine(output, "LoopSort0-Platform-LocalOnly-arm64.apk"), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFullPath(prepared.logPath), Path.Combine(output, "unity-build.log"), StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Local shell outputs must remain in the generated build directory.");
            return prepared;
        }
        static void RequireVersion()
        { if (Application.unityVersion != RequiredUnity) throw new BuildFailedException("Use the existing Unity " + RequiredUnity + "; do not upgrade it."); }
        static void RequireOriginalProject()
        {
            RequireVersion();
            if (EditorApplication.isPlaying) throw new BuildFailedException("Building must not enter Play Mode.");
            if (!Directory.Exists(Path.Combine(ProjectRoot(), HostSources))) throw new BuildFailedException("Local shell source directory is missing.");
        }
        static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string UniqueArgument(string[] arguments, string name)
        {
            string value = null;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] != name) continue;
                if (value != null || i + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[i + 1]) || arguments[i + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new BuildFailedException("Specify exactly one " + name + " followed by its complete value.");
                value = arguments[++i];
            }
            if (value == null) throw new BuildFailedException("Missing required argument " + name + ".");
            return value;
        }
        static string RequireAbsolutePath(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value) || value.IndexOfAny(new[] { '*', '?', '"', '\0' }) >= 0)
                throw new BuildFailedException(name + " must be an explicit absolute filesystem path.");
            string full;
            try { full = Path.GetFullPath(value); }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException)
            { throw new BuildFailedException(name + " is not a valid filesystem path: " + error.Message); }
            // Reject drive-relative paths (D:folder) and root-relative paths
            // (\\folder); neither names its full absolute location on Windows.
            string suppliedRoot = Path.GetPathRoot(value)?.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            string resolvedRoot = Path.GetPathRoot(full)?.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (!string.Equals(suppliedRoot, resolvedRoot, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException(name + " must include its complete drive or share root.");
            return full;
        }
        static string ValidateOutputRoot(string value)
        {
            string full = RequireAbsolutePath(value, "Local player output root").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string drive = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(full, drive, StringComparison.OrdinalIgnoreCase) || File.Exists(full))
                throw new BuildFailedException("Choose a dedicated output directory, not a drive/share root or an existing file.");
            foreach (string protectedFolder in new[] { "Assets", "Packages", "ProjectSettings", "Library" })
            {
                string protectedPath = Path.Combine(ProjectRoot(), protectedFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(full, protectedPath, StringComparison.OrdinalIgnoreCase) ||
                    full.StartsWith(protectedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("The local player output root must be outside " + protectedFolder + ".");
            }
            return full;
        }
        static string Quote(string value)
        { if (value.Contains('"')) throw new ArgumentException("Invalid quoted path."); return "\"" + value + "\""; }
        static void Write(string root, string relative, string value)
        {
            string path = PlatformLocalBootstrap.SafeChild(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, value, Utf8);
        }
        static void CopyFile(string from, string to)
        { Directory.CreateDirectory(Path.GetDirectoryName(to)); File.Copy(from, to, false); }
        static void CopyTree(string source, string destination)
        {
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                CopyFile(file, Path.Combine(destination, file.Substring(source.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Length + 1)));
        }
        static void CopySdkPackages(string original, string project)
        {
            string source = Path.Combine(original, "Packages/MiniGameSdk");
            string target = Path.Combine(project, "Packages/MiniGameSdk");
            string Package(string name)
            {
                string[] found = Directory.GetFiles(source, name + "-*.tgz", SearchOption.TopDirectoryOnly);
                if (found.Length != 1) throw new BuildFailedException("Expected exactly one pinned package: " + name);
                CopyFile(found[0], Path.Combine(target, Path.GetFileName(found[0])));
                return "file:MiniGameSdk/" + Path.GetFileName(found[0]);
            }
            string sdk = Package("com.minigame.platform-sdk"), host = Package("com.minigame.platform-host"),
                authoring = Package("com.minigame.platform-authoring"), localization = Package("com.unity.localization");
            Write(project, "Packages/manifest.json", "{\n  \"dependencies\": {\n" +
                "    \"com.minigame.platform-sdk\": \"" + sdk + "\",\n" +
                "    \"com.minigame.platform-host\": \"" + host + "\",\n" +
                "    \"com.minigame.platform-authoring\": \"" + authoring + "\",\n" +
                "    \"com.unity.localization\": \"" + localization + "\",\n" +
                "    \"com.unity.test-framework\": \"1.1.33\",\n" +
                "    \"com.unity.textmeshpro\": \"3.0.7\",\n" +
                "    \"com.unity.ugui\": \"1.0.0\",\n" +
                "    \"com.unity.modules.ui\": \"1.0.0\",\n" +
                "    \"com.unity.modules.imgui\": \"1.0.0\",\n" +
                "    \"com.unity.modules.imageconversion\": \"1.0.0\"\n  }\n}\n");
        }
        static string[] IncludeUIShaders(out FontBuiltinShaderInfo fontInfo)
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var list = settings.FindProperty("m_AlwaysIncludedShaders");
            var shaders = new List<Shader>();
            foreach (string name in new[] { "UI/Default", "Sprites/Default" })
            {
                Shader shader = Shader.Find(name); if (shader == null) throw new BuildFailedException("Missing host shader " + name);
                shaders.Add(shader);
            }
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/PlatformLocal/NotoSansSC-Medium.otf");
            if (font == null || font.material == null || font.material.shader == null)
                throw new BuildFailedException("The native Font dependency and its internal shader metadata must be available.");
            Shader fontShader = font.material.shader;
            fontInfo = new FontBuiltinShaderInfo {
                name = fontShader.name, flags = fontShader.hideFlags.ToString(), flagsValue = (int)fontShader.hideFlags,
                referencePath = AssetDatabase.GetAssetPath(fontShader), resourcePath = AssetDatabase.GetAssetPath(font),
                explicitlyIncluded = false,
                handling = "Engine Font dependency rooted by Resources and an inactive UGUI.Text font reference; not added to Always Included Shaders. Runtime rendering has not been tested."
            };
            // Font.material can point at Unity's internal DontSave GUI/Text
            // shader. Serializing that object into GraphicsSettings is invalid.
            // Remove only this exact internal reference from generated projects,
            // including projects prepared by an older version of this builder.
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var entry = list.GetArrayElementAtIndex(i);
                var shader = entry.objectReferenceValue as Shader;
                if (shader != fontShader || shader.name != "GUI/Text Shader" || (shader.hideFlags & HideFlags.DontSave) == 0) continue;
                entry.objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
                fontInfo.removedAlwaysIncludedReferences++;
            }
            foreach (Shader shader in shaders)
            {
                bool exists = false; for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) exists = true;
                if (!exists) { int i = list.arraySize++; list.GetArrayElementAtIndex(i).objectReferenceValue = shader; }
            }
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == fontShader) fontInfo.explicitlyIncluded = true;
            if (fontInfo.explicitlyIncluded)
                throw new BuildFailedException("The engine Font shader must not remain in this shell's Always Included Shaders list.");
            settings.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("FONT_BUILTIN_DEPENDENCY " + JsonUtility.ToJson(fontInfo));
            return shaders.Select(shader => shader.name).ToArray();
        }
        static string TagManager()
        {
            string[] names = new string[32]; names[0]="Default"; names[1]="TransparentFX"; names[2]="Ignore Raycast"; names[4]="Water"; names[5]="UI"; names[30]="PlatformModule";
            return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!78 &1\nTagManager:\n  serializedVersion: 2\n  tags: []\n  layers:\n" +
                string.Concat(names.Select(name => "  - " + (name ?? "") + "\n")) +
                "  m_SortingLayers:\n  - name: Default\n    uniqueID: 0\n    locked: 0\n  m_RenderingLayers:\n  - Default\n";
        }
    }
}
