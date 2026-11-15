Shader "LoopSort0/CargoOutline"
{
    Properties { _Color("Outline",Color)=(0,0,0,1) _Width("Width",Float)=0.055 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Cull Front ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Width;fixed4 _Color;
            struct appdata { float4 vertex:POSITION;float3 normal:NORMAL; };
            struct v2f { float4 position:SV_POSITION; };
            v2f vert(appdata v)
            {
                v2f o;float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
                world+=normalize(UnityObjectToWorldNormal(v.normal))*_Width;
                o.position=mul(UNITY_MATRIX_VP,float4(world,1));return o;
            }
            fixed4 frag(v2f i):SV_Target { return _Color; }
            ENDCG
        }
    }
}
