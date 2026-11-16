// The tutorial's spotlight (TutorialSpotlight): the park goes dark except whatever the tutorial points at, lit in its
// own shape. Pass 0 draws each lit sprite's shape into a mask; pass 1 lays the dark over the finished frame, leaving
// the shapes clear and fading out softly round their edges, so the light hugs the person or thing.
Shader "Hidden/Sunrise/Tutorial Spotlight"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off // Cull Off: people face left with a negative x scale

        Pass
        {
            Name "Mask"

            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            Varyings MaskVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                return CommonUnlitVertex(input);
            }

            half MaskFragment(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a - 0.5);
                return 1;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Dim"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DimFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 _ShadeColor;
            float _Darkness;
            float2 _GlowStep; // how far the light spills round the shape, in screen UVs (across, up)

            half Mask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
            }

            half4 DimFragment(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                // Lit on the shape; round it, softer the further it is: the average of how much of it each of
                // four rings round here touches, the near rings counting most (a smooth glow, not a hard halo). The
                // mask is half size and sampled smoothly, so few samples still blur well.
                half lit = Mask(uv);
                half glow = 0;
                half total = 0;
                for (int ring = 1; ring <= 4; ring++)
                {
                    float reach = ring / 4.0;
                    half weight = 1 - reach;
                    for (int i = 0; i < 12; i++)
                    {
                        float a = (i + ring * 0.37) * (TWO_PI / 12);
                        glow += Mask(uv + float2(cos(a), sin(a)) * _GlowStep * reach) * weight;
                        total += weight;
                    }
                }
                lit = max(lit, saturate(glow / total * 2.5));
                return half4(_ShadeColor.rgb, _Darkness * (1 - smoothstep(0, 1, lit)));
            }
            ENDHLSL
        }
    }
}
