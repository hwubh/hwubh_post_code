using UnityEditor.Sprites;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;

public class NativePluginVRSFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Header("Pipeline Shading Rate")]
        public bool enablePipelineVRS = true;
        public PipelineShadingRate rate = PipelineShadingRate.Rate2x2;
        public PipelineShadingRateCombiner combiner0 = PipelineShadingRateCombiner.Passthrough;
        public PipelineShadingRateCombiner combiner1 = PipelineShadingRateCombiner.Passthrough;

        [Header("Injection Points")]
        [Tooltip("在哪里设置 pipeline shading rate")]
        public RenderPassEvent injectPoint = RenderPassEvent.BeforeRenderingOpaques;

        [Header("Attachment Shading Rate (SRI)")]
        public bool enableAttachmentVRS = true;
        [Tooltip("在哪里绑定 SRI（attachment shading rate 生效起点）")]
        public RenderPassEvent setShadingRateImageInjectPoint = RenderPassEvent.BeforeRenderingOpaques;
        [Tooltip("在哪里解除 SRI（attachment shading rate 生效终点）")]
        public RenderPassEvent resetShadingRateImageInjectPoint = RenderPassEvent.BeforeRenderingPostProcessing;

        [Range(0.25f, 1.0f)] public float jndSensitivity = 0.5f;   // JND 灵敏度（Intel 实测 0.25/0.5/0.75 三档，越小越保守）
        public float jndEnvLuma = 0.1f;                            // 环境亮度补偿
        public float jndQuarterRateK = 2.0f;                       // 1/4 档放宽系数 K（Yang 2019）

        public ComputeShader sriComputeShader;
    }

    public Settings settings = new Settings();

    private SetPipelineShadingRatePass m_SetPipelineShadingRatePass;
    private SetShadingRateImagePass m_SetShadingRateImagePass;
    private ResetShadingRateImagePass m_ResetShadingRateImagePass;

    public override void Create()
    {
        m_SetPipelineShadingRatePass = new SetPipelineShadingRatePass();
        m_SetPipelineShadingRatePass.renderPassEvent = settings.injectPoint;

        m_SetShadingRateImagePass = new SetShadingRateImagePass();
        m_SetShadingRateImagePass.renderPassEvent = settings.setShadingRateImageInjectPoint;

        m_ResetShadingRateImagePass = new ResetShadingRateImagePass();
        m_ResetShadingRateImagePass.renderPassEvent = settings.resetShadingRateImageInjectPoint;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        if (settings.enablePipelineVRS && NativePluginBridge.IsPipelineVRSSupportedQuery())
        {
            // 配置 Pass：把当前 rate/combiner 打包进 data
            int packed = NativePluginBridge.PackRate(
                (int)settings.rate, (int)settings.combiner0, (int)settings.combiner1);
            m_SetPipelineShadingRatePass.Setup(packed);

            renderer.EnqueuePass(m_SetPipelineShadingRatePass);
        }

        if (settings.enableAttachmentVRS)
        {
            // 保证 reset 晚于 set，否则 SRI 的生效范围为空
            if (m_ResetShadingRateImagePass.renderPassEvent <= m_SetShadingRateImagePass.renderPassEvent)
                m_ResetShadingRateImagePass.renderPassEvent = m_SetShadingRateImagePass.renderPassEvent;

            m_SetShadingRateImagePass.Setup(settings.jndSensitivity, settings.jndEnvLuma, settings.jndQuarterRateK, settings.sriComputeShader);
            m_SetShadingRateImagePass.ConfigureInput(ScriptableRenderPassInput.Motion);
            renderer.EnqueuePass(m_SetShadingRateImagePass);
            renderer.EnqueuePass(m_ResetShadingRateImagePass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        m_SetPipelineShadingRatePass = null;
        m_SetShadingRateImagePass = null;
        m_ResetShadingRateImagePass = null;
    }

    /// <summary>
    /// 设置 pipeline shading rate 的 Pass。
    /// </summary>
    internal class SetPipelineShadingRatePass : ScriptableRenderPass
    {
        private int m_Packed;

        public SetPipelineShadingRatePass()
        {
            profilingSampler = new ProfilingSampler(nameof(SetPipelineShadingRatePass));
        }

        public void Setup(int packed)
        {
            m_Packed = packed;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var ptr = NativePluginBridge.RenderEventAndData;
            if (ptr == System.IntPtr.Zero)
                return;

            var cmd = CommandBufferPool.Get("SetPipelineShadingRate");
            if (cmd == null)
                return;

            cmd.IssuePluginEventAndData(
                ptr, NativePluginBridge.PipelineShadingRate_EVENT_ID, (System.IntPtr)m_Packed);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    /// <summary>
    /// 绑定 SRI 的 Pass（attachment shading rate 生效起点）。
    /// </summary>
    internal class SetShadingRateImagePass : ScriptableRenderPass
    {
        private float m_Sensitivity = 0.5f;
        private float m_EnvLuma = 0.1f;
        private float m_K = 2.13f;
        private ComputeShader m_SRICompute;
        private int m_Kernel = -1;
        private RTHandle m_SriSourceRT;
        private static bool s_DiagLogged;   // [VRS-DIAG] 临时：一次性日志开关，根因确认后删除
        private int m_DiagCount;            // [VRS-DIAG] 临时：节流计数，根因确认后删除

        public SetShadingRateImagePass()
        {
            profilingSampler = new ProfilingSampler(nameof(SetShadingRateImagePass));
        }

        public void Setup(float sensitivity, float envLuma, float k, ComputeShader computeShader)
        {
            m_Sensitivity = sensitivity;
            m_EnvLuma = envLuma;
            m_K = k;
            m_SRICompute = computeShader;
            m_Kernel = m_SRICompute.FindKernel("ComputeShadingRateWave");
        }

        private void EnsureTileResources(int sriW, int sriH)
        {
            var desc = new RenderTextureDescriptor(sriW, sriH, GraphicsFormat.R8_UInt, 0);
            desc.enableRandomWrite = true;
            RenderingUtils.ReAllocateIfNeeded(ref m_SriSourceRT, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_VRSSriSource");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var ptr = NativePluginBridge.RenderEventAndData;
            if (ptr == System.IntPtr.Zero)
                return;

            // [VRS-DIAG] 一次性：能力与事件指针。根因确认后删除
            if (!s_DiagLogged)
            {
                s_DiagLogged = true;
                Debug.Log($"[VRS-DIAG] first setSRI Execute: eventPtr={ptr != System.IntPtr.Zero}, attachmentSupported={NativePluginBridge.IsAttachmentVRSSupported() != 0}");
            }

            // 源 RT 尺寸 = tile 网格。native SRI 为 max-size 一次性分配（Fix Plan §3.5），
            // 不再经主线程 P/Invoke 同步尺寸
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            int tileSize = NativePluginBridge.GetShadingRateImageTileSizeQuery();
            int sriWith = Mathf.CeilToInt(desc.width  / (float)tileSize);
            int sriHeight = Mathf.CeilToInt(desc.height / (float)tileSize);

            EnsureTileResources(sriWith, sriHeight);

            var cmd = CommandBufferPool.Get("LuminanceToSRI + SetSRI");

            // [VRS] 亮度梯度图由 renderer 实例持有（原 PostProcessPass.s_LuminanceRT static 已移除）
            var lumRT = (renderingData.cameraData.renderer as UniversalRenderer)?.luminanceRT;
            if (lumRT != null)
            {
                int kernel = m_Kernel;

                cmd.SetComputeTextureParam(m_SRICompute, kernel, "_LumGrad", lumRT);
                cmd.SetComputeTextureParam(m_SRICompute, kernel, "_SriOut", m_SriSourceRT);
                cmd.SetComputeIntParams(m_SRICompute, "_LumSize", lumRT.rt.width, lumRT.rt.height);
                cmd.SetComputeIntParam(m_SRICompute, "_TileTexels", tileSize / 2);
                cmd.SetComputeFloatParam(m_SRICompute, "_Sensitivity", m_Sensitivity);
                cmd.SetComputeFloatParam(m_SRICompute, "_EnvLuma", m_EnvLuma);
                cmd.SetComputeFloatParam(m_SRICompute, "_K", m_K);
                // _MotionVectorTexture 无需显式绑定：URP MV pass 在 Configure 内
                // SetGlobalTexture("_MotionVectorTexture", ...) 全局生效
                cmd.SetRandomWriteTarget(0, m_SriSourceRT);
                cmd.DispatchCompute(m_SRICompute, kernel, sriWith, sriHeight, 1);   // 组数 = tile 数（一线程组一 tile）
                cmd.ClearRandomWriteTargets();

                // 2) 源贴图指针经 IssuePluginEventAndData 的 data 参数传入 native（GPU timeline），
                //    消除同帧多 pass 覆盖竞态。必须在 lumRT 非空时才绑定：dispatch 未执行时
                //    源 RT 是未初始化字节（非法 rate 编码），拷入 SRI 会导致 device removed（Fix Plan §3.5）
                IntPtr srcNativeTex = m_SriSourceRT.rt.GetNativeTexturePtr();
                // [VRS-DIAG] 临时诊断：节流输出事件参数。根因确认后删除
                m_DiagCount++;
                if (m_DiagCount <= 3 || m_DiagCount % 300 == 0)
                    Debug.Log($"[VRS-DIAG] setSRI #{m_DiagCount}: tile={tileSize}, sri={sriWith}x{sriHeight}, lum={lumRT.rt.width}x{lumRT.rt.height}, srcPtr=0x{srcNativeTex.ToInt64():X16}");
                cmd.IssuePluginEventAndData(ptr, NativePluginBridge.SetAttachmentShadingRate_EVENT_ID, srcNativeTex);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    /// <summary>
    /// 解除 SRI 的 Pass（attachment shading rate 生效终点）。
    /// </summary>
    internal class ResetShadingRateImagePass : ScriptableRenderPass
    {
        public ResetShadingRateImagePass()
        {
            profilingSampler = new ProfilingSampler(nameof(ResetShadingRateImagePass));
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var ptr = NativePluginBridge.RenderEventAndData;
            if (ptr == System.IntPtr.Zero)
                return;

            var cmd = CommandBufferPool.Get("ResetShadingRateImage");
            if (cmd == null)
                return;

            cmd.IssuePluginEventAndData(
                ptr, NativePluginBridge.ResetAttachmentShadingRate_EVENT_ID, System.IntPtr.Zero);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}