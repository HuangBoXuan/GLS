using System.Collections.Generic;
using UnityEngine;

namespace LoopSort0
{
    public sealed partial class GameController
    {
        // The extracted prefab is authored at 12 groups. Move the closed end,
        // stretch the rails from their fixed opening, and keep the source wheels.
        static void ResizeLongTruck(Transform root,Dictionary<string,Transform> nodes,SourceVisual visual,float delta)
        {
            if(Mathf.Approximately(delta,0))return;
            foreach(string path in visual.frontParts)
                if(nodes.TryGetValue(path,out var part))part.position+=root.forward*delta;
            foreach(string path in visual.middleParts)
                if(nodes.TryGetValue(path,out var part))part.position+=root.forward*(delta*.5f);
            foreach(string path in visual.stretchParts)
            {
                if(!nodes.TryGetValue(path,out var part))continue;
                var mesh=part.GetComponent<MeshFilter>()?.sharedMesh??part.GetComponent<SkinnedMeshRenderer>()?.sharedMesh;
                if(mesh==null)continue;
                Vector3 direction=part.InverseTransformDirection(root.forward);
                int axis=Mathf.Abs(direction.x)>Mathf.Abs(direction.y)?0:1;
                if(Mathf.Abs(direction.z)>Mathf.Abs(direction[axis]))axis=2;
                Bounds bounds=mesh.bounds;
                float length=bounds.size[axis]*Mathf.Abs(part.lossyScale[axis]);if(length<.0001f)continue;
                Vector3 before=part.localScale,after=before;after[axis]*=Mathf.Max(.01f,(length+delta)/length);
                Vector3 fixedEdge=Vector3.zero;fixedEdge[axis]=direction[axis]>0?bounds.min[axis]:bounds.max[axis];
                part.localPosition+=part.localRotation*Vector3.Scale(before-after,fixedEdge);part.localScale=after;
            }
        }
    }
}
