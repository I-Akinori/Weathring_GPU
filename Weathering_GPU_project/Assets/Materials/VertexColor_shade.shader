Shader "custom/VertexColor_shade"
{
    SubShader
    {
      Pass
      {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"

        struct appdata
        {
          float4 vertex : POSITION;
          float3 normal: NORMAL;
          float4 color: TEXCOORD1;
        };

        struct v2f
        {
          float4 vertex : SV_POSITION;
          float3 worldNormal: TEXCOORD0;
          float4 color: TEXCOORD1;
        };

        void vert(in appdata v, out v2f o)
        {
          o.vertex = UnityObjectToClipPos(v.vertex);
          o.worldNormal = UnityObjectToWorldNormal(v.normal);
          o.color = v.color;
        }

        void frag(in v2f i, out float4 col : SV_Target)
        {
            float shade = dot(i.worldNormal, normalize(float3(-1, 1, -1))) * 0.5 + 0.5;
            col = i.color * shade;
        }
        ENDCG
      }
    }
}