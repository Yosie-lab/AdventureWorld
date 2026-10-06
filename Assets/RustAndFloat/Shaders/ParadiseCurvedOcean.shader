// 『Rust & Float』リアルなエメラルド〜ブルーグラデーション＆曲線の水平線＆波のきらめき海面シェーダー
// URP (Unity 6 / Universal Render Pipeline) 完全対応
Shader "RustAndFloat/ParadiseCurvedOcean"
{
    Properties
    {
        [Header(Colors)]
        _ShallowColor ("浅瀬クリスタルエメラルド (Lagoon Emerald)", Color) = (0.015, 0.98, 0.78, 0.82)
        _MidColor ("沿岸鮮やかエメラルドターコイズ (Vivid Emerald Turquoise)", Color) = (0.01, 0.90, 0.72, 0.94)
        _DeepColor ("深海ディープエメラルドグリーン (Deep Emerald)", Color) = (0.005, 0.62, 0.55, 0.99)
        _HorizonColor ("水平線ミントスカイ (Horizon Mint Sky)", Color) = (0.22, 0.90, 0.84, 1.0)
        _SunGlitterColor ("太陽光きらめきカラー (Sun Glitter)", Color) = (1.0, 0.98, 0.88, 1.0)

        [Header(Waves and Normals)]
        _NormalMap1 ("波ノーマル 1", 2D) = "bump" {}
        _NormalMap2 ("波ノーマル 2", 2D) = "bump" {}
        _WaveScale1 ("波スケール 1", Float) = 0.025
        _WaveScale2 ("波スケール 2", Float) = 0.065
        _WaveSpeed1 ("波スクロール速度 1", Vector) = (0.02, 0.012, 0, 0)
        _WaveSpeed2 ("波スクロール速度 2", Vector) = (-0.015, 0.022, 0, 0)
        _NormalStrength ("波の凹凸強度", Range(0.1, 2.5)) = 0.85

        [Header(Sun Glitter and Sparkle)]
        _SunGlitterIntensity ("波のきらめき強度", Range(0.5, 8.0)) = 3.2
        _SunGlitterExponent ("きらめき集中度", Range(16, 256)) = 80
        _SparkleScale ("星屑グリッタースケール", Float) = 1.6
        _OverheadSparkleIntensity ("上空見下ろし波のキラキラ強度", Range(0.0, 5.0)) = 2.2

        [Header(Curved Horizon)]
        _IslandCenter ("島中心ワールド座標 (XZ)", Vector) = (512, 5.5, 512, 0)
        _ShallowRadius ("浅瀬半径 (m)", Float) = 380
        _DeepRadius ("深海遷移半径 (m)", Float) = 900
        _HorizonRadius ("水平線最大半径 (m)", Float) = 4500
        _CurvatureAmount ("地球曲率ドロップオフ (m)", Float) = 22.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
                float distFromCenter : TEXCOORD3;
            };

            TEXTURE2D(_NormalMap1);
            SAMPLER(sampler_NormalMap1);
            TEXTURE2D(_NormalMap2);
            SAMPLER(sampler_NormalMap2);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _MidColor;
                float4 _DeepColor;
                float4 _HorizonColor;
                float4 _SunGlitterColor;
                float4 _WaveSpeed1;
                float4 _WaveSpeed2;
                float4 _IslandCenter;
                float _WaveScale1;
                float _WaveScale2;
                float _NormalStrength;
                float _SunGlitterIntensity;
                float _SunGlitterExponent;
                float _SparkleScale;
                float _OverheadSparkleIntensity;
                float _ShallowRadius;
                float _DeepRadius;
                float _HorizonRadius;
                float _CurvatureAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // なだらかな曲線の水平線（地球の曲率シミュレーション）
                float2 dXZ = posWS.xz - _IslandCenter.xz;
                float dist = length(dXZ);

                if (dist > _ShallowRadius)
                {
                    float normDist = saturate((dist - _ShallowRadius) / max(1.0, (_HorizonRadius - _ShallowRadius)));
                    float curvatureDrop = pow(normDist, 1.85) * _CurvatureAmount;
                    posWS.y -= curvatureDrop;
                }

                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.distFromCenter = dist;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 posWS = input.positionWS;
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - posWS);

                // 2層の波ノーマルマップのサンプリングとスクロール合成
                float2 uv1 = posWS.xz * _WaveScale1 + _Time.y * _WaveSpeed1.xy;
                float2 uv2 = posWS.xz * _WaveScale2 + _Time.y * _WaveSpeed2.xy;

                float4 nSample1 = SAMPLE_TEXTURE2D(_NormalMap1, sampler_NormalMap1, uv1);
                float4 nSample2 = SAMPLE_TEXTURE2D(_NormalMap2, sampler_NormalMap2, uv2);

                float3 n1 = UnpackNormal(nSample1);
                float3 n2 = UnpackNormal(nSample2);
                float2 waveOffset = (n1.xy + n2.xy) * 0.5 * _NormalStrength;

                // ワールド海面法線（垂直上向き Y=1 に対する摂動）
                float3 normalWS = normalize(float3(waveOffset.x, 1.0, waveOffset.y));

                // 太陽光（メインライト）の取得
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 lightColor = mainLight.color;

                // 島中心からの距離による多層カラーグラデーション
                float dist = input.distFromCenter;

                // 1. 浅瀬〜沿岸 (0 -> 1)
                float tShallow = saturate(dist / max(1.0, _ShallowRadius));
                // 2. 沿岸〜中洋 (0 -> 1)
                float tDeep = saturate((dist - _ShallowRadius) / max(1.0, (_DeepRadius - _ShallowRadius)));
                // 3. 中洋〜水平線 (0 -> 1)
                float tHorizon = saturate((dist - _DeepRadius) / max(1.0, (_HorizonRadius - _DeepRadius)));

                // エメラルドグリーンからトロピカルブルー、ディープブルー、水平線への滑らかな合成
                float4 waterCol = lerp(_ShallowColor, _MidColor, smoothstep(0.0, 1.0, tShallow));
                waterCol = lerp(waterCol, _DeepColor, smoothstep(0.0, 1.0, tDeep));
                waterCol = lerp(waterCol, _HorizonColor, smoothstep(0.1, 1.0, tHorizon));

                // フレネル反射（水面の斜め反射）
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = 0.04 + 0.96 * pow(1.0 - NdotV, 4.0);

                // 空の反射色（鮮烈なエメラルド〜ターコイズ光の映り込み）
                float3 skyReflection = lerp(float3(0.02, 0.75, 0.70), float3(0.25, 0.92, 0.88), fresnel);
                float3 diffuseWater = lerp(waterCol.rgb, skyReflection, fresnel * 0.28);

                // 1. 太陽光のダイレクト・スペキュラ（直射日光ハイライト）
                float3 halfDir = normalize(lightDir + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float spec = pow(NdotH, _SunGlitterExponent);

                // 2. 太陽ハイライト周辺のダイナミック・グリッター
                float2 sparkleUV = posWS.xz * _SparkleScale;
                float sparkleNoise = sin(sparkleUV.x * 2.8 + _Time.y * 3.2) * cos(sparkleUV.y * 2.8 - _Time.y * 2.5);
                sparkleNoise = saturate((sparkleNoise - 0.4) * 2.5);
                float sunSparkle = pow(NdotH, _SunGlitterExponent * 0.4) * sparkleNoise * 1.8;

                // 3. 上空見下ろし対応：波頭（Wave Crest）の自然で細やかなキラキラ（メッシュ線皆無・モアレフリー設計）
                // 波テクスチャのオフセット（waveOffset）から純粋な波の尾根（Crest）をピンポイント抽出
                float waveSlope = length(waveOffset);
                float waveCrest = saturate((waveSlope - 0.10) * 3.5);

                // 近〜中距離でのみ上品に瞬くプロシージャル・スターダスト（波頭にのみ発生）
                float camDist = length(_WorldSpaceCameraPos - posWS);
                float sparkleFade = saturate(1.0 - camDist / 1500.0);
                sparkleFade = sparkleFade * sparkleFade; // 2乗でソフトフェード

                // 波頭の傾斜に乗ってピカピカと瞬く星屑ドット
                float2 dotUV = posWS.xz * 0.25 + _Time.y * float2(0.05, -0.04);
                float dotVal = sin(dotUV.x * 8.0) * cos(dotUV.y * 8.0);
                float starDust = pow(saturate(dotVal), 16.0) * (waveCrest * 0.85 + 0.15) * sparkleFade * 2.2;

                // 波頭のきらめき合成（波の尾根の自然なツヤ + 瞬く星屑）
                float overheadSparkle = (waveCrest * 0.28 + starDust) * _OverheadSparkleIntensity;

                // 4. 太陽方向の散乱照り返し（波頭のハイライト）
                float crestSunGlitter = pow(saturate(dot(normalWS, lightDir)), 8.0) * waveCrest * 0.65;

                float totalGlitter = (spec + sunSparkle) * _SunGlitterIntensity + (overheadSparkle + crestSunGlitter);
                float3 glitterColor = totalGlitter * _SunGlitterColor.rgb * (lightColor * 0.65 + 0.35);

                // 最終カラー合成（フォグで白飛びさせず、水平線カラーと自然に融合）
                float3 finalColor = diffuseWater * (lightColor * 0.65 + 0.35) + glitterColor;

                // 水平線遠景での大気フェード（遠くはなだらかな水平線スカイブルーに溶け込む）
                if (tHorizon > 0.4)
                {
                    float horizonFade = smoothstep(0.4, 1.0, tHorizon);
                    finalColor = lerp(finalColor, _HorizonColor.rgb, horizonFade * 0.85);
                }

                // アルファ値（浅瀬は透明度を持ち、沖合はしっかり不透明）
                float finalAlpha = waterCol.a;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
