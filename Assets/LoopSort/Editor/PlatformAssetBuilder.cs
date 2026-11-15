using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LoopSort0.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LoopSort0.Editor
{
    // Authoring only. This creates native assets without starting Lua or gameplay.
    // Every generated prefab contains Unity components; no game MonoBehaviour is serialized.
    public static class PlatformAssetBuilder
    {
        const string Source = "Assets/LoopSort/Resources/LoopSort";
        const string Root = "Assets/LoopSort/PlatformAssets";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        public sealed class Result
        {
            public string[] AssetRoots;
            public string IndexPath;
            public string GeneratedRoot;
        }

        public static Result Prepare(string gameId)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Native asset preparation requires Edit Mode.");
            if (string.IsNullOrWhiteSpace(gameId) || gameId.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
                throw new ArgumentException("Invalid module gameId.", nameof(gameId));
            var builder = new Builder(gameId);
            return builder.Run();
        }
        // An art revision reuses all imported geometry/tracks and rebuilds only
        // native visual prefabs/materials. Route and collision data stay intact.
        public static Result UpdateVolcanoSkins(string gameId)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update art in Edit Mode");
            if(!File.Exists(Root+"/index.json"))return Prepare(gameId);
            return new Builder(gameId).UpdateVolcanoSkins();
        }
        public static Result UpdateReferenceTracks(string gameId)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update tracks in Edit Mode");
            if(!File.Exists(Root+"/index.json"))return Prepare(gameId);
            return new Builder(gameId).UpdateReferenceTracks();
        }

        sealed class Builder
        {
            readonly string gameId;
            readonly HashSet<string> roots = new HashSet<string>(StringComparer.Ordinal);
            readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
            readonly Dictionary<int, Material> features = new Dictionary<int, Material>();
            readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
            readonly Dictionary<string, string> trackPaths = new Dictionary<string, string>();
            readonly JObject index = new JObject();
            PhysicMaterial transport;
            public Builder(string gameId) { this.gameId = gameId; }
            public Result UpdateReferenceTracks()
            {
                var previous=JObject.Parse(File.ReadAllText(Root+"/index.json"));
                foreach(var p in previous.Properties())index[p.Name]=p.Value.DeepClone();
                Named("floor",ReferenceConveyor.Material());Named("accent",ReferenceConveyor.Material(true));
                transport=AssetDatabase.LoadAssetAtPath<PhysicMaterial>((string)index["physicsMaterial"]);
                BuildTracks("main","main.json");BuildTracks("daily","daily-challenge.json");
                AssetDatabase.SaveAssets();
                string path=Root+"/index.json";File.WriteAllText(path,index.ToString(Formatting.Indented)+"\n",Utf8);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Reference raised conveyor installed for "+trackPaths.Count+" layouts; existing collision assets retained.");
                return new Result{AssetRoots=roots.ToArray(),IndexPath=Path.GetFullPath(path),GeneratedRoot=Root};
            }
            public Result UpdateVolcanoSkins()
            {
                var previous=JObject.Parse(File.ReadAllText(Root+"/index.json"));
                foreach(var p in previous.Properties())index[p.Name]=p.Value.DeepClone();
                foreach(var p in ((JObject)index["meshes"]).Properties())if(int.TryParse(p.Name,out int id))meshes[id]=AssetDatabase.LoadAssetAtPath<Mesh>((string)p.Value);
                foreach(var p in ((JObject)index["materials"]).Properties())if(!p.Name.StartsWith("volcano-",StringComparison.Ordinal))materials[p.Name]=AssetDatabase.LoadAssetAtPath<Material>((string)p.Value);
                foreach(var p in ((JObject)index["featureMaterials"]).Properties())features[int.Parse(p.Name)]=AssetDatabase.LoadAssetAtPath<Material>((string)p.Value);
                var ore=AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"/Theme/mine-ore.png");
                materials["cargo"].mainTexture=ore;EditorUtility.SetDirty(materials["cargo"]);
                materials["hidden"].mainTexture=ore;VolcanoArt.ConfigureHidden(materials["hidden"]);EditorUtility.SetDirty(materials["hidden"]);
                index["materials"]["signin-card"]=Add(Source+"/Shaders/SigninCard.mat");
                index["textures"]=new JObject();
                AddOriginalFiles("Theme","textures",".png");
                index["data"]["volcano-art"]=Add(Source+"/Data/volcano-art.json");
                BuildTemplate(Read("Data/carrier-visual.json"),"carrier","Carrier",true);
                BuildTemplate(Read("Data/long-carrier-visual.json"),"long-carrier","Carrier",true);
                foreach(JObject template in (JArray)Read("Data/feature-visuals.json")["visuals"])BuildTemplate(template,(string)template["id"],"Feature",false);
                BuildVolcanoLid();AssetDatabase.SaveAssets();
                string path=Root+"/index.json";File.WriteAllText(path,index.ToString(Formatting.Indented)+"\n",Utf8);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                return new Result{AssetRoots=roots.ToArray(),IndexPath=Path.GetFullPath(path),GeneratedRoot=Root};
            }

            public Result Run()
            {
                Directory.CreateDirectory(Root);
                foreach (string folder in new[] { "Meshes", "Materials", "Prefabs", "Tracks" }) Directory.CreateDirectory(Root + "/" + folder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                index["schema"] = 1; index["bundle"] = gameId + ".bundle";
                foreach (string section in new[] { "meshes", "primaryMeshes", "materials", "featureMaterials", "textures", "audio", "data", "animations", "prefabs", "tracks" }) index[section] = new JObject();
                index["tracks"]["main"] = new JObject(); index["tracks"]["daily"] = new JObject();
                AddOriginalFiles("Theme", "textures", ".png");
                AddOriginalFiles("Audio", "audio", null);
                AddOriginalFiles("Animations", "animations", ".json");
                AddOriginalFiles("Data", "data", ".json", "main", "daily-challenge");
                index["atlas"] = Add(Source + "/Theme/atlas.json");
                string font = Source + "/Fonts/NotoSansSC-Bold.otf";
                index["font"] = Add(font);
                foreach (string path in Directory.GetFiles(Source + "/Models", "*.obj").OrderBy(p => p, StringComparer.Ordinal))
                {
                    int id = int.Parse(Path.GetFileNameWithoutExtension(path));
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Slash(path));
                    if (model == null) throw new InvalidOperationException("Model import unavailable: " + path);
                    MeshFilter first = model.GetComponentInChildren<MeshFilter>(true);
                    Mesh primary = first != null ? first.sharedMesh : model.GetComponentInChildren<SkinnedMeshRenderer>(true)?.sharedMesh;
                    if (primary == null) throw new InvalidOperationException("Model has no mesh: " + path);
                    index["primaryMeshes"][id.ToString()] = SaveMesh(Object.Instantiate(primary), "primary-" + id);
                    Mesh complete = BuildComplete(model, id);
                    string completePath = SaveMesh(complete, id.ToString()); meshes[id] = AssetDatabase.LoadAssetAtPath<Mesh>(completePath);
                    index["meshes"][id.ToString()] = completePath;
                }
                foreach (int id in new[] { 377, 393, 395, 413, 427 })
                {
                    string path = SaveMesh(BuildMorph(id), "morph-" + id);
                    meshes[id] = AssetDatabase.LoadAssetAtPath<Mesh>(path); index["meshes"][id.ToString()] = path;
                }
                index["meshes"]["cell"] = SaveMesh(BuildCell(), "cell");
                BuildMaterials();
                var physics = new PhysicMaterial("LoopSort transport") { dynamicFriction = 0, staticFriction = 0, bounciness = .6f,
                    frictionCombine = PhysicMaterialCombine.Maximum, bounceCombine = PhysicMaterialCombine.Maximum };
                string physicsPath = Save(physics, Root + "/Materials/transport.physicMaterial");
                transport = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(physicsPath); index["physicsMaterial"] = physicsPath;
                BuildTemplate(Read("Data/carrier-visual.json"), "carrier", "Carrier", true);
                BuildTemplate(Read("Data/long-carrier-visual.json"), "long-carrier", "Carrier", true);
                foreach (JObject template in (JArray)Read("Data/feature-visuals.json")["visuals"])
                    BuildTemplate(template, (string)template["id"], "Feature", false);
                BuildVolcanoLid();
                BuildParticles();
                BuildTracks("main", "main.json"); BuildTracks("daily", "daily-challenge.json");
                AssetDatabase.SaveAssets();
                string indexPath = Root + "/index.json";
                File.WriteAllText(indexPath, index.ToString(Formatting.Indented) + "\n", Utf8);
                AssetDatabase.ImportAsset(indexPath, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Native platform assets prepared: " + roots.Count + " assets, " + trackPaths.Count + " distinct conveyor layouts.");
                return new Result { AssetRoots = roots.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
                    IndexPath = Path.GetFullPath(indexPath), GeneratedRoot = Root };
            }

            static JObject Read(string relative) => JObject.Parse(File.ReadAllText(Source + "/" + relative));
            string Add(string path) { path = Slash(path); roots.Add(path); return path; }
            void AddOriginalFiles(string folder, string section, string extension, params string[] excluded)
            {
                foreach (string path in Directory.GetFiles(Source + "/" + folder).OrderBy(p => p, StringComparer.Ordinal))
                {
                    if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || (extension != null && !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))) continue;
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (excluded.Contains(name)) continue;
                    if (AssetDatabase.LoadMainAssetAtPath(Slash(path)) == null) throw new InvalidOperationException("Missing imported asset " + path);
                    index[section][name] = Add(path);
                }
            }
            string Save(Object asset, string path)
            {
                Object existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null)
                {
                    if (existing.GetType() != asset.GetType()) throw new InvalidOperationException("Generated asset type changed: " + path);
                    // Invalidate native vertex buffers as well as serialized
                    // fields when replacing an already loaded generated mesh.
                    if(existing is Mesh previousMesh)previousMesh.Clear();
                    EditorUtility.CopySerialized(asset, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(asset);
                    if(existing is Mesh updatedMesh)updatedMesh.UploadMeshData(false);
                }
                else AssetDatabase.CreateAsset(asset, path);
                return Add(path);
            }
            string SaveMesh(Mesh mesh, string name) { mesh.name = "LoopSort " + name; return Save(mesh, Root + "/Meshes/" + name + ".asset"); }
            static Mesh BuildComplete(GameObject model, int id)
            {
                var parts = new List<CombineInstance>();
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                    for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                        parts.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub,
                            transform = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                var mesh = new Mesh { name = "Complete " + id, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(parts.ToArray(), false, true); mesh.RecalculateBounds(); return mesh;
            }
            static Mesh BuildMorph(int id)
            {
                JObject source = Read("Animations/" + id + ".json");
                var vertices = ((JArray)source["vertices"]).Select(Vector).ToArray();
                var mesh = new Mesh { name = "Morph " + id, indexFormat = IndexFormat.UInt32,
                    vertices = vertices, normals = ((JArray)source["normals"]).Select(Vector).ToArray(),
                    uv = ((JArray)source["uv"]).Select(v => new Vector2((float)v[0], (float)v[1])).ToArray(), subMeshCount = ((JArray)source["submeshes"]).Count };
                int sub = 0; foreach (JArray indices in (JArray)source["submeshes"]) mesh.SetTriangles(indices.Select(v => (int)v).ToArray(), sub++);
                foreach (JObject channel in (JArray)source["channels"]) foreach (JObject frame in (JArray)channel["frames"])
                {
                    var delta = new Vector3[vertices.Length];
                    foreach (JObject value in (JArray)frame["vertices"]) delta[(int)value["index"]] = Vector(value["position"]);
                    mesh.AddBlendShapeFrame((string)channel["name"], (float)frame["weight"], delta, null, null);
                }
                mesh.RecalculateBounds(); return mesh;
            }
            static Mesh BuildCell()
            {
                var vertices = new Vector3[24]; var normals = new Vector3[24]; var triangles = new int[36];
                Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                for (int face = 0; face < faces.Length; face++)
                {
                    Vector3 n = faces[face], u = Mathf.Abs(n.y) > .5f ? Vector3.right : Vector3.up, v = Vector3.Cross(n, u);
                    int a = face * 4, t = face * 6;
                    vertices[a] = (n-u-v)*.5f; vertices[a+1]=(n+u-v)*.5f; vertices[a+2]=(n+u+v)*.5f; vertices[a+3]=(n-u+v)*.5f;
                    for(int i=0;i<4;i++) normals[a+i]=n;
                    triangles[t]=a;triangles[t+1]=a+1;triangles[t+2]=a+2;triangles[t+3]=a;triangles[t+4]=a+2;triangles[t+5]=a+3;
                }
                var mesh = new Mesh {vertices=vertices,normals=normals,triangles=triangles}; mesh.RecalculateBounds();return mesh;
            }
            Material Surface(string name, Color color, int pattern = 0)
            {
                var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(Source + "/Shaders/ToySurface.shader")) { name=name, color=color, enableInstancing=true };
                material.SetFloat("_Pattern", pattern); return material;
            }
            void Named(string name, Material material)
            {
                string path = Save(material, Root + "/Materials/" + name + ".mat");
                materials[name]=AssetDatabase.LoadAssetAtPath<Material>(path); index["materials"][name] = path;
            }
            void BuildMaterials()
            {
                var colors = new Dictionary<string,uint> {{"ink",0x49263E},{"cream",0xFFF0D0},{"plum",0x603B55},{"mint",0x61C7B4},
                    {"teal",0x286363},{"amber",0xFFB83E},{"caramel",0xC88D65},{"coral",0xFF625E},{"accent",0xB8F3D8}};
                foreach(var pair in colors) Named(pair.Key,Surface(pair.Key,Hex(pair.Value)));
                Named("floor",ReferenceConveyor.Material());Named("accent",ReferenceConveyor.Material(true));
                Named("backdrop",new Material(AssetDatabase.LoadAssetAtPath<Shader>(Source+"/Shaders/ToyBackdrop.shader")));
                index["materials"]["signin-card"]=Add(Source+"/Shaders/SigninCard.mat");
                Named("cargo",Object.Instantiate(AssetDatabase.LoadAssetAtPath<Material>(Source+"/Shaders/CargoInstanced.mat")));
                var hidden=new Material(AssetDatabase.LoadAssetAtPath<Shader>(Source+"/Shaders/HiddenCargo.shader"));
                hidden.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"/Theme/mine-ore.png");VolcanoArt.ConfigureHidden(hidden);Named("hidden",hidden);
                var outline=new Material(AssetDatabase.LoadAssetAtPath<Shader>(Source+"/Shaders/CargoOutline.shader"));outline.color=Color.black;Named("outline",outline);
                var keyOutline=new Material(materials["outline"]);keyOutline.SetFloat("_Width",.028f);Named("keyOutline",keyOutline);
                var surfaces=new Dictionary<int,Tuple<uint,int>>();
                Action<int,uint,int> put=(id,color,pattern)=>surfaces[id]=Tuple.Create(color,pattern);
                foreach(int id in new[]{2,205,9,11})put(id,colors["amber"],0);
                foreach(int id in new[]{8,10})put(id,colors["mint"],2);
                foreach(int id in new[]{199,31,25})put(id,colors["cream"],0);
                foreach(int id in new[]{14,23})put(id,0xEBAE52,1);
                foreach(int id in new[]{15,206})put(id,colors["caramel"],5);
                foreach(int id in new[]{16,30})put(id,colors["ink"],0);
                put(22,colors["amber"],4);put(24,0xAFE9E7,6);put(26,colors["cream"],3);put(27,colors["caramel"],0);
                foreach(int id in new[]{28,32})put(id,colors["plum"],0);
                foreach(int id in new[]{33,202})put(id,colors["mint"],0);
                put(171,0x57D36A,0);put(177,colors["coral"],0);put(201,colors["teal"],0);
                // Volcano mechanisms: lava mouth, basalt interior and copper trim.
                put(202,0xF46B2D,0);put(201,0x342A2C,0);put(25,0xD89448,0);
                put(8,0xF46B2D,2);put(10,0xF46B2D,2);put(9,0xD89448,0);put(11,0xD89448,0);
                foreach(JObject item in (JArray)Read("Data/feature-visuals.json")["materials"])
                {
                    int id=(int)item["id"];var style=surfaces.TryGetValue(id,out var value)?value:Tuple.Create(colors["cream"],0);
                    Material material=Surface("Feature "+id,Hex(style.Item1),style.Item2);
                    if(id==8||id==9||id==10){material.SetColor("_EdgeColor",Hex(0xF7B45A));material.SetFloat("_EdgeWidth",.065f);}
                    if(id==24){material.SetFloat("_Opacity",.42f);material.SetInt("_SrcBlend",5);material.SetInt("_DstBlend",10);material.SetInt("_ZWrite",0);material.renderQueue=3000;}
                    if(item["textureScale"]!=null)material.mainTextureScale=new Vector2((float)item["textureScale"]["x"],(float)item["textureScale"]["y"]);
                    if(item["textureOffset"]!=null)material.mainTextureOffset=new Vector2((float)item["textureOffset"]["x"],(float)item["textureOffset"]["y"]);
                    string path=Save(material,Root+"/Materials/feature-"+id+".mat");features[id]=AssetDatabase.LoadAssetAtPath<Material>(path);index["featureMaterials"][id.ToString()]=path;
                }
            }
            void BuildTemplate(JObject source,string name,string sourceRoot,bool carrier)
            {
                var root=new GameObject(name);var nodes=new Dictionary<string,Transform>{{sourceRoot,root.transform}};
                try
                {
                    foreach(JObject node in ((JArray)source["nodes"]).OrderBy(n=>((string)n["path"]).Count(c=>c=='/')))
                    {
                        string path=(string)node["path"];int slash=path.LastIndexOf('/');if(slash<0)continue;
                        if(!nodes.TryGetValue(path.Substring(0,slash),out Transform parent))throw new InvalidOperationException("Visual parent missing: "+path);
                        var child=new GameObject(path.Substring(slash+1)).transform;child.SetParent(parent,false);
                        Apply(child,node["transform"]);nodes[path]=child;
                    }
                    foreach(JObject part in (JArray)source["parts"])
                    {
                        if(!nodes.TryGetValue((string)part["path"],out Transform target))throw new InvalidOperationException("Visual node missing: "+part["path"]);
                        int id=(int)part["mesh"];Mesh mesh=meshes[id];int[] ids=((JArray)part["materialIds"]).Select(v=>(int)v).ToArray();
                        var mats=new Material[Math.Max(mesh.subMeshCount,ids.Length)];bool wheel=(bool?)part["wheel"]??false;
                        for(int i=0;i<mats.Length;i++)
                        {
                            int material=ids[Math.Min(i,ids.Length-1)];
                            mats[i]=carrier?(wheel?(i==0?materials["ink"]:materials["caramel"]):material==31||material==32?materials["plum"]:materials["cream"])
                                :features.TryGetValue(material,out Material feature)?feature:materials["cream"];
                        }
                        if(mesh.blendShapeCount>0){var skin=target.gameObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterials=mats;skin.updateWhenOffscreen=true;skin.localBounds=mesh.bounds;}
                        else{target.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;target.gameObject.AddComponent<MeshRenderer>().sharedMaterials=mats;}
                    }
                    if(name=="carrier")
                    {
                        var deck=new GameObject("Cargo deck",typeof(MeshFilter),typeof(MeshRenderer));deck.transform.SetParent(root.transform,false);
                        deck.transform.localPosition=new Vector3(0,.91f,-.2f);deck.transform.localScale=new Vector3(3.6f,.15f,8);
                        deck.GetComponent<MeshFilter>().sharedMesh=meshes[382];deck.GetComponent<MeshRenderer>().sharedMaterial=materials["plum"];
                    }
                    if(carrier){var click=root.AddComponent<BoxCollider>();click.isTrigger=true;click.center=source["colliderCenter"]!=null?Vector(source["colliderCenter"]):new Vector3(0,1.2f,0);click.size=source["colliderSize"]!=null?Vector(source["colliderSize"]):new Vector3(2.15f,2.8f,4.8f);}
                    VolcanoArt.Apply(root,name,VolcanoMaterial);
                    PersistVolcanoMeshes(root,name);
                    string prefabPath=Root+"/Prefabs/"+name+".prefab";PrefabUtility.SaveAsPrefabAsset(root,prefabPath);index["prefabs"][name]=Add(prefabPath);
                    AssetDatabase.ImportAsset(prefabPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                }
                finally{Object.DestroyImmediate(root);}
            }

            Material VolcanoMaterial(string id)
            {
                string key="volcano-"+id;
                if(materials.TryGetValue(key,out var found))return found;
                var material=VolcanoArt.CreateMaterial(id);if(material==null)return null;
                string path=Save(material,Root+"/Materials/"+key+".mat");
                found=AssetDatabase.LoadAssetAtPath<Material>(path);materials[key]=found;index["materials"][key]=path;return found;
            }
            void PersistVolcanoMeshes(GameObject root,string id)
            {
                int part=0;
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null||!filter.sharedMesh.name.StartsWith("Volcano 3D /",StringComparison.Ordinal))continue;
                    // Runtime mesh instances cannot be referenced by a saved
                    // prefab. Give each moving part a stable native mesh asset.
                    string path=SaveMesh(Object.Instantiate(filter.sharedMesh),"volcano-"+id+"-"+(part++));
                    filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                }
            }
            void BuildVolcanoLid()
            {
                var root=new GameObject("Candy color lid");
                try
                {
                    if(!VolcanoArt.Apply(root,"curtain",VolcanoMaterial))return;
                    PersistVolcanoMeshes(root,"lid");
                    string path=Root+"/Prefabs/volcano-lid.prefab";
                    PrefabUtility.SaveAsPrefabAsset(root,path);index["prefabs"]["volcano-lid"]=Add(path);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                }
                finally{Object.DestroyImmediate(root);}
            }

            void BuildTracks(string kind,string filename)
            {
                foreach(JObject level in (JArray)Read("Data/"+filename)["levels"])
                {
                    bool closed=(bool?)level["closed"]??true;int subdivide=(int?)level["subdivide"]??1;
                    string descriptor=level["points"].ToString(Formatting.None)+"/"+closed+"/"+subdivide;
                    string hash=Hash(descriptor).Substring(0,24);
                    if(!trackPaths.TryGetValue(hash,out string path))
                    {
                        TrackPoint[] points=level["points"].ToObject<TrackPoint[]>();
                        path=BuildTrack(new NativeSpline(points,closed,subdivide),hash);trackPaths[hash]=path;
                    }
                    index["tracks"][kind][((int)level["id"]).ToString()]=path;
                }
            }
            void BuildParticles()
            {
                JObject source=Read("Animations/source-particles.json");
                var particleMaterials=new Dictionary<int,Material>();
                foreach(JObject item in (JArray)source["materials"])
                {
                    int id=(int)item["id"];
                    Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(Source+"/Shaders/"+(Bool(item,"lit")?"SourceParticleLit":"SourceParticle")+".shader");
                    var material=new Material(shader){name="Source particles "+(string)item["name"]};
                    material.SetColor("_Color",ColorValue(item["color"]));
                    material.SetFloat("_SrcBlend",Float(item,"srcBlend"));material.SetFloat("_DstBlend",Float(item,"dstBlend"));
                    material.SetFloat("_Cull",Float(item,"cull"));material.SetFloat("_ZWrite",Float(item,"zWrite"));
                    material.SetFloat("_Soft",Bool(item,"soft")?1:0);material.SetFloat("_SoftNear",Float(item,"softNear"));
                    material.SetFloat("_SoftRange",Mathf.Max(.0001f,Float(item,"softFar",1)-Float(item,"softNear")));
                    material.SetFloat("_Metallic",Float(item,"metallic"));material.SetFloat("_Glossiness",Float(item,"smoothness"));
                    int texture=Int(item,"texture");
                    if(texture!=0)
                    {
                        string texturePath=Source+"/Textures/particle-"+texture+".png";
                        material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                        if(material.mainTexture==null)throw new InvalidOperationException("Missing particle texture: "+texturePath);
                        Add(texturePath);
                    }
                    material.mainTextureScale=new Vector2(Float(item["scale"],"x",1),Float(item["scale"],"y",1));
                    material.mainTextureOffset=new Vector2(Float(item["offset"],"x"),Float(item["offset"],"y"));
                    int queue=Int(item,"queue",-1);if(queue>=0)material.renderQueue=queue;
                    string path=Save(material,Root+"/Materials/particle-"+id+".mat");particleMaterials[id]=AssetDatabase.LoadAssetAtPath<Material>(path);
                }
                // The source module authoring code only assigns native module settings.
                // Reuse it through reflection because it is intentionally internal to Runtime.
                // No SourceParticleEffect component, Play, Simulate or Lua entry is invoked.
                Type author=typeof(GameAssets).Assembly.GetType("LoopSort0.SourceParticleModules",true);
                var apply=author.GetMethod("Apply",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                if(apply==null)throw new MissingMethodException(author.FullName,"Apply");
                foreach(JObject effect in (JArray)source["effects"])
                {
                    string id=(string)effect["id"];var root=new GameObject("Particles "+id);
                    try
                    {
                        foreach(JObject node in (JArray)effect["nodes"])
                        {
                            string path=(string)node["path"];Transform target=root.transform;
                            foreach(string part in ("Emitter"+(path.Length==0?"":"/"+path)).Split('/'))
                            {
                                Transform child=target.Find(part);
                                if(child==null){child=new GameObject(part).transform;child.SetParent(target,false);}
                                target=child;
                            }
                            Apply(target,node["transform"]);if(path.Length==0)target.localPosition=Vector3.zero;
                            JToken systemData=node["system"];
                            if(systemData==null||systemData.Type==JTokenType.Null)continue;
                            ParticleSystem system=target.gameObject.AddComponent<ParticleSystem>();
                            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                            try{apply.Invoke(null,new object[]{system,systemData});}
                            catch(System.Reflection.TargetInvocationException exception){throw new InvalidOperationException("Particle authoring failed for "+id,exception.InnerException??exception);}
                            ParticleSystemRenderer renderer=system.GetComponent<ParticleSystemRenderer>();JToken data=node["renderer"];
                            if(data==null||data.Type==JTokenType.Null){renderer.enabled=false;continue;}
                            renderer.enabled=Bool(data,"m_Enabled");renderer.renderMode=(ParticleSystemRenderMode)Int(data,"m_RenderMode");
                            renderer.alignment=(ParticleSystemRenderSpace)Int(data,"m_RenderAlignment");renderer.sortMode=(ParticleSystemSortMode)Int(data,"m_SortMode");
                            renderer.minParticleSize=Float(data,"m_MinParticleSize");renderer.maxParticleSize=Float(data,"m_MaxParticleSize",.5f);
                            renderer.cameraVelocityScale=Float(data,"m_CameraVelocityScale");renderer.velocityScale=Float(data,"m_VelocityScale");
                            renderer.lengthScale=Float(data,"m_LengthScale",2);renderer.sortingFudge=Float(data,"m_SortingFudge");renderer.normalDirection=Float(data,"m_NormalDirection",1);
                            renderer.pivot=OptionalVector(data["m_Pivot"]);renderer.flip=OptionalVector(data["m_Flip"]);renderer.allowRoll=Bool(data,"m_AllowRoll",true);
                            renderer.sortingOrder=Int(data,"m_SortingOrder");renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                            if(renderer.renderMode==ParticleSystemRenderMode.Mesh)
                            {
                                int meshId=Int(data["m_Mesh"],"m_PathID");
                                if(meshes.TryGetValue(meshId,out Mesh mesh))renderer.mesh=mesh;
                                else throw new InvalidOperationException("Missing particle mesh "+meshId);
                            }
                            var references=data["m_Materials"] as JArray;
                            if(references!=null&&references.Count>0)
                            {
                                int material=Int(references[0],"m_PathID");renderer.sharedMaterial=material==0?null:particleMaterials[material];
                                if(references.Count>1){material=Int(references[1],"m_PathID");renderer.trailMaterial=material==0?null:particleMaterials[material];}
                            }
                        }
                        string prefabPath=Root+"/Prefabs/particle-"+id+".prefab";PrefabUtility.SaveAsPrefabAsset(root,prefabPath);index["prefabs"]["particle-"+id]=Add(prefabPath);
                    }
                    finally{Object.DestroyImmediate(root);}
                }
            }
            string BuildTrack(NativeSpline spline,string hash)
            {
                var root=new GameObject("Conveyor");
                try
                {
                    AddTrackMesh(root,"Raised colour bands",ReferenceConveyor.Build(spline),materials["floor"],hash);
                    int arrows=Mathf.CeilToInt(spline.Length/1.8f);var av=new Vector3[arrows*3];var at=new int[arrows*3];
                    for(int i=0;i<arrows;i++)
                    {
                        float d=i*1.8f;Vector3 p=spline.AtDistance(d)+Vector3.up*.012f;Vector3 forward=spline.AtDistance(d+.1f)-spline.AtDistance(d);
                        if(forward.sqrMagnitude<.00001f)forward=Vector3.forward;Quaternion q=Quaternion.LookRotation(forward);
                        av[3*i]=p+q*new Vector3(-.22f,0,-.18f);av[3*i+1]=p+q*new Vector3(0,0,.27f);av[3*i+2]=p+q*new Vector3(.22f,0,-.18f);
                        at[3*i]=3*i;at[3*i+1]=3*i+1;at[3*i+2]=3*i+2;
                    }
                    AddTrackMesh(root,"Arrows",Mesh(av,at),materials["accent"],hash);
                    string collisionPath=Root+"/Tracks/"+hash+"-collision.asset";
                    if(AssetDatabase.LoadAssetAtPath<Mesh>(collisionPath)==null)Save(Collision(spline),collisionPath);
                    var collider=root.AddComponent<MeshCollider>();collider.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(collisionPath);collider.sharedMaterial=transport;collider.contactOffset=.01f;collider.cookingOptions=(MeshColliderCookingOptions)30;
                    string path=Root+"/Tracks/"+hash+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);return Add(path);
                }
                finally{Object.DestroyImmediate(root);}
            }
            void AddTrackMesh(GameObject root,string name,Mesh mesh,Material material,string hash)
            {
                string path=Save(mesh,Root+"/Tracks/"+hash+"-"+name.Replace(' ','_')+".asset");
                var child=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(root.transform,false);
                child.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);child.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
                static Mesh Collision(NativeSpline spline)
            {
                int spans=Mathf.Max(1,Mathf.CeilToInt(spline.Length*4)),rings=spline.Closed?spans:spans+1;
                var vertices=new List<Vector3>();var triangles=new List<int>();
                for(int strip=0;strip<4;strip++)
                {
                    float minX,maxX,minY,maxY;
                    if(strip==0){minX=-.65f;maxX=.65f;minY=-.1f;maxY=0;}
                    else if(strip==3){minX=-.65f;maxX=.65f;minY=.75f;maxY=.85f;}
                    else{float center=strip==1?-.66f:.66f;minX=center-.05f;maxX=center+.05f;minY=0;maxY=.8f;}
                    int first=vertices.Count;
                    for(int i=0;i<rings;i++)
                    {
                        spline.Evaluate(i/(double)spans,out Vector3 center,out Vector3 forward,out Vector3 normal,out _);
                        Vector3 right=Vector3.Cross(normal,forward).normalized,up=Vector3.Cross(forward,right).normalized;
                        vertices.Add(center+right*minX+up*minY);vertices.Add(center+right*maxX+up*minY);vertices.Add(center+right*maxX+up*maxY);vertices.Add(center+right*minX+up*maxY);
                    }
                    for(int i=0;i<spans;i++)for(int edge=0;edge<4;edge++)Quad(triangles,first+i*4+edge,first+i*4+(edge+1)%4,first+((i+1)%rings)*4+(edge+1)%4,first+((i+1)%rings)*4+edge);
                    if(!spline.Closed){Quad(triangles,first,first+3,first+2,first+1);int end=first+(rings-1)*4;Quad(triangles,end,end+1,end+2,end+3);}
                }
                return Mesh(vertices.ToArray(),triangles.ToArray());
            }
        }
        static Mesh Mesh(Vector3[] vertices,int[] triangles,Vector2[] uv=null,Vector2[] uv2=null)
        {
            var mesh=new Mesh {indexFormat=vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16,vertices=vertices,triangles=triangles};
            if(uv!=null)mesh.uv=uv;if(uv2!=null)mesh.uv2=uv2;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void Quad(List<int> triangles,int a,int b,int c,int d){triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);}
        static Vector3 Vector(JToken value)=>value is JArray?new Vector3((float)value[0],(float)value[1],(float)value[2]):new Vector3((float)value["x"],(float)value["y"],(float)value["z"]);
        static void Apply(Transform target,JToken data)
        {
            target.localPosition=Vector(data["m_LocalPosition"]);target.localScale=Vector(data["m_LocalScale"]);JToken q=data["m_LocalRotation"];
            target.localRotation=new Quaternion((float)q["x"],(float)q["y"],(float)q["z"],(float)q["w"]);
        }
        static Color Hex(uint value)=>new Color(((value>>16)&255)/255f,((value>>8)&255)/255f,(value&255)/255f,1);
        static string Slash(string path)=>path.Replace('\\','/');
        static float Float(JToken source,string key,float fallback=0)=>source?[key]==null?fallback:(float)source[key];
        static int Int(JToken source,string key,int fallback=0)=>source?[key]==null?fallback:(int)source[key];
        static bool Bool(JToken source,string key,bool fallback=false)=>source?[key]==null?fallback:(bool)source[key];
        static Vector3 OptionalVector(JToken source)=>new Vector3(Float(source,"x"),Float(source,"y"),Float(source,"z"));
        static Color ColorValue(JToken source)=>new Color(Float(source,"r",1),Float(source,"g",1),Float(source,"b",1),Float(source,"a",1));
        static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    }
}
