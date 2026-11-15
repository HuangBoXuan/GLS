Shader "LoopSort0/ToyBackdrop"
{
    Properties
    {
        [PerRendererData] _MainTex("Backdrop",2D)="white"{}
        [HideInInspector] _ControlBand("Control belt placement",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Opaque" "CanUseSpriteAtlas"="True"}
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _ControlBand;
            struct input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            output vert(input i){output o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;return o;}
            fixed4 frag(output i):SV_Target
            {
                float2 uv=i.uv;
                float y=uv.y;
                // The lower and upper edges of the textured belt in candy-factory-board.
                const float sourceBottom=.037f,sourceTop=.106f;
                if(y<_ControlBand.w)
                {
                    if(y<_ControlBand.y)
                        uv.y=sourceBottom*saturate((y-_ControlBand.x)/(_ControlBand.y-_ControlBand.x));
                    else if(y<_ControlBand.z)
                        uv.y=lerp(sourceBottom,sourceTop,(y-_ControlBand.y)/(_ControlBand.z-_ControlBand.y));
                    else
                        uv.y=lerp(sourceTop,_ControlBand.w,(y-_ControlBand.z)/(_ControlBand.w-_ControlBand.z));
                }
                return tex2D(_MainTex,uv);
            }
            ENDCG
        }
    }
}
