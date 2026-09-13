Shader "RustAndFloat/FluffyCloud"
{
    Properties
    {
        _BaseColor ("Cloud Base Color (雲の基本色)", Color) = (0.95, 0.98, 1.0, 0.92)
        _ShadowColor ("Cloud Shadow Color (雲の陰色)", Color) = (0.75, 0.85, 0.95, 0.90)
        _RimColor ("Rim/Sun Glow Color (フチの輝き)", Color) = (1.0, 1.0, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-100" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _RimColor;
                float _RimPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);

                // 柔らかな陰影（Half-Lambert）
                float NdotL = dot(N, L) * 0.5 + 0.5;
                float3 col = lerp(_ShadowColor.rgb, _BaseColor.rgb, NdotL);

                // フチのふんわりとした光（Rim Light）
                float rim = 1.0 - saturate(dot(V, N));
                rim = pow(rim, _RimPower);
                col = lerp(col, _RimColor.rgb, rim * 0.45);

                float alpha = lerp(_ShadowColor.a, _BaseColor.a, NdotL);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
