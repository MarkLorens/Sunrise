// See-through hole for the sprite shader (SpriteLit3D, via a Custom Function node).
// SeeThrough.cs sets these every frame from the camera:
//   _SeeThroughPos       xyz = the middle of whoever you control (world space), w = hole radius (world units)
//   _SeeThroughSoftness  how much of the radius is a soft, dithered edge (0..1)
//   _SeeThroughStrength  0 = off, 1 = full hole (fades in and out)
//   _SeeThroughFrame     frame counter (0..63): the dither pattern moves every frame, and the camera's temporal
//                        anti-aliasing (CameraRig) averages those frames into a smooth fade.
// A pixel is cut when it is (1) inside the circle around the player as seen from the camera, (2) closer to the
// camera than the player, and (3) above the floor. The cut is a 4x4 dither pattern on top of alpha clipping, so
// it stays opaque and keeps writing depth (no sorting problems), like the rest of the park's sprites.
#ifndef SUNRISE_SEE_THROUGH_INCLUDED
#define SUNRISE_SEE_THROUGH_INCLUDED

float4 _SeeThroughPos;
float _SeeThroughSoftness;
float _SeeThroughStrength;
float _SeeThroughFrame;

// Cut: the material's "See Through Cut" switch (1 = props and scenery, 0 = characters, who never get a hole).
void SeeThroughAlpha_float(float Alpha, float3 PositionWS, float Cut, out float Out)
{
    Out = Alpha;
#if !defined(SHADERGRAPH_PREVIEW)
    // The moon's shadow pass runs this too, from the light's point of view: a tree's shadow keeps its full shape.
    #if defined(SHADERPASS) && defined(SHADERPASS_SHADOWCASTER) && (SHADERPASS == SHADERPASS_SHADOWCASTER)
    return;
    #endif
    if (Cut <= 0.0 || _SeeThroughStrength <= 0.0) return;
    if (PositionWS.y < 0.02) return; // the floor itself is never cut

    // Both points in view space; dividing x and y by the distance gives where they land on screen,
    // independent of aspect ratio and render-target flips.
    float3 fragV = TransformWorldToView(PositionWS);
    float3 playerV = TransformWorldToView(_SeeThroughPos.xyz);
    float fragDepth = -fragV.z;
    float playerDepth = max(-playerV.z, 0.01);
    // Cards stand upright while the camera looks down at them, so the top of a card is nearer the camera than its
    // feet. Compare how far along the ground they stand instead: a whole card is one ground distance away, so the
    // player's own card (and anyone standing level with them, or behind them) is never cut, only cards standing
    // closer to the camera.
    float3 forward = GetViewForwardDir();
    float2 ground = normalize(forward.xz + float2(0.0, 1e-5));
    float fragGround = dot(PositionWS.xz - _WorldSpaceCameraPos.xz, ground);
    float playerGround = dot(_SeeThroughPos.xz - _WorldSpaceCameraPos.xz, ground);
    if (fragGround >= playerGround - 0.1) return;

    float2 offset = fragV.xy / max(fragDepth, 0.01) - playerV.xy / playerDepth;
    float radius = _SeeThroughPos.w / playerDepth;
    float dist = length(offset) / max(radius, 1e-4);           // 0 in the middle, 1 at the edge
    float soft = max(_SeeThroughSoftness, 1e-3);
    float hole = (1.0 - smoothstep(1.0 - soft, 1.0, dist)) * _SeeThroughStrength;

    // Interleaved gradient noise: a per-pixel threshold with no visible grid, shifted every frame. The stronger the
    // hole, the more pixels drop out; temporal AA blends the frames, so the edge reads as a soft fade, not dots.
    float4 clip = TransformWorldToHClip(PositionWS);
    float2 pixel = floor((clip.xy / clip.w * 0.5 + 0.5) * _ScreenParams.xy) + 5.588238 * _SeeThroughFrame;
    float threshold = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
    if (hole > threshold) Out = 0.0;
#endif
}

void SeeThroughAlpha_half(half Alpha, half3 PositionWS, half Cut, out half Out)
{
    float o;
    SeeThroughAlpha_float(Alpha, PositionWS, Cut, o);
    Out = o;
}

#endif
