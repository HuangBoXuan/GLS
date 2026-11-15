using System;
using System.Collections.Generic;
using UnityEngine;
using LoopSort0.Core;

namespace LoopSort0
{
    public sealed class TrackGeometry : IDisposable
    {
        public readonly List<Vector3> Points = new List<Vector3>();
        readonly List<float> lengths = new List<float>();
        readonly List<Mesh> owned = new List<Mesh>();
        readonly List<Renderer> routeRenderers=new List<Renderer>();
        readonly List<Transform> portalModels=new List<Transform>();
        internal Bounds? PortalBounds {get;private set;}
        internal Bounds SurfaceBounds {get;private set;}
        readonly MaterialPropertyBlock revealProperties=new MaterialPropertyBlock();
        float arrowTravel;
        const float RouteSearchRadius = 2.5f;
        struct Segment
        {
            public Vector3 start,delta,tangent;
            public float startDistance,endDistance,length,inverseSquaredLength;
        }
        struct Projection
        {
            public int segment;
            public float squaredError,distance;
            public Vector3 position;
        }
        Segment[] segments = Array.Empty<Segment>();
        public float Length { get; private set; }
        public const float Surface = 1.1f;
        public const float HalfWidth = .72f;
        public NativeSpline Spline { get; }
        public bool Closed=>Spline.Closed;
        public TrackGeometry(LevelData level):this(BoardPresentation.TrackPointsFor(level),level.closed,level.subdivide) { }
        public TrackGeometry(TrackPoint[] source):this(source,true,1) { }
        public TrackGeometry(TrackPoint[] source,bool closed,int subdivide)
        {
            Spline=new NativeSpline(source,closed,subdivide);Length=Spline.Length;
            int count=Spline.SampleCount-(closed?1:0);
            for(int i=0;i<count;i++)
            {
                Spline.Evaluate(i/(double)(Spline.SampleCount-1),out Vector3 position,out _,out _,out float distance);
                Points.Add(position);lengths.Add(distance);
            }
            CacheSegments();
        }
        void CacheSegments()
        {
            segments=new Segment[Closed?Points.Count:Points.Count-1];
            for(int i=0;i<segments.Length;i++)
            {
                Vector3 delta=Points[(i+1)%Points.Count]-Points[i];float squaredLength=delta.sqrMagnitude;
                float length=Mathf.Sqrt(squaredLength);
                segments[i]=new Segment {start=Points[i],delta=delta,
                    tangent=length>.00001f?delta/length:Vector3.forward,
                    startDistance=lengths[i],endDistance=i+1<lengths.Count?lengths[i+1]:Length,
                    length=length,inverseSquaredLength=squaredLength>.00000001f?1f/squaredLength:0};
            }
        }
        public Vector3 Nearest(Vector3 position, out Vector3 tangent, out float distance)
            =>Project(position,float.NaN,out tangent,out distance);
        public Vector3 NearestOnRoute(Vector3 position,float previousDistance,out Vector3 tangent,out float distance)
            =>Project(position,previousDistance,out tangent,out distance);
        Vector3 Project(Vector3 position,float hint,out Vector3 tangent,out float distance)
        {
            tangent = Vector3.forward;distance = 0;
            if (segments.Length == 0 || Length <= 0) return Points[0];
            var best = new Projection { segment = -1,squaredError = float.PositiveInfinity };
            bool hasHint = !float.IsNaN(hint) && !float.IsInfinity(hint);
            if (!hasHint || Length <= RouteSearchRadius * 2)
            {
                ProjectRange(position,0,Length,ref best);
            }
            else
            {
                hint = Closed?Mathf.Repeat(hint,Length):Mathf.Clamp(hint,0,Length);
                float first = hint - RouteSearchRadius,last = hint + RouteSearchRadius;
                // Split a wrapped window into two ranges, preserving segment-index order.
                if(!Closed)ProjectRange(position,Mathf.Max(0,first),Mathf.Min(Length,last),ref best);
                else if (first < 0)
                {
                    ProjectRange(position,0,last,ref best);
                    ProjectRange(position,first + Length,Length,ref best);
                }
                else if (last >= Length)
                {
                    ProjectRange(position,0,last - Length,ref best);
                    ProjectRange(position,first,Length,ref best);
                }
                else ProjectRange(position,first,last,ref best);
            }
            if (best.segment >= 0)
            {
                tangent = segments[best.segment].tangent;distance = best.distance;
                return best.position;
            }
            // If an abnormal position projects outside the allowed route window, stay
            // on the remembered branch rather than snapping to the origin or another deck.
            if (hasHint)
            {
                distance = Closed?Mathf.Repeat(hint,Length):Mathf.Clamp(hint,0,Length);
                int index = FirstSegmentEndingAtOrAfter(distance);
                if (index < segments.Length) tangent = segments[index].tangent;
                return At(distance);
            }
            return Points[0];
        }
        int FirstSegmentEndingAtOrAfter(float distance)
        {
            int first = 0,last = segments.Length;
            while (first < last)
            {
                int middle = first + (last - first) / 2;
                if (segments[middle].endDistance < distance) first = middle + 1;
                else last = middle;
            }
            return first;
        }
        void ProjectRange(Vector3 position,float firstDistance,float lastDistance,ref Projection best)
        {
            // Binary-search the first intersecting segment; distant segments never
            // perform a projection. No per-query arrays, iterators or lists are needed.
            int first = FirstSegmentEndingAtOrAfter(firstDistance);
            for (int i = first; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment.startDistance > lastDistance) break;
                if (segment.inverseSquaredLength == 0) continue;
                float t = Mathf.Clamp01(Vector3.Dot(position - segment.start,segment.delta) * segment.inverseSquaredLength);
                float along = segment.startDistance + t * segment.length;
                // Keep the existing route-distance eligibility rule, including the
                // partially intersecting segments at either end of the search window.
                if (along < firstDistance || along > lastDistance) continue;
                Vector3 candidate = segment.start + t * segment.delta;
                float error = (position - candidate).sqrMagnitude;
                if (error < best.squaredError || (error == best.squaredError && (best.segment < 0 || i < best.segment)))
                {
                    best.segment = i;best.squaredError = error;
                    best.distance = along;best.position = candidate;
                }
            }
        }
        public Vector3 At(float distance)=>Spline.AtDistance(distance);
        public void Build(Transform parent,Material floor,PhysicMaterial physicsMaterial=null)
        {
            var reference=ReferenceConveyor.Build(Spline);owned.Add(reference);
            var surface=new GameObject("Conveyor raised colour bands",typeof(MeshFilter),typeof(MeshRenderer));
            surface.transform.SetParent(parent,false);surface.GetComponent<MeshFilter>().sharedMesh=reference;
            var renderer=surface.GetComponent<MeshRenderer>();renderer.sharedMaterial=floor;
            SurfaceBounds=renderer.bounds;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;routeRenderers.Add(renderer);
            BuildTrackCollider(parent,physicsMaterial);
            if(!Closed)BuildPortals(parent);
            revealProperties.SetFloat("_ArrowPeriod",Length/Mathf.Max(1,Mathf.RoundToInt(Length/1.8f)));
            SetEntranceClip(1);
        }
        internal void AnimateArrows(float deltaTime)
        {
            arrowTravel=Mathf.Repeat(arrowTravel+deltaTime*.75f,Length);
            revealProperties.SetFloat("_ArrowTravel",arrowTravel);
            foreach(var renderer in routeRenderers)if(renderer!=null)renderer.SetPropertyBlock(revealProperties);
        }
        void BuildTrackCollider(Transform parent,PhysicMaterial material)
        {
            // Source SplineMeshAutoCount: ceil(length * 4). Four extruded cube channels.
            int spans=Mathf.Max(1,Mathf.CeilToInt(Length*4f)),rings=Closed?spans:spans+1;
            var vertices=new List<Vector3>(rings*16);var triangles=new List<int>(spans*96+48);
            for(int strip=0;strip<4;strip++)
            {
                float minX,maxX,minY,maxY;
                if(strip==0){minX=-.65f;maxX=.65f;minY=-.1f;maxY=0;}
                else if(strip==3){minX=-.65f;maxX=.65f;minY=.75f;maxY=.85f;}
                else {float center=strip==1?-.66f:.66f;minX=center-.05f;maxX=center+.05f;minY=0;maxY=.8f;}
                int first=vertices.Count;
                for(int i=0;i<rings;i++)
                {
                    Spline.Evaluate(i/(double)spans,out Vector3 center,out Vector3 forward,out Vector3 normal,out _);
                    Vector3 right=Vector3.Cross(normal,forward).normalized,up=Vector3.Cross(forward,right).normalized;
                    vertices.Add(center+right*minX+up*minY);vertices.Add(center+right*maxX+up*minY);
                    vertices.Add(center+right*maxX+up*maxY);vertices.Add(center+right*minX+up*maxY);
                }
                for(int i=0;i<spans;i++)for(int edge=0;edge<4;edge++)
                {
                    int a=first+i*4+edge,d=first+i*4+(edge+1)%4;
                    int b=first+((i+1)%rings)*4+edge,c=first+((i+1)%rings)*4+(edge+1)%4;
                    Quad(triangles,a,d,c,b);
                }
                if(!Closed)
                {
                    Quad(triangles,first,first+3,first+2,first+1);
                    int end=first+(rings-1)*4;Quad(triangles,end,end+1,end+2,end+3);
                }
            }
            var mesh=new Mesh {name="APK conveyor collision channels"};owned.Add(mesh);
            if(vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            var go=new GameObject("Conveyor collision surface",typeof(MeshCollider));go.transform.SetParent(parent,false);
            var collider=go.GetComponent<MeshCollider>();collider.convex=false;collider.contactOffset=.01f;
            collider.cookingOptions=(MeshColliderCookingOptions)30;
            collider.sharedMaterial=material;collider.sharedMesh=mesh;
        }
        static void Quad(List<int> triangles,int a,int b,int c,int d)
        {
            triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);
        }
        void BuildPortals(Transform parent)
        {
            Spline.Evaluate(0,out Vector3 start,out Vector3 startForward,out _,out _);
            Spline.Evaluate(1,out Vector3 end,out Vector3 endForward,out _,out _);
            Quaternion endRotation=Quaternion.LookRotation(endForward,Vector3.up)*Quaternion.AngleAxis(180,Vector3.up);
            BuildEnd(parent,start,Quaternion.LookRotation(startForward,Vector3.up),false,start,startForward);
            BuildEnd(parent,end,endRotation,true,start,startForward);
        }
        void BuildEnd(Transform parent,Vector3 position,Quaternion rotation,bool teleport,Vector3 start,Vector3 startForward)
        {
            var go=new GameObject(teleport?"Conveyor exit portal":"Conveyor entry");go.transform.SetParent(parent,false);
            go.transform.SetPositionAndRotation(position,rotation);
            var model=CarrierFeatureView.CreateFeature("portal",go.transform).transform;portalModels.Add(model);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled||renderer.forceRenderingOff||renderer.sharedMaterial==null||renderer.sharedMaterial.shader.name.Contains("SourceHidden"))continue;
                var bounds=PortalBounds??renderer.bounds;bounds.Encapsulate(renderer.bounds);PortalBounds=bounds;
            }
            if(teleport)
            {
                var trigger=go.AddComponent<BoxCollider>();trigger.isTrigger=true;
                trigger.size=new Vector3(1.7287674f,1.7751122f,1.1545888f);
                trigger.center=new Vector3(.004192114f,.30314088f,-.16386585f);
                var portal=go.AddComponent<ConveyorPortal>();portal.destination=start;portal.forward=startForward;
                portal.offsetRotation=Quaternion.AngleAxis(Vector3.SignedAngle(go.transform.forward,startForward,Vector3.up),Vector3.up);
            }
            // End prefab child Collider/Cube, including serialized local transform.
            var wall=new GameObject("End boundary",typeof(BoxCollider));wall.transform.SetParent(go.transform,false);
            wall.transform.localPosition=new Vector3(0,.4573f,-.558f);wall.transform.localScale=new Vector3(2.2433f,1.9147f,1);
            var collider=wall.GetComponent<BoxCollider>();collider.size=new Vector3(.7697295f,.9324083f,.92536354f);
            collider.center=new Vector3(.0014954435f,-.08176604f,.10076768f);collider.contactOffset=.01f;
        }
        internal void SetEntranceClip(float value)
        {
            revealProperties.SetFloat("_RouteClip",value);
            foreach(var renderer in routeRenderers)if(renderer!=null)renderer.SetPropertyBlock(revealProperties);
        }
        internal void SetEntranceDetails(float time)
        {
            float scale=Mathf.Clamp01(time/.5f);
            foreach(var model in portalModels)
            {
                model.localScale=Vector3.one*Mathf.Max(0,MotionRunner.InOutBack(scale));
                model.localPosition=Vector3.back*(1-MotionRunner.OutBack(Mathf.Clamp01((time-.4f)/.2f)));
            }
        }
        public void Dispose() { foreach(var mesh in owned)if(mesh!=null)UnityEngine.Object.Destroy(mesh);owned.Clear(); }
    }
}
