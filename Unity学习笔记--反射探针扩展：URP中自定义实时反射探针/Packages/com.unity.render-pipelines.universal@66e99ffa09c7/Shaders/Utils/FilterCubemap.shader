Shader "Hidden/Universal Render Pipeline/FilterCubemap"
{
    Properties
    {
        _SourceCubemap ("Source Cubemap", Cube) = "" {}
        _InvOmegaP ("Inv Omega P", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        
        Pass
        {
            Name "FilterCubemapGGX"
            ZTest Always
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/FilterCubemap.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/Sampling.hlsl"
            
            TEXTURECUBE(_SourceCubemap);
            SAMPLER(s_trilinear_clamp_sampler);
            
            float _InvOmegaP;
            float _MipLevel; // 当前要生成的 mip 级别 (1-6)
            float _FaceIndex; // 当前渲染的 cubemap 面索引 (0-5)
            
            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                // 使用全屏三角形
                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);
                
                output.positionCS = pos;
                output.uv = uv;
                
                return output;
            }
            
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // 从 UV 坐标重建 cubemap 方向
                float2 positionNVC = input.uv * 2.0 - 1.0;
                
                // 使用面索引和 UV 坐标重建方向
                uint faceId = (uint)_FaceIndex;
                float3 N = CubemapTexelToDirection(positionNVC, faceId);
                
                // 计算粗糙度（从 mip 级别映射到粗糙度）
                // mipLevel 1-6 对应不同的粗糙度级别
                float perceptualRoughness = MipmapLevelToPerceptualRoughness(_MipLevel);
                float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
                
                // 对于 cubemap 过滤，我们假设 V == N（视角方向等于法线方向）
                float3 V = N;
                
                // 使用静态采样版本的 IntegrateLD
                // mipLevelIndex = _MipLevel - 1 (因为 mipLevel 1-6 对应 index 0-5)
                // 固定使用 34 个样本
                uint mipLevelIndex = (uint)_MipLevel - 1;
                float4 result = IntegrateLD_StaticSamples(
                    TEXTURECUBE_ARGS(_SourceCubemap, s_trilinear_clamp_sampler),
                    V,
                    N,
                    roughness,
                    _InvOmegaP,
                    mipLevelIndex
                );

                return result;
            }
            
            ENDHLSL
        }
    }
}
