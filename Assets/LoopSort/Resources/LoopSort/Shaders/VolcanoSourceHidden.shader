Shader "LoopSort0/VolcanoSourceHidden"
{
    SubShader { Tags {"Queue"="Transparent"} Pass { ZWrite Off ColorMask 0 Cull Off
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        float4 vert(float4 p:POSITION):SV_POSITION{return UnityObjectToClipPos(p);}
        fixed4 frag():SV_Target{clip(-1);return 0;}
        ENDCG
    }}
}
