// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Geometry/FlatShading"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}
    }

        SubShader
    {

        Tags{ "Queue" = "Geometry" "RenderType" = "Opaque" "LightMode" = "ForwardBase" }

        Pass
        {
            CGPROGRAM

            #include "UnityCG.cginc"
            #pragma vertex vert
            //Geometry Shader ステージのときに呼び出される
            #pragma geometry geom
            #pragma fragment frag

            float4 _Color;
            sampler2D _MainTex;

            struct v2g
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 vertex : TEXCOORD1;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float light : TEXCOORD1;
            };

            v2g vert(appdata_full v)
            {
                v2g o;
                o.vertex = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream)
            {
                g2f o;

                //法線ベクトルの計算(ライティングで使用)
                float3 vecA = IN[1].vertex - IN[0].vertex;
                float3 vecB = IN[2].vertex - IN[0].vertex;
                float3 local_normal = normalize(cross(vecA, vecB));
                float3 normal = normalize(mul(local_normal, (float3x3) unity_WorldToObject));

                //ライティングの計算
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                o.light = max(0., dot(normal, lightDir));

                o.uv = (IN[0].uv + IN[1].uv + IN[2].uv) / 3;
                float3 center = (IN[0].vertex + IN[1].vertex + IN[2].vertex) / 3;
                //メッシュ作成
                for (int i = 0; i < 3; i++)
                {
                    o.pos = IN[i].pos;
                    o.pos.xyz = UnityObjectToClipPos((IN[i].vertex - center) * 1.0 + center + local_normal);
                    triStream.Append(o);
                }
                //tristream.RestartStrip();//さらに他の三角メッシュを作成する時に必要
            }

            half4 frag(g2f i) : COLOR
            {
                float4 col = tex2D(_MainTex, i.uv);
                col.rgb *= i.light * _Color;
                return col;
            }

            ENDCG
        }
    }
        Fallback "Diffuse"
}