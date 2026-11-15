using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoopSort0
{
    // Rebuilds the APK's serialized particle modules; gameplay never sees these particles.
    internal sealed class SourceParticleEffect : MonoBehaviour
    {
        static JObject catalog;
        static readonly Dictionary<int,Material> materials=new Dictionary<int,Material>();
        ParticleSystem[] systems;
        bool[] resumeSystems;
        bool paused,counted;
        internal static int ActiveCount { get; private set; }

        internal static SourceParticleEffect Spawn(string id,Transform parent,Vector3 position,Quaternion rotation)
        {
            if(catalog==null)catalog=GameAssets.AnimationData<JObject>("source-particles");
            JToken effect=null;
            foreach(var item in catalog["effects"])if((string)item["id"]==id){effect=item;break;}
            if(effect==null)return null;
            var root=new GameObject("APK particles "+id);
            root.transform.SetParent(parent,false);root.transform.SetPositionAndRotation(position,rotation);
            root.AddComponent<TransientEffect>();
            var view=root.AddComponent<SourceParticleEffect>();
            var created=new List<ParticleSystem>();
            foreach(var node in effect["nodes"])
            {
                string path=(string)node["path"];
                var owner=Node(root.transform,path).gameObject;
                var t=node["transform"];
                owner.transform.localPosition=path.Length==0?Vector3.zero:Vector(t["m_LocalPosition"]);
                owner.transform.localRotation=Rotation(t["m_LocalRotation"]);
                owner.transform.localScale=Vector(t["m_LocalScale"]);
                if(node["system"]==null||node["system"].Type==JTokenType.Null)continue;
                var system=owner.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                SourceParticleModules.Apply(system,node["system"]);
                ApplyRenderer(system.GetComponent<ParticleSystemRenderer>(),node["renderer"]);
                created.Add(system);
            }
            view.systems=created.ToArray();view.resumeSystems=new bool[view.systems.Length];view.counted=true;ActiveCount++;
            // Every child is started once. Trail is stopped by the entrance sequence.
            foreach(var system in view.systems)system.Play(false);
            view.SetPaused(GameController.Instance!=null&&GameController.Instance.Paused);
            return view;
        }
        internal void Stop()
        {
            if(systems==null)return;
            foreach(var system in systems)
            {
                var main=system.main;main.loop=false;
                system.Stop(false,ParticleSystemStopBehavior.StopEmitting);
            }
        }

        static Transform Node(Transform root,string path)
        {
            var current=root;
            foreach(string part in ("Emitter"+(path.Length==0?"":"/"+path)).Split('/'))
            {
                var next=current.Find(part);
                if(next==null){next=new GameObject(part).transform;next.SetParent(current,false);}
                current=next;
            }
            return current;
        }

        static void ApplyRenderer(ParticleSystemRenderer renderer,JToken data)
        {
            if(data==null||data.Type==JTokenType.Null){renderer.enabled=false;return;}
            renderer.enabled=Bool(data,"m_Enabled");
            renderer.renderMode=(ParticleSystemRenderMode)Int(data,"m_RenderMode");
            renderer.alignment=(ParticleSystemRenderSpace)Int(data,"m_RenderAlignment");
            renderer.sortMode=(ParticleSystemSortMode)Int(data,"m_SortMode");
            renderer.minParticleSize=Float(data,"m_MinParticleSize");
            renderer.maxParticleSize=Float(data,"m_MaxParticleSize",.5f);
            renderer.cameraVelocityScale=Float(data,"m_CameraVelocityScale");
            renderer.velocityScale=Float(data,"m_VelocityScale");
            renderer.lengthScale=Float(data,"m_LengthScale",2);
            renderer.sortingFudge=Float(data,"m_SortingFudge");
            renderer.normalDirection=Float(data,"m_NormalDirection",1);
            renderer.pivot=Vector(data["m_Pivot"]);renderer.flip=Vector(data["m_Flip"]);
            renderer.allowRoll=Bool(data,"m_AllowRoll",true);
            renderer.sortingOrder=Int(data,"m_SortingOrder");
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            if(renderer.renderMode==ParticleSystemRenderMode.Mesh)
                renderer.mesh=GameAssets.CompleteMesh(Int(data["m_Mesh"],"m_PathID"));
            var refs=data["m_Materials"];
            if(refs!=null&&refs.HasValues)
            {
                renderer.sharedMaterial=Material(Int(refs[0],"m_PathID"));
                if(((JArray)refs).Count>1)renderer.trailMaterial=Material(Int(refs[1],"m_PathID"));
            }
        }

        static Material Material(int id)
        {
            if(id==0)return null;
            if(materials.TryGetValue(id,out var found)&&found!=null)return found;
            JToken source=null;foreach(var item in catalog["materials"])if(Int(item,"id")==id){source=item;break;}
            if(source==null)return null;
            bool lit=Bool(source,"lit");
            var shader=HotUpdate.HotContent.Load<Shader>("LoopSort/Shaders/"+(lit?"SourceParticleLit":"SourceParticle"));
            if(shader==null)return null;
            var material=new Material(shader){name="APK "+(string)source["name"]};
            material.SetColor("_Color",Color(source["color"]));
            material.SetFloat("_SrcBlend",Float(source,"srcBlend"));material.SetFloat("_DstBlend",Float(source,"dstBlend"));
            material.SetFloat("_Cull",Float(source,"cull"));material.SetFloat("_ZWrite",Float(source,"zWrite"));
            material.SetFloat("_Soft",Bool(source,"soft")?1:0);
            material.SetFloat("_SoftNear",Float(source,"softNear"));
            material.SetFloat("_SoftRange",Mathf.Max(.0001f,Float(source,"softFar",1)-Float(source,"softNear")));
            material.SetFloat("_Metallic",Float(source,"metallic"));material.SetFloat("_Glossiness",Float(source,"smoothness"));
            int texture=Int(source,"texture");
            if(texture!=0)material.mainTexture=HotUpdate.HotContent.Texture("LoopSort/Textures/particle-"+texture);
            var scale=source["scale"];var offset=source["offset"];
            material.mainTextureScale=new Vector2(Float(scale,"x",1),Float(scale,"y",1));
            material.mainTextureOffset=new Vector2(Float(offset,"x"),Float(offset,"y"));
            int queue=Int(source,"queue",-1);if(queue>=0)material.renderQueue=queue;
            materials[id]=material;return material;
        }

        void Update()
        {
            if(systems==null)return;
            SetPaused(GameController.Instance!=null&&GameController.Instance.Paused);
            if(paused)return;
            foreach(var system in systems)if(system!=null&&system.IsAlive(false))return;
            // Disable now so a result panel need not wait an extra destroy frame.
            if(counted){counted=false;ActiveCount--;}Destroy(gameObject);
        }
        void SetPaused(bool value)
        {
            if(paused==value)return;paused=value;
            for(int i=0;i<systems.Length;i++)
            {
                if(value){resumeSystems[i]=systems[i].isPlaying;if(resumeSystems[i])systems[i].Pause(false);}
                else if(resumeSystems[i])systems[i].Play(false);
            }
        }
        void OnDestroy(){if(counted){counted=false;ActiveCount--;}}
        internal static void ReleaseCaches()
        {
            foreach(var material in materials.Values)if(material!=null)Destroy(material);
            materials.Clear();catalog=null;
        }
        internal static float Float(JToken data,string key,float fallback=0)=>data?[key]==null?fallback:(float)data[key];
        internal static int Int(JToken data,string key,int fallback=0)=>data?[key]==null?fallback:(int)data[key];
        internal static bool Bool(JToken data,string key,bool fallback=false)=>data?[key]==null?fallback:(bool)data[key];
        internal static Vector3 Vector(JToken data)=>new Vector3(Float(data,"x"),Float(data,"y"),Float(data,"z"));
        static Quaternion Rotation(JToken data)=>new Quaternion(Float(data,"x"),Float(data,"y"),Float(data,"z"),Float(data,"w",1));
        internal static Color Color(JToken data)=>new Color(Float(data,"r",1),Float(data,"g",1),Float(data,"b",1),Float(data,"a",1));
    }
}
