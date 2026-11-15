Shader "LoopSort0/VolcanoSurface"
{
    Properties
    {
        _Color("Gameplay color",Color)=(1,1,1,1)
        _BaseColor("Mining surface",Color)=(.35,.25,.37,1)
        _TintStrength("Gameplay tint",Range(0,1))=0
        _Emission("Lava brightness",Range(0,1))=0
        _Fabric("Woven fabric",Range(0,1))=0
        _CandyStripe("Factory safety stripes",Float)=0
        _Polish("Surface polish",Range(0,1))=0
        _Metal("Metal finish",Range(0,1))=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _BaseColor;float _TintStrength,_Emission,_Fabric,_CandyStripe,_Polish,_Metal;
            UNITY_INSTANCING_BUFFER_START(Mine)
                UNITY_DEFINE_INSTANCED_PROP(fixed4,_Color)
            UNITY_INSTANCING_BUFFER_END(Mine)
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 position:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;float3 local:TEXCOORD2;float3 world:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);
                o.position=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.uv=v.uv;o.local=v.vertex.xyz;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n=normalize(i.normal);
                float3 color=lerp(_BaseColor.rgb,UNITY_ACCESS_INSTANCED_PROP(Mine,_Color).rgb,_TintStrength);
                float3 lightDir=normalize(float3(-.43,1,-.45));
                float lighting=.66+.34*saturate(dot(n,lightDir));
                float gloss=pow(saturate(dot(n,normalize(lightDir+float3(0,.92,-.39)))),19);
                if(_Fabric>.5)
                {
                    float2 tiles=(i.uv+float2(.49,2.22))*float2(6.14,.90);
                    float checker=frac(dot(floor(tiles),float2(.5,.5)))*2;
                    color=lerp(color,float3(.88,.88,.98),checker);
                    float2 edge=min(frac(tiles),1-frac(tiles));
                    color*=lerp(.92,1,smoothstep(0,.09,min(edge.x,edge.y)));
                    gloss*=.35;
                }
                if(_CandyStripe>.5)
                {
                    float along=_CandyStripe>1.5?dot(i.local.xy,float2(12,8)):
                        i.uv.y*1.4+i.uv.x*2.3-i.local.z*1.3+i.local.y*.5;
                    float stripe=smoothstep(.44,.49,frac(along));
                    color=lerp(color,float3(1,.69,.14),stripe);
                }
                if(_Polish<=.001||_Emission>.98)
                {
                    color*=lerp(lighting,1,_Emission);
                    return fixed4(lerp(color,float3(.98,.91,1),gloss*.24*(1-_Emission)),1);
                }
                float3 view=normalize(lerp(_WorldSpaceCameraPos.xyz-i.world,UNITY_MATRIX_V[2].xyz,unity_OrthoParams.w));
                float key=saturate(dot(n,lightDir));
                float fill=saturate(dot(n,normalize(float3(.62,.36,.70))));
                float sheen=saturate(dot(n,normalize(view+lightDir)));
                float bounce=saturate(dot(n,normalize(view+float3(.77,.65,.45))));
                float fresnel=pow(1-saturate(dot(n,view)),3);
                float3 base=color*(.43+.54*key+.13*fill);
                float3 tint=lerp(float3(.88,.94,1),lerp(sqrt(saturate(color)),float3(.93,.87,1),.32),_Metal);
                float soft=pow(sheen,10)*.25;
                float glint=pow(sheen,58)*.42+pow(bounce,30)*.11;
                base+=tint*(soft+glint+fresnel*(.06+.13*fill))*_Polish*(1-_Emission);
                return fixed4(lerp(base,color,_Emission),1);
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    Fallback "Diffuse"
}
