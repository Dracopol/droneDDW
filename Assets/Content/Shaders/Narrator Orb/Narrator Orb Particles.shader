// Narrator orb particles: shell, haze, dust and the four wisps from one baked quad mesh, every particle placed on the
// GPU (PARTICLE_VS and PARTICLE_FS in the web preview). Each quad is billboarded in view space at the world size of the
// preview's point. Additive; NarratorOrbMaterials sets every value each frame from the Narrator Orb Settings asset.
Shader "DroneDDW/Narrator Orb/Particles"
{
	Properties
	{
		_OrbRadius("Radius (m)", Float) = 0.12
		[HideInInspector] _OrbTime("Time", Float) = 0
		[HideInInspector] _Breathe("Breathe", Float) = 1
		[HideInInspector] _Morph("Morph", Float) = 0
		[HideInInspector] _ShapeA("Shape A", Float) = 1
		[HideInInspector] _ShapeB("Shape B", Float) = 3
		[HideInInspector] _RippleShape("Ripple shape", Vector) = (2.4, 0.32, 1.1, 0)
		[HideInInspector] _RippleAmount("Ripples", Float) = 1
		[HideInInspector] _Level("Level", Float) = 0
		[HideInInspector] _Hiss("Hiss", Float) = 0
		[HideInInspector] _Sparkle("Sparkle", Float) = 1.2
		[HideInInspector] _ShellRotation0("Shell rotation row 0", Vector) = (1, 0, 0, 0)
		[HideInInspector] _ShellRotation1("Shell rotation row 1", Vector) = (0, 1, 0, 0)
		[HideInInspector] _ShellRotation2("Shell rotation row 2", Vector) = (0, 0, 1, 0)
		[HideInInspector] _DustRotation0("Dust rotation row 0", Vector) = (1, 0, 0, 0)
		[HideInInspector] _DustRotation1("Dust rotation row 1", Vector) = (0, 1, 0, 0)
		[HideInInspector] _DustRotation2("Dust rotation row 2", Vector) = (0, 0, 1, 0)
		[HideInInspector] _NeutralDeep("Neutral deep (linear)", Vector) = (0.029556834, 0.080219820, 1, 1)
		[HideInInspector] _NeutralMid("Neutral mid (linear)", Vector) = (0.102241733, 0.238397574, 1, 1)
		[HideInInspector] _NeutralIce("Neutral ice (linear)", Vector) = (0.456411023, 0.745404210, 1, 1)
		[HideInInspector] _NeutralAccent("Neutral accent (linear)", Vector) = (1, 0.434153636, 0.822785754, 1)
		[HideInInspector] _NeutralFaint("Neutral faint (linear)", Vector) = (0.023153366, 0.045186204, 0.434153636, 1)
		[HideInInspector] _PoleDeep("Pole deep (linear)", Vector) = (0, 0, 0, 1)
		[HideInInspector] _PoleMid("Pole mid (linear)", Vector) = (0, 0, 0, 1)
		[HideInInspector] _PoleIce("Pole ice (linear)", Vector) = (0, 0, 0, 1)
		[HideInInspector] _PoleAccent("Pole accent (linear)", Vector) = (0, 0, 0, 1)
		[HideInInspector] _PoleFaint("Pole faint (linear)", Vector) = (0, 0, 0, 1)
		[HideInInspector] _BodyWeight("Body weight", Float) = 0
		[HideInInspector] _LeadWeight("Lead weight", Float) = 0
		[HideInInspector] _WispPhase("Wisp phases", Vector) = (0, 0, 0, 0)
		[HideInInspector] _WispLevel("Wisp levels", Vector) = (0.1, 0.1, 0.1, 0.1)
		[HideInInspector] _Poke("Poke (orb space, amount in w)", Vector) = (0, 0, 0, 0)
		[HideInInspector] _PokeSpeed("Poke speed", Float) = 0
		[HideInInspector] _SizeMultiplier("Size multiplier", Float) = 1
		[HideInInspector] _MinParticlePixels("Minimum particle pixels", Float) = 0
		[HideInInspector] _Intensity("Intensity", Float) = 1
		[HideInInspector] _WebBlend("Web-style blending", Float) = 1
		[HideInInspector] _AlphaFromBrightness("Alpha from brightness", Float) = 1
		[HideInInspector] _AlphaGain("Alpha gain", Float) = 1
		[HideInInspector] _AlphaSrcBlend("Alpha source blend", Float) = 1
		[HideInInspector] _AlphaDstBlend("Alpha destination blend", Float) = 1
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
			"RenderPipeline" = "UniversalPipeline"
			"IgnoreProjector" = "True"
		}

		Pass
		{
			Name "NarratorOrbParticles"
			Tags
			{
				"LightMode" = "UniversalForward"
			}

			Blend One One, [_AlphaSrcBlend] [_AlphaDstBlend]
			ZWrite Off
			ZTest LEqual
			Cull Off

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "NarratorOrbCommon.hlsl"
			#include "NarratorOrbShape.hlsl"
			#include "NarratorOrbRipple.hlsl"
			#include "NarratorOrbOutput.hlsl"

			float _Level;            // uLevel
			float _Hiss;             // uHiss
			float _Sparkle;          // uSparkle
			float4 _ShellRotation0;  // uRotShell, as rows
			float4 _ShellRotation1;
			float4 _ShellRotation2;
			float4 _DustRotation0;   // uRotDust, as rows
			float4 _DustRotation1;
			float4 _DustRotation2;
			float4 _NeutralDeep;     // uN*: the neutral palette, linear
			float4 _NeutralMid;
			float4 _NeutralIce;
			float4 _NeutralAccent;
			float4 _NeutralFaint;
			float4 _PoleDeep;        // uP*: the palette of the look being approached, linear
			float4 _PoleMid;
			float4 _PoleIce;
			float4 _PoleAccent;
			float4 _PoleFaint;
			float _BodyWeight;       // shell, haze and glow: how far the body has turned to the pole colour (uBodyW)
			float _LeadWeight;       // wisps and dust get there first, so semi steps read as two-tone (uLeadW)
			float4 _WispPhase;       // uWPhase
			float4 _WispLevel;       // uWLevel
			float4 _Poke;            // xyz: where (orb space), w: amount (uPoke)
			float _PokeSpeed;        // uPokeSpeed
			float _SizeMultiplier;
			float _MinParticlePixels;

			// The wisps' great circles (AXES) and how far each one runs (wispSpan), fixed by the seeded layout. The
			// spans are checked against the bake in the layout test.
			static const float3 WispAxes[4] =
			{
				float3(0.3, 1.0, 0.2), float3(1.0, 0.25, -0.4), float3(-0.5, 0.4, 1.0), float3(0.2, -0.8, 0.7)
			};
			static const float4 WispSpans = float4(2.46178409, 2.75801714, 2.53889972, 2.35235952);

			// A three.js point of this size spans size × (target height / 2) / depth pixels; at the preview's 40°
			// view that is a world-space diameter of size × tan 20°
			static const float PointSizeToMeters = 0.36397023;

			struct Attributes
			{
				float3 direction : POSITION;           // aDir
				float4 random : TEXCOORD0;             // aRand
				float3 radiusLayerCorner : TEXCOORD1;  // aRad, aLayer, the quad corner (0 to 3)
				float3 seed : TEXCOORD2;               // aSeed
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 color : TEXCOORD0;   // vColor
				float alpha : TEXCOORD1;    // vAlpha
				float2 sprite : TEXCOORD2;  // -1 to 1 across the quad (gl_PointCoord)
				UNITY_VERTEX_OUTPUT_STEREO
			};

			float3 Rotate(float4 row0, float4 row1, float4 row2, float3 v)
			{
				return float3(dot(row0.xyz, v), dot(row1.xyz, v), dot(row2.xyz, v));
			}

			// Pointer (later: fingertip) push, the original scatter without per-particle springs
			float3 PokeOffset(float3 p)
			{
				float amt = _Poke.w;
				if (amt <= 0.001)
				{
					return float3(0.0, 0.0, 0.0);
				}

				float3 dl = p - _Poke.xyz;
				float dd = length(dl);
				if (dd > 0.1 || dd < 1e-6)
				{
					return float3(0.0, 0.0, 0.0);
				}

				float f = PositivePow(1.0 - dd / 0.1, 2.0) * 0.07 * amt / dd;
				float k = 0.6 * f * dd / max(length(p), 1e-6);
				float sw = 1.6 + _PokeSpeed * 3.0;
				float n = 0.35 * sin(p.x * 140.0 + _OrbTime * 6.0) * cos(p.z * 140.0 - _OrbTime * 5.0);
				return float3(
					dl.x * f * (1.0 + n) + p.x * k - dl.z * f * sw,
					dl.y * f * (1.0 + n) + p.y * k + sin(_OrbTime * 4.0 + p.x * 90.0) * f * dd * 0.6,
					dl.z * f * (1.0 - n) + p.z * k + dl.x * f * sw);
			}

			Varyings Vertex(Attributes input)
			{
				Varyings output = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 aDir = input.direction;
				float4 aRand = input.random;
				float aRad = input.radiusLayerCorner.x;
				float3 aSeed = input.seed;
				int layer = (int)(input.radiusLayerCorner.y + 0.5);
				int corner = (int)(input.radiusLayerCorner.z + 0.5);

				// shell and haze follow the body weight, dust and wisps lead
				float cw = layer <= 1 ? _BodyWeight : _LeadWeight;
				float3 cDeep = lerp(_NeutralDeep.rgb, _PoleDeep.rgb, cw);
				float3 cMid = lerp(_NeutralMid.rgb, _PoleMid.rgb, cw);
				float3 cIce = lerp(_NeutralIce.rgb, _PoleIce.rgb, cw);
				float3 cAccent = lerp(_NeutralAccent.rgb, _PoleAccent.rgb, cw);
				float3 cFaint = lerp(_NeutralFaint.rgb, _PoleFaint.rgb, cw);
				float flash = PositivePow(0.5 + 0.5 * sin(_OrbTime * 29.0 + aRand.z * 60.0), 10.0);
				float3 pos = float3(0.0, 0.0, 0.0);
				float3 col = float3(0.0, 0.0, 0.0);
				float size = 0.0;
				float alpha = 0.0;
				float pokeK = 1.0;
				if (layer == 0)
				{
					float3 d0 = Rotate(_ShellRotation0, _ShellRotation1, _ShellRotation2, aDir);
					float4 rp = Ripple(d0);
					float rip = rp.w;
					float3 d = normalize(d0 + rp.xyz * _RippleAmount);
					pos = d * (_OrbRadius * aRad * ShapeMix(d) * _Breathe * (1.0 + _RippleAmount * 0.06 * rip));
					float ice = step(aRand.y, 0.1);
					col = lerp(cDeep, cMid, aRand.x);
					col = lerp(col, lerp(col, cIce, 0.7), ice);
					float ring = clamp(rip * _RippleAmount, 0.0, 1.0);
					col = lerp(col, cIce, ring * 0.7)
						* (1.0 + 0.12 * _Level + 1.4 * ring + ice * _Sparkle * _Hiss * flash * 2.0);
					size = 0.0028 * (1.0 + 0.6 * ring);
					alpha = 0.6;
				}
				else if (layer <= 2)
				{
					float3 d = Rotate(_DustRotation0, _DustRotation1, _DustRotation2, aDir);
					float sm = ShapeMix(d);
					if (layer == 1)
					{
						pos = d * (_OrbRadius * aRad * sm * _Breathe);
						col = lerp(cDeep, cMid, aRand.x) * (1.0 + 0.35 * _Level);
					}
					else
					{
						pos = d * (_OrbRadius * aRad * sm * (1.0 + 0.05 * _Level + 0.03 * _Sparkle * _Hiss * flash));
						col = lerp(cMid, cIce, aRand.x * 0.8) * (1.0 + _Sparkle * _Hiss * flash * 4.0);
					}

					size = 0.0026;
					alpha = 0.75;
				}
				else
				{
					int k = layer - 3;
					float kk = (float)k;

					// the wisp's great circle as the preview sets it up: n = AXES[k], u = up × n, v = n × u
					float3 n = normalize(WispAxes[k]);
					float3 u = cross(float3(0.0, 1.0, 0.0), n);
					if (dot(u, u) < 1e-3)
					{
						u = float3(1.0, 0.0, 0.0);
					}

					u = normalize(u);
					float3 v = cross(n, u);

					float t = frac(aSeed.x + _WispPhase[k]);
					float ang = kk * 1.7 + t * WispSpans[k];
					float taper = sin(3.14159265 * t);
					float s = aSeed.y * 0.26 * taper + 0.2 * sin(ang * 3.0 + kk + _OrbTime * 0.9);
					float3 d = normalize(u * cos(ang) + v * sin(ang) + n * s);
					float rr = 1.01 + aSeed.z * 0.035 * taper + 0.04 * sin(t * 9.0 + kk + _OrbTime * 1.4) * taper;
					pos = d * (_OrbRadius * rr * ShapeMix(d) * _Breathe);
					float lvl = _WispLevel[k];
					col = lerp(cIce, cAccent, PositivePow(t, 1.3)) * ((0.25 + 0.5 * taper) * lvl)
						+ cFaint * ((1.0 - lvl) * 0.9 * taper);
					size = 0.0036;
					alpha = 1.0;
					pokeK = 1.8;
				}

				pos += PokeOffset(pos) * pokeK;

				// Billboard: the quad lies in the view plane around the particle, sized like the preview's point
				float diameter = size * PointSizeToMeters * _SizeMultiplier;
				float3 centerVS = TransformWorldToView(TransformObjectToWorld(pos));
				float depth = max(-centerVS.z, 1e-4);

				// Anti-shimmer: a particle under the minimum pixel size is drawn at that size, fainter by the same
				// area. The projection is this eye's; [1][1] is 1 / tan(half the vertical field of view).
				float4x4 projection = UNITY_MATRIX_P;
				float pixels = diameter * abs(projection[1][1]) * 0.5 * _ScreenParams.y / depth;
				if (_MinParticlePixels > 0.0 && pixels < _MinParticlePixels)
				{
					float grow = _MinParticlePixels / max(pixels, 1e-4);
					diameter *= grow;
					alpha /= grow * grow;
				}

				float2 sprite = float2(corner == 1 || corner == 2 ? 1.0 : -1.0, corner >= 2 ? 1.0 : -1.0);
				centerVS.xy += sprite * (0.5 * diameter);
				output.positionCS = TransformWViewToHClip(centerVS);
				output.color = col;
				output.alpha = alpha;
				output.sprite = sprite;
				return output;
			}

			float4 Fragment(Varyings input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				// the original soft-dot sprite: 1 at the centre, 0.55 at r 0.35, 0 at the edge. Outside the circle it
				// adds nothing, which replaces the preview's discard.
				float r = length(input.sprite);
				float a = r < 0.35 ? lerp(1.0, 0.55, r / 0.35) : lerp(0.55, 0.0, saturate((r - 0.35) / 0.65));
				return NarratorOrbOutput(input.color, input.alpha * a);
			}
			ENDHLSL
		}
	}

	FallBack Off
}
