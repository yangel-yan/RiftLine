Shader "Riftline/Regional Sky"
{
    Properties
    {
        _TexA ("Current Environment", Cube) = "" {}
        _TexB ("Next Environment", Cube) = "" {}
        _Blend ("Region Blend", Range(0,1)) = 0
        _Exposure ("Exposure", Range(0,4)) = 1
        _Rotation ("Rotation", Range(0,360)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURECUBE(_TexA); SAMPLER(sampler_TexA);
            TEXTURECUBE(_TexB); SAMPLER(sampler_TexB);
            CBUFFER_START(UnityPerMaterial)
            float _Blend, _Exposure, _Rotation;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float angle = radians(_Rotation);
                float s, c; sincos(angle, s, c);
                float3 position = input.positionOS.xyz;
                float3 rotated = float3(c*position.x-s*position.z, position.y, s*position.x+c*position.z);
                output.positionCS = TransformObjectToHClip(rotated);
                output.direction = position;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 a = SAMPLE_TEXTURECUBE(_TexA, sampler_TexA, input.direction).rgb;
                half3 b = SAMPLE_TEXTURECUBE(_TexB, sampler_TexB, input.direction).rgb;
                return half4(lerp(a,b,_Blend)*_Exposure,1);
            }
            ENDHLSL
        }
    }
}
