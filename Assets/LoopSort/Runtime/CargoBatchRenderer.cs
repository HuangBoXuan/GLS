using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoopSort0
{
    // Keep the source physics/packing rules, but submit the visible cells together.
    // Hidden/dissolving cells retain their own material until the reveal completes.
    [DefaultExecutionOrder(1000)]
    internal sealed class CargoBatchRenderer : MonoBehaviour
    {
        const int BatchSize=1023;
        readonly List<PieceView> visible=new List<PieceView>(512);
        readonly Matrix4x4[] packedMatrices=new Matrix4x4[BatchSize],beltMatrices=new Matrix4x4[BatchSize];
        readonly Vector4[] packedColors=new Vector4[BatchSize],beltColors=new Vector4[BatchSize];
        MaterialPropertyBlock properties;
        Material material;Mesh packedMesh,beltMesh;Camera targetCamera;
        bool supported;
        static readonly int ColorId=Shader.PropertyToID("_Color");

        public void Initialize(Mesh packed,Mesh belt,Material shared,Camera camera)
        {
            packedMesh=packed;beltMesh=belt;material=shared;targetCamera=camera;
            supported=SystemInfo.supportsInstancing&&shared!=null&&shared.enableInstancing&&shared.shader.isSupported;
            properties=new MaterialPropertyBlock();
        }
        internal void Add(PieceView view)
        {
            if(view.BatchIndex>=0)return;
            view.BatchIndex=visible.Count;visible.Add(view);
        }
        internal void Remove(PieceView view)
        {
            int index=view.BatchIndex;if(index<0)return;
            int last=visible.Count-1;
            if(index!=last){visible[index]=visible[last];visible[index].BatchIndex=index;}
            visible.RemoveAt(last);view.BatchIndex=-1;
        }
        void LateUpdate()
        {
            if(!supported)return;
            int packedCount=0,beltCount=0;
            for(int i=0;i<visible.Count;i++)
            {
                var view=visible[i];
                var mesh=view.Filter.sharedMesh;
                bool batched=view.Renderer.sharedMaterial==material&&(mesh==packedMesh||mesh==beltMesh);
                if(view.Renderer.enabled==batched)view.Renderer.enabled=!batched;
                if(!batched)continue;
                if(mesh==packedMesh)
                {
                    packedMatrices[packedCount]=view.transform.localToWorldMatrix;packedColors[packedCount]=view.Tint;
                    if(++packedCount==BatchSize){Submit(mesh,packedMatrices,packedColors,packedCount);packedCount=0;}
                }
                else
                {
                    beltMatrices[beltCount]=view.transform.localToWorldMatrix;beltColors[beltCount]=view.Tint;
                    if(++beltCount==BatchSize){Submit(mesh,beltMatrices,beltColors,beltCount);beltCount=0;}
                }
            }
            if(packedCount>0)Submit(packedMesh,packedMatrices,packedColors,packedCount);
            if(beltCount>0)Submit(beltMesh,beltMatrices,beltColors,beltCount);
        }
        void Submit(Mesh mesh,Matrix4x4[] matrices,Vector4[] colors,int count)
        {
            properties.SetVectorArray(ColorId,colors);
            Graphics.DrawMeshInstanced(mesh,0,material,matrices,count,properties,ShadowCastingMode.Off,false,gameObject.layer,targetCamera,LightProbeUsage.Off);
        }
        void OnDisable()
        {
            foreach(var view in visible)if(view!=null&&view.Renderer!=null)view.Renderer.enabled=true;
        }
    }
}
