#ifndef NARRATOR_ORB_RIPPLE_INCLUDED
#define NARRATOR_ORB_RIPPLE_INCLUDED

#include "NarratorOrbCommon.hlsl"

// Syllable ripples: rings that travel over the surface from a point near the front (RIPPLE_GLSL in the web preview).
// .w is the ring strength; .xyz pushes particles along the surface, away from the origin, so they bunch up at the
// leading edge (bright) and thin out behind it (dark).
#define NARRATOR_ORB_RIPPLE_COUNT 6
float4 _RippleOrigins[NARRATOR_ORB_RIPPLE_COUNT];   // xyz: start direction (orb space), w: start time (uRip)
float _RippleStrengths[NARRATOR_ORB_RIPPLE_COUNT];  // 0 = slot unused (uRipS)
float4 _RippleShape;                                // x: speed (rad/s), y: width (rad), z: life (s) (uRipShape)
float _RippleAmount;                                // reaction.ripples (uRipAmp)

float4 Ripple(float3 d)
{
	float4 acc = float4(0.0, 0.0, 0.0, 0.0);
	for (int i = 0; i < NARRATOR_ORB_RIPPLE_COUNT; i++)
	{
		float s = _RippleStrengths[i];
		float age = _OrbTime - _RippleOrigins[i].w;
		if (s <= 0.0 || age < 0.0 || age > _RippleShape.z)
		{
			continue;
		}

		float3 o = _RippleOrigins[i].xyz;
		float c = clamp(dot(d, o), -1.0, 1.0);
		float x = (acos(c) - age * _RippleShape.x) / _RippleShape.y;
		float fade = 1.0 - age / _RippleShape.z;
		float k = exp(-x * x) * fade * fade * s;
		float3 t = d - o * c;
		float tl = length(t);
		if (tl > 1e-4)
		{
			acc.xyz += t * (k * 0.15 / tl);
		}

		acc.w += k;
	}

	return acc;
}

#endif
