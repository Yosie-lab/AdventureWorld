Shader "RustAndFloat/ClearBlueSky"
{
    Properties
    {
        _TopColor ("Zenith Color (天頂の深い群青)", Color) = (0.01, 0.26, 0.85, 1)
        _MidColor ("Mid-Sky Color (鮮やかなアズールブルー)", Color) = (0.06, 0.50, 0.98, 1)
        _HorizonColor ("Horizon Color (地平線の澄んだシアンブルー)", Color) = (0.42, 0.76, 0.98, 1)
        _GroundColor ("Ground/Sea Color (海面方向)", Color) = (0.25, 0.60, 0.90, 1)
        _SunColor ("Sun Disc Color (太陽光)", Color) = (1.0, 0.98, 0.90, 1)
        _SunSize ("Sun Size", Range(0.01, 0.1)) = 0.035
        _SunGlow ("Sun Glow Tightness", Range(0.5, 5.0)) = 2.5
        _HorizonOffset ("Horizon Offset", Range(-0.2, 0.2)) = 0.01
        _Exponent ("Sky Curve Exponent", Range(0.3, 2.5)) = 0.65
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _MidColor;
            fixed4 _HorizonColor;
            fixed4 _GroundColor;
            fixed4 _SunColor;
            half _SunSize;
            half _SunGlow;
            half _HorizonOffset;
            half _Exponent;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(v.vertex.xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float y = dir.y - _HorizonOffset;

                fixed3 col;
                if (y >= 0.0)
                {
                    // 仰角yが少し上がるだけで一気に深く鮮やかな青空が立ち上がるカーブ
                    float t = pow(saturate(y), _Exponent);
                    if (t < 0.25)
                    {
                        // 水平線直上：クリアシアンからアズールブルーへ
                        col = lerp(_HorizonColor.rgb, _MidColor.rgb, t / 0.25);
                    }
                    else
                    {
                        // 中空から天頂：アズールブルーから吸い込まれるような深い群青へ
                        col = lerp(_MidColor.rgb, _TopColor.rgb, (t - 0.25) / 0.75);
                    }
                }
                else
                {
                    // 地平線下（海方向）
                    float t = saturate(-y * 4.0);
                    col = lerp(_HorizonColor.rgb, _GroundColor.rgb, t);
                }

                // 太陽の描画（空の青を白飛びさせないよう局所的に）
                float3 sunDir = _WorldSpaceLightPos0.xyz;
                float sunDot = saturate(dot(dir, sunDir));

                // 太陽本体のシャープなディスク
                float sunDisk = step(1.0 - _SunSize * 0.015, sunDot);

                // 太陽直近の引き締まった光彩（青を侵食しない）
                float sunHalo = pow(sunDot, 180.0 / _SunGlow) * 0.6;
                float softHalo = pow(sunDot, 40.0 / _SunGlow) * 0.15;

                col += _SunColor.rgb * (sunDisk + sunHalo + softHalo);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
