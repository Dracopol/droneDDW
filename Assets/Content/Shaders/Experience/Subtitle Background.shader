// The subtitle strip's dark see-through background: an unlit quad that ignores depth (ZTest Always is written here,
// not a material setting, because URP's Unlit shader has no depth-test switch), so nothing in the world covers it.
// Plain HLSL with the built-in include file, so it runs in URP and with single-pass instanced stereo on Quest.
Shader "DroneDDW/Experience Subtitle Background"
{
    Properties
    {
        _Color ("Colour", Color) = (0.07, 0.08, 0.1, 0.92)
    }

    SubShader
    {
        Tags { "Queue" = "Overlay-500" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            half4 _Color;

            struct Attributes
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }

    Fallback Off
}
