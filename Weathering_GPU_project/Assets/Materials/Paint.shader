Shader "Custom/vertex_color"
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
            Cull Off

            Tags { "RenderType" = "Opaque" }
            LOD 200

            CGPROGRAM
            // Physically based Standard lighting model, and enable shadows on all light types
            #pragma surface surf Standard vertex:vert // fullforwardshadowsÅ®vertex:vert

            // Use shader model 3.0 target, to get nicer looking lighting
            #pragma target 3.0

            sampler2D _MainTex;

            struct Input
            {
                float2 uv_MainTex;
                float4 vertColor; // í«â¡
            };

            void vert(inout appdata_full v, out Input o) {  // vert()Çí«â¡
                UNITY_INITIALIZE_OUTPUT(Input, o);
                //o.vertColor = v.color;
                o.vertColor = v.texcoord1;
            }

            half _Glossiness;
            half _Metallic;
            fixed4 _Color;

            // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
            // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
            // #pragma instancing_options assumeuniformscaling
            UNITY_INSTANCING_BUFFER_START(Props)
                // put more per-instance properties here
            UNITY_INSTANCING_BUFFER_END(Props)

            void surf(Input IN, inout SurfaceOutputStandard o)
            {

                // Albedo comes from a texture tinted by color
                fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
                fixed g;
                fixed m;
                
                fixed rust = IN.vertColor.r;
                fixed dust = IN.vertColor.g;
                fixed chalk = IN.vertColor.b;

                fixed4 rc0 = fixed4(255.0 / 255.0, 255.0 / 255.0, 1.0, 1.0);
                fixed4 rc1 = fixed4(191.0 / 255.0, 95.0 / 255.0, 0.0, 1.0);
                fixed4 rust_mask = rc0 * (1 - rust) + rc1 * rust;

                rc0 = fixed4(191.0 / 255.0, 95.0 / 255.0, 0.0, 1.0);
                rc1 = fixed4(100.0 / 255.0, 50.0 / 255.0, 0.0, 1.0);
                fixed4 rust_col = rc0 * (1 - rust) + rc1 * rust;

                c = c * rust_mask;
                c = c * (1 - rust * 0.1) + rust_col * rust * 0.1;
                c = c * (1 - dust);
                g = _Glossiness * (1 - rust * 0.1) * (1 - dust) * (1 - chalk);
                m = _Metallic * (1 - rust * 0.1) * (1 - dust) * (1 - chalk);

                //dust = 0;
                o.Albedo = c.rgb * (1 - dust); // ïœçX
                // Metallic and smoothness come from slider variables
                o.Metallic = m;
                o.Smoothness = g;
                o.Alpha = c.a;

                //o.Metallic = _Metallic;
                //o.Smoothness = _Glossiness;
                //o.Albedo = IN.vertColor.rgb;
            }
            ENDCG
        }
            FallBack "Diffuse"
}