using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// 反射探针阴影贴图分辨率选项
    /// 包含FromAsset选项（使用UniversalRenderPipelineAsset中的配置）和ShadowResolution的所有值
    /// </summary>
    public enum ReflectionProbeShadowResolution
    {
        /// <summary>
        /// 使用UniversalRenderPipelineAsset中的配置
        /// </summary>
        [InspectorName("From Asset")]
        FromAsset = -1,

        /// <summary>
        /// Use this for 256x256 shadow resolution.
        /// </summary>
        _256 = 256,

        /// <summary>
        /// Use this for 512x512 shadow resolution.
        /// </summary>
        _512 = 512,

        /// <summary>
        /// Use this for 1024x1024 shadow resolution.
        /// </summary>
        _1024 = 1024,

        /// <summary>
        /// Use this for 2048x2048 shadow resolution.
        /// </summary>
        _2048 = 2048,

        /// <summary>
        /// Use this for 4096x4096 shadow resolution.
        /// </summary>
        _4096 = 4096,

        /// <summary>
        /// Use this for 8192x8192 shadow resolution.
        /// </summary>
        _8192 = 8192,
    }

    /// <summary>
    /// 为反射探针提供额外的渲染数据
    /// 包含每个探针自己管理的实时纹理 RTHandle
    /// </summary>
    [RequireComponent(typeof(ReflectionProbe))]
    [DisallowMultipleComponent]
    public class UniversalAdditionalReflectionProbeData : MonoBehaviour
    {
        private RTHandle m_CustomRealtimeTexture;

        [SerializeField]
        private ReflectionProbeRefreshMode m_CustomRefreshMode = ReflectionProbeRefreshMode.OnAwake;

        [SerializeField]
        private bool m_RenderMainLightShadows = true;

        [SerializeField]
        private ReflectionProbeShadowResolution m_MainLightShadowmapResolution = ReflectionProbeShadowResolution.FromAsset;

        [SerializeField]
        private bool m_RenderAdditionalLightShadows = false;

        [SerializeField]
        private ReflectionProbeShadowResolution m_AdditionalLightShadowmapResolution = ReflectionProbeShadowResolution.FromAsset;

        [SerializeField]
        private bool m_DrawGizmosPass = false;

        [SerializeField]
        private bool m_FinalBlitPass = false;

        [SerializeField]
        private bool m_SSAO = false;

        /// <summary>
        /// 获取或设置自定义实时纹理
        /// </summary>
        public RTHandle customRealtimeTexture
        {
            get => m_CustomRealtimeTexture;
            set => m_CustomRealtimeTexture = value;
        }

        /// <summary>
        /// 获取或设置自定义刷新模式
        /// 此模式用于控制反射探针的更新行为，替代 ReflectionProbe 原生的 refreshMode
        /// </summary>
        public ReflectionProbeRefreshMode customRefreshMode
        {
            get => m_CustomRefreshMode;
            set => m_CustomRefreshMode = value;
        }

        /// <summary>
        /// 获取或设置是否渲染主光源阴影
        /// </summary>
        public bool renderMainLightShadows
        {
            get => m_RenderMainLightShadows;
            set => m_RenderMainLightShadows = value;
        }

        /// <summary>
        /// 获取或设置主光源阴影贴图分辨率
        /// </summary>
        public ReflectionProbeShadowResolution mainLightShadowmapResolution
        {
            get => m_MainLightShadowmapResolution;
            set => m_MainLightShadowmapResolution = value;
        }

        /// <summary>
        /// 获取主光源阴影贴图分辨率（int值）
        /// 如果设置为FromAsset，则返回UniversalRenderPipelineAsset中的配置值
        /// </summary>
        /// <returns>阴影贴图分辨率</returns>
        public int GetMainLightShadowmapResolution()
        {
            if (m_MainLightShadowmapResolution == ReflectionProbeShadowResolution.FromAsset)
            {
                var asset = UniversalRenderPipeline.asset;
                return asset != null ? asset.mainLightShadowmapResolution : 512;
            }
            return (int)m_MainLightShadowmapResolution;
        }

        /// <summary>
        /// 获取或设置是否渲染附加光源阴影
        /// </summary>
        public bool renderAdditionalLightShadows
        {
            get => m_RenderAdditionalLightShadows;
            set => m_RenderAdditionalLightShadows = value;
        }

        /// <summary>
        /// 获取或设置附加光源阴影贴图分辨率
        /// </summary>
        public ReflectionProbeShadowResolution additionalLightShadowmapResolution
        {
            get => m_AdditionalLightShadowmapResolution;
            set => m_AdditionalLightShadowmapResolution = value;
        }

        /// <summary>
        /// 获取附加光源阴影贴图分辨率（int值）
        /// 如果设置为FromAsset，则返回UniversalRenderPipelineAsset中的配置值
        /// </summary>
        /// <returns>阴影贴图分辨率</returns>
        public int GetAdditionalLightShadowmapResolution()
        {
            if (m_AdditionalLightShadowmapResolution == ReflectionProbeShadowResolution.FromAsset)
            {
                var asset = UniversalRenderPipeline.asset;
                return asset != null ? asset.additionalLightsShadowmapResolution : 512;
            }
            return (int)m_AdditionalLightShadowmapResolution;
        }

        /// <summary>
        /// 获取或设置是否启用 Draw Gizmos Pass
        /// </summary>
        public bool drawGizmosPass
        {
            get => m_DrawGizmosPass;
            set => m_DrawGizmosPass = value;
        }

        /// <summary>
        /// 获取或设置是否启用 Final Blit Pass
        /// </summary>
        public bool finalBlitPass
        {
            get => m_FinalBlitPass;
            set => m_FinalBlitPass = value;
        }

        /// <summary>
        /// 获取或设置是否启用 SSAO
        /// </summary>
        public bool SSAO
        {
            get => m_SSAO;
            set => m_SSAO = value;
        }

        /// <summary>
        /// 确保实时纹理已创建
        /// </summary>
        /// <param name="probe">反射探针</param>
        public void EnsureRealtimeTexture(ReflectionProbe probe)
        {
            if (probe == null)
                return;

            // 创建 Cubemap RenderTexture 描述符
            var descriptor = new RenderTextureDescriptor(probe.resolution, probe.resolution);
            var asset = UniversalRenderPipeline.asset;
            descriptor.graphicsFormat = UniversalRenderPipeline.MakeRenderTextureGraphicsFormat(
                probe.hdr && asset != null && asset.supportsHDR,
                asset != null ? asset.hdrColorBufferPrecision : HDRColorBufferPrecision._32Bits,
                Graphics.preserveFramebufferAlpha);
            descriptor.dimension = TextureDimension.Cube;
            descriptor.volumeDepth = 1;
            descriptor.useMipMap = true;
            descriptor.msaaSamples = 1;
            descriptor.enableRandomWrite = false;
            descriptor.bindMS = false;
            descriptor.useDynamicScale = false;
            descriptor.depthBufferBits = 0;
            descriptor.stencilFormat = GraphicsFormat.None;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            descriptor.autoGenerateMips = false;

            // 使用 RenderingUtils.ReAllocateHandleIfNeeded 创建 RTHandle（符合 URP 代码风格）
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_CustomRealtimeTexture, descriptor, FilterMode.Trilinear, TextureWrapMode.Clamp, 1, 0, name : $"ReflectionProbe_{probe.GetInstanceID()}_RealtimeTexture");
        }

        /// <summary>
        /// 释放实时纹理
        /// </summary>
        public void ReleaseRealtimeTexture()
        {
            if (m_CustomRealtimeTexture != null && m_CustomRealtimeTexture.rt != null)
            {
                // 释放到资源池里
                TextureDesc currentRTDesc = RTHandleResourcePool.CreateTextureDesc(m_CustomRealtimeTexture.rt.descriptor, TextureSizeMode.Explicit, m_CustomRealtimeTexture.rt.anisoLevel, m_CustomRealtimeTexture.rt.mipMapBias, m_CustomRealtimeTexture.rt.filterMode, m_CustomRealtimeTexture.rt.wrapMode, m_CustomRealtimeTexture.name);
                RenderingUtils.AddStaleResourceToPoolOrRelease(currentRTDesc, m_CustomRealtimeTexture);
                m_CustomRealtimeTexture = null;
            }
        }

        private void OnDestroy()
        {
            ReleaseRealtimeTexture();
        }

        private void OnDisable()
        {
            // 可选：在禁用时释放纹理以节省内存
            // ReleaseRealtimeTexture();
        }
    }

    /// <summary>
    /// ReflectionProbe 的扩展方法
    /// </summary>
    public static class ReflectionProbeExtensions
    {
        /// <summary>
        /// 获取或创建反射探针的额外数据组件
        /// </summary>
        /// <param name="probe">反射探针</param>
        /// <returns>额外数据组件</returns>
        public static UniversalAdditionalReflectionProbeData GetUniversalAdditionalReflectionProbeData(this ReflectionProbe probe)
        {
            if (probe == null)
                return null;

            var gameObject = probe.gameObject;
            if (!gameObject.TryGetComponent<UniversalAdditionalReflectionProbeData>(out var probeData))
            {
                probeData = gameObject.AddComponent<UniversalAdditionalReflectionProbeData>();
            }

            return probeData;
        }
    }
}
