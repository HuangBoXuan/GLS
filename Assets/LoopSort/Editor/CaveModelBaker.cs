using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoopSort0.Editor
{
    // The cave must enclose a real, upright passage. Its rock paint comes from
    // the guide, but its geometry is a stone arch rather than an image silhouette.
    public static class CaveModelBaker
    {
        const float Width=3.25f,Height=2.6f;
        const string Destination="Assets/LoopSort/Resources/LoopSort/Models/volcano-cave.asset";
        [MenuItem("Tools/LoopSort/Rebuild standing stone cave")]
        public static void Bake()
        {
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                image.LoadImage(File.ReadAllBytes("Assets/LoopSort/Resources/LoopSort/Theme/mine-cave.png"));
                var pixels=image.GetPixels32();int iw=image.width,ih=image.height;
                Vector2 Paint(float u,float v)
                {
                    int sx=Mathf.Clamp(Mathf.RoundToInt(u*(iw-1)),0,iw-1),sy=Mathf.Clamp(Mathf.RoundToInt(v*(ih-1)),0,ih-1);
                    int bx=sx,by=sy,best=int.MaxValue;
                    for(int y=Math.Max(0,sy-80);y<=Math.Min(ih-1,sy+80);y++)for(int x=Math.Max(0,sx-80);x<=Math.Min(iw-1,sx+80);x++)
                    {
                        var c=pixels[y*iw+x];int d=(x-sx)*(x-sx)+(y-sy)*(y-sy);
                        if(c.a<250||c.b<c.r*.65f||c.r<45||d>=best)continue;
                        bx=x;by=y;best=d;
                    }
                    return new Vector2((bx+.5f)/iw,(by+.5f)/ih);
                }
                Vector2 lava=Vector2.zero;int lavaScore=-1;
                for(int y=0;y<ih;y++)for(int x=0;x<iw;x++)
                {
                    var c=pixels[y*iw+x];int score=1000-Math.Abs(255-c.r)-Math.Abs(145-c.g)-Math.Abs(30-c.b);
                    if(c.a<250||c.r<220||c.g<100||c.b>65||score<=lavaScore)continue;
                    lavaScore=score;lava=new Vector2((x+.5f)/iw,(y+.5f)/ih);
                }
                var mesh=new Builder();
                Rect Rock(float u,float v,float radius=.034f)
                {
                    Vector2 p=Paint(u,v);
                    // Stay inside opaque paint; transparent outline pixels must
                    // never cut holes into the structural stone faces.
                    float r=radius;
                    for(int pass=0;pass<8;pass++)
                    {
                        bool safe=true;
                        for(int gy=0;gy<5;gy++)for(int gx=0;gx<5;gx++)
                        {
                            int x=Mathf.Clamp(Mathf.RoundToInt((p.x-r+gx*r*.5f)*(iw-1)),0,iw-1),y=Mathf.Clamp(Mathf.RoundToInt((p.y-r+gy*r*.5f)*(ih-1)),0,ih-1);
                            var c=pixels[y*iw+x];if(c.a<250||c.b<c.r*.6f)safe=false;
                        }
                        if(safe)break;r*=.65f;
                    }
                    return new Rect(p.x-r,p.y-r,r*2,r*2);
                }
                var fire=new Rect(lava.x,lava.y,0,0);
                const float inner=1.02f,outer=1.60f,spring=1.25f,depth=.96f;
                for(int i=0;i<28;i++)
                    mesh.Stone(Wedge(inner+.025f,outer-.025f,spring,i*Mathf.PI/28,(i+1)*Mathf.PI/28,2),.78f,0,fire,Color.white);
                foreach(float sign in new[]{-1f,1f})
                {
                    float x=sign*(inner+outer)*.5f;
                    mesh.Box(x,.062f,.82f,.124f,1.16f,Rock(sign<0?.24f:.80f,.17f),.035f);
                    mesh.Box(x,.152f,.69f,.084f,1.06f,Rock(sign<0?.23f:.82f,.21f),.03f);
                    mesh.Box(x,.72f,.52f,1.02f,.78f,fire,.01f);
                    float step=(spring-.194f)/3;
                    for(int i=0;i<3;i++)
                        mesh.Box(x,.194f+(i+.5f)*step,outer-inner,step-.025f,depth,Rock(sign<0?.24f:.80f,.27f+i*.115f),.055f);
                }
                var patches=new[]{new Vector2(.30f,.55f),new Vector2(.39f,.65f),new Vector2(.47f,.76f),new Vector2(.55f,.78f),new Vector2(.67f,.74f),new Vector2(.76f,.65f),new Vector2(.82f,.54f)};
                for(int i=0;i<7;i++)
                {
                    float a=i*Mathf.PI/7+.013f,b=(i+1)*Mathf.PI/7-.013f;
                    var patch=patches[6-i];
                    mesh.Stone(Wedge(inner,outer+(i==3?.035f:0),spring,a,b,3),depth,.075f,Rock(patch.x,patch.y),Color.white);
                }
                var target=AssetDatabase.LoadAssetAtPath<Mesh>(Destination);
                if(target==null){target=new Mesh();AssetDatabase.CreateAsset(target,Destination);}else target.Clear();
                mesh.Write(target);EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);
            }
            finally{UnityEngine.Object.DestroyImmediate(image);}
        }
        static Vector2[] Wedge(float inner,float outer,float spring,float a,float b,int steps)
        {
            var polygon=new List<Vector2>();
            for(int i=0;i<=steps;i++){float angle=Mathf.Lerp(a,b,i/(float)steps);polygon.Add(new Vector2(Mathf.Cos(angle)*outer,spring+Mathf.Sin(angle)*outer));}
            for(int i=steps;i>=0;i--){float angle=Mathf.Lerp(a,b,i/(float)steps);polygon.Add(new Vector2(Mathf.Cos(angle)*inner,spring+Mathf.Sin(angle)*inner));}
            return polygon.ToArray();
        }
        sealed class Builder
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();readonly List<Color> colors=new List<Color>();readonly List<int> triangles=new List<int>();
            int Vertex(Vector3 p,Vector2 t,Color color){int i=vertices.Count;vertices.Add(new Vector3(p.x/Width,p.y/Height,p.z));uv.Add(t);colors.Add(color);return i;}
            Vector2 UV(Rect r,float u,float v)=>new Vector2(r.x+u*r.width,r.y+v*r.height);
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Rect paint,Color shade)
            {
                int s=vertices.Count;Vertex(a,UV(paint,0,0),shade);Vertex(b,UV(paint,1,0),shade);Vertex(c,UV(paint,1,1),shade);Vertex(d,UV(paint,0,1),shade);
                triangles.AddRange(new[]{s,s+1,s+2,s,s+2,s+3});
            }
            public void Box(float x,float y,float w,float h,float depth,Rect paint,float bevel)
            {
                Stone(new[]{new Vector2(x-w*.5f,y-h*.5f),new Vector2(x+w*.5f,y-h*.5f),new Vector2(x+w*.5f,y+h*.5f),new Vector2(x-w*.5f,y+h*.5f)},depth,bevel,paint,Color.white);
            }
            public void Stone(Vector2[] shape,float depth,float bevel,Rect paint,Color shade)
            {
                var center=Vector2.zero;var min=shape[0];var max=shape[0];
                foreach(var p in shape){center+=p;min=Vector2.Min(min,p);max=Vector2.Max(max,p);}center/=shape.Length;
                var rings=new Vector3[4][];
                for(int k=0;k<4;k++)
                {
                    rings[k]=new Vector3[shape.Length];
                    float z=k==0?-depth*.5f:k==1?-depth*.5f+bevel:k==2?depth*.5f-bevel:depth*.5f;
                    for(int i=0;i<shape.Length;i++)
                    {
                        var p=shape[i];if(k==0||k==3){var delta=p-center;p-=delta.normalized*Mathf.Min(bevel,delta.magnitude*.25f);}
                        rings[k][i]=new Vector3(p.x,p.y,z);
                    }
                }
                for(int face=0;face<2;face++)
                {
                    int k=face==0?0:3,start=vertices.Count;
                    Vertex(new Vector3(center.x,center.y,rings[k][0].z),UV(paint,.5f,.5f),shade);
                    for(int i=0;i<shape.Length;i++)
                    {
                        var p=rings[k][i];Vertex(p,UV(paint,Mathf.InverseLerp(min.x,max.x,p.x),Mathf.InverseLerp(min.y,max.y,p.y)),shade);
                    }
                    for(int i=0;i<shape.Length;i++)
                    {
                        int a=start+1+i,b=start+1+(i+1)%shape.Length;
                        if(face==0)triangles.AddRange(new[]{start,b,a});else triangles.AddRange(new[]{start,a,b});
                    }
                }
                for(int k=0;k<3;k++)for(int i=0;i<shape.Length;i++)
                {
                    int next=(i+1)%shape.Length;
                    var sidePaint=new Rect(paint.center.x-paint.width*.08f,paint.center.y-paint.height*.08f,paint.width*.16f,paint.height*.16f);
                    Quad(rings[k][i],rings[k][next],rings[k+1][next],rings[k+1][i],sidePaint,shade);
                }
            }
            public void Write(Mesh mesh)
            {
                mesh.name="Volcano 3D / illustrated cave";mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            }
        }
    }
}
