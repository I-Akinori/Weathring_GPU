Shader "Custom/ThickPaint"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_BackTex("Back Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
		_BackGlossiness("Smoothness", Range(0,1)) = 0.5
		_BackMetallic("Metallic", Range(0,1)) = 0.0
		_Thick("Thick", Range(0.005, 0.1)) = 0.01
	}
		SubShader
		{
			
			Pass
	{

	Name "FORWARD"
	Tags { "LightMode" = "ForwardBase" }

	CGPROGRAM
	#pragma vertex vert
	#pragma geometry geom
	#pragma fragment frag
	#pragma target 3.0
	#pragma multi_compile_instancing
	#pragma multi_compile_fog
	#pragma multi_compile_fwdbase
	#include "HLSLSupport.cginc"
	#include "UnityShaderVariables.cginc"
	#include "UnityShaderUtilities.cginc"

	#define UNITY_PASS_FORWARDBASE
	#include "UnityCG.cginc"
	#include "Lighting.cginc"
	#include "UnityPBSLighting.cginc"
	#include "AutoLight.cginc"

	sampler2D _MainTex;
	float4 _MainTex_ST;
	sampler2D _BackTex;
	float4 _BackTex_ST;
	half _Glossiness;
	half _Metallic; 
	half _BackGlossiness;
	half _BackMetallic;
	half _Thick;
	fixed4 _Color;

	struct Input
	{
		float2 uv_MainTex;
		float4 vertColor;
	};

	void surf(Input IN, inout SurfaceOutputStandard o)
	{

		fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
		fixed g;
		fixed m;

		if (IN.uv_MainTex.x < 0) {
			c = tex2D(_BackTex, -IN.uv_MainTex) * _Color;
			m = _BackMetallic;
			g = _BackGlossiness;
		}

		fixed rust = IN.vertColor.r;
		fixed dust = IN.vertColor.g;
		fixed chalk = IN.vertColor.b;

		fixed4 rc0 = fixed4(255.0 / 255.0, 255.0 / 255.0, 1.0, 1.0);
		fixed4 rc1 = fixed4(191.0 / 255.0, 95.0 / 255.0, 0.0, 1.0);
		fixed4 rust_mask = rc0 * (1 - rust) + rc1 * rust;

		rc0 = fixed4(191.0 / 255.0, 95.0 / 255.0, 0.0, 1.0);
		rc1 = fixed4(100.0 / 255.0, 50.0 / 255.0, 0.0, 1.0);
		fixed4 rust_col = rc0 * (1 - rust) + rc1 * rust;

		//c = c * (1 - rust) + rc0 * rust;
		c = c * rust_mask;
		c = c * (1 - rust * 0.2) + rust_col * rust * 0.2;
		c = c * (1 - dust);
		g = _Glossiness * (1 - rust * 0.2) * (1 - dust) * (1 - chalk);
		m = _Metallic * (1 - rust * 0.2) * (1 - dust) * (1 - chalk);

		o.Metallic = m;
		o.Smoothness = g;
		o.Albedo = c.rgb;
		o.Alpha = c.a;
	}

	struct v2g
	{
		float4 pos         : SV_POSITION;
		float3 nor		   : NORMAL;
		float2 texcoord    : TEXCOORD0;
		float4 vertColor	: TEXCOORD1;
		float2 texcoord2	: TEXCOORD2;
		float2 texcoord3	: TEXCOORD3;
	};

	v2g vert(appdata_full v) {
		v2g o;
		UNITY_INITIALIZE_OUTPUT(v2g, o);
		o.pos = v.vertex;
		o.nor = v.normal;
		o.texcoord = v.texcoord;
		o.vertColor = v.texcoord1;
		o.texcoord2 = v.texcoord2;
		o.texcoord3 = v.texcoord3;

		return o;
	}

	struct g2f
	{
		UNITY_POSITION(pos);
		float2 pack0 : TEXCOORD0;
		float4 vertColor	: TEXCOORD1;
		float3 worldNormal : TEXCOORD2;
		float3 worldPos : TEXCOORD3;
		float4 lmap : TEXCOORD4;
		UNITY_SHADOW_COORDS(5)
		UNITY_FOG_COORDS(6)
	#ifndef LIGHTMAP_ON
		#if UNITY_SHOULD_SAMPLE_SH
		half3 sh : TEXCOORD7;
		#endif
	#endif
		UNITY_VERTEX_INPUT_INSTANCE_ID
		UNITY_VERTEX_OUTPUT_STEREO
	};

	[maxvertexcount(12)]
	void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream)
	{
		// 主にこのあたりを書き換えること
		float3 center = (IN[0].pos + IN[1].pos + IN[2].pos) / 3;
		float4 vertices[6];
		float3 normals[6];
		int index_v[12] = {4, 0, 2, 1, 3, 5,	// 上面，側面，底面 
			2, 3, 4, 5, 0, 1				// 残りの側面
		};

		g2f o;
		for (int i = 0; i < 3; i++) {
			vertices[2 * i] = UnityObjectToClipPos(IN[i].pos + IN[i].nor * _Thick);
			vertices[2 * i + 1] = UnityObjectToClipPos(IN[i].pos + IN[i].nor * 0.001);
			normals[2 * i] = UnityObjectToWorldNormal(  IN[i].nor);
			normals[2 * i + 1] = UnityObjectToWorldNormal(- IN[i].nor);
		}
		for (int i = 0; i < 12; i++)
		{
			int n = index_v[i] / 2;
			UNITY_INITIALIZE_OUTPUT(g2f, o);

			UNITY_SETUP_INSTANCE_ID(g2f);
			UNITY_TRANSFER_INSTANCE_ID(g2f, o);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			o.pos = vertices[index_v[i]];
			if (index_v[i] % 2 == 0)
				o.pack0.xy = TRANSFORM_TEX(IN[n].texcoord, _MainTex);
			else {
				o.pack0.xy = -TRANSFORM_TEX(IN[n].texcoord, _MainTex);
				if (o.pack0.x == 0 && o.pack0.y == 0)
					o.pack0.xy = -float2(0.5, 0.5);
			}
			o.vertColor = IN[n].vertColor;
			float3 worldPos = mul(unity_ObjectToWorld, IN[n].pos).xyz;
			float3 worldNormal = normals[index_v[i]];
			o.worldPos = worldPos;
			o.worldNormal = worldNormal;

#ifdef DYNAMICLIGHTMAP_ON
			o.lmap.zw = IN[n].texcoord3.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif

#ifdef LIGHTMAP_ON
			o.lmap.xy = IN[n].texcoord2.xy * unity_LightmapST.xy + unity_LightmapST.zw;
#endif

#ifndef LIGHTMAP_ON
#if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
			o.sh = 0;
#ifdef VERTEXLIGHT_ON
			o.sh += Shade4PointLights(
				unity_4LightPosX0,
				unity_4LightPosY0,
				unity_4LightPosZ0,
				unity_LightColor[0].rgb,
				unity_LightColor[1].rgb,
				unity_LightColor[2].rgb,
				unity_LightColor[3].rgb,
				unity_4LightAtten0,
				worldPos,
				worldNormal);
#endif
			o.sh = ShadeSHPerVertex(worldNormal, o.sh);
#endif
#endif

			UNITY_TRANSFER_SHADOW(o, IN[n].texcoord2.xy);
			UNITY_TRANSFER_FOG(o, o.pos);

			triStream.Append(o);
			if (i == 5)
				triStream.RestartStrip();
		}

	}

	fixed4 frag(g2f IN) : SV_Target
	{
		UNITY_SETUP_INSTANCE_ID(IN);
		float3 worldPos = IN.worldPos;
		float3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));

	#ifndef USING_DIRECTIONAL_LIGHT
		fixed3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
	#else
		fixed3 lightDir = _WorldSpaceLightPos0.xyz;
	#endif

		Input surfIN;
		UNITY_INITIALIZE_OUTPUT(Input, surfIN);
		surfIN.uv_MainTex.x = 1.0;
		surfIN.uv_MainTex = IN.pack0.xy;
		surfIN.vertColor = IN.vertColor;

		SurfaceOutputStandard o;
		UNITY_INITIALIZE_OUTPUT(SurfaceOutputStandard, o);
		o.Albedo = 0.0;
		o.Emission = 0.0;
		o.Alpha = 0.0;
		o.Occlusion = 1.0;
		o.Normal = IN.worldNormal;

		surf(surfIN, o);

		UNITY_LIGHT_ATTENUATION(atten, IN, worldPos)

		fixed4 c = 0;

		UnityGI gi;
		UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
		gi.indirect.diffuse = 0;
		gi.indirect.specular = 0;
		gi.light.color = _LightColor0.rgb;
		gi.light.dir = lightDir;

		UnityGIInput giInput;
		UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
		giInput.light = gi.light;
		giInput.worldPos = worldPos;
		giInput.worldViewDir = worldViewDir;
		giInput.atten = atten;

	#if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
		giInput.lightmapUV = IN.lmap;
	#else
		giInput.lightmapUV = 0.0;
	#endif

	#if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
		giInput.ambient = IN.sh;
	#else
		giInput.ambient.rgb = 0.0;
	#endif

		giInput.probeHDR[0] = unity_SpecCube0_HDR;
		giInput.probeHDR[1] = unity_SpecCube1_HDR;

	#if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
		giInput.boxMin[0] = unity_SpecCube0_BoxMin;
	#endif

	#ifdef UNITY_SPECCUBE_BOX_PROJECTION
		giInput.boxMax[0] = unity_SpecCube0_BoxMax;
		giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
		giInput.boxMax[1] = unity_SpecCube1_BoxMax;
		giInput.boxMin[1] = unity_SpecCube1_BoxMin;
		giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
	#endif

		LightingStandard_GI(o, giInput, gi);
		c += LightingStandard(o, worldViewDir, gi);

		UNITY_APPLY_FOG(IN.fogCoord, c);
		UNITY_OPAQUE_ALPHA(c.a);

		return c;
	}

	ENDCG
	}
	
	Pass
	{

	Name "FORWARD"
	Tags { "LightMode" = "ForwardAdd" }
	ZWrite Off Blend One One

	CGPROGRAM
	#pragma vertex vert
	#pragma geometry geom
	#pragma fragment frag
	#pragma target 3.0
	#pragma multi_compile_instancing
	#pragma multi_compile_fog
	#pragma skip_variants INSTANCING_ON
	#pragma multi_compile_fwdadd_fullshadows
	#include "HLSLSupport.cginc"
	#include "UnityShaderVariables.cginc"
	#include "UnityShaderUtilities.cginc"

	#define UNITY_PASS_FORWARDADD
	#include "UnityCG.cginc"
	#include "Lighting.cginc"
	#include "UnityPBSLighting.cginc"
	#include "AutoLight.cginc"

	sampler2D _MainTex;
	float4 _MainTex_ST;
	sampler2D _BackTex;
	float4 _BackTex_ST;
	half _Glossiness;
	half _Metallic;
	half _BackGlossiness;
	half _BackMetallic;
	half _Thick;
	fixed4 _Color;

	struct Input
	{
		float2 uv_MainTex;
		float4 vertColor;
	};

	void surf(Input IN, inout SurfaceOutputStandard o)
	{
		fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
		fixed g;
		fixed m;

		if (IN.uv_MainTex.x < 0) {
			c = tex2D(_BackTex, -IN.uv_MainTex) * _Color;
			m = _BackMetallic;
			g = _BackGlossiness;
		}

		fixed rust = IN.vertColor.r;
		fixed dust = IN.vertColor.g;
		fixed chalk = IN.vertColor.b;

		fixed4 rc0 = fixed4(255.0 / 255.0, 255.0 / 255.0, 1.0, 1.0);
		fixed4 rc1 = fixed4(191.0 / 255.0, 95.0 / 255.0, 0.0, 1.0);
		fixed4 rust_mask = rc0 * (1 - rust) + rc1 * rust;

		rc0 = fixed4(191.0 / 255.0, 95.0 / 255.0, 0.0, 1.0);
		rc1 = fixed4(100.0 / 255.0, 50.0 / 255.0, 0.0, 1.0);
		fixed4 rust_col = rc0 * (1 - rust) + rc1 * rust;


		//c = c * (1 - rust) + rc0 * rust;
		c = c * rust_mask;
		c = c * (1 - rust * 0.2) + rust_col * rust * 0.2;
		c = c * (1 - dust);
		g = _Glossiness * (1 - rust * 0.2) * (1 - dust) * (1 - chalk);
		m = _Metallic * (1 - rust * 0.2) * (1 - dust) * (1 - chalk);

		o.Metallic = m;
		o.Smoothness = g;
		o.Albedo = c.rgb;
		o.Alpha = c.a;
	}

	struct v2g
	{
		//UNITY_POSITION(pos);
		float4 pos			: SV_POSITION;
		float3 nor		   : NORMAL;
		float2 texcoord    : TEXCOORD0;
		float4 vertColor    : TEXCOORD1;
		float2 texcoord2	: TEXCOORD2;
	};

	struct g2f
	{
		//UNITY_POSITION(pos);
		float4 pos			: SV_POSITION;
		float2 pack0 : TEXCOORD0;
		float4 vertColor    : TEXCOORD1;
		float3 worldNormal : TEXCOORD2;
		float3 worldPos : TEXCOORD3;
		UNITY_SHADOW_COORDS(4)
			UNITY_FOG_COORDS(5)
			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
	};

	v2g vert(appdata_full v)
	{
		UNITY_SETUP_INSTANCE_ID(v);
		v2g o;
		UNITY_INITIALIZE_OUTPUT(v2g, o);
		UNITY_TRANSFER_INSTANCE_ID(v, o);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
		o.pos = v.vertex;
		o.nor = v.normal;
		o.texcoord = v.texcoord;
		o.vertColor = v.texcoord1;
		o.texcoord2 = v.texcoord2;
		//o.texcoord2 = v.texcoord3;
		return o;
	}

	[maxvertexcount(12)]
	void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream)
	{	
		// 主にこのあたりを書き換えること
		float3 center = (IN[0].pos + IN[1].pos + IN[2].pos) / 3;
		float4 vertices[6];
		float3 normals[6];
		int index_v[12] = { 4, 0, 2, 1, 3, 5,	// 上面，側面，底面 
			2, 3, 4, 5, 0, 1				// 残りの側面
		};

		g2f o;
		for (int i = 0; i < 3; i++) {
			vertices[2 * i] = UnityObjectToClipPos(IN[i].pos + IN[i].nor * _Thick);
			vertices[2 * i + 1] = UnityObjectToClipPos(IN[i].pos + IN[i].nor * 0.001);
			normals[2 * i] = UnityObjectToWorldNormal(IN[i].nor);
			normals[2 * i + 1] = UnityObjectToWorldNormal(-IN[i].nor);
		}
		for (int i = 0; i < 12; i++)
		{
			int n = index_v[i] / 2;
			UNITY_INITIALIZE_OUTPUT(g2f, o);

			UNITY_SETUP_INSTANCE_ID(g2f);
			UNITY_TRANSFER_INSTANCE_ID(g2f, o);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			o.pos = vertices[index_v[i]];
			if (index_v[i] % 2 == 0)
				o.pack0.xy = TRANSFORM_TEX(IN[n].texcoord, _MainTex);
			else {
				o.pack0.xy = -TRANSFORM_TEX(IN[n].texcoord, _MainTex);
				if (o.pack0.x == 0 && o.pack0.y == 0)
					o.pack0.xy = -float2(0.5, 0.5);
			}
			o.vertColor = IN[n].vertColor;

			float3 worldPos = mul(unity_ObjectToWorld, IN[n].pos).xyz;
			float3 worldNormal = normals[index_v[i]];
			o.worldPos = worldPos;
			o.worldNormal = worldNormal;

			UNITY_TRANSFER_SHADOW(o, IN[n].texcoord2.xy);
			UNITY_TRANSFER_FOG(o, o.pos);

			triStream.Append(o);
			if (i == 5)
				triStream.RestartStrip();
		}
	}

	fixed4 frag(g2f IN) : SV_Target
	{
		UNITY_SETUP_INSTANCE_ID(IN);

		Input surfIN;
		UNITY_INITIALIZE_OUTPUT(Input,surfIN);
		surfIN.uv_MainTex.x = 1.0;
		surfIN.uv_MainTex = IN.pack0.xy;
		surfIN.vertColor = IN.vertColor;

		float3 worldPos = IN.worldPos;
		float3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
	#ifndef USING_DIRECTIONAL_LIGHT
		fixed3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
	#else
		fixed3 lightDir = _WorldSpaceLightPos0.xyz;
	#endif

		SurfaceOutputStandard o;
		UNITY_INITIALIZE_OUTPUT(SurfaceOutputStandard, o);
		o.Albedo = 0.0;
		o.Emission = 0.0;
		o.Alpha = 0.0;
		o.Occlusion = 1.0;
		o.Normal = IN.worldNormal;

		surf(surfIN, o);
		UNITY_LIGHT_ATTENUATION(atten, IN, worldPos)
		fixed4 c = 0;

		UnityGI gi;
		UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
		gi.indirect.diffuse = 0;
		gi.indirect.specular = 0;
		gi.light.color = _LightColor0.rgb;
		gi.light.dir = lightDir;
		gi.light.color *= atten;

		c += LightingStandard(o, worldViewDir, gi);
		c.a = 0.0;

		UNITY_APPLY_FOG(IN.fogCoord, c);
		UNITY_OPAQUE_ALPHA(c.a);

		return c;
	}

	ENDCG

	}
		}
			FallBack "Diffuse"
}
