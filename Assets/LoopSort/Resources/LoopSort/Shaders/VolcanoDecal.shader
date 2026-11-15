Shader "LoopSort0/VolcanoDecal"
{
    Properties
    {
        _MainTex("Painted mechanism",2D)="white"{}
        _Color("Gameplay color",Color)=(1,1,1,1)
        _TintStrength("Gameplay tint",Range(0,1))=0
        _UVRect("Trimmed UV rectangle",Vector)=(0,0,1,1)
        [HideInInspector] _ZWrite("Depth write",Float)=1
    }
    SubShader
    {
        Tags {"Queue"="AlphaTest+10" "RenderType"="TransparentCutout"}
        Pass
        {
            Cull Off ZWrite [_ZWrite] Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _UVRect; float _TintStrength;
            UNITY_INSTANCING_BUFFER_START(Volcano)
                UNITY_DEFINE_INSTANCED_PROP(fixed4,_Color)
            UNITY_INSTANCING_BUFFER_END(Volcano)
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=_UVRect.xy+v.uv*_UVRect.zw;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);fixed4 c=tex2D(_MainTex,i.uv);clip(c.a-.82);
                fixed4 tint=UNITY_ACCESS_INSTANCED_PROP(Volcano,_Color);
                // Material flags keep basalt/iron neutral while keys, lids and
                // ore-type plates still communicate the live matching color.
                c.rgb*=lerp(fixed3(1,1,1),tint.rgb,_TintStrength);return c;
            }
            ENDCG
        }
    }
}
