Shader "KingsMarch/AuraSteam"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 localPos : TEXCOORD1;
            };

            sampler2D _MainTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                // オブジェクト空間座標を渡す（ノイズ・高さ計算用）
                o.localPos = v.vertex.xy;
                return o;
            }

            // ─── Procedural Noise ─────────────────────────────

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash(i), hash(i + float2(1, 0)), f.x),
                    lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x),
                    f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int j = 0; j < 3; j++)
                {
                    v += a * vnoise(p);
                    p *= 2.0;
                    a *= 0.5;
                }
                return v;
            }

            // ─── Fragment ─────────────────────────────────────

            fixed4 frag(v2f i) : SV_Target
            {
                // スプライトのアルファ = 体の形状マスク
                float spriteA = tex2D(_MainTex, i.uv).a;
                if (spriteA < 0.01) discard;

                // オブジェクト空間の正規化 (64x64, PPU=16, pivot=(32,8))
                // x: [-2, +2] → [0, 1],  y: [-0.5, +3.5] → [0, 1]
                float2 normPos = float2(
                    (i.localPos.x + 2.0) / 4.0,
                    (i.localPos.y + 0.5) / 4.0
                );

                // 上昇するFBMノイズ（蒸気パターン）
                float2 nUV = normPos * 6.0;
                nUV.y -= _Time.y * 1.5;
                float steam = fbm(nUV);

                // 第2レイヤー: 細かい揺らぎを加算
                float2 nUV2 = normPos * 12.0 + float2(5.3, 2.7);
                nUV2.y -= _Time.y * 2.0;
                float detail = vnoise(nUV2);
                steam = steam * 0.7 + detail * 0.3;

                // 高さグラデーション: 下部=濃い、上部=薄く消散
                float hFade = 1.0 - smoothstep(0.3, 1.0, normPos.y) * 0.6;

                // ティアカラーそのまま、ノイズで蒸気の濃淡
                float alpha = spriteA * steam * hFade * i.color.a;

                return fixed4(i.color.rgb, saturate(alpha));
            }
            ENDCG
        }
    }
}
