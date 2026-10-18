// サンクチュアリ・ゼロ：白亜の床 → 青いワイヤーフレームグリッド発光
// URP (Unity 6) 対応 / SRP Batcher 互換
Shader "RustAndFloat/SanctuaryGridGlow"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("ベースカラー（白亜）", Color) = (0.92, 0.90, 0.85, 1)

        [Header(Grid)]
        _GridColor ("グリッドカラー", Color) = (0.08, 0.45, 1.0, 1)
        _GridCellSize ("グリッド間隔 (m)", Float) = 2.0
        _LineWidth ("線の太さ", Range(0.005, 0.12)) = 0.035
        _SubGridAlpha ("サブグリッド強度", Range(0, 1)) = 0.25

        [Header(Glow)]
        _GlowIntensity ("グロー強度", Range(0, 1)) = 0
        _EmissionStrength ("エミッション倍率 (HDR)", Float) = 5.0
        _RevealRadius ("リビール半径 (m)", Float) = 0
        _CenterWorld ("中心ワールド座標", Vector) = (512, 62, 512, 0)

        [Header(Animation)]
        _PulseSpeed ("パルス速度", Float) = 1.2
        _ScanSpeed ("スキャンライン速度", Float) = 3.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── 頂点データ ──
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            // ── SRP Batcher 用 CBUFFER ──
            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half4  _GridColor;
                float  _GridCellSize;
                float  _LineWidth;
                float  _SubGridAlpha;
                float  _GlowIntensity;
                float  _EmissionStrength;
                float  _RevealRadius;
                float4 _CenterWorld;
                float  _PulseSpeed;
                float  _ScanSpeed;
            CBUFFER_END

            // ── 頂点シェーダー ──
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogFactor  = ComputeFogFactor(vpi.positionCS.z);
                return OUT;
            }

            // ── グリッド線マスク生成 ──
            float GridMask(float2 worldXZ, float cellSize, float lineW)
            {
                float2 cellUV = worldXZ / cellSize;
                float2 grid     = abs(frac(cellUV) - 0.5);
                float  lineDist = min(grid.x, grid.y);
                return 1.0 - smoothstep(0.0, lineW, lineDist);
            }

            // ── フラグメントシェーダー ──
            half4 frag(Varyings IN) : SV_Target
            {
                // 上面のみ描画（法線 Y > 0.3 で判定）
                float facingUp = saturate((IN.normalWS.y - 0.3) / 0.3);

                // メイングリッド
                float mainGrid = GridMask(IN.positionWS.xz, _GridCellSize, _LineWidth * 0.5);

                // サブグリッド（半分の間隔）
                float subGrid  = GridMask(IN.positionWS.xz, _GridCellSize * 0.5, _LineWidth * 0.25);
                float gridMask = saturate(mainGrid + subGrid * _SubGridAlpha);

                // 中心からの距離
                float2 delta = IN.positionWS.xz - _CenterWorld.xz;
                float  dist  = length(delta);

                // 放射状リビール（中心から外へ広がる）
                float reveal = smoothstep(_RevealRadius + 1.5, max(_RevealRadius - 1.5, 0.0), dist);

                // パルス（呼吸のような明滅 0.82〜1.0）
                float pulse = 0.82 + 0.18 * sin(_Time.y * _PulseSpeed * 6.2832);

                // スキャンリング（中心から放射状に走る輪）
                float scanPhase = frac(_Time.y * _ScanSpeed * 0.04);
                float scanDist  = scanPhase * (_RevealRadius + 4.0);
                float scanRing  = 1.0 + 0.55 * exp(-((dist - scanDist) * (dist - scanDist)) / 5.0);

                // 角度方向のキラキラ（ゆっくり回転）
                float angle   = atan2(delta.y, delta.x);
                float angular = 0.92 + 0.08 * sin(angle * 6.0 + _Time.y * 0.8);

                // 総合グロー
                float glow = gridMask * _GlowIntensity * reveal * pulse * scanRing * angular * facingUp;

                // ベース（白亜）＋ グリッド発光（HDRエミッション）
                half3 baseCol  = _BaseColor.rgb;
                half3 emission = _GridColor.rgb * _EmissionStrength * glow;
                half3 finalCol = baseCol * (1.0 - glow * 0.6) + emission;

                // 中心付近のコア発光（ぼんやり明るくなる）
                float coreGlow = exp(-(dist * dist) / ((_RevealRadius * 0.4 + 1.0) * (_RevealRadius * 0.4 + 1.0) * 2.0));
                finalCol += _GridColor.rgb * coreGlow * _GlowIntensity * 0.3;

                // フォグ適用
                finalCol = MixFog(finalCol, IN.fogFactor);

                return half4(finalCol, 1.0);
            }
            ENDHLSL
        }

        // 影を落とす用パス
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings  { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
