using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using LoopSort0.Core;
using UnityEditor;
using UnityEngine;
using XLua;
using Object=UnityEngine.Object;

namespace LoopSort0.Editor
{
    // Run in an isolated editor: builds serialized native geometry, checks it
    // after a prefab reload, and renders it from two real camera angles.
    public static class VolcanoModelPreview
    {
        const string Output="Design/MechanismUI/Models3D";
        [MenuItem("LoopSort/Art/Rebuild solid volcanic models")]
        public static void Rebuild()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Rebuild models in Edit Mode");
            PlatformAssetBuilder.UpdateVolcanoSkins("loopsort0");
            AssetDatabase.SaveAssets();
        }
        public static void BatchTailViews()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Tail views require an isolated batch editor");
            try
            {
                Directory.CreateDirectory(Output);Rebuild();
                foreach(string id in new[]{"carrier","long-carrier"})
                {
                    var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoopSort/PlatformAssets/Prefabs/"+id+".prefab"));
                    try{Render(root,Output+"/"+id+"-rear.png",Quaternion.Euler(42,212,0));}
                    finally{Object.DestroyImmediate(root);}
                }
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BatchHomePreview()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Home artwork export requires an isolated batch editor");
            var owned=new List<Object>();var root=new GameObject("Home continuous conveyor and game carts");
            try
            {
                var spline=new NativeSpline(new[]{
                    new TrackPoint{x=-5.7f,z=-8},new TrackPoint{x=-5.7f,z=8},
                    new TrackPoint{x=5.7f,z=8},new TrackPoint{x=5.7f,z=-8}},true,1);
                spline.Evaluate(0,out var head,out var headForward,out _,out _);spline.Evaluate(1,out var tail,out var tailForward,out _,out _);
                if(Vector3.Distance(head,tail)>.001f||Vector3.Angle(headForward,tailForward)>.1f)throw new Exception("Home conveyor seam is not continuous");
                var track=ReferenceConveyor.Build(spline);var floor=ReferenceConveyor.Material();owned.Add(track);owned.Add(floor);
                AddPart("Continuous raised conveyor",root.transform,track,floor,Vector3.zero);
                int count=Mathf.CeilToInt(spline.Length/1.8f);var vertices=new Vector3[count*3];var triangles=new int[count*3];
                for(int i=0;i<count;i++)
                {
                    float d=i*spline.Length/count;var center=spline.AtDistance(d);var p=center+Vector3.up*.015f;
                    var forward=spline.AtDistance(d+.05f)-center;
                    var q=Quaternion.LookRotation(forward.normalized);
                    vertices[i*3]=p+q*new Vector3(-.22f,0,-.18f);vertices[i*3+1]=p+q*new Vector3(0,0,.27f);vertices[i*3+2]=p+q*new Vector3(.22f,0,-.18f);
                    for(int k=0;k<3;k++)triangles[i*3+k]=i*3+k;
                }
                var arrows=new Mesh{name="Home arrows",vertices=vertices,triangles=triangles};arrows.RecalculateBounds();
                var accent=ReferenceConveyor.Material(true);owned.Add(arrows);owned.Add(accent);AddPart("Mint arrows",root.transform,arrows,accent,Vector3.zero);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoopSort/PlatformAssets/Prefabs/carrier.prefab");
                var cargo=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/LoopSort/PlatformAssets/Meshes/421.asset");
                if(prefab==null||cargo==null)throw new Exception("Home preview requires the game carrier and cargo assets");
                uint[] colors={0xFF5B59,0xFFD04D,0x4276F5,0x57D36A};
                for(int i=0;i<4;i++)
                {
                    var cart=Object.Instantiate(prefab,root.transform);cart.name="Game minecart "+(i+1);
                    cart.transform.localPosition=new Vector3(i%2==0?-2.05f:2.05f,0,i<2?3.15f:-3.15f);
                    var material=new Material(Resources.Load<Shader>("LoopSort/Shaders/InstancedCargo")){color=ArtTheme.Hex(colors[i]),enableInstancing=true};owned.Add(material);
                    for(int group=0;group<4;group++)AddPart("Cargo "+group,cart.transform,cargo,material,new Vector3(0,1.69f,1.29f-group));
                }
                Directory.CreateDirectory("Design/MechanismUI/HomeMinecarts");
                // Model references must never overwrite the illustrated home artwork.
                string path="Design/MechanismUI/HomeMinecarts/home-model-reference.png";
                Render(root,path,Quaternion.Euler(80,0,0),true,1536);
                File.WriteAllText("Design/MechanismUI/HomeMinecarts/home-export.json",JsonConvert.SerializeObject(new{status="passed",continuousClosedTrack=true,carts=4,cargoGroups=16,carrierPrefab=AssetDatabase.GetAssetPath(prefab),runtimeArtworkWritten=false,path},Formatting.Indented));
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
            finally{Object.DestroyImmediate(root);foreach(var item in owned)Object.DestroyImmediate(item);}
        }
        static void AddPart(string name,Transform parent,Mesh mesh,Material material,Vector3 position)
        {
            var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(parent,false);part.transform.localPosition=position;
            part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        internal static void UpdateArtworkBounds()
        {
            const string theme="Assets/LoopSort/Resources/LoopSort/Theme/",data="Assets/LoopSort/Resources/LoopSort/Data/volcano-art.json";
            var atlas=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(theme+"atlas.json"));var art=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(data));
            const string id="mine-tag";
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!image.LoadImage(File.ReadAllBytes(theme+id+".png")))throw new Exception("Invalid artwork "+id);
                var pixels=image.GetPixels32();int left=image.width,right=0,bottom=image.height,top=0;
                for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)if(pixels[y*image.width+x].a>32)
                {left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
                if(left>right)throw new Exception("Empty artwork "+id);
                left=Mathf.Max(0,left-1);right=Mathf.Min(image.width-1,right+1);bottom=Mathf.Max(0,bottom-1);top=Mathf.Min(image.height-1,top+1);
                var rect=new Newtonsoft.Json.Linq.JArray(left/(float)image.width,bottom/(float)image.height,(right-left+1)/(float)image.width,(top-bottom+1)/(float)image.height);
                atlas[id]=new Newtonsoft.Json.Linq.JArray{rect};
                art["tag"]=rect.DeepClone();
            }
            finally{Object.DestroyImmediate(image);}
            File.WriteAllText(theme+"atlas.json",atlas.ToString(Formatting.Indented)+"\n");File.WriteAllText(data,art.ToString(Formatting.Indented)+"\n");
            AssetDatabase.ImportAsset(theme+"mine-tag.png",ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(theme+"atlas.json",ImportAssetOptions.ForceSynchronousImport);AssetDatabase.ImportAsset(data,ImportAssetOptions.ForceSynchronousImport);
        }
        public static void BatchPreview()
        {
            try
            {
                if(!Application.isBatchMode)throw new InvalidOperationException("BatchPreview requires an isolated batch editor");
                PlayerSettings.companyName="CodexModelPreview";PlayerSettings.productName="LoopSort3DPreview";
                Directory.CreateDirectory(Output);Rebuild();
                var receipt=new List<object>();
                foreach(string id in new[]{"carrier","long-carrier","portal","barrier","ice","tag","sticky","key","lock","rope","carpet","generator","volcano-lid"})
                {
                    string path="Assets/LoopSort/PlatformAssets/Prefabs/"+id+".prefab";
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(prefab==null)throw new Exception("Missing prefab "+id);
                    var root=Object.Instantiate(prefab);
                    try
                    {
                        if(id=="lock"||id=="rope"||id=="carpet")
                        {
                            float half=id=="carpet"?3.48f:1.7f;
                            root.transform.Find("Left").localPosition=new Vector3(-half,0,0);
                            root.transform.Find("Right").localPosition=new Vector3(half,0,0);
                            root.transform.Find(id=="lock"?"Center":"Scale").localScale=new Vector3(id=="carpet"?half*2+2.1f:4.8f,1,1);
                            if(id=="carpet")
                            {
                                var block=new MaterialPropertyBlock();block.SetVector("_MainTex_ST",new Vector4(3,1,0,0));
                                root.transform.Find("Scale/Carpet").GetComponent<Renderer>().SetPropertyBlock(block);
                                foreach(float x in new[]{-1.51f,1.51f})
                                {
                                    var clasp=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoopSort/PlatformAssets/Prefabs/carpet-divider.prefab"),root.transform);
                                    clasp.transform.localPosition=Vector3.right*x;
                                }
                            }
                            if(id=="lock")root.transform.Find("Door").localScale=new Vector3(4.8f,1,1);
                        }
                        string meshPrefix="/volcano-"+(id=="volcano-lid"?"lid":id)+"-";
                        var filters=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&AssetDatabase.GetAssetPath(f.sharedMesh).Contains(meshPrefix)).ToArray();
                        if(filters.Length==0)throw new Exception("No replacement mesh in "+id);
                        foreach(var filter in filters)
                        {
                            var mesh=filter.sharedMesh;
                            if(mesh==null||mesh.vertexCount<=4||mesh.bounds.size.z<=.01f)throw new Exception("Flat or missing mesh "+id);
                            if(!EditorUtility.IsPersistent(mesh))throw new Exception("Unsaved native mesh "+id);
                            if(mesh.subMeshCount!=filter.GetComponent<Renderer>().sharedMaterials.Length)throw new Exception("Invalid surface slots "+id);
                        }
                        if(root.GetComponentsInChildren<Renderer>(true).Any(r=>!r.forceRenderingOff&&r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="LoopSort0/VolcanoDecal"))throw new Exception("Visible sprite plane in "+id);
                        if(id=="rope")VolcanoArt.FitChain(root.transform);
                        Render(root,Output+"/"+id+"-angle.png",Quaternion.Euler(48,id=="portal"?212:32,0));
                        Render(root,Output+"/"+id+"-top.png",Quaternion.Euler(80,id=="portal"?180:0,0));
                        receipt.Add(new{id,parts=filters.Length,vertices=filters.Sum(f=>f.sharedMesh.vertexCount),triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3),serialized=true});
                    }
                    finally{Object.DestroyImmediate(root);}
                }
                bool dedicatedCart=Environment.GetCommandLineArgs().Contains("-dedicatedCart");
                if(Environment.GetCommandLineArgs().Contains("-guideModels")||dedicatedCart)
                {
                    foreach(string feature in new[]{"ice","tag","volcano-lid"})
                    {
                        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoopSort/PlatformAssets/Prefabs/carrier.prefab"));
                        try
                        {
                            if(feature=="tag")
                            {
                                var color=new Color(.26f,.46f,.96f);VolcanoArt.TintCarrier(root.transform,color);
                                if(dedicatedCart)
                                {
                                    // Keep model references separate from the illustrated guide asset.
                                    Render(root,Output+"/dedicated-carrier-render.png",Quaternion.Euler(52,22,0),true);
                                }
                            }
                            else Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoopSort/PlatformAssets/Prefabs/"+feature+".prefab"),root.transform);
                            Render(root,Output+"/guide-"+feature+"-angle.png",Quaternion.Euler(52,22,0));
                            Render(root,Output+"/guide-"+feature+"-top.png",Quaternion.Euler(80,0,0));
                        }
                        finally{Object.DestroyImmediate(root);}
                    }
                }
                int luaCount=0;
                using(var lua=new LuaEnv())
                {
                    foreach(string path in Directory.GetFiles("Assets/StreamingAssets/LoopSort/lua","*.lua",SearchOption.AllDirectories))
                    {using(var chunk=lua.LoadString(File.ReadAllText(path),"@"+path)){}luaCount++;}
                    lua.AddLoader((ref string module,ModuleLuaBuffer buffer)=>
                    {
                        string path="Assets/StreamingAssets/LoopSort/lua/"+module.Replace('.','/')+".lua";
                        if(!File.Exists(path))return false;
                        buffer.Copy(File.ReadAllBytes(path));return true;
                    });
                    lua.Global.Set("previewSourceMesh",AssetDatabase.LoadAssetAtPath<Mesh>("Assets/LoopSort/PlatformAssets/Meshes/397.asset"));
                    var generated=lua.DoString(@"
                        local Assets=require('platform.assets')
                        local a=setmetatable({owned={},disposed=false},Assets)
                        a.mesh=function() return previewSourceMesh end
                        local chain=a:chain_mesh(5.4,4.8)
                        assert(chain.vertexCount>100 and chain.subMeshCount==2)
                        assert(chain==a:chain_mesh(5.4,4.8))
                        local blanket=a:carpet_mesh(3)
                        assert(blanket.bounds.size.y>.05)
                        local Layout=require('game.visual_clearance')
                        for axis=0,1 do
                            local row={}
                            for i=1,6 do
                                local x=(i-1)*2.32
                                local b=axis==0 and {x-1.38,-2.7,x+1.38,2.7} or {-2.7,x-1.38,2.7,x+1.38}
                                row[i]={box=b,axis=axis,ropes=i<=5 and {'row'} or {}}
                            end
                            local shifts=Layout.solve(row)
                            for i=1,5 do assert(math.abs(shifts[i][axis+1])<.00001) end
                            local gap=row[6].box[axis+1]+shifts[6][axis+1]-row[5].box[axis+3]
                            assert(gap>=.85999,'Insufficient space for chain and both gaps')
                        end
                        return chain,blanket
                    ");
                    foreach(var mesh in generated.OfType<Mesh>())Object.DestroyImmediate(mesh);
                }
                var errors=new[]{"VolcanoSurface","HiddenCargo","InstancedCargo"}.SelectMany(id=>ShaderUtil.GetShaderMessages(AssetDatabase.LoadAssetAtPath<Shader>("Assets/LoopSort/Resources/LoopSort/Shaders/"+id+".shader")).Where(m=>m.severity.ToString()=="Error").Select(m=>id+": "+m.message)).ToArray();
                if(errors.Length>0)throw new Exception(string.Join("\n",errors));
                File.WriteAllText(Output+"/validation.json",JsonConvert.SerializeObject(new{status="passed",prefabs=receipt,luaSyntaxCount=luaCount,nativeChainAndBlanket="passed",chainClearanceAxes="X/Z passed",shaderErrors=errors},Formatting.Indented));
                if(Environment.GetCommandLineArgs().Contains("-volcanoModelsOnly")){EditorApplication.Exit(0);return;}
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/LoopSort/Scenes/Gameplay.unity");
                bool guideModels=Environment.GetCommandLineArgs().Contains("-guideModels");
                int[] queue=dedicatedCart?Array.Empty<int>():guideModels?new[]{101,201}:Environment.GetCommandLineArgs().Contains("-volcanoCorrections")?new[]{250,334}:new[]{7,20,60,101,158,201,250,300,334,349};
                File.WriteAllText("Design/MechanismUI/volcano-request.json",JsonConvert.SerializeObject(new{action="capture",directory=Output+"/Gameplay",level=dedicatedCart?201:guideModels?20:4,queue,quitAfter=true}));
                EditorApplication.isPlaying=true;
            }
            catch(Exception e)
            {
                Directory.CreateDirectory(Output);File.WriteAllText(Output+"/validation-error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);
            }
        }
        static void Render(GameObject root,string path,Quaternion rotation,bool transparent=false,int resolution=768)
        {
            var preview=new PreviewRenderUtility();
            try
            {
                var source=root;root=Object.Instantiate(root);
                foreach(var renderer in source.GetComponentsInChildren<Renderer>(true))if(renderer.HasPropertyBlock())
                {
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                    string nodePath=AnimationUtility.CalculateTransformPath(renderer.transform,source.transform);
                    var copy=nodePath.Length==0?root.transform:root.transform.Find(nodePath);copy.GetComponent<Renderer>().SetPropertyBlock(block);
                }
                preview.AddSingleGO(root);
                var renderers=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&!r.forceRenderingOff&&r.gameObject.activeInHierarchy&&r.sharedMaterials.Any(m=>m!=null&&m.shader.name!="LoopSort0/VolcanoSourceHidden")).ToArray();
                var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                var camera=preview.camera;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=transparent?Color.clear:new Color(.19f,.14f,.20f);
                camera.nearClipPlane=.01f;camera.farClipPlane=1000;camera.transform.rotation=rotation;camera.transform.position=bounds.center-camera.transform.forward*Mathf.Max(20,bounds.size.magnitude*2);
                float extent=0;
                for(int i=0;i<8;i++)
                {
                    var p=camera.transform.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    extent=Mathf.Max(extent,Mathf.Max(Mathf.Abs(p.x),Mathf.Abs(p.y)));
                }
                camera.orthographicSize=extent*1.12f;
                preview.BeginPreview(new Rect(0,0,resolution,resolution),GUIStyle.none);
                var previousTarget=camera.targetTexture;
                var target=RenderTexture.GetTemporary(resolution,resolution,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;
                try
                {
                    RenderTexture.active=target;
                    var image=new Texture2D(resolution,resolution,transparent?TextureFormat.RGBA32:TextureFormat.RGB24,false);
                    try
                    {
                        image.ReadPixels(new Rect(0,0,resolution,resolution),0,0);image.Apply();
                        if(transparent)
                        {
                            var pixels=image.GetPixels32();int left=resolution,right=0,bottom=resolution,top=0;
                            for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++)if(pixels[y*resolution+x].a>8)
                            {left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
                            if(left>right)throw new Exception("Empty guide artwork");
                            left=Mathf.Max(0,left-12);bottom=Mathf.Max(0,bottom-12);right=Mathf.Min(resolution-1,right+12);top=Mathf.Min(resolution-1,top+12);
                            var cropped=new Texture2D(right-left+1,top-bottom+1,TextureFormat.RGBA32,false);
                            try{cropped.SetPixels(image.GetPixels(left,bottom,cropped.width,cropped.height));cropped.Apply();File.WriteAllBytes(path,cropped.EncodeToPNG());}
                            finally{Object.DestroyImmediate(cropped);}
                        }
                        else File.WriteAllBytes(path,image.EncodeToPNG());
                    }
                    finally{Object.DestroyImmediate(image);}
                }
                finally{RenderTexture.active=previous;camera.targetTexture=previousTarget;preview.EndPreview();RenderTexture.ReleaseTemporary(target);}
            }
            finally{preview.Cleanup();}
        }
    }
}
