Shader "LoopSort0/IllustratedLid"
{
    Properties
    {
        _MainTex("Authored guide paint",2D)="white"{}
        _Color("Color",Color)=(1,1,1,1)
        _EmblemColor("Crystal color",Color)=(1,1,1,1)
        _TintEmblem("Tint lid crystals",Float)=0
        _Cutoff("Cutout",Range(0,1))=.4
    }
    SubShader
    {
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        Pass
        {
            Name "IllustratedPaint"
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;float _Cutoff,_TintEmblem;fixed4 _EmblemColor;
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
                if(_TintEmblem>.5)
                {
                    // Only the pale crystal artwork in the lid's central panel
                    // receives the unlocking color. Preserve its painted facets.
                    float lo=min(paint.r,min(paint.g,paint.b));
                    float hi=max(paint.r,max(paint.g,paint.b));
                    float panel=step(.26,i.uv.x)*step(i.uv.x,.75)*step(.30,i.uv.y)*step(i.uv.y,.75);
                    float mask=panel*smoothstep(.16,.22,lo)*smoothstep(.55,.68,lo/max(hi,.001));
                    paint.rgb=lerp(paint.rgb,paint.rgb*(_EmblemColor.rgb*.92+.08),mask);
                }
                // Lighting is painted into the reference. A second Lambert pass
                // would wash out the purple stones and turn orange seams yellow.
                return fixed4(paint.rgb*i.color.rgb,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
