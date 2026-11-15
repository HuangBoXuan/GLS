using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LoopSort0.Editor
{
    // Offline art references from the actual native assets. Never starts gameplay.
    [InitializeOnLoad]
    public static class MechanismArtReferenceExporter
    {
        const string Output = "Design/MechanismUI/SourceModels";
        const string Request = "Design/MechanismUI/export-models.request";
        const string AssetsRoot = "Assets/LoopSort/PlatformAssets/";
        static MechanismArtReferenceExporter() { EditorApplication.delayCall += Requested; }
        static void Requested()
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Requested; return; }
            try { Export(); File.Delete(Request); }
            catch (Exception e) { File.WriteAllText(Output + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("LoopSort/Art/Export actual mechanism references")]
        public static void Export()
        {
            Directory.CreateDirectory(Output);
            foreach (string id in new[] { "carrier", "barrier", "ice", "tag", "sticky", "key", "lock", "rope", "carpet", "generator", "portal", "curtain", "hidden" })
            {
                var root = new GameObject("Actual mechanism " + id);
                root.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    Build(id, root.transform);
                    Render(root, Output + "/" + id + ".png");
                }
                finally { Object.DestroyImmediate(root); }
            }
            File.WriteAllText(Output + "/provenance.txt", "Rendered in Unity Edit Mode from Assets/LoopSort/PlatformAssets native prefabs and meshes. No guide/gallery illustrations used. Group structures use original Left/Right/Scale/Center/Door transforms; source model geometry retained. Cargo uses mesh 421. Curtain uses morph-395 in its runtime transform. These are model references, not replacement assets.\n");
            Debug.Log("Actual mechanism references exported: " + Path.GetFullPath(Output));
        }
        static GameObject Prefab(string id, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot + "Prefabs/" + id + ".prefab");
            if (asset == null) throw new Exception("Missing native prefab " + id);
            var go = Object.Instantiate(asset, parent, false); go.hideFlags = HideFlags.HideAndDontSave; return go;
        }
        static GameObject Model(string name, string mesh, string material, Transform parent, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetsRoot + "Meshes/" + mesh + ".asset");
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot + "Materials/" + material + ".mat"); return go;
        }
        static void Cargo(Transform parent, bool hidden = false)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetsRoot + "Meshes/421.asset");
            for (int i = 0; i < 4; i++)
            {
                var go = Model("Source cargo " + i, "421", hidden && i < 3 ? "hidden" : "cargo", parent, Vector3.zero, Vector3.one);
                go.transform.localScale = Vector3.one;
                go.transform.localPosition = new Vector3(0, 1.69f, 1.29f-i);
                if (!hidden || i == 3)
                {
                    var p = new MaterialPropertyBlock(); p.SetColor("_Color", i < 2 ? new Color(.26f,.46f,.96f) : new Color(.34f,.83f,.42f)); go.GetComponent<Renderer>().SetPropertyBlock(p);
                }
            }
        }
        static void Carrier(Transform parent)
        {
            var go = Prefab("carrier", parent);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "TopPart1" || t.name == "TopPart2V2") t.gameObject.SetActive(false);
            Cargo(parent);
        }
        static void Build(string id, Transform root)
        {
            if (id == "hidden") { Model("Actual hidden cargo", "421", "hidden", root, Vector3.zero, Vector3.one); return; }
            if (id == "curtain")
            {
                var go = Model("Actual Curtain-v7", "morph-395", "cargo", root, new Vector3(0,.181f,-2.7f), new Vector3(103,106,100));
                go.transform.localRotation = Quaternion.Euler(-90,0,0);
                var p = new MaterialPropertyBlock(); p.SetColor("_Color", new Color(.26f,.46f,.96f)); go.GetComponent<Renderer>().SetPropertyBlock(p); return;
            }
            if (id == "carrier") { Carrier(root); return; }
            if (id == "barrier" || id == "ice" || id == "tag") { Prefab(id, root); return; }
            if (id == "lock" || id == "rope" || id == "carpet")
            {
                var g = Prefab(id,root); var left=g.transform.Find("Left"); var right=g.transform.Find("Right");
                if(left!=null)left.localPosition=new Vector3(-1.8f,0,0); if(right!=null)right.localPosition=new Vector3(1.8f,0,0);
                var center=g.transform.Find(id=="lock"?"Center":"Scale"); if(center!=null)center.localScale=new Vector3(3.6f+(id=="rope"?1.4f:id=="carpet"?2.1f:1.4f),1,1);
                var door=g.transform.Find("Door"); if(door!=null)door.localScale=new Vector3(5,1,1); return;
            }
            Prefab(id,root);
        }
        static void Render(GameObject go, string path)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(go);
                var renderers=go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&!(r is ParticleSystemRenderer)).ToArray();
                if(renderers.Length==0)throw new Exception("No active geometry: "+go.name);
                var bounds=renderers[0].bounds; foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                var cam=preview.camera; cam.orthographic=true; cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.14f,.12f,.15f,1); cam.nearClipPlane=.01f; cam.farClipPlane=1000;
                cam.transform.rotation=Quaternion.Euler(idForCamera(go.name)=="portal"?50:65,idForCamera(go.name)=="portal"?180:idForCamera(go.name)=="tag"?45:0,0); cam.transform.position=bounds.center-cam.transform.forward*Mathf.Max(20,bounds.size.magnitude*2);
                float maxX=0,maxY=0;
                for(int a=0;a<8;a++){var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((a&1)==0?-1:1,(a&2)==0?-1:1,(a&4)==0?-1:1));var v=cam.transform.InverseTransformPoint(p);maxX=Mathf.Max(maxX,Mathf.Abs(v.x));maxY=Mathf.Max(maxY,Mathf.Abs(v.y));}
                cam.orthographicSize=Mathf.Max(maxX,maxY)*1.18f;
                preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(40,25,0);preview.lights[1].intensity=.6f;preview.ambientColor=new Color(.6f,.6f,.6f);
                preview.BeginPreview(new Rect(0,0,768,768),GUIStyle.none); cam.Render();
                var texture=preview.EndPreview(); var old=RenderTexture.active; RenderTexture.active=texture as RenderTexture;
                var png=new Texture2D(768,768,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,768,768),0,0);png.Apply();File.WriteAllBytes(path,png.EncodeToPNG());Object.DestroyImmediate(png);RenderTexture.active=old;
            }
            finally { preview.Cleanup(); }
        }
        static string idForCamera(string value) { return value.Replace("Actual mechanism ", ""); }
    }
}
