using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopSort0
{
    // Same-build vertices and blend-shape deltas, without OBJ vertex reordering.
    public sealed class SourceMorphView : MonoBehaviour
    {
        [Serializable] sealed class Geometry
        {
            public string name;public float[][] vertices,normals,uv;public int[][] submeshes;public Channel[] channels;
        }
        [Serializable] sealed class Channel {public string name;public Frame[] frames;}
        [Serializable] sealed class Frame {public float weight;public Delta[] vertices;}
        [Serializable] sealed class Delta {public int index;public SourceVector position;}
        [Serializable] sealed class Clips {public Clip[] clips;}
        [Serializable] sealed class Clip {public int id;public float duration;public Key[][] curves;}
        [Serializable] sealed class Key {public float time;public float[] coefficients;}
        static readonly Dictionary<int,Mesh> meshes=new Dictionary<int,Mesh>();
        static Clip curtainClip;
        internal static void ReleaseCaches()
        {
            foreach(var mesh in meshes.Values)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
            meshes.Clear();curtainClip=null;
        }
        SkinnedMeshRenderer view;
        public int Channels=>view.sharedMesh.blendShapeCount;
        public static bool Supports(int id)=>id==377||id==393||id==395||id==413||id==427;
        public static SourceMorphView Attach(GameObject owner,int id,Material[] materials)
        {
            var result=owner.AddComponent<SourceMorphView>();result.view=owner.AddComponent<SkinnedMeshRenderer>();
            result.view.sharedMesh=Load(id);result.view.sharedMaterials=materials;result.view.updateWhenOffscreen=true;
            result.view.localBounds=result.view.sharedMesh.bounds;return result;
        }
        static Mesh Load(int id)
        {
            if(meshes.TryGetValue(id,out Mesh mesh))return mesh;
            var source=GameAssets.AnimationData<Geometry>(id.ToString());
            mesh=new Mesh {name="APK morph "+source.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            var vertices=new Vector3[source.vertices.Length];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var p=source.vertices[i];vertices[i]=new Vector3(p[0],p[1],p[2]);
                p=source.normals[i];normals[i]=new Vector3(p[0],p[1],p[2]);
                p=source.uv[i];uv[i]=new Vector2(p[0],p[1]);
            }
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.subMeshCount=source.submeshes.Length;
            for(int i=0;i<source.submeshes.Length;i++)mesh.SetTriangles(source.submeshes[i],i);
            foreach(var channel in source.channels)foreach(var frame in channel.frames)
            {
                var delta=new Vector3[vertices.Length];foreach(var v in frame.vertices)delta[v.index]=v.position.Value;
                mesh.AddBlendShapeFrame(channel.name,frame.weight,delta,null,null);
            }
            mesh.RecalculateBounds();meshes[id]=mesh;return mesh;
        }
        public void Weight(int channel,float value){if(channel>=0&&channel<Channels)view.SetBlendShapeWeight(channel,value);}
        public void ResetWeights(){for(int i=0;i<Channels;i++)Weight(i,0);view.enabled=true;}
        public float CurtainDuration
        {
            get{EnsureClip();return curtainClip.duration;}
        }
        static void EnsureClip()
        {
            if(curtainClip!=null)return;
            foreach(var clip in GameAssets.AnimationData<Clips>("curtain-clips").clips)if(clip.id==558)curtainClip=clip;
        }
        public void SampleCurtain(float time)
        {
            EnsureClip();transform.localScale=new Vector3(Curve(curtainClip.curves[0],time),Curve(curtainClip.curves[1],time),Curve(curtainClip.curves[2],time));
            Weight(0,Curve(curtainClip.curves[3],time));Weight(1,Curve(curtainClip.curves[4],time));
            view.enabled=Curve(curtainClip.curves[5],time)>.5f;
        }
        static float Curve(Key[] keys,float time)
        {
            int index=0;while(index+1<keys.Length&&keys[index+1].time<=time)index++;
            var key=keys[index];float dt=Mathf.Max(0,time-key.time);var c=key.coefficients;
            return ((c[0]*dt+c[1])*dt+c[2])*dt+c[3];
        }
    }
}
