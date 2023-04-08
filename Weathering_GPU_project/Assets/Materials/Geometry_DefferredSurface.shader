Shader "Custom/DeferredSurface"
{

    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
    }

        SubShader
        {

        Tags { "RenderType" = "Opaque" }

        CGINCLUDE

        #include "HLSLSupport.cginc"
        #include "UnityShaderVariables.cginc"

        #define UNITY_PASS_DEFERRED
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "UnityPBSLighting.cginc"

        sampler2D _MainTex;
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        struct Input
        {
            float2 uv_MainTex;
        };

        struct v2g
        {
            float4 pos         : SV_POSITION;
            float2 pack0       : TEXCOORD0;
        };

        struct g2f
        {
            float4 pos : SV_POSITION;
            float2 pack0       : TEXCOORD0;
            half3  worldNormal : TEXCOORD1;
            float3 worldPos    : TEXCOORD2;
        #ifndef DIRLIGHTMAP_OFF
            half3  viewDir     : TEXCOORD3;
        #endif
            float4 lmap        : TEXCOORD4;
            #ifdef LIGHTMAP_OFF
                #if UNITY_SHOULD_SAMPLE_SH
            half3 sh           : TEXCOORD5;
                #endif
        #else
            #ifdef DIRLIGHTMAP_OFF
            float4 lmapFadePos : TEXCOORD5;
            #endif
        #endif
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }

        float4 _MainTex_ST;

        v2g vert(appdata_full v)
        {
            v2g o;
            UNITY_INITIALIZE_OUTPUT(v2g, o);
            o.pos = v.vertex;
            o.pack0.xy = TRANSFORM_TEX(v.texcoord, _MainTex);

            return o;
        }

        [maxvertexcount(3)]
        void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream)
        {
            g2f o;

            //法線ベクトルの計算(ライティングで使用)
            float3 vecA = IN[1].pos - IN[0].pos;
            float3 vecB = IN[2].pos - IN[0].pos;
            float3 normal = cross(vecA, vecB);
            o.worldNormal = normalize(mul(normal, (float3x3) unity_WorldToObject));
            
            //UVの計算
            o.pack0 = (IN[0].pack0 + IN[1].pack0 + IN[2].pack0) / 3;
            float4 center = (IN[0].pos + IN[1].pos + IN[2].pos) / 3;

            //メッシュ作成 (法線，UV以外はジオメトリシェーダーでは加工せずにそのまま流す)
            for (int i = 0; i < 3; i++)
            {
                o.pos = UnityObjectToClipPos(IN[i].pos + float4(0, 1, 0, 0));
                o.worldPos = mul(unity_ObjectToWorld, o.pos).xyz;
                //o.worldNormal = UnityObjectToWorldNormal(v.normal);
                //o.worldNormal = float3(0, 1, 0);
                
#ifndef DIRLIGHTMAP_OFF
                o.viewDir = UnityWorldSpaceViewDir(o.worldPos);
#endif

#ifndef DYNAMICLIGHTMAP_OFF
                o.lmap.zw = IN[i].worldPos.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#else
                o.lmap.zw = 0;
#endif

#ifndef LIGHTMAP_OFF
                o.lmap.xy = IN[i].worldNormal.xy * unity_LightmapST.xy + unity_LightmapST.zw;
#ifdef DIRLIGHTMAP_OFF
                o.lmapFadePos.xyz = (mul(unity_ObjectToWorld, o.worldPos).xyz - unity_ShadowFadeCenterAndType.xyz) * unity_ShadowFadeCenterAndType.w;
                o.lmapFadePos.w = (-UnityObjectToViewPos(o.worldPos).z) * (1.0 - unity_ShadowFadeCenterAndType.w);
#endif
#else
                o.lmap.xy = 0;
#if UNITY_SHOULD_SAMPLE_SH
                o.sh = 0;
                o.sh = ShadeSHPerVertex(o.worldNormal, o.sh);
#endif
#endif
                triStream.Append(o);
            }
            //tristream.RestartStrip();//さらに他の三角メッシュを作成する時に必要
        }
        half4 frag_test(g2f IN) : COLOR
        {
            float4 col = tex2D(_MainTex, IN.pack0);
            col.rgb *= _Color * 0;
            return col;
        }
        void frag(g2f IN,
            out half4 outDiffuse        : SV_Target0,
            out half4 outSpecSmoothness : SV_Target1,
            out half4 outNormal : SV_Target2,
            out half4 outEmission : SV_Target3)
        {
            Input surfIN;
            UNITY_INITIALIZE_OUTPUT(Input, surfIN);
            surfIN.uv_MainTex = IN.pack0.xy;

            float3 worldPos = IN.worldPos;
            fixed3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));

            SurfaceOutputStandard o;
            UNITY_INITIALIZE_OUTPUT(SurfaceOutputStandard, o);
            o.Albedo = 0.0;
            o.Emission = 0.0;
            o.Alpha = 0.0;
            o.Occlusion = 1.0;
            o.Normal = IN.worldNormal;

            surf(surfIN, o);

            UnityGI gi;
            UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
            gi.indirect.diffuse = 0;
            gi.indirect.specular = 0;
            gi.light.color = 0;
            gi.light.dir = half3(0, 1, 0);
            gi.light.ndotl = LambertTerm(o.Normal, gi.light.dir);

            UnityGIInput giInput;
            UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
            giInput.light = gi.light;
            giInput.worldPos = worldPos;
            giInput.worldViewDir = worldViewDir;
            giInput.atten = 1;

            #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
            giInput.lightmapUV = IN.lmap;
            #else
            giInput.lightmapUV = 0.0;
            #endif

            #if UNITY_SHOULD_SAMPLE_SH
            giInput.ambient = IN.sh;
            #else
            giInput.ambient.rgb = 0.0;
            #endif

            giInput.probeHDR[0] = unity_SpecCube0_HDR;
            giInput.probeHDR[1] = unity_SpecCube1_HDR;

            #if UNITY_SPECCUBE_BLENDING || UNITY_SPECCUBE_BOX_PROJECTION
            giInput.boxMin[0] = unity_SpecCube0_BoxMin; // .w holds lerp value for blending
            #endif

            #if UNITY_SPECCUBE_BOX_PROJECTION
            giInput.boxMax[0] = unity_SpecCube0_BoxMax;
            giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
            giInput.boxMax[1] = unity_SpecCube1_BoxMax;
            giInput.boxMin[1] = unity_SpecCube1_BoxMin;
            giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
            #endif

            LightingStandard_GI(o, giInput, gi);

            outEmission = LightingStandard_Deferred(o, worldViewDir, gi, outDiffuse, outSpecSmoothness, outNormal);
            #ifndef UNITY_HDR_ON
            outEmission.rgb = exp2(-outEmission.rgb);
            #endif

            UNITY_OPAQUE_ALPHA(outDiffuse.a);
        }

        ENDCG

        Pass
        {

        Tags { "LightMode" = "Deferred" }

        CGPROGRAM
        #pragma vertex vert//vert_surf
        #pragma geometry geom
        #pragma fragment frag//frag_surf
        #pragma target 3.0
        #pragma exclude_renderers nomrt
        #pragma multi_compile_prepassfinal
        #pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
        ENDCG

        }

        }

        FallBack "Diffuse"

}