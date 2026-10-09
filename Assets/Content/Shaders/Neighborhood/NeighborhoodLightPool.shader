// The soft glow a street lamp throws on the ground: an unlit, additive quad lying on the paving.
// Plain HLSL with the built-in include file, so it runs in URP and in the built-in pipeline, and with
// single-pass instanced stereo on Quest. Fades with fog like the rest of the street.
Shader "DroneDDW/Neighborhood Light Pool"
{
    Properties
    {
        _MainTex ("Falloff", 2D) = "white" {}
        // A Vector, not a Color: Unity would treat a Color as gamma and convert it in linear projects.
        _Color ("Colour (linear, additive)", Vector) = (0.22, 0.14, 0.07, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-20" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = tex2D(_MainTex, input.uv).r * _Color;
                color.a = 1;
                UNITY_APPLY_FOG_COLOR(input.fogCoord, color, half4(0, 0, 0, 0));
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
