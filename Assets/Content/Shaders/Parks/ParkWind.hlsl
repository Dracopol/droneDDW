#ifndef PARK_WIND_INCLUDED
#define PARK_WIND_INCLUDED

// Wind for the park's plants. ParkWind sets these every frame as globals, so the vegetation material keeps URP Lit's
// own per-material data (and the SRP Batcher). They are zero until ParkWind runs, so plants stand still otherwise.
float4 _ParkWindDirection; // xy: direction the wind blows towards on the ground (unit); z: breeze; w: sway hertz
float4 _ParkWindGust;      // xy: direction gust fronts travel (unit); z: front position along it (m); w: gust strength
float4 _ParkWindShape;     // x: bend of a 7 m tree top in a full gust (m); y: flutter (m at 1 m height); z: gust width (m)

// How far a point of a plant moves in the wind, in world space. A plant's origin is its base, so the bend grows with the
// square of the height above it: trunks stay put while crowns sway, and neighbouring plants sway a little out of step.
float3 ParkWindOffset(float3 positionWS, float3 originWS)
{
	float height = max(0.0, positionWS.y - originWS.y);
	float phase = dot(originWS.xz, float2(0.13, 0.17));
	float sway = 0.6 + 0.4 * sin(_Time.y * 6.2831853 * _ParkWindDirection.w + phase);
	float along = dot(originWS.xz, _ParkWindGust.xy) - _ParkWindGust.z;
	float width = max(1.0, _ParkWindShape.z);
	float gust = _ParkWindGust.w * exp(-along * along / (width * width));
	float strength = _ParkWindDirection.z * sway + gust;
	float relativeHeight = height / 7.0;
	float bend = _ParkWindShape.x * strength * relativeHeight * relativeHeight;
	float flutter = _ParkWindShape.y * strength * min(height, 1.0) * sin(_Time.y * 9.0 + dot(positionWS, float3(3.1, 1.7, 2.3)));
	return float3(_ParkWindDirection.x * (bend + flutter), 0.0, _ParkWindDirection.y * (bend + flutter));
}

float3 ParkWindObjectToWorld(float3 positionOS)
{
	float3 positionWS = TransformObjectToWorld(positionOS);
	return positionWS + ParkWindOffset(positionWS, TransformObjectToWorld(float3(0.0, 0.0, 0.0)));
}

float4 ParkWindObjectToHClip(float3 positionOS)
{
	return TransformWorldToHClip(ParkWindObjectToWorld(positionOS));
}

VertexPositionInputs ParkWindVertexPositionInputs(float3 positionOS)
{
	return GetVertexPositionInputs(TransformWorldToObject(ParkWindObjectToWorld(positionOS)));
}

#endif
