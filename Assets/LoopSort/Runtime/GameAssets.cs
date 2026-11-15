using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace LoopSort0
{
    public static class GameAssets
    {
        const string Root = "LoopSort/";
        static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Mesh> completeMeshes = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, MaterialPropertyBlock> cargoColors = new Dictionary<int, MaterialPropertyBlock>();
        static Material cargoMaterial;
        static Dictionary<int, Color> palette;
        public static T Data<T>(string name) => JsonConvert.DeserializeObject<T>(HotUpdate.HotContent.Text(Root + "Data/" + name));
        public static T AnimationData<T>(string name)=>JsonConvert.DeserializeObject<T>(HotUpdate.HotContent.Text(Root+"Animations/"+name));
        public static Sprite Sprite(string name) => ArtTheme.Sprite(name);
        public static Font Font => HotUpdate.HotContent.Load<Font>(Root + "Fonts/NotoSansSC-Bold");
        public static AudioClip Audio(string name) => HotUpdate.HotContent.Load<AudioClip>(Root + "Audio/" + name);
        public static Mesh Mesh(int id)
        {
            if (meshes.TryGetValue(id, out Mesh mesh) && mesh != null) return mesh;
            var model = HotUpdate.HotContent.Load<GameObject>(Root + "Models/" + id);
            if (model == null) return null;
            var filter = model.GetComponentInChildren<MeshFilter>();
            mesh = filter != null ? filter.sharedMesh : model.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMesh;
            meshes[id] = mesh; return mesh;
        }
        // OBJ material groups may import as separate children. Preserve every group,
        // including the lock's keyhole and all three surfaces of the truck body.
        public static Mesh CompleteMesh(int id)
        {
            if(completeMeshes.TryGetValue(id,out Mesh result)&&result!=null)return result;
            var model=HotUpdate.HotContent.Load<GameObject>(Root+"Models/"+id);if(model==null)return null;
            var parts=new List<CombineInstance>();
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;if(mesh==null)continue;
                for(int sub=0;sub<mesh.subMeshCount;sub++)parts.Add(new CombineInstance {
                    mesh=mesh,subMeshIndex=sub,transform=model.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix });
            }
            if(parts.Count==0)return Mesh(id);
            result=new Mesh {name="APK complete "+id,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            result.CombineMeshes(parts.ToArray(),false,true);result.RecalculateBounds();
            completeMeshes[id]=result;return result;
        }
        [Serializable] sealed class Palette { public Swatch[] colors; }
        [Serializable] sealed class Swatch { public int id; public float r,g,b,a; }
        public static Color Color(int id)
        {
            if (palette == null)
            {
                palette = new Dictionary<int, Color>();
                foreach (var s in Data<Palette>("palette").colors) palette[s.id] = ArtTheme.CargoColor(s.id,new Color(s.r,s.g,s.b,1));
            }
            if (palette.TryGetValue(id, out Color color)) return color;
            var basis = palette.TryGetValue(id % 100, out color) ? color : UnityEngine.Color.magenta;
            return id >= 200 ? basis * .6f : UnityEngine.Color.Lerp(basis, UnityEngine.Color.white, .35f);
        }
        public static Material CargoMaterial
        {
            get
            {
                if(cargoMaterial==null)cargoMaterial=HotUpdate.HotContent.Load<Material>(Root+"Shaders/CargoInstanced");
                return cargoMaterial;
            }
        }
        public static void SetCargoAppearance(Renderer renderer,int color,Material cover=null)
        {
            if(renderer.TryGetComponent<PieceView>(out var piece))piece.Tint=Color(color);
            var material=cover!=null?cover:CargoMaterial;
            if(renderer.sharedMaterial!=material)renderer.sharedMaterial=material;
            // Clear the instance tint when using the question-mark cover shader.
            if(cover!=null){renderer.SetPropertyBlock(null);return;}
            MaterialPropertyBlock properties;
            if(!cargoColors.TryGetValue(color,out properties))
            {
                properties=new MaterialPropertyBlock();properties.SetColor("_Color",Color(color));
                cargoColors[color]=properties;
            }
            // Unity copies the block, so every renderer can reuse the cached color data.
            renderer.SetPropertyBlock(properties);
        }
        public static Material NewMaterial(Color color) => ArtTheme.Surface(color);
        internal static void ReleaseCaches()
        {
            foreach(var mesh in completeMeshes.Values)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
            completeMeshes.Clear();meshes.Clear();cargoColors.Clear();palette=null;cargoMaterial=null;
            VolcanoArt.Release();CarrierFeatureView.ReleaseCaches();SourceMorphView.ReleaseCaches();SourceParticleEffect.ReleaseCaches();SourceTransformClip.ReleaseCaches();ArtTheme.ReleaseCaches();
        }
        public static GameObject Model(string name, Mesh mesh, Material material, Transform parent, Vector3 position, Vector3 scale)
        {
            var result = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            result.transform.SetParent(parent,false); result.transform.localPosition=position; result.transform.localScale=scale;
            result.GetComponent<MeshFilter>().sharedMesh=mesh; result.GetComponent<MeshRenderer>().sharedMaterial=material; return result;
        }
    }
    [Serializable] public sealed class SourceVector { public float x,y,z; public Vector3 Value => new Vector3(x,y,z); }
    [Serializable] public sealed class SourceRotation { public float x,y,z,w; public Quaternion Value => new Quaternion(x,y,z,w); }
    [Serializable] public sealed class SourceTransform { public SourceVector m_LocalPosition,m_LocalScale; public SourceRotation m_LocalRotation; }
    [Serializable] public sealed class SourceNode { public string path; public SourceTransform transform; }
    [Serializable] public sealed class SourcePart { public string path; public int mesh; public bool wheel; public int[] materialIds; }
    [Serializable] public sealed class SourceVisual
    {
        public SourceNode[] nodes; public SourcePart[] parts;
        public string[] frontParts,middleParts,stretchParts;
        public SourceVector colliderCenter,colliderSize,outlet;
    }
}
