Shader "LoopSort0/SigninCard"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture",2D)="white"{}
        _Body("Card body",Color)=(.973,.965,1,1)
        _Border("Outline",Color)=(.82,.79,.91,1)
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            fixed4 _Body,_Border;
            float4 _ClipRect;
            struct appdata {float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float2 local:TEXCOORD1;};
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.color=v.color;
                o.uv=v.uv;
                o.local=v.vertex.xy;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                // Derive pixel extents from the quad so wide and regular cards
                // keep round corners and the header shares the same contour.
                float2 size=1/max(fwidth(i.uv),.0001);
                float radius=min(size.x,size.y)*.13;
                float2 corner=abs((i.uv-.5)*size)-(size*.5-.5-radius);
                float edge=length(max(corner,0))+min(max(corner.x,corner.y),0)-radius;
                float band=smoothstep(.69-fwidth(i.uv.y)*.5,.69+fwidth(i.uv.y)*.5,i.uv.y);
                fixed3 face=lerp(_Body.rgb,i.color.rgb,band);
                fixed3 border=lerp(_Border.rgb,i.color.rgb,band);
                fixed4 color=fixed4(lerp(border,face,1-smoothstep(-.5,.5,edge+1)),i.color.a*(1-smoothstep(-.5,.5,edge)));
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(i.local,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a-.001);
                #endif
                color.rgb*=color.a;
                return color;
            }
            ENDCG
        }
    }
}
