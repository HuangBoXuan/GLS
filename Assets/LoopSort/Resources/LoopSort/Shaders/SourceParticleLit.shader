Shader "LoopSort/Source Particle Lit"
{
    Properties
    {
        _MainTex("Particle",2D)="white"{}
        _Color("Color",Color)=(1,1,1,1)
        _SrcBlend("Source Blend",Float)=1
        _DstBlend("Destination Blend",Float)=0
        _Cull("Cull",Float)=2
        _ZWrite("Depth Write",Float)=1
        _Metallic("Metallic",Range(0,1))=0
        _Glossiness("Smoothness",Range(0,1))=.4
        _Soft("Soft Particles",Float)=0
        _SoftNear("Soft Near",Float)=0
        _SoftRange("Soft Range",Float)=1
    }
    SubShader
    {
        Tags{"RenderType"="Opaque" "Queue"="Geometry"}
        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]
        ZWrite [_ZWrite]
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;fixed4 _Color;half _Metallic,_Glossiness;
        struct Input {float2 uv_MainTex;float4 particleColor;};
        void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.particleColor=v.color;}
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            fixed4 color=tex2D(_MainTex,i.uv_MainTex)*_Color*i.particleColor;
            o.Albedo=color.rgb;o.Alpha=color.a;o.Metallic=_Metallic;o.Smoothness=_Glossiness;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
