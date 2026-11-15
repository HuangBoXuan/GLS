using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoopSort0.Editor
{
    // The guide is the authored paint layer, not a loose visual reference.
    // Bake a closed relief mesh once in the editor; no pixel reads or mesh
    // generation are required on the device. Rebuild after changing its art.
    public static class CrustModelBaker
    {
        const string Source="Assets/LoopSort/Resources/LoopSort/Theme/mine-crust.png";
        public const string Destination="Assets/LoopSort/Resources/LoopSort/Models/volcano-crust.asset";
        const int Columns=48,Rows=112;

        [MenuItem("Tools/LoopSort/Rebuild illustrated lava crust")]
        public static void Bake()
        {
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                image.LoadImage(File.ReadAllBytes(Source));
                var pixels=image.GetPixels32();int iw=image.width,ih=image.height;
                int minX=iw,minY=ih,maxX=0,maxY=0;
                for(int y=0;y<ih;y++)for(int x=0;x<iw;x++)if(pixels[y*iw+x].a>127)
                {minX=Math.Min(x,minX);maxX=Math.Max(x,maxX);minY=Math.Min(y,minY);maxY=Math.Max(y,maxY);}
                int width=maxX-minX+1,height=maxY-minY+1;
                // Distance from the painted molten channels gives each boulder
                // its own rounded shoulder. Preserve the actual irregular seams.
                var distance=new float[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    var c=pixels[(y+minY)*iw+x+minX];
                    bool molten=c.r>c.b*1.65f&&c.g>c.b*1.35f;
                    distance[y*width+x]=c.a<128||molten?0:10000;
                }
                for(int pass=0;pass<2;pass++)
                {
                    for(int y=1;y<height;y++)for(int x=1;x<width;x++)
                    {int i=y*width+x;distance[i]=Mathf.Min(distance[i],Mathf.Min(distance[i-1]+1,Mathf.Min(distance[i-width]+1,distance[i-width-1]+1.4142f)));}
                    for(int y=height-2;y>=0;y--)for(int x=width-2;x>=0;x--)
                    {int i=y*width+x;distance[i]=Mathf.Min(distance[i],Mathf.Min(distance[i+1]+1,Mathf.Min(distance[i+width]+1,distance[i+width+1]+1.4142f)));}
                }
                var positions=new List<Vector3>();var uvs=new List<Vector2>();var colors=new List<Color>();
                var triangles=new List<int>();var edges=new Dictionary<long,Vector2Int>();
                int stride=Columns+1;
                Color32 Pixel(float u,float v)=>pixels[(minY+Mathf.Clamp(Mathf.RoundToInt(v*(height-1)),0,height-1))*iw+minX+Mathf.Clamp(Mathf.RoundToInt(u*(width-1)),0,width-1)];
                for(int y=0;y<=Rows;y++)for(int x=0;x<=Columns;x++)
                {
                    float u=x/(float)Columns,v=y/(float)Rows;
                    int px=Mathf.Clamp(Mathf.RoundToInt(u*(width-1)),0,width-1),py=Mathf.Clamp(Mathf.RoundToInt(v*(height-1)),0,height-1);
                    var c=Pixel(u,v);float shoulder=Mathf.SmoothStep(0,1,distance[py*width+px]/28f);
                    float rise=.035f+shoulder*(.39f+.20f*Mathf.InverseLerp(45,140,c.r));
                    // The illustration's lower end is the rear sill and wheels.
                    // Give it real depth rather than painting wheels on a flat lid.
                    float sill=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.065f,.21f,v));
                    rise=Mathf.Lerp(-.95f,rise,sill);
                    // The guide looks toward the rear; align that end with the
                    // cart's +Z tail. Finish coordinates match VolcanoArt's nodes.
                    positions.Add(new Vector3(.5f-u,.5f-v,-rise));
                    uvs.Add(new Vector2((minX+u*(width-1)+.5f)/iw,(minY+v*(height-1)+.5f)/ih));
                    colors.Add(Color.white);
                }
                void Edge(int a,int b)
                {
                    long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
                    if(edges.ContainsKey(key))edges.Remove(key);else edges.Add(key,new Vector2Int(a,b));
                }
                void Triangle(int a,int b,int c)
                {triangles.Add(a);triangles.Add(b);triangles.Add(c);Edge(a,b);Edge(b,c);Edge(c,a);}
                for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++)
                {
                    if(Pixel((x+.5f)/Columns,(y+.5f)/Rows).a<32)continue;
                    int a=y*stride+x,b=a+1,c=a+stride,d=c+1;
                    Triangle(a,c,b);Triangle(b,c,d);
                }
                // Snap the boundary to opaque paint before extruding. Leaving
                // transparent grid corners on the side walls creates black
                // vertical slivers that do not exist in the illustration.
                var boundary=new HashSet<int>();foreach(var edge in edges.Values){boundary.Add(edge.x);boundary.Add(edge.y);}
                foreach(int index in boundary)
                {
                    int sx=Mathf.Clamp(Mathf.RoundToInt(uvs[index].x*iw-.5f),minX,maxX),sy=Mathf.Clamp(Mathf.RoundToInt(uvs[index].y*ih-.5f),minY,maxY);
                    if(pixels[sy*iw+sx].a>=220)continue;
                    int nearestX=sx,nearestY=sy,best=int.MaxValue;
                    for(int dy=-24;dy<=24;dy++)for(int dx=-24;dx<=24;dx++)
                    {
                        int x=sx+dx,y=sy+dy,dist=dx*dx+dy*dy;
                        if(x<minX||x>maxX||y<minY||y>maxY||dist>=best||pixels[y*iw+x].a<220)continue;
                        best=dist;nearestX=x;nearestY=y;
                    }
                    if(best==int.MaxValue)continue;
                    positions[index]=new Vector3(.5f-(nearestX-minX)/(float)(width-1),.5f-(nearestY-minY)/(float)(height-1),positions[index].z);
                    uvs[index]=new Vector2((nearestX+.5f)/iw,(nearestY+.5f)/ih);
                }
                const float underside=1.0f;
                int floor=positions.Count;positions.Add(new Vector3(0,0,underside));uvs.Add(new Vector2(.5f,.5f));colors.Add(new Color(.42f,.42f,.42f,0));
                foreach(var edge in edges.Values)
                {
                    int a=edge.x,b=edge.y,start=positions.Count;
                    positions.Add(positions[a]);positions.Add(positions[b]);
                    positions.Add(new Vector3(positions[b].x,positions[b].y,underside));
                    positions.Add(new Vector3(positions[a].x,positions[a].y,underside));
                    for(int j=0;j<4;j++)uvs.Add(uvs[a]);
                    for(int j=0;j<4;j++)colors.Add(new Color(.72f,.72f,.72f,0));
                    triangles.AddRange(new[]{start+1,start,start+2,start+2,start,start+3,floor,start+2,start+3});
                }
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Destination);
                if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,Destination);}else mesh.Clear();
                mesh.name="Volcano 3D / illustrated crust";
                mesh.SetVertices(positions);mesh.SetUVs(0,uvs);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);
                Debug.Log("Illustrated crust baked: "+mesh.vertexCount+" vertices, "+triangles.Count/3+" triangles.");
            }
            finally {UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
