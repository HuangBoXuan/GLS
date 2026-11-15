Shader "LoopSort0/InstancedCargo"
{
    Properties
    {
        _Color("Color",Color)=(1,1,1,1)
        _MainTex("Albedo",2D)="white"{}
        _Glossiness("Smoothness",Range(0,1))=0.55
        _Metallic("Metallic",Range(0,1))=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            half _Glossiness,_Metallic;
            UNITY_INSTANCING_BUFFER_START(Cargo)
                UNITY_DEFINE_INSTANCED_PROP(fixed4,_Color)
            UNITY_INSTANCING_BUFFER_END(Cargo)
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float3 worldPosition:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);
                o.vertex=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.worldPosition=mul(unity_ObjectToWorld,v.vertex).xyz;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);float3 n=normalize(i.normal);
                float3 color=UNITY_ACCESS_INSTANCED_PROP(Cargo,_Color).rgb;
                // Preserve the original warm tops and saturated side colors.
                float light=.62+.38*saturate(dot(n,normalize(float3(-.45,1,-.4))));
                color=lerp(color*light,float3(1,.96,.86),pow(saturate(n.y),8)*.065);
                // One shared, instanced pass: the existing rounded mesh supplies
                // the reflection edge, without lights, extra geometry or textures.
                float3 view=normalize(_WorldSpaceCameraPos.xyz-i.worldPosition);
                float3 halfway=normalize(view+normalize(float3(-.28,.48,-.85)));
                float gloss=saturate(_Glossiness);
                float reflection=pow(saturate(dot(n,halfway)),lerp(20,64,gloss));
                float2 front=view.xz*rsqrt(max(dot(view.xz,view.xz),.0001));
                float2 side=n.xz*rsqrt(max(dot(n.xz,n.xz),.0001));
                float facing=saturate(dot(front,side));
                // Soft horizontal glint on the camera-facing rounded shoulder.
                // Flat top/side faces retain their original palette and shading.
                float shoulder=smoothstep(.035,.22,n.y)*(1-smoothstep(.52,.82,n.y));
                float edge=shoulder*pow(facing,8);
                float highlight=saturate(reflection*(.23+.22*gloss)+edge*.26);
                float3 sheen=lerp(float3(1,1,.98),color,saturate(_Metallic));
                color=lerp(color,sheen,highlight);
                return fixed4(color,1);
            }
            ENDCG
        }
    }
    FallBack Off
}
