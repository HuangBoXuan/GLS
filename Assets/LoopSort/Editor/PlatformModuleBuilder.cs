using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MiniGamePlatform.HotUpdate;
using MiniGamePlatform.SDK.EditorChecks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace LoopSort0.Editor
{
    /// <summary>Builds an unregistered local module and honest review materials, never publishes it.</summary>
    public static class PlatformModuleBuilder
    {
        const string LocalGameId = "loopsort0";
        const string ExpectedUnityVersion = "2022.3.62f2";
        const string LuaDirectory = "Assets/StreamingAssets/LoopSort/lua";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly string[] SharedModules = {
            "json", "rule_model", "rule_engine", "conveyor_spline", "rule_session",
            "level_repository", "module_runtime", "peripheral_config", "activity_config", "guide_levels", "signin", "visual_clearance"
        };
        static readonly string[] Capabilities = {
            "lua", "lua_late_update_v1", "assetbundle", "module_owned_resources_v1",
            "unity_native_v1", "ugui", "mesh3d", "data_text"
        };
        // Host-owned UGUI primitives use these built-in shaders. Game materials carry project shaders.
        static readonly string[] HostShaders = { "UI/Default", "Sprites/Default", "GUI/Text Shader" };
        static readonly Regex LiteralRequire = new Regex(@"\brequire\s*\(?\s*(['""])([^'""\s]+)\1", RegexOptions.CultureInvariant);
        static readonly Regex GameBridge = new Regex(@"\bCS\s*(?:\.\s*LoopSort0|\[\s*['""]LoopSort0['""]\s*\])", RegexOptions.CultureInvariant);

        [MenuItem("LoopSort/平台迁移/构建本地 Android 模块（不运行游戏）")]
        public static void BuildLocalMenu() => BuildLocal();

        public static string BuildLocal()
        {
            string directory = PrepareLocal();
            return BuildPrepared(directory);
        }

        // Kept separate from BuildPrepared so a failed Android toolchain/build can be retried
        // against exactly the staged Lua, data and definition, without inventing passed gates.
        public static string PrepareLocal()
        {
            RequireEditor();
            CollectLua(ProjectRoot(), LocalGameId);
            return PrepareLocal(PlatformAssetBuilder.Prepare(LocalGameId));
        }

        // Explicitly reuse a completed authoring result. This never guesses whether an
        // older cache is current, and never accepts an index outside this project's output.
        public static string PrepareLocalFromAssets(string preparedIndexPath)
        {
            RequireEditor();
            string project = ProjectRoot();
            string expected = SafeChild(project, "Assets/LoopSort/PlatformAssets/index.json");
            string actual = Path.GetFullPath(preparedIndexPath);
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Use the completed native index from this project: " + expected);
            RejectLinks(project, actual);
            if (!File.Exists(actual)) throw new BuildFailedException("Native authoring has not produced its completed index: " + actual);
            var index = JObject.Parse(File.ReadAllText(actual, Utf8));
            if ((int?)index["schema"] != 1 || (string)index["bundle"] != LocalGameId + ".bundle")
                throw new BuildFailedException("Native asset index schema or bundle identity differs from the local module.");

            string[] sections = { "meshes", "primaryMeshes", "materials", "featureMaterials", "textures", "audio", "data", "animations", "prefabs", "tracks" };
            string[] singleAssets = { "atlas", "font", "physicsMaterial" };
            foreach (string section in sections)
                if (!(index[section] is JObject) || !index[section].HasValues)
                    throw new BuildFailedException("Native asset index section is incomplete: " + section);
            foreach (string name in singleAssets)
                if (index[name]?.Type != JTokenType.String)
                    throw new BuildFailedException("Native asset index reference is missing: " + name);
            if (!(index["tracks"]["main"] is JObject) || !index["tracks"]["main"].HasValues ||
                !(index["tracks"]["daily"] is JObject) || !index["tracks"]["daily"].HasValues)
                throw new BuildFailedException("Native asset index is missing main or daily conveyor layouts.");
            foreach (JProperty property in index.Properties())
                if (property.Name != "schema" && property.Name != "bundle" &&
                    !sections.Contains(property.Name) && !singleAssets.Contains(property.Name))
                    throw new BuildFailedException("Unknown native asset index section: " + property.Name);

            var roots = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string section in sections)
                foreach (JValue value in ((JObject)index[section]).Descendants().OfType<JValue>())
                    AddPreparedAsset(value, project, roots);
            foreach (string name in singleAssets) AddPreparedAsset((JValue)index[name], project, roots);
            return PrepareLocal(new PlatformAssetBuilder.Result {
                AssetRoots = roots.ToArray(), IndexPath = actual, GeneratedRoot = "Assets/LoopSort/PlatformAssets"
            });
        }

        static void AddPreparedAsset(JValue value, string project, ISet<string> roots)
        {
            string path = value.Type == JTokenType.String ? (string)value : null;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("\\") || path.Split('/').Any(part => part == "." || part == ".." || part.Length == 0) ||
                path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Native asset index must contain project asset paths: " + value.Path);
            string absolute = SafeChild(project, path);
            RejectLinks(project, absolute);
            if (!File.Exists(absolute) || AssetDatabase.LoadMainAssetAtPath(path) == null)
                throw new BuildFailedException("Native index asset is missing or has not imported: " + path);
            roots.Add(path);
        }

        static string PrepareLocal(PlatformAssetBuilder.Result assets)
        {
            RequireEditor();
            string project = ProjectRoot();
            // The version appears twice in review paths. Keep it compact so
            // Windows/Mono can export long authored track names and metadata.
            string version = "local_" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string directory = SafeChild(project, "Builds/PlatformModules/" + version);
            if (Directory.Exists(directory)) throw new BuildFailedException("Immutable local module already exists: " + directory);

            // Fail early on an incomplete source tree. Asset authoring can take minutes;
            // collect again immediately before writing so edits made during it are not lost.
            CollectLua(project, LocalGameId);
            string data = PlatformDataBuilder.Export();
            var definition = new ModuleDefinition {
                gameId = LocalGameId, moduleVersion = version, runtimeType = "unity_lua",
                entry = "games." + LocalGameId + ".main", luaRoot = "games/" + LocalGameId,
                minHostApiVersion = 7, requiredCapabilities = Capabilities.ToArray(),
                platform = "android", dataRoots = new[] { "data/" + LocalGameId },
                assetRoots = assets.AssetRoots.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
                ruleVersion = "1", saveSchemaVersion = "2", levelMapPath = "data/" + LocalGameId + "/level-map.json"
            };
            FailIssues(ModuleDefinitionValidator.Validate(definition, HostProfile.ForPlatform("android")));
            FailIssues(AssetDependencyValidator.Validate(definition, HostComponentInventory.FromCapabilities(Capabilities, HostShaders)));
            // The official packager compiles syntax with load(); it never calls the game chunk.
            var luaFiles = CollectLua(project, LocalGameId);

            Directory.CreateDirectory(directory);
            string source = SafeChild(directory, "source");
            foreach (var file in luaFiles) WriteNew(source, file.Key, Utf8.GetBytes(file.Value));
            CopyDataPack(data, source, LocalGameId);
            WriteNew(source, "data/" + LocalGameId + "/assets/index.json", File.ReadAllBytes(assets.IndexPath));
            WriteNew(source, "data/" + LocalGameId + "/services.json", ServiceConfiguration(project));
            WriteNew(directory, "module-definition.json", Encode(JObject.Parse(JsonUtility.ToJson(definition))));
            WriteNew(directory, "preparation.json", Encode(new JObject {
                ["schema"] = 1, ["kind"] = "unregistered-local-module-preparation",
                ["gameId"] = LocalGameId, ["moduleVersion"] = version, ["registrationConfirmed"] = false,
                ["productionReady"] = false, ["unityVersion"] = Application.unityVersion,
                ["sdkVersion"] = "0.2.0-localtest.9730783e9569c94e", ["ruleVersionRegistered"] = false,
                ["definitionSha256"] = Sha256(SafeChild(directory, "module-definition.json")),
                ["sourceFiles"] = Inventory(source), ["assetFiles"] = AssetInventory(definition.assetRoots),
                ["dataPack"] = data, ["hostShaders"] = new JArray(HostShaders),
                ["createdAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            }));
            Debug.Log("Local platform module prepared (unregistered, not yet built): " + directory);
            return directory;
        }

        public static string BuildPrepared(string preparationDirectory)
        {
            RequireEditor();
            string project = ProjectRoot();
            string directory = Path.GetFullPath(preparationDirectory);
            RequireWithin(SafeChild(project, "Builds/PlatformModules"), directory);
            RejectLinks(project, directory);
            string preparationPath = SafeChild(directory, "preparation.json");
            var preparation = JObject.Parse(File.ReadAllText(preparationPath, Utf8));
            if ((string)preparation["kind"] != "unregistered-local-module-preparation" ||
                (string)preparation["gameId"] != LocalGameId || (bool?)preparation["registrationConfirmed"] != false)
                throw new BuildFailedException("Not a local LoopSort module preparation.");
            string source = SafeChild(directory, "source");
            CheckInventory(source, (JArray)preparation["sourceFiles"]);
            CheckAssetInventory((JArray)preparation["assetFiles"]);
            if (Sha256(SafeChild(directory, "module-definition.json")) != (string)preparation["definitionSha256"])
                throw new BuildFailedException("Module definition changed after preparation; prepare a fresh immutable version.");
            var definition = ModuleDefinition.Load(File.ReadAllText(SafeChild(directory, "module-definition.json"), Utf8));
            if (definition.gameId != LocalGameId || definition.moduleVersion != (string)preparation["moduleVersion"])
                throw new BuildFailedException("Definition identity differs from its immutable preparation.");
            if (definition.requiredCapabilities == null || !definition.requiredCapabilities.SequenceEqual(Capabilities))
                throw new BuildFailedException("Prepared capability declaration changed; create a new preparation.");
            string output = SafeChild(directory, "output");
            // Official SDK builder performs definition/dependency checks, Android bundle build,
            // Lua syntax compilation (load only) and immutable manifest/file hashing.
            ModuleBuildResult result = AndroidModuleBuilder.Build(definition, source, output,
                HostComponentInventory.FromCapabilities(definition.requiredCapabilities, HostShaders));
            WriteReview(directory, source, result, definition);
            Debug.Log("Local Android module built: " + result.ModulePath + "\nManifest SHA-256: " + result.ManifestSha256
                + "\nReview is a draft: no Play Mode, gameplay tests, APK/device evidence or platform publication.");
            return result.ModulePath;
        }

        static SortedDictionary<string, string> CollectLua(string project, string gameId)
        {
            string luaRoot = SafeChild(project, LuaDirectory);
            string platform = SafeChild(luaRoot, "platform");
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string name in SharedModules)
                AddLua(result, SafeChild(luaRoot, "game/" + name + ".lua"), "game/" + name + ".lua", gameId);
            // Only the bridge-free shared theme belongs in the downloadable UI
            // namespace. The standalone UI factories reference game C# types.
            AddLua(result, SafeChild(luaRoot, "ui/menu_theme.lua"), "ui/menu_theme.lua", gameId);
            AddLua(result, SafeChild(luaRoot, "ui/signin.lua"), "ui/signin.lua", gameId);
            if (!File.Exists(SafeChild(platform, "main.lua")))
                throw new BuildFailedException("Real platform entry is not ready: platform/main.lua");
            foreach (string path in EnumerateFiles(platform).Where(p => p.EndsWith(".lua", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal))
            {
                string relative = Relative(platform, path);
                AddLua(result, path, relative == "main.lua" ? "main.lua" : "platform/" + relative, gameId);
            }
            foreach (var file in result)
                foreach (Match match in LiteralRequire.Matches(file.Value))
                {
                    string name = match.Groups[2].Value;
                    if (name.StartsWith("sdk.", StringComparison.Ordinal)) continue;
                    if (!name.StartsWith("games." + gameId + ".", StringComparison.Ordinal) ||
                        !result.ContainsKey(name.Replace('.', '/') + ".lua"))
                        throw new BuildFailedException("Missing or foreign Lua dependency: " + name + " in " + file.Key);
                }
            return result;
        }

        static void AddLua(IDictionary<string, string> files, string path, string relative, string gameId)
        {
            if (!File.Exists(path)) throw new BuildFailedException("Required Lua source is missing: " + path);
            string source = File.ReadAllText(path, Utf8);
            if (GameBridge.IsMatch(source)) throw new BuildFailedException("Game-specific C# bridge cannot enter platform module: " + path);
            string rewritten = LiteralRequire.Replace(source, match => {
                string name = match.Groups[2].Value;
                if (name.StartsWith("game.", StringComparison.Ordinal) || name.StartsWith("platform.", StringComparison.Ordinal) ||
                    name.StartsWith("ui.", StringComparison.Ordinal))
                    return match.Value.Replace(name, "games." + gameId + "." + name);
                return match.Value;
            });
            files.Add("games/" + gameId + "/" + relative, rewritten);
        }

        static void CopyDataPack(string packDirectory, string source, string gameId)
        {
            var pack = JObject.Parse(File.ReadAllText(SafeChild(packDirectory, "data-pack.json"), Utf8));
            var inventory = (JArray)pack["files"];
            CheckInventory(packDirectory, inventory, false);
            foreach (JObject file in inventory)
            {
                string relative = (string)file["path"];
                WriteNew(source, "data/" + gameId + "/" + relative, File.ReadAllBytes(SafeChild(packDirectory, relative)));
            }
            var index = JObject.Parse(File.ReadAllText(SafeChild(packDirectory, "levels/index.json"), Utf8));
            var catalogs = new JObject();
            foreach (JProperty catalog in ((JObject)index["catalogs"]).Properties())
            {
                var levels = new JArray();
                foreach (JObject shard in (JArray)catalog.Value["shards"])
                    foreach (JToken id in (JArray)shard["ids"])
                        levels.Add(new JObject { ["levelId"] = id.DeepClone(), ["catalog"] = catalog.Name,
                            ["shard"] = "levels/" + (string)shard["path"] });
                catalogs[catalog.Name] = new JObject { ["sourceVersion"] = catalog.Value["version"].DeepClone(), ["levels"] = levels };
            }
            WriteNew(source, "data/" + gameId + "/level-map.json", Encode(new JObject {
                ["schema"] = 1, ["ruleVersion"] = "1", ["serverRegistered"] = false, ["catalogs"] = catalogs
            }));
        }

        static byte[] ServiceConfiguration(string project)
        {
            string path = SafeChild(project, "Assets/LoopSort/Platform/module-services.json");
            var config = JObject.Parse(File.ReadAllText(path, Utf8));
            string[] names = { "leaderboard", "teams", "cloud_save", "cloud_load", "ad_reward" };
            foreach (JProperty property in config.Properties())
            {
                if (!names.Contains(property.Name) || property.Value.Type != JTokenType.String)
                    throw new BuildFailedException("Unknown module service option: " + property.Name);
                string value = property.Value.Value<string>();
                if (value.Length > 0 && (value.Contains(":") || value.Contains("\\") || value.StartsWith("/", StringComparison.Ordinal)
                    || value.Split('/').Any(part => part == "." || part == ".." || part.Length == 0)))
                    throw new BuildFailedException("Service configuration must contain only relative routes: " + property.Name);
            }
            foreach (string name in names) if (config[name] == null) config[name] = "";
            return Encode(config);
        }

        static void WriteReview(string directory, string source, ModuleBuildResult result, ModuleDefinition definition)
        {
            string review = SafeChild(directory, "output/review/" + definition.gameId + "/" + definition.moduleVersion);
            string reviewSource = SafeChild(review, "source");
            foreach (string file in EnumerateFiles(source))
                WriteNew(reviewSource, Relative(source, file), File.ReadAllBytes(file));
            var dependencies = AssetDatabase.GetDependencies(definition.assetRoots, true)
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
            foreach (string asset in dependencies)
            {
                if (asset.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || asset.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Unexpected game code in review asset dependencies: " + asset);
                if (File.Exists(asset)) WriteNew(reviewSource, asset, File.ReadAllBytes(asset));
                if (File.Exists(asset + ".meta")) WriteNew(reviewSource, asset + ".meta", File.ReadAllBytes(asset + ".meta"));
            }
            WriteNew(review, "module-definition.json", File.ReadAllBytes(SafeChild(directory, "module-definition.json")));
            WriteNew(review, "host-configuration.template.json", Encode(new JObject {
                ["configured"] = false, ["gameIdRegistration"] = "pending", ["localGameId"] = definition.gameId,
                ["independentProductId"] = "", ["hubProductId"] = "", ["moduleSelectionHttpsUrl"] = "",
                ["moduleCdnHttpsBaseUrl"] = "", ["trustedCatalog"] = "", ["serverRuleVersionRegistered"] = false,
                ["adsDriverConfigured"] = false, ["paymentDriverConfigured"] = false,
                ["note"] = "Host-only template. Fill real registered identities and trusted service configuration in the product shell; never put secrets or product advertising identifiers into the downloadable module."
            }));
            WriteNew(review, "review-status.json", Encode(new JObject {
                ["schema"] = 1, ["kind"] = "local-review-draft", ["gameId"] = definition.gameId,
                ["moduleVersion"] = definition.moduleVersion, ["manifestSha256"] = result.ManifestSha256,
                ["registrationConfirmed"] = false, ["officialDeliveryGatePassed"] = false, ["published"] = false,
                ["unityVersion"] = Application.unityVersion, ["sdkReferenceUnityVersion"] = "2022.3.76f1",
                ["ruleVersion"] = definition.ruleVersion, ["saveSchemaVersion"] = definition.saveSchemaVersion,
                ["completed"] = new JArray("SDK definition validation", "SDK native asset dependency validation", "Android AssetBundle build",
                    "SDK Lua syntax compilation via load (game chunks not executed)", "SDK immutable manifest packaging"),
                ["isolatedLuaBehaviorChecks"] = Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyProductLua")>=0,
                ["notRun"] = new JArray("Play Mode", "gameplay execution", "SDK test suites", "independent Android APK gate", "hub Android APK gate", "device performance validation", "online service validation"),
                ["externalRequirements"] = new JArray("permanent game/product registration", "approved server rule and save contracts",
                    "signed product catalogs and HTTPS service/CDN configuration", "rights documentation from the game team", "dual-product evidence before official delivery"),
                ["sourceInventory"] = Inventory(reviewSource), ["requiredHostShaders"] = new JArray(HostShaders)
            }));
            WriteNew(review, "README.txt", Utf8.GetBytes(
                "LoopSort0 本地模块审核草案\n\n" +
                "module-definition.json、source/ 与 review-status.json 为可复查构建材料。modules/ 中是官方 SDK 构建器生成的 Android Lua/资源模块。\n" +
                "loopsort0 是未登记的本地标识；规则 1、存档 2 是当前源码协议值，未声明服务器已登记。Unity 保持 2022.3.62f2。\n" +
                "未执行游戏场景、Play Mode、完整 SDK 测试门禁、APK、设备验收或发布；可选的隔离 Lua 行为检查结果记录在构建回执中。此处不是 PrepareModuleDelivery.ps1 生成的正式交付，不含伪造的 gate-report 或 delivery.json。\n" +
                "正式交付前由平台登记身份与产品，配置服务/签名/CDN，并按 SDK 内 VerifyModuleRelease.ps1 与 PrepareModuleDelivery.ps1 提供真实双包证据。\n"));
        }

        static JArray AssetInventory(IEnumerable<string> roots)
        {
            var files = AssetDatabase.GetDependencies(roots.ToArray(), true).Where(File.Exists)
                .SelectMany(path => File.Exists(path + ".meta") ? new[] { path, path + ".meta" } : new[] { path })
                .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal);
            return new JArray(files.Select(path => FileRecord(path.Replace('\\', '/'), path)));
        }

        static void CheckAssetInventory(JArray files)
        {
            if (files == null || files.Count == 0) throw new BuildFailedException("Prepared asset inventory is missing.");
            foreach (JObject item in files)
            {
                string path = (string)item["path"];
                if (!File.Exists(path) || Sha256(path) != (string)item["sha256"] || new FileInfo(path).Length != (long)item["bytes"])
                    throw new BuildFailedException("Asset changed after preparation; prepare a fresh immutable version: " + path);
            }
        }

        static JArray Inventory(string root) => new JArray(EnumerateFiles(root).OrderBy(p => p, StringComparer.Ordinal)
            .Select(path => FileRecord(Relative(root, path), path)));

        static JObject FileRecord(string relative, string path) => new JObject {
            ["path"] = relative, ["bytes"] = new FileInfo(path).Length, ["sha256"] = Sha256(path)
        };

        static void CheckInventory(string root, JArray files, bool exact = true)
        {
            if (files == null || files.Count == 0) throw new BuildFailedException("Preparation inventory is missing.");
            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject item in files)
            {
                string relative = (string)item["path"], path = SafeChild(root, relative);
                if (!expected.Add(relative) || !File.Exists(path) || Sha256(path) != (string)item["sha256"] ||
                    new FileInfo(path).Length != (long)item["bytes"])
                    throw new BuildFailedException("Preparation content changed or is incomplete: " + relative);
            }
            if (exact && !expected.SetEquals(EnumerateFiles(root).Select(path => Relative(root, path))))
                throw new BuildFailedException("Unexpected source files appeared after preparation.");
        }

        static IEnumerable<string> EnumerateFiles(string directory)
        {
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new BuildFailedException("Linked source directories are not accepted: " + directory);
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new BuildFailedException("Linked source entry: " + path);
                if ((attributes & FileAttributes.Directory) != 0)
                    foreach (string file in EnumerateFiles(path)) yield return file;
                else yield return path;
            }
        }

        static void RequireEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new BuildFailedException("Exit Play Mode before asset-only module builds.");
            if (Application.unityVersion != ExpectedUnityVersion)
                throw new BuildFailedException("This project must stay on Unity " + ExpectedUnityVersion + "; actual " + Application.unityVersion);
        }

        static void FailIssues(IReadOnlyList<AuthoringIssue> issues)
        {
            if (issues.Count > 0) throw new BuildFailedException(issues[0].Code + ": " + issues[0].Path + ": " + issues[0].Message);
        }

        static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string Relative(string root, string path) => Path.GetFullPath(path).Substring(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');
        static byte[] Encode(JToken value) => Utf8.GetBytes(value.ToString(Formatting.Indented) + "\n");

        static string SafeChild(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new BuildFailedException("Expected relative path.");
            string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            RequireWithin(root, path); RejectLinks(root, path); return path;
        }

        static void RequireWithin(string root, string path)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Path leaves the intended module directory: " + path);
        }

        static void RejectLinks(string root, string path)
        {
            string stop = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            for (string current = path; !string.Equals(current, stop, StringComparison.OrdinalIgnoreCase); current = Path.GetDirectoryName(current))
            {
                RequireWithin(stop, current);
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new BuildFailedException("Linked build path is not accepted: " + current);
            }
        }

        static void WriteNew(string root, string relative, byte[] bytes)
        {
            if (relative.StartsWith("data/", StringComparison.Ordinal) && bytes.LongLength >= 4L * 1024 * 1024)
                throw new BuildFailedException("Module text data must stay below the host's 4 MiB read limit: " + relative);
            string path = SafeChild(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(bytes, 0, bytes.Length);
        }

        static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
