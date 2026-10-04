// Narrator orb glow shells (GLOW_VS and GLOW_FS in the web preview): the six halos drawn from the inside (Cull Front)
// and the inner shell drawn from the outside (Cull Back). Each shell's size factor and base opacity are baked into the
// mesh's first UV; _OpacityScale animates them all. Additive.
Shader "DroneDDW/Narrator Orb/Glow"
{
	Properties
	{
		[Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 1
		_OrbRadius("Radius (m)", Float) = 0.12
		[HideInInspector] _Breathe("Breathe", Float) = 1
		[HideInInspector] _Morph("Morph", Float) = 0
		[HideInInspector] _ShapeA("Shape A", Float) = 1
		[HideInInspector] _ShapeB("Shape B", Float) = 3
		[HideInInspector] _GlowColor("Colour (linear)", Vector) = (0.068478170, 0.147027266, 1, 1)
		[HideInInspector] _OpacityScale("Opacity scale", Float) = 1
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
			Name "NarratorOrbGlow"
			Tags
			{
				"LightMode" = "UniversalForward"
			}

			Blend One One, [_AlphaSrcBlend] [_AlphaDstBlend]
			ZWrite Off
			ZTest LEqual
			Cull [_Cull]

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "NarratorOrbCommon.hlsl"
			#include "NarratorOrbShape.hlsl"
			#include "NarratorOrbOutput.hlsl"

			float4 _GlowColor;    // uColor, linear
			float _OpacityScale;  // multiplies each shell's base opacity

			struct Attributes
			{
				float3 positionOS : POSITION;
				float2 shell : TEXCOORD0;  // x: size factor (uK), y: base opacity
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float opacity : TEXCOORD0;  // uOpacity
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings Vertex(Attributes input)
			{
				Varyings output = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 d = normalize(input.positionOS);
				output.positionCS = TransformObjectToHClip(d * (_OrbRadius * input.shell.x * ShapeMix(d) * _Breathe));
				output.opacity = input.shell.y * _OpacityScale;
				return output;
			}

			float4 Fragment(Varyings input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				return NarratorOrbOutput(_GlowColor.rgb, input.opacity);
			}
			ENDHLSL
		}
	}

	FallBack Off
}
