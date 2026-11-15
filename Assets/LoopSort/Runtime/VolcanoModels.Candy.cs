using System.Collections.Generic;
using UnityEngine;

namespace LoopSort0
{
    public static partial class VolcanoModels
    {
        // These closed-state envelopes match the original animation nodes.
        // The new art stays inside the track and mechanism clearances.
        static Bounds? CandyFootprint(string id)
        {
            switch(id)
            {
                case "cave": return new Bounds(P(0,.5540274f,0),P(1.0584614f,1.1080548f,1.16f));
                case "crust": return new Bounds(P(0,0,.1875f),P(1,1,1.625f));
                case "lid": return new Bounds(P(0,0,-.015f),P(1,1,.47f));
                case "blanket": return new Bounds(P(0,0,-.108672276f),P(1,1,.28734454f));
                case "hopper-frame": case "hopper-left": case "hopper-right":
                    return new Bounds(P(0,0,-.02f),P(1,1,.48f));
                case "gate": case "gate-left": case "gate-right": case "barrier":
                    return new Bounds(Vector3.zero,P(1,1,.44f));
                default: return null;
            }
        }

        static void CandyGate(Builder b,float w,float d)
        {
            b.RoundBox("chocolate-dark",P(0,0,0),P(w,.22f,d),.046f);
            for(int row=0;row<4;row++)for(int column=0;column<4;column++)
            {
                float x=(column-1.5f)*w*.243f,z=(row-1.5f)*d*.241f;
                b.RoundBox("chocolate",P(x,.135f,z),P(w*.234f,.29f,d*.229f),.094f);
            }
        }

        static void CandyGateBeams(Builder b,float w,float d)
        {
            foreach(float side in new[]{-1f,1f})
            {
                float z=side*d*.445f;
                b.RoundBox("dark",P(0,0,z),P(w,.32f,d*.105f),.063f);
                b.RoundBox("copper",P(0,.123f,z),P(w,.21f,d*.095f),.054f);
                b.RoundBox("mechanism-trim",P(0,.215f,z),P(w*.98f,.083f,d*.074f),.039f);
            }
        }

        static void CandyGatePost(Builder b,float w,float d,float side)
        {
            b.RoundBox("dark",P(0,0,0),P(w*.82f,.29f,d*.95f),.066f);
            b.RoundBox("iron",P(0,.15f,0),P(w*.74f,.24f,d*.83f),.062f);
            b.Box("rock",P(0,.26f,0),P(w*.52f,.12f,d*.68f),.025f);
            for(int i=0;i<3;i++)
                b.Box("warning",P(0,.329f,d*(-.27f+i*.09f)),P(w*.64f,.03f,d*.041f),.012f,
                    Quaternion.Euler(0,side*34,0));
            foreach(float end in new[]{-1f,1f})
            {
                float z=end*d*.41f;
                b.RoundBox("dark",P(0,.085f,z),P(w,.35f,d*.181f),.085f);
                b.RoundBox("copper",P(0,.246f,z),P(w*.96f,.25f,d*.164f),.087f);
                b.RoundBox("mechanism-trim",P(0,.351f,z),P(w*.86f,.09f,d*.132f),.043f);
                b.Ellipsoid("bolt-face",P(0,.392f,z),P(w*.133f,.039f,w*.133f));
            }
            CandyCord(b,"sugar-pink",w*.21f,P(0,.39f,d*.37f),P(side*w*.24f,.48f,d*.31f),
                P(side*w*.32f,.52f,d*.238f),P(side*w*.14f,.46f,d*.20f));
            CandyCord(b,"sugar-pink",w*.17f,P(side*w*.31f,.34f,-d*.17f),P(side*w*.32f,.42f,-d*.14f),
                P(-side*w*.19f,.43f,-d*.10f),P(-side*w*.26f,.34f,-d*.065f));
        }

        static void CandyCrust(Builder b,float w,float d)
        {
            b.RoundBox("dark",P(0,-.13f,0),P(w,.48f,d),.17f);
            b.RoundBox("sugar-pink",P(0,.067f,0),P(w*.96f,.15f,d*.965f),.071f);
            b.Ellipsoid("glaze-indigo",P(-.332f*w,.25f,-.324f*d),P(w*.167f,.229f,d*.174f),.51f,-5);
            b.Ellipsoid("glaze-blue",P(.012f*w,.275f,-.338f*d),P(w*.207f,.248f,d*.157f),.45f,3);
            b.Ellipsoid("glaze-indigo",P(.349f*w,.234f,-.321f*d),P(w*.150f,.216f,d*.176f),.49f,-5);
            b.Ellipsoid("glaze-blue",P(-.342f*w,.244f,-.002f*d),P(w*.158f,.226f,d*.168f),.46f,4);
            b.Ellipsoid("glaze-indigo",P(0,.287f,-.010f*d),P(w*.216f,.252f,d*.181f),.43f,-3);
            b.Ellipsoid("glaze-blue",P(.350f*w,.259f,.013f*d),P(w*.150f,.230f,d*.171f),.48f,4);
            b.Ellipsoid("glaze-indigo",P(-.336f*w,.259f,.326f*d),P(w*.164f,.232f,d*.170f),.46f,-5);
            b.Ellipsoid("glaze-blue",P(-.014f*w,.268f,.334f*d),P(w*.180f,.235f,d*.158f),.52f,4);
            b.Ellipsoid("glaze-indigo",P(.342f*w,.219f,.347f*d),P(w*.145f,.206f,d*.148f),.49f,-2);
            float radius=Mathf.Min(w*.205f,d*.16f);
            Vector3 candy=P(w*.292f,.472f,d*.326f);
            b.Cylinder("chocolate-dark",candy-Vector3.up*.05f,radius*1.07f,.15f,Vector3.up,24);
            b.Ellipsoid("sugar-pink",candy,P(radius,.113f,radius),.69f);
            var spiral=new Vector3[20];
            for(int i=0;i<spiral.Length;i++)
            {
                float t=i/(float)(spiral.Length-1),angle=3.6f*Mathf.PI*t;
                spiral[i]=candy+P(Mathf.Cos(angle)*radius*t*.82f,.116f-.036f*t*t,Mathf.Sin(angle)*radius*t*.82f);
            }
            CandyCord(b,"lava-core",radius*.058f,spiral);
        }

        static void CandyBlanket(Builder b,float w,float d)
        {
            b.Box("dark",P(0,-.035f,0),P(w,.17f,d*.98f),.07f);
            b.Box("fabric",P(0,.085f,0),P(w*.98f,.10f,d*.93f),.041f);
            foreach(float edge in new[]{-1f,1f})
                b.Box("copper",P(0,.075f,edge*d*.474f),P(w*.985f,.07f,d*.027f),.021f);
        }
        static void CandyBlanketRoller(Builder b,float w,float d)
        {
            b.Cylinder("dark",Vector3.zero,w*.39f,d*.98f,Vector3.forward,16);
            b.Cylinder("mechanism-trim",Vector3.zero,w*.43f,d*.87f,Vector3.forward,20);
            foreach(float end in new[]{-1f,1f})
            {
                b.Cylinder("sugar-pink",P(0,0,end*d*.451f),w*.50f,d*.105f,Vector3.forward,18);
                b.Cylinder("copper",P(0,0,end*d*.494f),w*.36f,d*.030f,Vector3.forward,16);
                b.Cylinder("bolt-face",P(0,0,end*d*.51f),w*.22f,d*.013f,Vector3.forward,12);
            }
        }

        static void CandyHopperFrame(Builder b,float w,float d)
        {
            var rim=Octagon(.5f,.5f,.145f);
            b.Polygon("dark",rim,w,d,-.09f,.38f,.076f,0,.77f);
            b.Polygon("copper",rim,w*.995f,d*.995f,.095f,.26f,.080f,0,.79f);
            b.Polygon("mechanism-trim",rim,w*.969f,d*.969f,.195f,.107f,.035f,0,.84f);
            foreach(float side in new[]{-1f,1f})
            {
                b.RoundBox("hazard",P(side*w*.456f,.213f,0),P(w*.076f,.092f,d*.56f),.030f);
                b.RoundBox("dark",P(0,.204f,side*d*.423f),P(w*.35f,.18f,d*.118f),.053f);
                b.RoundBox("gold",P(0,.291f,side*d*.423f),P(w*.252f,.063f,d*.078f),.031f);
                b.RoundBox("lamp-glow",P(0,.320f,side*d*.423f),P(w*.184f,.026f,d*.045f),.013f);
                foreach(float x in new[]{-.362f,.362f})
                {
                    b.Box("copper",P(x*w,.192f,side*d*.391f),P(w*.14f,.208f,d*.15f),.075f,
                        Quaternion.Euler(0,x*side*47f,0));
                    b.Ellipsoid("bolt-face",P(x*w,.299f,side*d*.391f),P(.067f,.042f,.067f));
                }
            }
        }

        static void Peppermint(Builder b,Vector3 center,float radius,string body,string stripe)
        {
            b.Cylinder(body,center,radius,.051f,Vector3.up,24);
            for(int i=0;i<6;i++)
            {
                float angle=i*Mathf.PI/3;
                var outline=new Vector2[10];
                for(int j=0;j<5;j++)
                {
                    float t=j/4f,dist=Mathf.Lerp(.14f,.956f,t),turn=.70f*t*t;
                    outline[j]=new Vector2(Mathf.Cos(angle+turn),Mathf.Sin(angle+turn))*dist;
                    outline[9-j]=new Vector2(Mathf.Cos(angle+turn+.42f),Mathf.Sin(angle+turn+.42f))*dist;
                }
                for(int j=0;j<outline.Length;j++)outline[j]=outline[j]*radius+new Vector2(center.x,center.z);
                b.Polygon(stripe,outline,1,1,center.y+.029f,.012f,.0025f);
            }
        }

        static void CandyCord(Builder b,string material,float radius,params Vector3[] points)
        {
            for(int i=1;i<points.Length;i++)
            {
                Vector3 a=points[i-1],c=points[i],direction=c-a;
                b.Cylinder(material,(a+c)*.5f,radius,direction.magnitude+radius*1.45f,direction,8);
            }
            b.Ellipsoid(material,points[0],Vector3.one*radius);
            b.Ellipsoid(material,points[points.Length-1],Vector3.one*radius);
        }

        sealed partial class Builder
        {
            readonly Dictionary<int,Vector3> curvedNormals=new Dictionary<int,Vector3>();

            int SurfaceVertex(Vector3 point,Vector3 normal)
            {
                int index=vertices.Count;
                vertices.Add(point);uv.Add(new Vector2(point.x,point.z));curvedNormals[index]=normal;
                return index;
            }
            void SurfaceQuad(int slot,int a,int b,int c,int d,Vector3 normal)
            {
                var faces=indices[slot];
                if(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),normal)<0)
                {faces.Add(a);faces.Add(c);faces.Add(b);faces.Add(a);faces.Add(d);faces.Add(c);}
                else
                {faces.Add(a);faces.Add(b);faces.Add(c);faces.Add(a);faces.Add(c);faces.Add(d);}
            }
            public void RoundBox(string material,Vector3 center,Vector3 size,float radius,Quaternion? orientation=null)
            {
                int slot=Slot(material);
                var rotation=orientation??Quaternion.identity;
                Vector3 half=size*.5f,edge=Vector3.Min(Vector3.one*radius,half*.92f),inside=half-edge;
                var samples=new float[3][];
                for(int i=0;i<3;i++)
                    samples[i]=new[]{-half[i],-half[i]+edge[i]*.5f,-inside[i],inside[i],half[i]-edge[i]*.5f,half[i]};
                for(int axis=0;axis<3;axis++)foreach(int side in new[]{-1,1})
                {
                    int u=(axis+1)%3,v=(axis+2)%3,first=vertices.Count;
                    var outward=Vector3.zero;outward[axis]=side;
                    for(int row=0;row<6;row++)for(int col=0;col<6;col++)
                    {
                        Vector3 point=outward*half[axis];point[u]=samples[u][col];point[v]=samples[v][row];
                        Vector3 nearest=Vector3.Min(Vector3.Max(point,-inside),inside);
                        Vector3 offset=point-nearest;
                        var corner=P(offset.x/edge.x,offset.y/edge.y,offset.z/edge.z).normalized;
                        var normal=P(corner.x/edge.x,corner.y/edge.y,corner.z/edge.z).normalized;
                        SurfaceVertex(center+rotation*(nearest+Vector3.Scale(corner,edge)),rotation*normal);
                    }
                    for(int row=0;row<5;row++)for(int col=0;col<5;col++)
                    {
                        int a=first+row*6+col;
                        SurfaceQuad(slot,a,a+1,a+7,a+6,rotation*outward);
                    }
                }
            }
            public void ArchTube(string material,float height,float radius,float thickness,float depth,float start,float end)
            {
                const int sides=12;
                int slot=Slot(material),first=vertices.Count,segments=Mathf.CeilToInt((end-start)*24/Mathf.PI);
                for(int i=0;i<=segments;i++)
                {
                    float angle=Mathf.Lerp(start,end,i/(float)segments);
                    var radial=P(Mathf.Cos(angle),Mathf.Sin(angle),0);
                    var center=Vector3.up*height+radial*radius;
                    for(int j=0;j<sides;j++)
                    {
                        float section=j*(2*Mathf.PI/sides);
                        float x=Mathf.Cos(section),z=Mathf.Sin(section);
                        var normal=radial*(x/thickness)+Vector3.forward*(z/depth);
                        SurfaceVertex(center+radial*(x*thickness)+Vector3.forward*(z*depth),normal.normalized);
                    }
                }
                for(int i=0;i<segments;i++)for(int j=0;j<sides;j++)
                {
                    int a=first+i*sides+j,b=first+i*sides+(j+1)%sides;
                    SurfaceQuad(slot,a,b,b+sides,a+sides,curvedNormals[a]+curvedNormals[b]);
                }
                foreach(int index in new[]{0,segments})
                {
                    float angle=index==0?start:end;
                    var center=P(Mathf.Cos(angle)*radius,height+Mathf.Sin(angle)*radius,0);
                    var normal=P(-Mathf.Sin(angle),Mathf.Cos(angle),0)*(index==0?-1:1);
                    int row=first+index*sides;
                    for(int j=0;j<sides;j++)
                        Face(slot,center,vertices[row+j],vertices[row+(j+1)%sides],normal);
                }
            }
            public void Ellipsoid(string material,Vector3 center,Vector3 radii,float roundness=1,float yaw=0)
            {
                int slot=Slot(material);
                int first=vertices.Count;
                var rotation=Quaternion.Euler(0,yaw,0);var inverse=Quaternion.Inverse(rotation);
                const int longitude=12,latitude=6;
                Vector3 Point(int x,int y)
                {
                    float angle=x*2f*Mathf.PI/longitude;
                    float pitch=(y/(float)latitude-.5f)*Mathf.PI;
                    var sphere=P(Mathf.Cos(angle)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Sin(angle)*Mathf.Cos(pitch));
                    return center+rotation*Vector3.Scale(radii,P(SignedPower(sphere.x,roundness),SignedPower(sphere.y,roundness),SignedPower(sphere.z,roundness)));
                }
                for(int y=0;y<latitude;y++)for(int x=0;x<longitude;x++)
                {
                    var a=Point(x,y);var c=Point(x+1,y);var d=Point(x+1,y+1);var e=Point(x,y+1);
                    Vector3 normal=(a+c+d+e)*.25f-center;
                    if(y!=0)Face(slot,a,c,d,normal);
                    if(y!=latitude-1)Face(slot,a,d,e,normal);
                }
                float gradient=2f/roundness-1;
                for(int i=first;i<vertices.Count;i++)
                {
                    var v=inverse*(vertices[i]-center);
                    curvedNormals[i]=rotation*P(SignedPower(v.x/radii.x,gradient)/radii.x,
                        SignedPower(v.y/radii.y,gradient)/radii.y,SignedPower(v.z/radii.z,gradient)/radii.z).normalized;
                }
            }
            static float SignedPower(float value,float power)=>Mathf.Sign(value)*Mathf.Pow(Mathf.Abs(value),power);
        }
    }
}
