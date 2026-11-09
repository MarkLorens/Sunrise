// The edge round things the player can act on (ActionOutline). Pass 0 draws each outlined sprite into a mask, storing
// how far it is from the camera; pass 1 lays the colour over the finished frame wherever the mask is just outside a
// sprite, so sprites stacked on one another (a person's line art and colour layers) get one outline round the lot.
// Where something stands in front of the outlined sprite (the person you're in, walking past it), the scene's depth
// there is nearer than the sprite's and the outline is left out, so it goes behind them like the sprite does.
Shader "Hidden/Sunrise/Action Outline"
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

            // Distance from the camera; 0 (the cleared mask) means no sprite here.
            float MaskFragment(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a - 0.5);
                return LinearEyeDepth(input.positionCS.z, _ZBufferParams);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment OutlineFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // How much nearer something must be to hide the outline: enough that the ground at an outlined
            // person's feet doesn't, small enough that someone standing in front of them does.
            #define DEPTH_SLACK 0.1

            half4 _OutlineColor;
            float2 _OutlineStep; // the outline's width in screen UVs, across and up

            // Point sampled: blending a sprite's depth with the empty 0 around it would make up depths.
            float Mask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r;
            }

            float Nearer(float a, float b)
            {
                return a == 0 ? b : b == 0 ? a : min(a, b);
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                if (Mask(uv) > 0) return 0; // on the sprite itself

                // The nearest outlined sprite close by. Two rings of samples, so thin bits (an arm, a pole) still
                // get a solid edge.
                float sprite = 0;
                for (int i = 0; i < 16; i++)
                {
                    float a = i * (TWO_PI / 16);
                    float2 step = float2(cos(a), sin(a)) * _OutlineStep;
                    sprite = Nearer(sprite, Nearer(Mask(uv + step), Mask(uv + step * 0.5)));
                }
                if (sprite == 0) return 0;

                float scene = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                if (scene < sprite - DEPTH_SLACK) return 0; // someone's in front of it here
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
