Shader "Hidden/LoopSort/CandyModalBlur"
{
    Properties { _MainTex ("Scene", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 frag(v2f_img i) : SV_Target
            {
                // A 5 by 5 bilinear Gaussian, evaluated on a small snapshot once.
                const float offsets[5] = {-4.15,-1.55,0,1.55,4.15};
                const float weights[5] = {.07027,.31622,.22702,.31622,.07027};
                half3 color = 0;
                [unroll] for(int y=0;y<5;y++)
                    [unroll] for(int x=0;x<5;x++)
                    {
                        float2 offset=float2(offsets[x],offsets[y])*_MainTex_TexelSize.xy;
                        color += tex2D(_MainTex,i.uv+offset).rgb*weights[x]*weights[y];
                    }
                return fixed4(color*.72,1);
            }
            ENDCG
        }
    }
}
