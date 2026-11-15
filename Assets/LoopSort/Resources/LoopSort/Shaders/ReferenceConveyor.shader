Shader "LoopSort0/ReferenceConveyor"
{
    Properties
    {
        _Color("Arrow colour",Color)=(1,1,1,1)
        _UseVertexColor("Use cross-section colour bands",Float)=1
        [HideInInspector] _RouteClip("Conveyor reveal",Float)=1
        [HideInInspector] _ArrowTravel("Arrow travel",Float)=0
        [HideInInspector] _ArrowPeriod("Arrow spacing",Float)=0
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;float _UseVertexColor,_RouteClip,_ArrowTravel,_ArrowPeriod;
            struct appdata {float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float2 route:TEXCOORD1;};
            struct v2f {float4 position:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float route:TEXCOORD1;};
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;o.route=v.route.x;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                if(_RouteClip<1)clip(_RouteClip-i.route);
                float3 color=i.color.rgb;
                // Discrete, broad panels on the lane only; rims remain solid.
                float seam=frac(i.uv.y/1.12);
                float panel=step(.5,frac(i.uv.y/2.24));
                float stripe=lerp(1.09,.94,panel)*lerp(.77,1,smoothstep(.01,.06,seam));
                color*=lerp(1,stripe,i.color.a*_UseVertexColor);
                // Arc-length UVs carry the arrows continuously around bends. The
                // lane mask keeps them off the rims, without another mesh/draw call.
                float period=max(.001,_ArrowPeriod);
                float along=frac((i.uv.y-_ArrowTravel)/period)*period;
                float across=abs(i.uv.x-.5)*1.39;
                float edge=min(min(along,.45-along),(.22*(1-along/.45)-across));
                float arrow=smoothstep(-.012,.012,edge)*i.color.a*_UseVertexColor*step(.01,_ArrowPeriod);
                color=lerp(color,float3(.62,.81,.98),arrow*.72);
                // Mesh colours store the reference image's sRGB values. Avoid
                // washing them out when the player uses linear rendering.
                #ifndef UNITY_COLORSPACE_GAMMA
                color=GammaToLinearSpace(color);
                #endif
                color=lerp(_Color.rgb,color,_UseVertexColor);
                return fixed4(color,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
