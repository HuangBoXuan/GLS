using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace LoopSort0.Editor
{
    /// <summary>Asset-only command-line entry for a separate Unity batch worker.</summary>
    public static class PlatformModuleBatchBuild
    {
        public static void Build()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildFailedException("Platform module batch building requires an isolated batch editor in Edit Mode.");
            string receiptPath = ReceiptArgument(Environment.GetCommandLineArgs());
            if (File.Exists(receiptPath))
                throw new BuildFailedException("Build receipts are immutable; choose a new receipt path: " + receiptPath);

            var receipt = new JObject {
                ["schema"] = 1, ["kind"] = "unregistered-local-module-batch-build",
                ["unityVersion"] = Application.unityVersion, ["gameId"] = "loopsort0",
                ["registered"] = false, ["published"] = false,
                ["gameExecuted"] = false, ["testsRun"] = false,
                ["signingCertificate"] = JValue.CreateNull(),
                ["note"] = "AssetBundle/module packaging only. No APK is signed by this entry and no game chunk or test is run."
            };
            try
            {
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verifyProductLua")>=0)
                {
                    receipt["testsRun"]=true;
                    receipt["note"]="Isolated Lua syntax/sign-in/ad-flow checks and local module packaging. No game scene, signed APK, device run or publication.";
                    DailySigninPreview.Check();
                }
                string indexPath = Path.GetFullPath(Path.Combine(Application.dataPath, "LoopSort/PlatformAssets/index.json"));
                bool rebuildAssets = Array.IndexOf(Environment.GetCommandLineArgs(), "-rebuildPlatformAssets") >= 0;
                receipt["nativeAssetsRebuilt"] = rebuildAssets;
                string preparation = rebuildAssets ? PlatformModuleBuilder.PrepareLocal() :
                    PlatformModuleBuilder.PrepareLocalFromAssets(indexPath);
                receipt["preparationDirectory"] = preparation;
                string modulePath = PlatformModuleBuilder.BuildPrepared(preparation);
                var definition = JObject.Parse(File.ReadAllText(Path.Combine(preparation, "module-definition.json")));
                string manifest = Path.Combine(modulePath, "manifest.json");
                receipt["modulePath"] = modulePath;
                receipt["moduleVersion"] = definition["moduleVersion"].DeepClone();
                receipt["manifestSha256"] = Sha256(manifest);
                receipt["status"] = "built";
                receipt["completedAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                WriteReceipt(receiptPath, receipt);
                Debug.Log("PLATFORM_MODULE_BUILD_RECEIPT " + receiptPath);
            }
            catch (Exception exception)
            {
                receipt["status"] = "failed";
                receipt["error"] = exception.ToString();
                receipt["completedAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                if (!File.Exists(receiptPath)) WriteReceipt(receiptPath, receipt);
                throw;
            }
        }

        static string ReceiptArgument(string[] arguments)
        {
            string value = null;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] != "-platformBuildReceipt") continue;
                if (value != null || i + 1 >= arguments.Length || !Path.IsPathRooted(arguments[i + 1]))
                    throw new BuildFailedException("Specify exactly one -platformBuildReceipt followed by an absolute output path.");
                value = arguments[++i];
            }
            if (value == null)
                throw new BuildFailedException("Missing -platformBuildReceipt <absolute-path.json>.");
            return Path.GetFullPath(value);
        }

        static void WriteReceipt(string path, JObject receipt)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(receipt.ToString(Formatting.Indented) + "\n");
        }

        static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
