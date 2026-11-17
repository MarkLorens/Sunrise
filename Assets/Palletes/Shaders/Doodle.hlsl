// The sprite doodle effect (Alan Zucconi, "Sprite Doodle Shader Effect", 2019) for the sprite Shader Graph
// (SpriteLit3D, through a Custom Function node on the vertex position). Every vertex is nudged by a random amount
// that only changes PerSecond times a second, so the outlines "boil" like a drawing redrawn by hand.
//   Strength:  how far a vertex may move, in world units, across the screen (0 = off)
//   PerSecond: how many times a second the drawing is redrawn
// The nudge comes from the vertex's own position plus the snapped time, as in the article, so the corners a tile
// shares with its neighbours move together and the floor stays seamless. DoodleSettings sets both per group.
#ifndef SUNRISE_DOODLE_INCLUDED
#define SUNRISE_DOODLE_INCLUDED

// Nikita Miropolskiy's random3 (the one the article uses): a pseudo-random vector in [-0.5, 0.5] for a point.
float3 DoodleRandom3(float3 c)
{
    float j = 4096.0 * sin(dot(c, float3(17.0, 59.4, 15.0)));
    float3 r;
    r.z = frac(512.0 * j);
    j *= 0.125;
    r.x = frac(512.0 * j);
    j *= 0.125;
    r.y = frac(512.0 * j);
    return r - 0.5;
}

// The article's snap: x rounded to the nearest multiple of snap.
float DoodleSnap(float x, float snap)
{
    return snap * round(x / snap);
}

void Doodle_float(float3 PositionOS, float Strength, float PerSecond, out float3 Out)
{
    Out = PositionOS;
#if !defined(SHADERGRAPH_PREVIEW)
    if (Strength <= 0.0 || PerSecond <= 0.0) return;
    float time = DoodleSnap(_Time.y, 1.0 / PerSecond);
    float2 nudge = DoodleRandom3(PositionOS + time).xy * 2.0 * Strength;
    // Across the screen (the camera's right and up) whatever way the sprite faces, turned into the sprite's own
    // space with its scale undone, so Strength means the same on a tiny spirit and a big Ferris wheel.
    float3 nudgeWS = UNITY_MATRIX_V[0].xyz * nudge.x + UNITY_MATRIX_V[1].xyz * nudge.y;
    Out = PositionOS + TransformWorldToObjectDir(nudgeWS, false);
#endif
}

void Doodle_half(half3 PositionOS, half Strength, half PerSecond, out half3 Out)
{
    float3 o;
    Doodle_float(PositionOS, Strength, PerSecond, o);
    Out = o;
}

#endif
