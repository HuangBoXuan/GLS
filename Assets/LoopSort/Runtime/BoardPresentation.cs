using System.Collections.Generic;
using System;
using LoopSort0.Core;
using UnityEngine;

namespace LoopSort0
{
    public static class BoardPresentation
    {
        // The source's layout point is treated as the mouth/pivot, rather than the body centre.
        public static Vector3 CarrierPosition(Vector3 mouth,Quaternion rotation,Vector3 pivotOffset)
        {
            var position=mouth-rotation*pivotOffset;
            // Imported Y values include legacy per-prefab offsets. Flat-board
            // carts and spares share one base, including levels 33, 57 and 123.
            position.y=Config.carrierLift-1f;return position;
        }

        [Serializable] sealed class Settings
        {
            public float pitch,targetRadius,framing,top,bottom,left,right;
            public float viewBottom=.16f,viewTop=.885f;
            public float carrierLift=1.755f,generatorVisualOffset=-2.27f;
        }
        static Settings settings;
        static Settings Config=>settings??(settings=GameAssets.Data<Settings>("presentation"));
        internal static float CameraPitch=>Config.pitch;
        // The .745 conveyor underside is the common physical base. Keep roots
        // on one plane; compensate the taller authored hopper inside its root.
        internal static float GeneratorVisualOffset=>Config.generatorVisualOffset;
        internal static TrackPoint[] TrackPointsFor(LevelData level)=>level.points;
        public static Bounds BoundsFor(TrackGeometry track,IReadOnlyList<Transform> carriers,Vector3 pivot)
        {
            var bounds=new Bounds(track.Points[0],Vector3.zero);
            foreach(var point in track.Points)bounds.Encapsulate(point);
            for(int i=0;i<carriers.Count;i++)
            {
                var carrier=carriers[i];
                bounds.Encapsulate(carrier.position);bounds.Encapsulate(carrier.TransformPoint(pivot));
            }
            Vector3 min=bounds.min,max=bounds.max;
            min.x-=Config.left;max.x+=Config.right;min.z-=Config.bottom;max.z+=Config.top;
            bounds.SetMinMax(min,max);return bounds;
        }
        // Cache the visible envelope in camera-aligned coordinates on level load
        // and layout changes. Animation and physics do not move the framing.
        public static Bounds VisibleBoundsFor(TrackGeometry track,IEnumerable<Transform> roots)
        {
            var projection=Matrix4x4.Rotate(Quaternion.Inverse(CameraRotation));
            var result=ProjectBounds(track.SurfaceBounds,projection);
            if(track.PortalBounds.HasValue)
                result.Encapsulate(ProjectBounds(track.PortalBounds.Value,projection));
            foreach(var root in roots)foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy||renderer.sharedMaterial==null||renderer.sharedMaterial.shader.name.Contains("SourceHidden"))continue;
                var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                if(renderer.forceRenderingOff&&filter.sharedMesh.name!="Volcano 3D / cart")continue;
                result.Encapsulate(ProjectBounds(filter.sharedMesh.bounds,projection*filter.transform.localToWorldMatrix));
            }
            return result;
        }

        static Quaternion CameraRotation=>Quaternion.Euler(Config.pitch,0,0);

        static Bounds ProjectBounds(Bounds bounds,Matrix4x4 projection)
        {
            var e=bounds.extents;
            var size=2f*new Vector3(
                Mathf.Abs(projection.m00)*e.x+Mathf.Abs(projection.m01)*e.y+Mathf.Abs(projection.m02)*e.z,
                Mathf.Abs(projection.m10)*e.x+Mathf.Abs(projection.m11)*e.y+Mathf.Abs(projection.m12)*e.z,
                Mathf.Abs(projection.m20)*e.x+Mathf.Abs(projection.m21)*e.y+Mathf.Abs(projection.m22)*e.z);
            return new Bounds(projection.MultiplyPoint3x4(bounds.center),size);
        }

        public static void Frame(Camera camera,Bounds bounds,ScreenOffset offset=null,Bounds? visible=null,float bottomInset=0)
        {
            var rotation=CameraRotation;
            var padded=ProjectBounds(bounds,Matrix4x4.Rotate(Quaternion.Inverse(rotation)));
            padded.Expand(2f*Config.targetRadius);
            float aspect=Mathf.Max(.1f,camera.aspect);
            float offsetX=offset?.X??0,offsetY=offset?.Y??0;
            float bottom=Mathf.Lerp(Mathf.Clamp(Config.viewBottom,0,.45f),1,bottomInset);
            float top=Mathf.Lerp(Mathf.Clamp(Config.viewTop,.55f,1),1,bottomInset);
            float width=Mathf.Max(.1f,Config.framing-2*Mathf.Abs(offsetX));
            float height=Mathf.Max(.1f,top-bottom-2*Mathf.Abs(offsetY));
            float fit=Mathf.Max(padded.size.x/(2*aspect*width),padded.size.y/(2*height));

            // Lower high layouts into the visible play area. Keep the native
            // zoom and any lower authored placement that already fits.
            float boardTop=Mathf.Lerp(Mathf.Min(Config.viewTop,.885f),1,bottomInset);
            float centerY=(bottom+boardTop)*.5f;
            Vector3 focus=padded.center;
            if(visible.HasValue)
            {
                var box=visible.Value;
                float nativeCenterY=(bottom+top)*.5f+offsetY+(box.center.y-focus.y)/(2*fit);
                focus.y=box.center.y;
                float leftSpace=Mathf.Max(.05f,.5f+offsetX-.015f),rightSpace=Mathf.Max(.05f,.485f-offsetX);
                fit=Mathf.Max(fit,(focus.x-box.min.x)/(2*aspect*leftSpace),(box.max.x-focus.x)/(2*aspect*rightSpace),
                    box.size.y/(2*Mathf.Max(.1f,boardTop-bottom)));
                float halfHeight=box.extents.y/(2*fit);
                centerY=Mathf.Max(bottom+halfHeight,Mathf.Min(centerY,nativeCenterY));
            }
            camera.orthographicSize=fit;
            camera.transform.rotation=rotation;
            Vector3 screenOffset=new Vector3(offsetX*2*fit*aspect,(centerY-.5f)*2*fit,40);
            camera.transform.position=rotation*(focus-screenOffset);
        }
    }
}
