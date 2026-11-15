Shader "LoopSort0/HiddenCargo"
{
    Properties
    {
        _Color("Mystery cover",Color)=(0.149,0.325,0.784,1)
        _QuestionColor("Question mark",Color)=(1,0.792,0.639,1)
        _FlowTime("Flow time",Float)=0
        _Reveal("Cover dissolve",Range(0,1))=0
        _CargoColor("Revealed cargo",Color)=(1,1,1,1)
        _MainTex("Ore facets",2D)="white"{}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color,_QuestionColor,_CargoColor;float _FlowTime,_Reveal;
            struct appdata { float4 vertex:POSITION;float3 normal:NORMAL; };
            struct v2f { float4 position:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float4 face:TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal);o.face=float4(v.vertex.xyz,v.normal.y);return o;
            }
            float hash(float2 p)
            {
                // Avoid amplifying platform-dependent sin() precision before frac().
                float3 cell=frac(p.xyx*.1031);
                cell+=dot(cell,cell.yzx+19.19);
                return frac((cell.x+cell.y)*cell.z);
            }
            float noise3(float3 p)
            {
                float3 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                float2 a=cell.xy+cell.z*float2(37,113),b=a+float2(37,113);
                float lo=lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);
                float hi=lerp(lerp(hash(b),hash(b+float2(1,0)),f.x),lerp(hash(b+float2(0,1)),hash(b+1),f.x),f.y);
                return lerp(lo,hi,f.z);
            }
            float segment(float2 p,float2 a,float2 b)
            {
                float2 delta=b-a;return length(p-a-delta*saturate(dot(p-a,delta)/dot(delta,delta)));
            }
            float mark(float2 q)
            {
                float hook=max(abs(length(q-float2(0,.11))-.142)-.046,.11-q.y);
                hook=min(hook,length(q-float2(-.142,.11))-.046);
                float bend=segment(q,float2(.142,.11),float2(0,-.086))-.046;
                float stem=segment(q,float2(0,-.086),float2(0,-.13))-.046;
                float dotMark=length(q-float2(0,-.246))-.052;
                float distance=min(min(hook,bend),min(stem,dotMark));
                float aa=max(fwidth(distance)*.5,.003);
                return 1-smoothstep(-aa,aa,distance);
            }
            float crack(float2 p)
            {
                float2 start=float2(.50,.11),elbow=float2(.28,.19),stepUp=float2(.29,.30),tip=float2(.17,.48);
                return min(min(segment(p,start,elbow),segment(p,elbow,stepUp)),segment(p,stepUp,tip));
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal);
                float light=.72+.28*saturate(dot(n,normalize(float3(-.4,1,-.3))));
                // Draw flat decoration on the top face in mesh space. The side
                // faces and rounded edges pick up depth from the mesh normals.
                float top=smoothstep(.76,.98,i.face.w);
                float2 face=i.face.xz/float2(1.503,1.002);
                float facets=tex2D(_MainTex,face+.5).r;
                float rim=smoothstep(.32,.49,max(abs(face.x),abs(face.y)));
                float3 cover=_Color.rgb*lerp(.69,lerp(.87,1.15,facets),top);
                cover=lerp(cover,cover*.55,rim*top*.57);
                cover+=float3(.07,.13,.28)*top*saturate((face.y-face.x)*1.20+.17);
                float seam=min(crack(face),crack(-face));
                float groove=1-smoothstep(.022,.046,seam);
                float seamMask=1-smoothstep(.013,.027,seam);
                float pulse=.9+.1*sin(_FlowTime*2+face.y*5);
                cover=lerp(cover,float3(.16,.08,.29),groove*top);
                cover=lerp(cover,float3(.98,.17,.46),seamMask*top*pulse);
                // Corner guards live on the actual cube face. The beveled
                // shoulders and rivets share the top face's local coordinates.
                float2 corner=abs(face);
                float clip=min(min(corner.x-.277,corner.y-.255),corner.x+corner.y-.666);
                float guard=smoothstep(-.007,.007,clip)*top;
                float guardBevel=smoothstep(.001,.031,clip);
                float rivet=length((corner-float2(.371,.364))*float2(1,1.12));
                float3 steel=lerp(float3(.68,.62,.80),float3(.32,.27,.50),guardBevel);
                steel=lerp(steel,float3(.85,.76,.87),1-smoothstep(.022,.038,rivet));
                cover=lerp(cover,steel,guard);
                float2 glyph=i.face.xz/max(abs(i.face.y)*1.78,.63);
                cover=lerp(cover,float3(.34,.15,.27),mark(glyph+float2(-.023,.026))*top);
                cover=lerp(cover,_QuestionColor.rgb,mark(glyph)*top)*light;
                // Use the same camera-facing shoulder glint as the other cargo.
                float3 view=normalize(_WorldSpaceCameraPos.xyz-i.world);
                float2 front=view.xz*rsqrt(max(dot(view.xz,view.xz),.0001));
                float facing=saturate(dot(front,n.xz));
                float shoulder=smoothstep(.035,.22,n.y)*(1-smoothstep(.52,.82,n.y));
                cover=lerp(cover,float3(.86,.9,1),shoulder*pow(facing,8)*.26);
                if(_Reveal<=0)return fixed4(cover,1);
                // Erase the cover in irregular patches while keeping the solid
                // cargo underneath opaque. No halfway material swap or flip.
                float erosion=noise3(i.world*5.5)*.7+noise3(i.world*11.2+31)*.3;
                float threshold=lerp(-.1,1.1,saturate(_Reveal));
                float exposed=smoothstep(erosion-.035,erosion+.035,threshold);
                float edge=1-smoothstep(0,.055,abs(erosion-threshold));
                float cargoLight=.62+.38*saturate(dot(n,normalize(float3(-.45,1,-.4))));
                float3 cargo=lerp(_CargoColor.rgb*cargoLight,float3(1,.96,.86),pow(saturate(n.y),8)*.065);
                float3 surface=lerp(cover,cargo,exposed);
                return fixed4(lerp(surface,_QuestionColor.rgb*1.15,edge*.65),1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
