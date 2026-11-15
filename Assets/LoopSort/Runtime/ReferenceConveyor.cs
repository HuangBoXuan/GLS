using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoopSort0
{
    // Reference-image cross section, swept along the existing gameplay spline.
    // Colours are explicit bands, independent of scene light and pipe normals.
    public static class ReferenceConveyor
    {
        public const string ShaderPath="LoopSort/Shaders/ReferenceConveyor";
        static readonly Vector2[] Rim={
            new Vector2(.665f,.003f),new Vector2(.695f,.012f),
            new Vector2(.730f,.023f),new Vector2(.750f,.340f),
            new Vector2(.790f,.515f),new Vector2(.825f,.540f),
            new Vector2(.940f,.525f),new Vector2(.985f,.450f),
            new Vector2(.985f,-.290f),new Vector2(.950f,-.355f)};
        static readonly uint[] Bands={0x395B86,0x424773,0x545988,0x8D9BB9,0xE5E9FC,0xC1D1E9,0x8E9FBD,0x585981,0x393957};

        public static Material Material(bool arrows=false)
        {
            var shader=Application.isPlaying?HotUpdate.HotContent.Load<Shader>(ShaderPath):Resources.Load<Shader>(ShaderPath);
            if(shader==null)throw new InvalidOperationException("Missing reference conveyor shader");
            var material=new Material(shader){name=arrows?"Reference blue arrows":"Reference raised blue conveyor",enableInstancing=true};
            material.SetFloat("_UseVertexColor",arrows?0:1);
            material.color=arrows?ArtTheme.Hex(0xACD7FF):Color.white;return material;
        }

        public static Mesh Build(NativeSpline spline)
        {
            int count=Math.Max(spline.SampleCount,Mathf.CeilToInt(spline.Length*6)+1);
            var centers=new Vector3[count];var sides=new Vector3[count];var up=new Vector3[count];var distances=new float[count];
            for(int i=0;i<count;i++)
            {
                spline.Evaluate(i/(double)(count-1),out centers[i],out Vector3 forward,out _,out distances[i]);
                sides[i]=Vector3.Cross(Vector3.up,forward).normalized;up[i]=Vector3.Cross(forward,sides[i]).normalized;
            }
            var vertices=new List<Vector3>(count*40);var colors=new List<Color32>(count*40);
            var uv=new List<Vector2>(count*40);var route=new List<Vector2>(count*40);var triangles=new List<int>((count-1)*120);
            // Broad blue panels with subtle lateral colour variation.
            Strip(-.695f,0,.695f,0,0x467BA8,0x376796,true);
            for(int sign=-1;sign<=1;sign+=2)
                for(int band=0;band<Bands.Length;band++)
                    Strip(sign*Rim[band].x,Rim[band].y,sign*Rim[band+1].x,Rim[band+1].y,Bands[band],Bands[band],false);
            // A continuous dark underbody is visible below the raised outer edge.
            Strip(-.950f,-.355f,.950f,-.355f,0x383651,0x383651,false);
            var mesh=new Mesh{name="Reference conveyor raised rim and colour bands",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,route);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;

            void Strip(float x0,float y0,float x1,float y1,uint c0,uint c1,bool floor)
            {
                int first=vertices.Count;Color32 left=ArtTheme.Hex(c0),right=ArtTheme.Hex(c1);left.a=right.a=(byte)(floor?255:0);
                for(int i=0;i<count;i++)
                {
                    vertices.Add(centers[i]+sides[i]*x0+up[i]*y0);vertices.Add(centers[i]+sides[i]*x1+up[i]*y1);
                    colors.Add(left);colors.Add(right);uv.Add(new Vector2(0,distances[i]));uv.Add(new Vector2(1,distances[i]));
                    route.Add(new Vector2(i/(float)(count-1),0));route.Add(new Vector2(i/(float)(count-1),0));
                    if(i+1==count)continue;int a=first+i*2,b=a+2;
                    triangles.Add(a);triangles.Add(b);triangles.Add(b+1);triangles.Add(a);triangles.Add(b+1);triangles.Add(a+1);
                }
            }
        }
    }
}
