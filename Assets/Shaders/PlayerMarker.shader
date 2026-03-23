Shader "POELike/PlayerMarker"
{
    Properties
    {
        _Color            ("箭头颜色",          Color)  = (0.2, 0.8, 1.0, 1.0)
        _RimColor         ("外圈颜色",          Color)  = (1.0, 1.0, 1.0, 0.6)
        _Radius           ("箭头大小(px)",       Float)  = 22.0
        _LineWidth        ("线宽(px)",           Float)  = 2.5
        _PlayerScreenPos  ("玩家屏幕坐标(归一化)", Vector) = (0.5, 0.5, 0, 0)
        _MarkerScreenSize ("屏幕分辨率",          Vector) = (1920, 1080, 0, 0)
        _ForwardAngle     ("Forward角度(弧度)",   Float)  = 0.0
        _Time2            ("动画时间",            Float)  = 0.0
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
            Name "PlayerMarker"
            ZWrite Off
            ZTest  Always
            Cull   Off
            Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── 属性 ──────────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _RimColor;
                float  _Radius;
                float  _LineWidth;
                float4 _PlayerScreenPos;
                float4 _MarkerScreenSize;
                float  _ForwardAngle;
                float  _Time2;
            CBUFFER_END

            // ── 顶点输入/输出 ─────────────────────────────────────────
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

            // ── SDF 工具函数 ──────────────────────────────────────────

            // 线段 SDF：点 p 到线段 (a→b) 的有符号距离
            float sdSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float  h  = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // 三角形 SDF（实心）
            float sdTriangle(float2 p, float2 a, float2 b, float2 c)
            {
                float2 e0 = b - a, e1 = c - b, e2 = a - c;
                float2 v0 = p - a, v1 = p - b, v2 = p - c;
                float2 pq0 = v0 - e0 * saturate(dot(v0, e0) / dot(e0, e0));
                float2 pq1 = v1 - e1 * saturate(dot(v1, e1) / dot(e1, e1));
                float2 pq2 = v2 - e2 * saturate(dot(v2, e2) / dot(e2, e2));
                float  s   = sign(e0.x * e2.y - e0.y * e2.x);
                float2 d   = min(min(
                    float2(dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                    float2(dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                    float2(dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
                return -sqrt(d.x) * sign(d.y);
            }

            // ── 片元着色器 ────────────────────────────────────────────
            float4 frag(Varyings i) : SV_Target
            {
                float2 pixelPos    = i.uv * _MarkerScreenSize.xy;
                float2 playerPixel = _PlayerScreenPos.xy * _MarkerScreenSize.xy;

                // 以玩家为中心的局部坐标
                float2 local = pixelPos - playerPixel;

                // 将局部坐标旋转到箭头朝向空间
                // _ForwardAngle 是从屏幕 +Y 轴顺时针的角度（弧度）
                // 加 PI 修正箭头默认朝向，使尖端指向 forward 方向
                float cosA = cos(-_ForwardAngle + 3.14159265);
                float sinA = sin(-_ForwardAngle + 3.14159265);
                float2 rot = float2(
                    local.x * cosA - local.y * sinA,
                    local.x * sinA + local.y * cosA
                );
                // rot.y > 0 为箭头朝向（forward）

                float R  = _Radius;       // 箭头整体大小
                float lw = _LineWidth;    // 线宽

                // ── 箭头头部（等腰三角形）────────────────────────────
                // 顶点：正上方，底边两侧
                float2 tipA  = float2( 0.0,       R * 1.0);
                float2 tipB  = float2(-R * 0.55,  R * 0.15);
                float2 tipC  = float2( R * 0.55,  R * 0.15);
                float  dHead = sdTriangle(rot, tipA, tipB, tipC);

                // ── 箭头杆部（矩形，用两条线段近似）─────────────────
                float2 stemTop = float2(0.0,  R * 0.15);
                float2 stemBot = float2(0.0, -R * 0.85);
                float  stemW   = R * 0.22;
                // 杆部：到中心线的距离 < stemW/2，且在 y 范围内
                float  dStemLine = sdSegment(rot, stemTop, stemBot);
                // 用矩形 SDF 代替：
                float2 stemLocal = rot - float2(0.0, (R * 0.15 + (-R * 0.85)) * 0.5);
                float2 stemHalf  = float2(stemW * 0.5, (R * 0.15 - (-R * 0.85)) * 0.5);
                float2 stemD     = abs(stemLocal) - stemHalf;
                float  dStem     = length(max(stemD, 0.0)) + min(max(stemD.x, stemD.y), 0.0);

                // ── 底部圆点（可选，增加视觉稳定感）─────────────────
                float  dBase = length(rot - float2(0.0, -R * 0.85)) - R * 0.18;

                // ── 合并箭头形状 ──────────────────────────────────────
                float dArrow = min(min(dHead, dStem), dBase);

                // 填充（实心箭头）
                float fillAlpha = smoothstep(lw, -lw * 0.5, dArrow);

                // 描边（外发光）
                float glowAlpha = smoothstep(lw * 3.5, 0.0, dArrow + lw * 1.2) * 0.45;

                // 超出范围丢弃
                float dist = length(local);
                if (dist > R * 1.5 + lw * 4.0) discard;

                float4 col = float4(0, 0, 0, 0);

                // 外发光（用 RimColor）
                col = lerp(col, _RimColor, glowAlpha * _RimColor.a);
                // 箭头填充
                col = lerp(col, _Color, fillAlpha);

                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
