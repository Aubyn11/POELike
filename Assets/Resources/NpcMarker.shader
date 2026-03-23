Shader "POELike/NpcMarker"
{
    Properties
    {
        _Color        ("NPC颜色",           Color)  = (0.9, 0.7, 0.2, 1.0)
        _OutlineColor ("描边颜色",           Color)  = (1.0, 1.0, 1.0, 1.0)
        _Radius       ("圆形半径(px)",       Float)  = 14.0
        _OutlineWidth ("描边宽度(px)",       Float)  = 3.0
        _Hovered      ("是否悬停(0/1)",      Float)  = 0.0
        _NpcScreenPos ("NPC屏幕坐标(归一化)", Vector) = (0.5, 0.5, 0, 0)
        _NpcScreenSize ("屏幕分辨率",          Vector) = (1920, 1080, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Overlay"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline"  = "UniversalPipeline"
        }

        Pass
        {
            Name "NpcMarker"
            ZWrite Off
            ZTest  Always
            Cull   Off
            Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _OutlineColor;
                float  _Radius;
                float  _OutlineWidth;
                float  _Hovered;
                float4 _NpcScreenPos;
                float4 _NpcScreenSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = float4(v.positionOS.x * 2.0 - 1.0,
                                      v.positionOS.y * 2.0 - 1.0,
                                      0.0, 1.0);
                float2 uv = v.positionOS.xy;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif
                o.uv = uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 pixelPos  = i.uv * _NpcScreenSize.xy;
                float2 npcPixel  = _NpcScreenPos.xy * _NpcScreenSize.xy;

                // 到NPC中心的距离
                float dist = length(pixelPos - npcPixel);

                float R  = _Radius;
                float ow = _OutlineWidth;

                // 抗锯齿边缘宽度
                float aa = 1.5;

                // 实心圆内部
                float innerAlpha = 1.0 - smoothstep(R - aa, R + aa, dist);

                // 描边环（仅悬停时显示）
                float outlineAlpha = 0.0;
                if (_Hovered > 0.5)
                {
                    float outerR = R + ow;
                    float ring   = smoothstep(R + aa, R + aa * 2.0, dist)
                                 * (1.0 - smoothstep(outerR - aa, outerR + aa, dist));
                    outlineAlpha = ring;
                }

                // 合并：描边在外圈，实心圆在内
                float4 col = float4(0, 0, 0, 0);

                if (outlineAlpha > 0.01)
                {
                    col = float4(_OutlineColor.rgb, outlineAlpha * _OutlineColor.a);
                }
                if (innerAlpha > 0.01)
                {
                    // 悬停时内圆略微变亮
                    float3 baseColor = _Color.rgb;
                    if (_Hovered > 0.5)
                        baseColor = lerp(baseColor, float3(1,1,1), 0.25);
                    col = float4(baseColor, innerAlpha * _Color.a);
                }

                return col;
            }
            ENDHLSL
        }
    }
}
