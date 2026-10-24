Shader "RustAndFloat/FluffyCloud"
{
    Properties
    {
        _BaseColor ("Cloud Base Color (雲の基本色)", Color) = (0.98, 0.99, 1.0, 0.94)
        _ShadowColor ("Cloud Shadow Color (雲の陰色)", Color) = (0.76, 0.84, 0.94, 0.88)
        _SunScatteringColor ("Forward Scattering (逆光透過光)", Color) = (1.0, 0.94, 0.82, 1.0)
        _RimColor ("Rim/Silver Lining (銀色のフチ)", Color) = (1.0, 1.0, 1.0, 1.0)
        _RimPower ("Rim Sharpness", Range(0.5, 8.0)) = 2.4
        _NoiseScale ("Organic Noise Scale", Range(0.01, 0.5)) = 0.08
        _NoiseSpeed ("Deformation Speed", Range(0.01, 1.0)) = 0.12
        _DisplaceAmount ("Displacement Amount", Range(0.0, 3.0)) = 0.85
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
                float3 worldPos : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _SunScatteringColor;
                float4 _RimColor;
                float _RimPower;
                float _NoiseScale;
                float _NoiseSpeed;
                float _DisplaceAmount;
            CBUFFER_END

            // 軽量な擬似3Dサインノイズ（雲の有機的なうねり・呼吸）
            float CloudNoise(float3 p)
            {
                float n = sin(p.x) * cos(p.y) + sin(p.y) * cos(p.z) + sin(p.z) * cos(p.x);
                n += 0.5 * (sin(p.x * 2.1 + 1.2) * cos(p.y * 1.9 + 2.3));
                return n;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                // ワールド座標ベースで有機的な微小ディスプレイスメントを適用
                float3 worldPosRaw = TransformObjectToWorld(input.positionOS.xyz);
                float3 noiseCoord = worldPosRaw * _NoiseScale + float3(_Time.y * _NoiseSpeed, _Time.y * _NoiseSpeed * 0.7, _Time.y * _NoiseSpeed * 0.5);
                float disp = CloudNoise(noiseCoord) * _DisplaceAmount;

                // 法線方向に沿ってふっくらと押し出し（球体感を崩し、綿菓子のような塊にする）
                float3 displacedOS = input.positionOS.xyz + input.normalOS * (disp * 0.35);

                VertexPositionInputs posInputs = GetVertexPositionInputs(displacedOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                output.worldPos = posInputs.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);

                // 1. 雲特有のソフトな半ランバート拡散（上空のやわらかな陽光）
                float NdotL = dot(N, L) * 0.5 + 0.5;
                // 下部への穏やかな大気アンビエントグラデーション
                float skyUp = saturate(N.y * 0.5 + 0.5);
                float3 baseTone = lerp(_ShadowColor.rgb, _BaseColor.rgb, NdotL * skyUp);

                // 2. 前方散乱（Forward Scattering / Mie Scattering）
                // 太陽を背にして雲を見たとき、内部を光が透過して温かく輝く現象
                float VdotL = dot(V, -L);
                float forwardScatter = pow(saturate(VdotL), 3.0) * (1.0 - NdotL * 0.5);
                baseTone += _SunScatteringColor.rgb * (forwardScatter * 0.65);

                // 3. フチのきらめく銀色の縁取り（Silver Lining / Fresnel Rim）
                float rim = 1.0 - saturate(dot(V, N));
                float rimFactor = pow(rim, _RimPower);
                float3 finalColor = lerp(baseTone, _RimColor.rgb, rimFactor * 0.55);

                // 太陽光のハイライト感
                finalColor += _RimColor.rgb * (pow(rimFactor, 1.8) * saturate(dot(N, L)) * 0.35);

                // 雲の自然な透過アルファ（フチをわずかに柔らかく）
                float alpha = lerp(_ShadowColor.a, _BaseColor.a, NdotL);
                alpha *= saturate(1.0 - pow(rim, 5.0) * 0.4);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
