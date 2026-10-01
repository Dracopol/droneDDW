#ifndef PARK_WATER_INCLUDED
#define PARK_WATER_INCLUDED

// Moving water for the park's ponds. ParkWater sets these every frame as globals, so the water material keeps URP Lit's
// own per-material data (and the SRP Batcher). With nothing set, the water lies still.
#define PARK_RIPPLE_COUNT 8
float4 _ParkRipples[PARK_RIPPLE_COUNT];         // xyz: where a ripple started (world), w: when (shader time)
float _ParkRippleStrengths[PARK_RIPPLE_COUNT];  // 0 = slot unused
float4 _ParkWaterShape;                         // x: ring speed (m/s), y: ring lifetime (s), z: ring wavelength (m), w: waves

// The surface's slope at a point: three crossing trains of small waves, plus expanding rings where things touched it
float2 ParkWaterSlope(float3 positionWS)
{
	float2 p = positionWS.xz;
	float t = _Time.y;
	float2 slope = float2(0.0, 0.0);
	slope += float2(0.80, 0.30) * cos(dot(p, float2(1.6, 0.6)) + t * 1.3) * 0.035;
	slope += float2(-0.40, 0.90) * cos(dot(p, float2(-0.9, 2.1)) + t * 1.7) * 0.025;
	slope += float2(0.25, -0.95) * cos(dot(p, float2(0.7, -2.9)) + t * 2.3) * 0.018;
	slope *= _ParkWaterShape.w;

	float lifetime = max(0.01, _ParkWaterShape.y);
	float waveNumber = 6.2831853 / max(0.01, _ParkWaterShape.z);
	for (int i = 0; i < PARK_RIPPLE_COUNT; i++)
	{
		float age = t - _ParkRipples[i].w;
		float strength = _ParkRippleStrengths[i];
		if (strength <= 0.0 || age < 0.0 || age > lifetime)
		{
			continue;
		}

		float2 offset = p - _ParkRipples[i].xz;
		float distance = length(offset);
		float x = distance - age * _ParkWaterShape.x;
		float packet = exp(-x * x * 8.0) * cos(x * waveNumber);
		float fade = strength * (1.0 - age / lifetime) / (1.0 + distance);
		slope += offset / max(distance, 0.001) * packet * fade * 0.5;
	}

	return slope;
}

// Wraps URP's lighting: tilts flat water's normal by the slope, and runs bright streaks down the fountain's sheets
half4 ParkWaterFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
	if (inputData.normalWS.y > 0.6)
	{
		float2 slope = ParkWaterSlope(inputData.positionWS);
		inputData.normalWS = normalize(inputData.normalWS + float3(-slope.x, 0.0, -slope.y));
	}
	else
	{
		float streak = frac(inputData.positionWS.y * 3.0 + _Time.y * 2.5 + dot(inputData.positionWS.xz, float2(5.3, 4.1)));
		surfaceData.albedo *= 0.8 + 0.45 * smoothstep(0.25, 0.75, streak);
	}

	return UniversalFragmentPBR(inputData, surfaceData);
}

#endif
