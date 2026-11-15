Shader "LoopSort0/ToySurface"
{
    Properties
    {
        _Color("Toy color",Color)=(1,1,1,1)
        _Pattern("Surface pattern",Float)=0
        _Opacity("Opacity",Range(0,1))=1
        [HideInInspector] _RouteClip("Conveyor reveal",Float)=1
        _EdgeColor("Carpet border",Color)=(1,.78,.12,1)
        _EdgeWidth("Carpet border width",Range(0,.2))=0
        [HideInInspector] _SrcBlend("Source blend",Float)=1
        [HideInInspector] _DstBlend("Destination blend",Float)=0
        [HideInInspector] _ZWrite("Depth write",Float)=1
        _MainTex("Unused compatibility texture",2D)="white"{}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            float _Pattern,_Opacity,_EdgeWidth,_RouteClip;float4 _MainTex_ST;fixed4 _EdgeColor;
            UNITY_INSTANCING_BUFFER_START(Toy)
                UNITY_DEFINE_INSTANCED_PROP(fixed4,_Color)
            UNITY_INSTANCING_BUFFER_END(Toy)
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float2 route:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;float3 world:TEXCOORD2;float2 localUV:TEXCOORD3;float route:TEXCOORD4;UNITY_VERTEX_INPUT_INSTANCE_ID};
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);
                o.vertex=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.localUV=v.uv;o.route=v.route.x;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);float3 n=normalize(i.normal);
                if(_RouteClip<1)clip(_RouteClip-i.route);
                float3 color=UNITY_ACCESS_INSTANCED_PROP(Toy,_Color).rgb;
                float2 uv=i.uv;float pattern=0;
                if(_Pattern>.5&&_Pattern<1.5)pattern=step(.67,frac((uv.x+uv.y)*18))*.22;
                if(_Pattern>1.5&&_Pattern<2.5)pattern=(step(.91,frac(uv.x*12))+step(.91,frac(uv.y*10)))*.13;
                if(_Pattern>2.5&&_Pattern<3.5)pattern=step(.82,frac(uv.y*14))*.3;
                if(_Pattern>3.5&&_Pattern<4.5){float stripe=step(.5,frac((uv.x+uv.y)*5));color=lerp(color,float3(.29,.15,.24),stripe);}
                if(_Pattern>4.5&&_Pattern<5.5)pattern=step(.8,frac(uv.y*18))*.25;
                if(_Pattern>5.5&&_Pattern<6.5)pattern=step(.93,frac(uv.x*7+sin(uv.y*18)*.2))*.2;
                if(_Pattern>6.5){color*=lerp(1.08,.98,uv.x);pattern=step(.5,frac(uv.y*.36))*.09;}
                color*=1-pattern;
                if(_EdgeWidth>0)
                {
                    // Unscaled UV keeps a continuous gold hem as the carpet
                    // spans several trucks, rather than repeating gold seams.
                    float edge=min(i.localUV.y,1-i.localUV.y);
                    float hem=1-smoothstep(_EdgeWidth-.008,_EdgeWidth+.008,edge);
                    color=lerp(color,_EdgeColor.rgb,hem);
                }
                float lighting=.70+.30*saturate(dot(n,normalize(float3(-.45,1,-.45))));
                // Keep the belt's mint panels saturated; a broad white highlight
                // makes the entire flat lane look washed out in the top-down view.
                float highlight=_Pattern>6.5 ? .018 : .07;
                color=lerp(color*lighting,float3(1,.96,.86),pow(saturate(n.y),8)*highlight);
                return fixed4(color,_Opacity);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
