Shader "LoopSort/Source Particle"
{
    Properties
    {
        _MainTex("Particle",2D)="white"{}
        _Color("Color",Color)=(1,1,1,1)
        _SrcBlend("Source Blend",Float)=5
        _DstBlend("Destination Blend",Float)=10
        _Cull("Cull",Float)=0
        _ZWrite("Depth Write",Float)=0
        _Soft("Soft Particles",Float)=0
        _SoftNear("Soft Near",Float)=0
        _SoftRange("Soft Range",Float)=1
        _Metallic("Metallic",Range(0,1))=0
        _Glossiness("Smoothness",Range(0,1))=0
    }
    SubShader
    {
        Tags{"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]
        ZWrite [_ZWrite]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST,_Color;
            float _Soft,_SoftNear,_SoftRange;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 projected:TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);
                o.projected=ComputeScreenPos(o.pos);COMPUTE_EYEDEPTH(o.projected.z);
                o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.color=v.color*_Color;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 color=tex2D(_MainTex,i.uv)*i.color;
                if(_Soft>.5)
                {
                    float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.projected)));
                    color.a*=saturate((scene-i.projected.z-_SoftNear)/max(.0001,_SoftRange));
                }
                return color;
            }
            ENDCG
        }
    }
}
