Shader "LoopSort0/IllustratedRock"
{
    Properties
    {
        _MainTex("Authored guide paint",2D)="white"{}
        _Color("Color",Color)=(1,1,1,1)
        _Cutoff("Cutout",Range(0,1))=.4
    }
    SubShader
    {
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;float _Cutoff;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 position:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);v2f o;
                o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 paint=tex2D(_MainTex,i.uv);
                clip(paint.a-_Cutoff);
                // Lighting is painted into the reference. A second Lambert pass
                // would wash out the purple stones and turn orange seams yellow.
                return fixed4(paint.rgb*i.color.rgb,1);
            }
            ENDCG
        }
        UsePass "Legacy Shaders/Transparent/Cutout/VertexLit/SHADOWCASTER"
    }
    Fallback "Transparent/Cutout/VertexLit"
}
