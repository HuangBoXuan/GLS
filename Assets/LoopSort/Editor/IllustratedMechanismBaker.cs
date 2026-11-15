using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoopSort0.Editor
{
    // Authoring-only relief bake. Runtime shares these meshes and the guide paint.
    public static class IllustratedMechanismBaker
    {
        [MenuItem("Tools/LoopSort/Rebuild illustrated gate and blanket")]
        public static void Bake()
        {
            BakePart("gate", "gate", .17f, .83f, false);
            BakePart("gate", "gate-left", 0, .17f, false);
            BakePart("gate", "gate-right", .83f, 1, false);
            BakePart("blanket", "blanket", 0, 1, true);
        }
        [MenuItem("Tools/LoopSort/Rebuild illustrated hopper and lid")]
        public static void BakeHopperAndLid()
        {
            BakePart("lid","lid",0,1,false);
            BakePart("hopper","hopper-frame",0,1,false);
            BakePart("hopper","hopper-left",.035f,.5f,false,.19f,.85f);
            BakePart("hopper","hopper-right",.5f,.965f,false,.19f,.85f);
        }
        [MenuItem("Tools/LoopSort/Rebuild illustrated cave and one-way barrier")]
        public static void BakeCaveAndBarrier()
        {
            CaveModelBaker.Bake();
            BakePart("barrier","barrier",0,1,false);
        }
        static void BakePart(string art,string id,float cropLeft,float cropRight,bool fabric,float cropBottom=0,float cropTop=1)
        {
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                image.LoadImage(File.ReadAllBytes("Assets/LoopSort/Resources/LoopSort/Theme/mine-"+art+".png"));
                var pixels=image.GetPixels32();int iw=image.width,ih=image.height;
                int minX=iw,minY=ih,maxX=0,maxY=0;
                for(int y=0;y<ih;y++)for(int x=0;x<iw;x++)if(pixels[y*iw+x].a>127)
                {minX=Math.Min(x,minX);maxX=Math.Max(x,maxX);minY=Math.Min(y,minY);maxY=Math.Max(y,maxY);}
                int fullWidth=maxX-minX+1,fullHeight=maxY-minY+1;
                maxX=minX+Mathf.RoundToInt((fullWidth-1)*cropRight);
                minX+=Mathf.RoundToInt((fullWidth-1)*cropLeft);
                maxY=minY+Mathf.RoundToInt((fullHeight-1)*cropTop);
                minY+=Mathf.RoundToInt((fullHeight-1)*cropBottom);
                int width=maxX-minX+1,height=maxY-minY+1;
                const int columns=64,rows=48;int stride=columns+1;
                var vertices=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
                var edges=new Dictionary<long,Vector2Int>();
                Color32 Pixel(float u,float v)=>pixels[(minY+Mathf.RoundToInt(v*(height-1)))*iw+minX+Mathf.RoundToInt(u*(width-1))];
                bool Included(float u,float v)=>Pixel(u,v).a>=32&&!(id=="hopper-frame"&&u>.035f&&u<.965f&&v>.19f&&v<.85f);
                for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
                {
                    float u=x/(float)columns,v=y/(float)rows;var c=Pixel(u,v);
                    // Shared cut lines make the closed doors meet the housing
                    // exactly, independent of the relief grid resolution.
                    if(id=="hopper-frame")
                    {
                        if(x==2)u=.035f;else if(x==62)u=.965f;
                        if(y==9)v=.19f;else if(y==41)v=.85f;
                        c=Pixel(u,v);
                    }
                    bool copper=c.r>c.b*1.5f&&c.g>c.b*1.2f;
                    float rise;
                    if(fabric)
                    {
                        float rim=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.13f,.045f,Mathf.Min(v,1-v)));
                        rise=.045f+rim*.15f+.035f*Mathf.Pow(Mathf.Sin(u*3*Mathf.PI)*Mathf.Sin(v*2*Mathf.PI),2);
                        if(copper)rise+=.055f;
                    }
                    else if(art=="lid")
                    {
                        bool emblem=c.r>140&&c.g>130&&c.b>120;
                        rise=copper?.25f:emblem?.23f:.10f+.10f*Mathf.InverseLerp(35,110,c.r);
                    }
                    else if(art=="barrier")rise=copper?.16f:.12f+.10f*Mathf.InverseLerp(35,110,c.r);
                    else if(art=="hopper")rise=copper?.07f:.14f+.12f*Mathf.InverseLerp(35,110,c.r);
                    else rise=copper?.22f:.10f+.12f*Mathf.InverseLerp(35,110,c.r);
                    vertices.Add(new Vector3(u-.5f,v-.5f,-rise));
                    uv.Add(new Vector2((minX+u*(width-1)+.5f)/iw,(minY+v*(height-1)+.5f)/ih));colors.Add(Color.white);
                }
                // Painted bevels cross grid cells diagonally. Smooth their
                // height transition so they do not become sawtooth side walls.
                if(art=="hopper"||art=="lid"||art=="barrier")for(int pass=0;pass<3;pass++)
                {
                    var previous=vertices.ToArray();
                    for(int y=1;y<rows;y++)for(int x=1;x<columns;x++)
                    {
                        int i=y*stride+x;var p=previous[i];
                        p.z=(p.z*4+previous[i-1].z+previous[i+1].z+previous[i-stride].z+previous[i+stride].z)/8;
                        vertices[i]=p;
                    }
                }
                void Edge(int a,int b){long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);if(edges.ContainsKey(key))edges.Remove(key);else edges.Add(key,new Vector2Int(a,b));}
                void Triangle(int a,int b,int c){triangles.AddRange(new[]{a,b,c});Edge(a,b);Edge(b,c);Edge(c,a);}
                for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
                {
                    if(!Included((x+.5f)/columns,(y+.5f)/rows))continue;
                    int a=y*stride+x,b=a+1,c=a+stride,d=c+1;Triangle(a,c,b);Triangle(b,c,d);
                }
                var boundary=new HashSet<int>();foreach(var e in edges.Values){boundary.Add(e.x);boundary.Add(e.y);}
                foreach(int i in boundary)
                {
                    int sx=Mathf.Clamp(Mathf.RoundToInt(uv[i].x*iw-.5f),minX,maxX),sy=Mathf.Clamp(Mathf.RoundToInt(uv[i].y*ih-.5f),minY,maxY);
                    if(pixels[sy*iw+sx].a>=220)continue;
                    int bx=sx,by=sy,best=int.MaxValue;
                    for(int dy=-24;dy<=24;dy++)for(int dx=-24;dx<=24;dx++)
                    {
                        int x=sx+dx,y=sy+dy,d=dx*dx+dy*dy;
                        if(x<minX||x>maxX||y<minY||y>maxY||d>=best||pixels[y*iw+x].a<220)continue;
                        best=d;bx=x;by=y;
                    }
                    if(best==int.MaxValue)continue;
                    vertices[i]=new Vector3((bx-minX)/(float)(width-1)-.5f,(by-minY)/(float)(height-1)-.5f,vertices[i].z);
                    uv[i]=new Vector2((bx+.5f)/iw,(by+.5f)/ih);
                }
                float bottom=fabric?.035f:.22f;
                foreach(var e in edges.Values)
                {
                    int s=vertices.Count;var a=vertices[e.x];var b=vertices[e.y];
                    vertices.AddRange(new[]{a,b,new Vector3(b.x,b.y,bottom),new Vector3(a.x,a.y,bottom)});
                    for(int j=0;j<4;j++){uv.Add(uv[e.x]);colors.Add(new Color(.72f,.72f,.72f,1));}
                    triangles.AddRange(new[]{s+1,s,s+2,s+2,s,s+3});
                }
                // A separate underside shares the authored top footprint.
                int count=vertices.Count;
                for(int i=0;i<(columns+1)*(rows+1);i++){var p=vertices[i];vertices.Add(new Vector3(p.x,p.y,bottom));uv.Add(uv[i]);colors.Add(new Color(.45f,.45f,.45f,1));}
                int surfaceIndices=0;for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)if(Included((x+.5f)/columns,(y+.5f)/rows))surfaceIndices+=6;
                for(int i=0;i<surfaceIndices;i+=3)triangles.AddRange(new[]{triangles[i]+count,triangles[i+2]+count,triangles[i+1]+count});
                string path="Assets/LoopSort/Resources/LoopSort/Models/volcano-"+id+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
                mesh.name="Volcano 3D / illustrated "+id;mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);
            }
            finally{UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
