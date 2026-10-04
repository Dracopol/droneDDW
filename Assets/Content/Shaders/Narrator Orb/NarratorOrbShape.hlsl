#ifndef NARRATOR_ORB_SHAPE_INCLUDED
#define NARRATOR_ORB_SHAPE_INCLUDED

// The orb's shapes as radial functions: a radius multiplier for each direction from the centre (SHAPE_GLSL in the web
// preview). Ids follow NarratorOrbShape: 0 sphere, 1 bloom, 2 pebble, 3 cube, 4 crystal, 5 star. A shape baked from a
// mesh (a direction to radius lookup) would be one more id here and one more branch in ShapeRadius. Every pow base
// below is already zero or more; PositivePow (pow of abs, from the URP core library) only spares the compiler warning.
float _Morph;   // -1 = full A, 0 = neutral sphere, +1 = full B (uMorph)
float _ShapeA;  // uShapeA
float _ShapeB;  // uShapeB

static const float NarratorOrbPhi = 1.61803399;
static const float3 NarratorOrbIcosahedron[12] =
{
	float3(0.0, 1.0, NarratorOrbPhi), float3(0.0, -1.0, NarratorOrbPhi),
	float3(0.0, 1.0, -NarratorOrbPhi), float3(0.0, -1.0, -NarratorOrbPhi),
	float3(1.0, NarratorOrbPhi, 0.0), float3(-1.0, NarratorOrbPhi, 0.0),
	float3(1.0, -NarratorOrbPhi, 0.0), float3(-1.0, -NarratorOrbPhi, 0.0),
	float3(NarratorOrbPhi, 0.0, 1.0), float3(-NarratorOrbPhi, 0.0, 1.0),
	float3(NarratorOrbPhi, 0.0, -1.0), float3(-NarratorOrbPhi, 0.0, -1.0)
};

float ShapeRadius(float3 d, float s)
{
	// sphere
	if (s < 0.5)
	{
		return 1.0;
	}

	// bloom, five petals facing the viewer (+Z)
	if (s < 1.5)
	{
		float ph = atan2(d.y, d.x + 1e-6);
		float side = 1.0 - d.z * d.z;
		float p = 0.5 + 0.5 * cos(5.0 * ph - 7.8539816);
		return (0.93 + (0.3 * p * p + 0.04 * sin(3.0 * ph + 2.0 * d.z)) * side) * (1.0 - 0.18 * d.z * d.z);
	}

	// pebble
	if (s < 2.5)
	{
		float3 q = d / float3(1.2, 0.74, 1.02);
		return rsqrt(dot(q, q)) + 0.03 * sin(5.0 * d.x + 3.0 * d.z);
	}

	// cube
	if (s < 3.5)
	{
		float3 a = PositivePow(abs(d) + 1e-5, 7.0);
		return 0.84 * PositivePow(a.x + a.y + a.z, -1.0 / 7.0);
	}

	// crystal
	if (s < 4.5)
	{
		float3 a = PositivePow(abs(float3(d.x, d.y * 0.86, d.z)) + 1e-5, 1.25);
		return 1.12 * PositivePow(a.x + a.y + a.z, -0.8);
	}

	// star
	float m = 0.0;
	for (int i = 0; i < 12; i++)
	{
		m = max(m, dot(d, NarratorOrbIcosahedron[i]) * 0.5257311);
	}

	return 0.86 + 0.5 * PositivePow(m, 14.0);
}

float ShapeMix(float3 d)
{
	float wA = clamp(-_Morph, 0.0, 1.0);
	float wB = clamp(_Morph, 0.0, 1.0);
	float r = 1.0;
	if (wA > 0.0)
	{
		r += wA * (ShapeRadius(d, _ShapeA) - 1.0);
	}

	if (wB > 0.0)
	{
		r += wB * (ShapeRadius(d, _ShapeB) - 1.0);
	}

	return r;
}

#endif
