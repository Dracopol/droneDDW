// Narrator orb ripple rings (RING_VS and RING_FS in the web preview): a shell just outside the particles that lights up
// where the syllable rings pass, worked out per pixel. Additive.
Shader "DroneDDW/Narrator Orb/Ripple Rings"
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
		[HideInInspector] _GlowColor("Colour (linear)", Vector) = (0.456411023, 0.745404210, 1, 1)
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
			Name "NarratorOrbRippleRings"
			Tags
			{
				"LightMode" = "UniversalForward"
			}

			Blend One One, [_AlphaSrcBlend] [_AlphaDstBlend]
			ZWrite Off
			ZTest LEqual
			Cull Back

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

			float4 _GlowColor;  // uColor, linear

			struct Attributes
			{
				float3 positionOS : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 direction : TEXCOORD0;  // vDir
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings Vertex(Attributes input)
			{
				Varyings output = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 vDir = normalize(input.positionOS);
				output.positionCS = TransformObjectToHClip(vDir * (_OrbRadius * 1.012 * ShapeMix(vDir) * _Breathe));
				output.direction = vDir;
				return output;
			}

			float4 Fragment(Varyings input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				float k = clamp(Ripple(normalize(input.direction)).w * _RippleAmount, 0.0, 1.5);
				return NarratorOrbOutput(_GlowColor.rgb, k * 0.3);
			}
			ENDHLSL
		}
	}

	FallBack Off
}
