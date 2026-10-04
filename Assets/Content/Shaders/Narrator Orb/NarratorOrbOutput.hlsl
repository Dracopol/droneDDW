#ifndef NARRATOR_ORB_OUTPUT_INCLUDED
#define NARRATOR_ORB_OUTPUT_INCLUDED

// Every orb pass ends here. The web preview writes each fragment in sRGB (colorspace_fragment) and adds it to the
// canvas as it is, while URP adds in linear space. With web blending on, a fragment adds exactly what it adds in the
// browser: its sRGB colour, clamped like the canvas, times alpha, turned back to linear for the sRGB target.
// Colour leaves premultiplied and is blended One One, the same sum as the preview's SrcAlpha One, which frees the alpha
// channel for the alpha mode: the glow's brightness, the fragment's alpha, or nothing (set by the blend factors).
float _Intensity;
float _WebBlend;             // 1 = add what the browser adds, 0 = plain linear colour times alpha
float _AlphaFromBrightness;  // 1 = alpha is the added brightness, 0 = alpha is the fragment's alpha
float _AlphaGain;

// three.js r184 LinearToSRGB (sRGBTransferOETF)
float3 NarratorOrbEncodeLikeThree(float3 linearColor)
{
	float3 c = max(linearColor, 0.0);
	return lerp(1.055 * PositivePow(c, 0.41666) - 0.055, c * 12.92, step(c, 0.0031308));
}

// The exact sRGB decode, the inverse of what the sRGB target applies when it stores the colour
float3 NarratorOrbDecodeSrgb(float3 srgbColor)
{
	return lerp(PositivePow((srgbColor + 0.055) / 1.055, 2.4), srgbColor / 12.92, step(srgbColor, 0.04045));
}

float4 NarratorOrbOutput(float3 color, float alpha)
{
	float a = saturate(alpha);
	float3 linearAdd = color * a;
	float3 webAdd = NarratorOrbDecodeSrgb(min(NarratorOrbEncodeLikeThree(color), 1.0) * a);
	float3 added = lerp(linearAdd, webAdd, _WebBlend) * _Intensity;
	float brightness = saturate(max(added.r, max(added.g, added.b)) * _AlphaGain);
	return float4(added, lerp(a, brightness, _AlphaFromBrightness));
}

#endif
