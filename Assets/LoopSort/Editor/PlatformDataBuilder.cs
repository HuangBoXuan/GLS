using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace LoopSort0.Editor
{
    // Data preparation only: no Lua VM, gameplay execution or platform identity.
    public static class PlatformDataBuilder
    {
        const int TargetShardBytes = 512 * 1024;
        const int HostDataLimitBytes = 4 * 1024 * 1024;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        sealed class LevelRecord
        {
            public int Id;
            public string Json;
            public int Bytes;
        }

        sealed class Catalog
        {
            public string Name, SourcePath, Version, SourceHash;
            public long SourceBytes;
            public List<LevelRecord> Levels;
        }

        sealed class OutputFile
        {
            public readonly string Path;
            public readonly byte[] Bytes;
            public readonly string Hash;

            public OutputFile(string path, byte[] bytes)
            {
                Path = path;
                Bytes = bytes;
                Hash = Sha256(bytes);
            }
        }

        [MenuItem("LoopSort/平台迁移/导出关卡分片")]
        public static void ExportMenu() => Export();

        public static string Export()
        {
            string project = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            // Parse and validate every source, then construct every output in memory
            // before creating a destination. Invalid input produces no export folder.
            var catalogs = new[] {
                ReadCatalog(project, "main", "Assets/LoopSort/Resources/LoopSort/Data/main.json"),
                ReadCatalog(project, "daily", "Assets/LoopSort/Resources/LoopSort/Data/daily-challenge.json")
            };
            var files = new List<OutputFile>();
            var catalogIndex = new JObject();
            foreach (Catalog catalog in catalogs)
                catalogIndex[catalog.Name] = BuildCatalog(catalog, files);

            var index = new JObject { ["schema"] = 1, ["catalogs"] = catalogIndex };
            byte[] indexBytes = Encode(index);
            RequireHostSize(indexBytes.Length, "levels/index.json");
            files.Add(new OutputFile("levels/index.json", indexBytes));

            var sources = new JArray(catalogs.Select(catalog => new JObject {
                ["catalog"] = catalog.Name, ["path"] = catalog.SourcePath,
                ["version"] = catalog.Version, ["bytes"] = catalog.SourceBytes,
                ["sha256"] = catalog.SourceHash, ["levelCount"] = catalog.Levels.Count
            }));
            var inventory = new JArray(files.OrderBy(file => file.Path, StringComparer.Ordinal).Select(file => new JObject {
                ["path"] = file.Path, ["bytes"] = file.Bytes.LongLength, ["sha256"] = file.Hash
            }));
            DateTime created = DateTime.UtcNow;
            var pack = new JObject {
                ["schema"] = 1,
                ["kind"] = "platform-level-data",
                ["createdAtUtc"] = created.ToString("o", CultureInfo.InvariantCulture),
                ["completePlayableModule"] = false,
                ["destination"] = "data/<host.CurrentGameId>/levels",
                ["copyInstruction"] = "Copy the contents of levels/ into the registered game's data/<gameId>/levels/ directory. data-pack.json is an export inventory, not a platform module manifest.",
                ["targetShardBytes"] = TargetShardBytes,
                ["hostDataLimitBytes"] = HostDataLimitBytes,
                ["sources"] = sources,
                ["files"] = inventory
            };
            byte[] packBytes = Encode(pack);

            string outputRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(project, "Builds", "PlatformData"));
            RequireDescendant(project, outputRoot);
            RejectLinkedAncestors(project, outputRoot);
            string name = created.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            string output = System.IO.Path.GetFullPath(System.IO.Path.Combine(outputRoot, name));
            RequireDescendant(outputRoot, output);
            if (Directory.Exists(output) || File.Exists(output))
                throw new BuildFailedException("Platform data export already exists: " + output);

            Directory.CreateDirectory(output);
            foreach (OutputFile file in files)
                WriteNew(output, file.Path, file.Bytes);
            // Written last: an interrupted export lacks its completed inventory.
            WriteNew(output, "data-pack.json", packBytes);
            Debug.Log("Platform level data exported: " + output + ". This is data preparation, not a runnable platform module.");
            return output;
        }

        static Catalog ReadCatalog(string project, string name, string relativePath)
        {
            byte[] bytes = File.ReadAllBytes(System.IO.Path.Combine(project, relativePath));
            string text = Utf8.GetString(bytes);
            if (text.Length > 0 && text[0] == '\ufeff') text = text.Substring(1);
            JObject source;
            using (var input = new StringReader(text))
            using (var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None })
            {
                source = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new BuildFailedException("Extra JSON content in " + relativePath);
            }
            var versionToken = source["version"];
            string version = versionToken?.Type == JTokenType.String ? versionToken.Value<string>() : null;
            if (string.IsNullOrWhiteSpace(version))
                throw new BuildFailedException("Catalog version must be a nonempty string: " + relativePath);
            var levels = source["levels"] as JArray;
            if (levels == null || levels.Count == 0)
                throw new BuildFailedException("Catalog levels must be a nonempty array: " + relativePath);

            var seen = new HashSet<int>();
            var records = new List<LevelRecord>(levels.Count);
            foreach (JToken item in levels)
            {
                var level = item as JObject;
                JToken idToken = level?["id"];
                if (idToken == null || idToken.Type != JTokenType.Integer ||
                    !int.TryParse(idToken.ToString(Formatting.None), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
                    throw new BuildFailedException("Level IDs must be positive Int32 integers in " + relativePath);
                if (!seen.Add(id)) throw new BuildFailedException("Duplicate level ID " + id + " in " + relativePath);
                // Serialize the entire JObject; no level fields are projected away.
                string json = level.ToString(Formatting.None);
                records.Add(new LevelRecord { Id = id, Json = json, Bytes = Utf8.GetByteCount(json) });
            }
            records.Sort((a, b) => a.Id.CompareTo(b.Id));
            return new Catalog {
                Name = name, SourcePath = relativePath, Version = version,
                SourceHash = Sha256(bytes), SourceBytes = bytes.LongLength, Levels = records
            };
        }

        static JObject BuildCatalog(Catalog catalog, List<OutputFile> files)
        {
            string prefix = "{\"version\":" + JsonConvert.SerializeObject(catalog.Version) + ",\"levels\":[";
            int envelopeBytes = Utf8.GetByteCount(prefix) + 2; // ]}
            int currentBytes = envelopeBytes;
            var pending = new List<LevelRecord>();
            var shards = new JArray();
            foreach (LevelRecord level in catalog.Levels)
            {
                RequireHostSize((long)envelopeBytes + level.Bytes, catalog.Name + " level " + level.Id);
                int addition = level.Bytes + (pending.Count > 0 ? 1 : 0);
                if (pending.Count > 0 && (long)currentBytes + addition > TargetShardBytes)
                {
                    AppendShard(catalog.Name, prefix, pending, shards, files);
                    pending.Clear(); currentBytes = envelopeBytes; addition = level.Bytes;
                }
                pending.Add(level); currentBytes += addition;
            }
            if (pending.Count > 0) AppendShard(catalog.Name, prefix, pending, shards, files);
            return new JObject { ["version"] = catalog.Version, ["shards"] = shards };
        }

        static void AppendShard(string catalog, string prefix, List<LevelRecord> levels, JArray shards, List<OutputFile> files)
        {
            if (shards.Count >= 9999)
                throw new BuildFailedException("Too many shards for the four-digit catalog path format: " + catalog);
            string path = catalog + "/" + (shards.Count + 1).ToString("D4", CultureInfo.InvariantCulture) + ".json";
            byte[] bytes = Utf8.GetBytes(prefix + string.Join(",", levels.Select(level => level.Json)) + "]}");
            RequireHostSize(bytes.Length, path);
            if (levels.Count > 1 && bytes.Length > TargetShardBytes)
                throw new BuildFailedException("Multi-level shard exceeded its target size: " + path);
            files.Add(new OutputFile("levels/" + path, bytes));
            shards.Add(new JObject { ["path"] = path, ["ids"] = new JArray(levels.Select(level => level.Id)) });
        }

        static void RequireHostSize(long bytes, string name)
        {
            if (bytes >= HostDataLimitBytes)
                throw new BuildFailedException("Data must be smaller than the host's 4 MiB limit: " + name + " (" + bytes + " bytes)");
        }

        static byte[] Encode(JToken json) => Utf8.GetBytes(json.ToString(Formatting.Indented) + "\n");

        static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        static void RequireDescendant(string parent, string child)
        {
            string prefix = System.IO.Path.GetFullPath(parent).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
                + System.IO.Path.DirectorySeparatorChar;
            if (!System.IO.Path.GetFullPath(child).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Platform export path escapes its expected directory: " + child);
        }

        static void RejectLinkedAncestors(string project, string directory)
        {
            string stop = System.IO.Path.GetFullPath(project);
            for (string current = directory; !string.Equals(current, stop, StringComparison.OrdinalIgnoreCase); current = System.IO.Path.GetDirectoryName(current))
            {
                RequireDescendant(stop, current);
                if ((Directory.Exists(current) || File.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new BuildFailedException("Platform export cannot traverse a linked directory: " + current);
            }
        }

        static void WriteNew(string output, string relative, byte[] bytes)
        {
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(output, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            RequireDescendant(output, path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes, 0, bytes.Length);
        }
    }
}
