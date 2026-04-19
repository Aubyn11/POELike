Shader "POELike/SkillRuntimeOverlay"
{
    Properties
    {
        _Color ("颜色", Color) = (1,0.4,0.1,0.35)
        _CenterScreenPos ("屏幕中心", Vector) = (0.5,0.5,0,0)
        _RadiusPx ("半径", Float) = 40
        _ThicknessPx ("厚度", Float) = 3
        _SkillScreenSize ("屏幕大小", Vector) = (1920,1080,0,0)
        _FillAlpha ("填充透明度", Float) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SkillRuntimeOverlay"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _CenterScreenPos;
                float _RadiusPx;
                float _ThicknessPx;
                float4 _SkillScreenSize;
                float _FillAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = float4(v.positionOS.x * 2.0 - 1.0, v.positionOS.y * 2.0 - 1.0, 0.0, 1.0);
                float2 uv = v.positionOS.xy;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif
                o.uv = uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 pixelPos = i.uv * _SkillScreenSize.xy;
                float2 centerPx = _CenterScreenPos.xy * _SkillScreenSize.xy;
                float dist = distance(pixelPos, centerPx);
                float outer = smoothstep(_RadiusPx + _ThicknessPx, _RadiusPx, dist);
                float inner = smoothstep(_RadiusPx, _RadiusPx - _ThicknessPx, dist);
                float ring = saturate(outer - inner);
                float fill = smoothstep(_RadiusPx, _RadiusPx * 0.65, dist) * _FillAlpha;
                float alpha = saturate(ring * _Color.a + fill);
                if (alpha <= 0.001)
                    discard;

                return float4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
