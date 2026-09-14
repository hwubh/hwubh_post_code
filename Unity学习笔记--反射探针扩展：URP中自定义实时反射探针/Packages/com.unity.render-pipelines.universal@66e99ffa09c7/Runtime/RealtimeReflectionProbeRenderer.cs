using Unity.Collections;
using UnityEngine.Profiling;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal 
{
    /// <summary>
    /// 矩阵扩展方法，用于计算 Cubemap 面的 View 矩阵
    /// </summary>
    public static class MatrixExtensions
    {
        // Cubemap 六个面的方向向量定义（在探针的本地坐标系中）
        // 顺序：PositiveX, NegativeX, PositiveY, NegativeY, PositiveZ, NegativeZ
        private static readonly Vector3[] faceRights = {
            new Vector3( 0, 0,-1),  // Positive X (右侧) - 看向 +X 方向
            new Vector3( 0, 0, 1),  // Negative X (左侧) - 看向 -X 方向
            new Vector3( 1, 0, 0),  // Positive Y (上方) - 看向 +Y 方向
            new Vector3( 1, 0, 0),  // Negative Y (下方) - 看向 -Y 方向
            new Vector3( 1, 0, 0),  // Positive Z (前方) - 看向 +Z 方向
            new Vector3(-1, 0, 0)   // Negative Z (后方) - 看向 -Z 方向
        };

        // 手动反转Y轴，因为反射探针的画面是上下颠倒的。
        private static readonly Vector3[] faceUps = {
            new Vector3(0,-1, 0),   // Positive X - 上方向为 +Y
            new Vector3(0,-1, 0),   // Negative X - 上方向为 +Y
            new Vector3(0, 0, 1),   // Positive Y - 上方向为 -Z
            new Vector3(0, 0,-1),   // Negative Y - 上方向为 +Z
            new Vector3(0,-1, 0),   // Positive Z - 上方向为 +Y
            new Vector3(0,-1, 0)    // Negative Z - 上方向为 +Y
        };

        private static readonly Vector3[] faceForwards  = {
            new Vector3(-1, 0, 0),   // Positive X - 右方向为 -Z
            new Vector3( 1, 0, 0),   // Negative X - 右方向为 +Z
            new Vector3( 0,-1, 0),   // Positive Y - 右方向为 +X
            new Vector3( 0, 1, 0),   // Negative Y - 右方向为 +X
            new Vector3( 0, 0,-1),   // Positive Z - 右方向为 +X
            new Vector3( 0, 0, 1)    // Negative Z - 右方向为 -X
        };

        /// <summary>
        /// 为 Cubemap 的指定面设置 View 矩阵（worldToCameraMatrix）
        /// 此方法会正确处理手性问题，确保正确的面剔除行为
        /// </summary>
        /// <param name="matrix">要设置的矩阵（通常为 worldToCameraMatrix）</param>
        /// <param name="cameraPosition">相机在世界空间中的位置</param>
        /// <param name="face">Cubemap 面的索引 (0-5: PositiveX, NegativeX, PositiveY, NegativeY, PositiveZ, NegativeZ)</param>
        /// <returns>计算好的 worldToCameraMatrix</returns>
        public static Matrix4x4 SetViewMatrix(this Matrix4x4 matrix, Transform cameraTransform, int face)
        {
            if (face < 0 || face >= 6)
            {
                Debug.LogError($"Invalid cubemap face index: {face}. Must be between 0 and 5.");
                return matrix;
            }

            // 获取该面的方向向量（在探针的本地坐标系中，假设探针 transform 为 identity）
            Vector3 right = faceRights[face];
            Vector3 up = faceUps[face];
            Vector3 forward = faceForwards[face];

            Vector3 worldRight = cameraTransform.TransformDirection(right);
            Vector3 worldUp = cameraTransform.TransformDirection(up);
            Vector3 worldForward = cameraTransform.TransformDirection(forward);

            // 构建 View 矩阵
            // View 矩阵的行向量应该是 (right, up, -forward)
            // 因为相机空间是右手坐标系，Z 轴指向相机后方（负 forward 方向）
            Matrix4x4 viewMatrix = new Matrix4x4();
            
            // 设置行向量：第一行 = right, 第二行 = up, 第三行 = -forward
            viewMatrix.SetRow(0, new Vector4(worldRight.x, worldRight.y, worldRight.z, 0));
            viewMatrix.SetRow(1, new Vector4(worldUp.x, worldUp.y, worldUp.z, 0));
            viewMatrix.SetRow(2, new Vector4(worldForward.x, worldForward.y, worldForward.z, 0));
            viewMatrix.SetRow(3, new Vector4(0, 0, 0, 1));

            // 应用平移：将世界坐标转换为相机相对坐标
            Matrix4x4 translateMatrix = Matrix4x4.Translate(-cameraTransform.position);
            Matrix4x4 resultMatrix = viewMatrix * translateMatrix;

            return resultMatrix;
        }
    }


    /// <summary>
    /// CopyFacePass 的数据结构
    /// </summary>
    class CopyFacePassData
    {
        public TextureHandle sourceTexture;      // 源纹理（m_FaceRT）
        public TextureHandle destinationTexture;  // 目标纹理（m_IntermediumRT）
        public CubemapFace face;                  // 要复制的面
    }

    /// <summary>
    /// GenerateMipsPass 的数据结构
    /// </summary>
    class GenerateMipsPassData
    {
        public TextureHandle sourceTexture;        // 需要生成 mipmap 的纹理
    }

    /// <summary>
    /// ConvolveMipPass 的数据结构
    /// </summary>
    class ConvolveMipPassData
    {
        public TextureHandle sourceCubemap;       // 源 Cubemap（m_IntermediumRT）
        public TextureHandle destinationCubemap;   // 目标 Cubemap（最终输出）
        public int mipLevel;                     // 要卷积的 mip 级别
        public CubemapFace face;                  // 要卷积的面
        public float invOmegaP;                    // 卷积参数
        public Material filterMaterial;            // Filter Material
    }

    public class RealtimeReflectionProbeRenderer
    {
        // 用于渲染每个 cubemap 面的 RTHandle（复用，避免频繁创建临时纹理）
        private RTHandle m_FaceRT;
        private RTHandle m_FaceColorRT;
        private RTHandle m_FaceDepthRT;
        
        // 中转 RTHandle，用于存储渲染结果并生成 mipmap
        private RTHandle m_IntermediumRT;

        // 缓存的 FilterCubemap Material
        private Material m_FilterCubemapMaterial;

        // 用于设置 Material 参数的 MaterialPropertyBlock
        private MaterialPropertyBlock m_FilterCubemapPropertyBlock = new MaterialPropertyBlock();

        // Reflection Probe Render Graph 实例
        private RenderGraph m_ReflectionProbeRenderGraph;

        /// <summary>
        /// 获取 FilterCubemap Material，如果不存在则自动创建/更新
        /// </summary>
        private Material filterCubemapMaterial
        {
            get
            {
                // 获取 shader
                Shader filterShader = null;
                var resources = ReflectionProbeResources.instance;
                if (resources != null)
                {
                    filterShader = resources.filterCubemapShader;
                }

                if (filterShader == null)
                {
                    Debug.LogError("FilterCubemap shader not found! Please ensure ReflectionProbeResources asset exists and has the shader assigned.");
                    return null;
                }

                // 检查并创建/更新缓存的 Material
                if (m_FilterCubemapMaterial == null)
                {
                    m_FilterCubemapMaterial = new Material(filterShader);
                    m_FilterCubemapMaterial.hideFlags = HideFlags.HideAndDontSave;
                }

                return m_FilterCubemapMaterial;
            }
        }

        Camera renderFaceCamera;
        Camera m_RenderFaceCamera
        {
            get
            {
                if (renderFaceCamera == null)
                {
                    GameObject cameraGo = new GameObject("Reflection Probes Camera");
                    cameraGo.hideFlags = HideFlags.HideAndDontSave;
                    renderFaceCamera = cameraGo.AddComponent<Camera>();
                    cameraGo.AddComponent<UniversalAdditionalCameraData>();
                    renderFaceCamera.enabled = false;
                    renderFaceCamera.cameraType = CameraType.Reflection;
                    renderFaceCamera.allowHDR = true;
                    renderFaceCamera.useOcclusionCulling = true;
                    renderFaceCamera.orthographic = false;
                    renderFaceCamera.allowMSAA = false;
                    renderFaceCamera.fieldOfView = 90f;
                    renderFaceCamera.aspect = 1f;
                }

                return renderFaceCamera;
            }
        }

#region 传统渲染
        /// <summary>
        /// 渲染所有六个立方体面
        /// </summary>
        /// <param name="context">渲染上下文</param>
        /// <param name="probe">反射探针</param>
        public void RenderAll(ScriptableRenderContext context, ReflectionProbe probe)
        {
            if (probe == null)
                return;

            var probeData = probe.GetUniversalAdditionalReflectionProbeData();
            if (probeData == null)
                return;

            // 确保实时纹理已创建
            probeData.EnsureRealtimeTexture(probe);
            var cubemapTexture = probeData.customRealtimeTexture;
            
            if (cubemapTexture == null)
            {
                Debug.LogError($"Failed to create realtime texture for ReflectionProbe '{probe.name}'.");
                return;
            }

            // 遍历六个立方体面，渲染每个面
            RenderFaces(context, probe);

            // 使用 GGX 过滤生成各个粗糙度的预过滤环境贴图
            ConvolveMips(context, probe);
        }  

        /// <summary>
        /// 渲染指定的 cubemap 面（使用位掩码，可以一次渲染多个面）
        /// </summary>
        /// <param name="context">渲染上下文</param>
        /// <param name="probe">反射探针</param>
        /// <param name="faceMask">面位掩码：bit 0-5 对应 face 0-5，范围 1-63。例如：1 = face 0, 2 = face 1, 63 (0x3F) = 所有6个面</param>
        public void RenderFaces(ScriptableRenderContext context, ReflectionProbe probe, int faceMask = 63)
        {
            if (probe == null || faceMask == 0)
                return;

            var probeData = probe.GetUniversalAdditionalReflectionProbeData();
            if (probeData == null)
                return;

            // 确保实时纹理已创建
            probeData.EnsureRealtimeTexture(probe);
            var cubemapTexture = probeData.customRealtimeTexture;
            
            if (cubemapTexture == null)
            {
                Debug.LogError($"Failed to create realtime texture for ReflectionProbe '{probe.name}'.");
                return;
            }

            SetupCameraForProbe(probe);

            // 准备用于渲染每个 cubemap 面的 RenderTextureDescriptor
            RenderTextureDescriptor faceRTDesc = cubemapTexture.rt.descriptor;
            faceRTDesc.dimension = TextureDimension.Tex2D;
            faceRTDesc.useMipMap = false;
            faceRTDesc.depthBufferBits = 24;

            RenderingUtils.ReAllocateHandleIfNeeded(ref m_FaceRT, faceRTDesc, FilterMode.Point, TextureWrapMode.Clamp, 1, 0, "ReflectionProbe_FaceRT", true);
            
            // 创建中转纹理（如果尚未创建）
            RenderTextureDescriptor intermediumRTDesc = faceRTDesc;
            intermediumRTDesc.useMipMap = true;
            intermediumRTDesc.dimension = TextureDimension.Cube;
            intermediumRTDesc.depthBufferBits = 0;
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_IntermediumRT, intermediumRTDesc, FilterMode.Trilinear, TextureWrapMode.Clamp, 1, 0, "ReflectionProbe_IntermediumRT");

            // 遍历所有6个面，渲染被位掩码标记的面
            for (int face = 0; face < 6; face++)
            {
                // 检查该面是否在掩码中：faceMask 的第 face 位是否为 1
                if ((faceMask & (1 << face)) == 0)
                        continue;

                m_RenderFaceCamera.worldToCameraMatrix = Matrix4x4.identity.SetViewMatrix(m_RenderFaceCamera.gameObject.transform, face);

                // 使用成员变量 m_FaceRT 作为渲染目标
                m_RenderFaceCamera.targetTexture = m_FaceRT;

                // 获取 additional camera data
                UniversalAdditionalCameraData additionalCameraData = null;
                if (m_RenderFaceCamera.gameObject != null)
                    m_RenderFaceCamera.gameObject.TryGetComponent(out additionalCameraData);

                // 获取 renderer 和 frameData
                var renderer = UniversalRenderPipeline.GetRenderer(m_RenderFaceCamera, additionalCameraData);
                if (renderer == null)
                {
                    Debug.LogWarning($"Trying to render {m_RenderFaceCamera.name} with an invalid renderer. Camera rendering will be skipped.");
                    return;
                }

                using ContextContainer frameData = renderer.frameData;

                // 创建 cameraData
                var cameraData = UniversalRenderPipeline.CreateCameraData(frameData, m_RenderFaceCamera, additionalCameraData);
                UniversalRenderPipeline.InitializeAdditionalCameraData(m_RenderFaceCamera, additionalCameraData, true, true, cameraData);

                // 配置反射探针专用的相机设置：只执行必要的pass，禁用不需要的功能
                ConfigureReflectionProbeCameraData(cameraData, additionalCameraData, probeData);

                // 使用专门为反射探针设计的方法，它会在渲染完成后自动 copy 到 cubemap
                UniversalRenderPipeline.RenderSingleCameraForReflectionProbe(context, cameraData, ref m_FaceRT, ref m_IntermediumRT, (CubemapFace)face, probeData);
            }
        }

        /// <summary>
        /// 卷积指定的 mip 级别（使用位掩码，可以一次卷积多个 mip）
        /// </summary>
        /// <param name="context">渲染上下文</param>
        /// <param name="probe">反射探针</param>
        /// <param name="mipMask">mip 位掩码：bit 0-6 对应 mip 0-6。例如：1 = mip0, 2 = mip1, 124 (0x7C) = mip2-6, 126 (0x7E) = mip1-6</param>
        public void ConvolveMips(ScriptableRenderContext context, ReflectionProbe probe, int mipMask = 127)
        {
            if (probe == null || mipMask == 0)
                return;

            var probeData = probe.GetUniversalAdditionalReflectionProbeData();
            if (probeData == null)
                return;

            var cubemapTexture = probeData.customRealtimeTexture;
            if (cubemapTexture == null || m_IntermediumRT == null || cubemapTexture.rt == null || m_IntermediumRT.rt == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            cmd.name = "ConvolveMips";

            // 检查是否需要处理 mip0（复制 mip0）
            // 注意：CopyMip0 必须在 GenerateMips 之前，以便在 IndividualFaces 模式下，6 个面全部渲染完成后
            // 立即将 mip0 复制到最终 cubemap，而 GenerateMips 仅为后续的 ConvolveMip 准备 m_IntermediumRT 的 mip 链
            if ((mipMask & 1) != 0)
            {
                // 先复制 mip0：将 m_IntermediumRT 的 mip0（6 个面）复制到 cubemapTexture 的 mip0
                for (int face = 0; face < 6; face++)
                {
                    cmd.CopyTexture(
                        m_IntermediumRT.rt, face, 0, // 源：m_IntermediumRT，面索引，mip0
                        cubemapTexture.rt, face, 0    // 目标：cubemapTexture，面索引，mip0
                    );
                }

                // 再生成 mipmap，供后续 ConvolveMip mip1-6 使用
                cmd.GenerateMips(m_IntermediumRT.rt);
            }

            // mipMask > 1 表示需要处理 mip1-6
            if (mipMask > 1)
            {              
                // 获取 Material（会自动检查并创建/更新）
                Material filterMaterial = filterCubemapMaterial;
                if (filterMaterial == null)
                    return;
            
                // 使用 MaterialPropertyBlock 设置源 cubemap
                m_FilterCubemapPropertyBlock.SetTexture("_SourceCubemap", m_IntermediumRT.rt);
                
                // 计算 invOmegaP
                // invOmegaP = 1 / omegaP, where omegaP = FOUR_PI / (6.0 * cubemapWidth * cubemapWidth)
                int cubemapWidth = m_IntermediumRT.rt.width;
                float omegaP = (4.0f * Mathf.PI) / (6.0f * cubemapWidth * cubemapWidth);
                float invOmegaP = 1.0f / omegaP;
                m_FilterCubemapPropertyBlock.SetFloat("_InvOmegaP", invOmegaP);
                
                // 遍历 mip1 到 mip6，只处理 mipMask 中标记的 mip 级别
                for (int mipLevel = 1; mipLevel <= 6; mipLevel++)
                {
                    // 检查该 mip 级别是否在掩码中：mipMask 的第 mipLevel 位是否为 1
                    if ((mipMask & (1 << mipLevel)) == 0)
                        continue;

                    m_FilterCubemapPropertyBlock.SetFloat("_MipLevel", mipLevel);
                    
                    // 遍历六个面
                    for (int face = 0; face < 6; face++)
                    {
                        m_FilterCubemapPropertyBlock.SetFloat("_FaceIndex", face);
                        
                        // 设置渲染目标为 cubemap 的特定 mip 级别和面
                        CoreUtils.SetRenderTarget(cmd, cubemapTexture, ClearFlag.None, mipLevel, (CubemapFace)face);
                        
                        // 使用 MaterialPropertyBlock 绘制全屏三角形
                        CoreUtils.DrawFullScreen(cmd, filterMaterial, m_FilterCubemapPropertyBlock);
                    }
                }
            }
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            context.Submit();

            probe.realtimeTexture = cubemapTexture;
        }

        /// <summary>
        /// 设置相机参数以匹配探针设置
        /// </summary>
        private void SetupCameraForProbe(ReflectionProbe probe)
        {
            // 将 ReflectionProbe 中与相机相关的属性赋值到相机上
            m_RenderFaceCamera.nearClipPlane = probe.nearClipPlane;
            m_RenderFaceCamera.farClipPlane = probe.farClipPlane;
            m_RenderFaceCamera.clearFlags = (CameraClearFlags)probe.clearFlags;
            m_RenderFaceCamera.backgroundColor = probe.backgroundColor;
            m_RenderFaceCamera.cullingMask = probe.cullingMask;
            m_RenderFaceCamera.allowHDR = probe.hdr;
            m_RenderFaceCamera.gameObject.transform.SetPositionAndRotation(probe.transform.position, Quaternion.identity);
        }

#endregion 传统渲染

        /// <summary>
        /// 配置反射探针专用的相机数据
        /// 只执行必要的pass（mainlightshadowmap, opaque draw, skybox draw, transparency draw）
        /// 禁用不需要的功能（MSAA、motion vector、post-processing等）
        /// </summary>
        /// <param name="cameraData">相机数据</param>
        /// <param name="additionalCameraData">额外的相机数据</param>
        /// <param name="probeData">反射探针数据</param>
        private void ConfigureReflectionProbeCameraData(UniversalCameraData cameraData, UniversalAdditionalCameraData additionalCameraData, UniversalAdditionalReflectionProbeData probeData)
        {
            // 配置 additionalCameraData（如果存在）
            if (additionalCameraData != null)
            {
                // 禁用后处理
                additionalCameraData.renderPostProcessing = false;
                
                // 禁用抗锯齿
                additionalCameraData.antialiasing = AntialiasingMode.None;
                additionalCameraData.antialiasingQuality = AntialiasingQuality.Low;
                
                // 禁用NaN停止和抖动
                additionalCameraData.stopNaN = false;
                additionalCameraData.dithering = false;
                
                // 禁用深度和颜色纹理复制（反射探针不需要）
                additionalCameraData.requiresDepthOption = CameraOverrideOption.Off;
                additionalCameraData.requiresColorOption = CameraOverrideOption.Off;

                // 根据probeData设置配置阴影渲染
                if (probeData != null)
                {
                    // 只有当mainlight或additionallight阴影至少有一个启用时，才启用阴影渲染
                    bool renderShadows = probeData.renderMainLightShadows || probeData.renderAdditionalLightShadows;
                    additionalCameraData.renderShadows = renderShadows;
                }
                else
                {
                    // 如果没有probeData，默认启用阴影（保持原有行为）
                    additionalCameraData.renderShadows = true;
                }
            }

            // 配置 cameraData
            // 禁用后处理
            cameraData.postProcessEnabled = false;
            
            // 禁用抗锯齿
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.antialiasingQuality = AntialiasingQuality.Low;
            
            // 禁用NaN停止和抖动
            cameraData.isStopNaNEnabled = false;
            cameraData.isDitheringEnabled = false;
            
            // 禁用深度和颜色纹理复制
            cameraData.requiresDepthTexture = false;
            cameraData.requiresOpaqueTexture = false;
            cameraData.postProcessingRequiresDepthTexture = false;
            
            // 禁用MSAA（设置为1表示无MSAA）
            // 注意：cameraTargetDescriptor是结构体，需要重新赋值
            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            cameraData.cameraTargetDescriptor = descriptor;
            
            // 禁用GPU遮挡剔除（反射探针不需要）
            cameraData.useGPUOcclusionCulling = false;

            // 根据probeData设置配置阴影距离
            if (probeData != null)
            {
                bool renderShadows = probeData.renderMainLightShadows || probeData.renderAdditionalLightShadows;
                if (!renderShadows)
                {
                    // 如果都不启用，禁用阴影距离
                    cameraData.maxShadowDistance = 0.0f;
                }
                else
                {
                    // 使用反射探针的 shadowDistance，而不是管线的 shadowDistance
                    // 这样可以避免影响其他相机的渲染
                    ReflectionProbe probe = probeData.GetComponent<ReflectionProbe>();
                    if (probe != null)
                    {
                        cameraData.maxShadowDistance = Mathf.Min(probe.shadowDistance, m_RenderFaceCamera.farClipPlane);
                        cameraData.maxShadowDistance = (cameraData.maxShadowDistance >= m_RenderFaceCamera.nearClipPlane) ? cameraData.maxShadowDistance : 0.0f;
                    }
                }
            }
        }

        /// <summary>
        /// 初始化 Reflection Probe Render Graph
        /// </summary>
        private void InitializeRenderGraph()
        {
            if (m_ReflectionProbeRenderGraph == null)
            {
                m_ReflectionProbeRenderGraph = new RenderGraph("ReflectionProbeRenderGraph");
            }
        }

        /// <summary>
        /// 记录 CopyFacePass - 将渲染的面复制到中间 Cubemap 纹理
        /// </summary>
        /// <param name="renderGraph">Render Graph 实例</param>
        /// <param name="sourceHandle">源纹理 TextureHandle</param>
        /// <param name="destHandle">目标纹理 TextureHandle</param>
        /// <param name="face">要复制的面</param>
        private void RecordCopyFacePass(RenderGraph renderGraph, TextureHandle sourceHandle, TextureHandle destHandle, CubemapFace face)
        {
            using (var builder = renderGraph.AddUnsafePass<CopyFacePassData>("CopyFacePass", out var passData))
            {
                passData.sourceTexture = sourceHandle;
                passData.destinationTexture = destHandle;
                passData.face = face;
                
                // 声明资源读写关系
                builder.UseTexture(passData.sourceTexture, AccessFlags.Read);
                builder.UseTexture(passData.destinationTexture, AccessFlags.Write);
                
                // 设置执行函数
                builder.SetRenderFunc(static (CopyFacePassData data, UnsafeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    RTHandle sourceRTHandle = data.sourceTexture;
                    RTHandle destRTHandle = data.destinationTexture;
                    
                    if (sourceRTHandle != null && destRTHandle != null && sourceRTHandle.rt != null && destRTHandle.rt != null)
                    {
                        cmd.CopyTexture(
                            sourceRTHandle.rt, 0, 0,  // 源：m_FaceRT，mip0
                            destRTHandle.rt, (int)data.face, 0  // 目标：m_IntermediumRT，指定面，mip0
                        );
                    }
                });
            }
        }

        /// <summary>
        /// 记录 GenerateMipsPass - 为中间 Cubemap 纹理生成 Mipmap
        /// </summary>
        /// <param name="renderGraph">Render Graph 实例</param>
        /// <param name="intermediumHandle">中间纹理 TextureHandle</param>
        private void RecordGenerateMipsPass(RenderGraph renderGraph, TextureHandle intermediumHandle)
        {
            using (var builder = renderGraph.AddUnsafePass<GenerateMipsPassData>("GenerateMipsPass", out var passData))
            {
                passData.sourceTexture = intermediumHandle;
                builder.UseTexture(passData.sourceTexture, AccessFlags.Write);
                
                builder.SetRenderFunc(static (GenerateMipsPassData data, UnsafeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    // 从 TextureHandle 获取 RTHandle
                    RTHandle sourceRTHandle = data.sourceTexture;
                    
                    if (sourceRTHandle != null && sourceRTHandle.rt != null)
                    {
                        cmd.GenerateMips(sourceRTHandle.rt);
                    }
                });
            }
        }

        /// <summary>
        /// 记录 ConvolveMipPass - 对指定的 Mip 级别和面进行 GGX 卷积
        /// </summary>
        /// <param name="renderGraph">Render Graph 实例</param>
        /// <param name="sourceHandle">源 Cubemap TextureHandle</param>
        /// <param name="destHandle">目标 Cubemap TextureHandle</param>
        /// <param name="mipLevel">要卷积的 mip 级别</param>
        /// <param name="face">要卷积的面</param>
        /// <param name="filterMaterial">Filter Material</param>
        /// <param name="invOmegaP">卷积参数</param>
        private void RecordConvolveMipPass(RenderGraph renderGraph, TextureHandle sourceHandle, TextureHandle destHandle, int mipLevel, CubemapFace face, Material filterMaterial, float invOmegaP)
        {
            using (var builder = renderGraph.AddUnsafePass<ConvolveMipPassData>("ConvolveMipPass", out var passData))
            {
                passData.sourceCubemap = sourceHandle;
                passData.destinationCubemap = destHandle;
                passData.mipLevel = mipLevel;
                passData.face = face;
                passData.invOmegaP = invOmegaP;
                passData.filterMaterial = filterMaterial;
                
                // 声明资源读写关系
                builder.UseTexture(passData.sourceCubemap, AccessFlags.Read);
                builder.UseTexture(passData.destinationCubemap, AccessFlags.Write);

                // 设置渲染目标为 cubemap 的特定 mip 级别和面
                //builder.SetRenderAttachment(passData.destinationCubemap, 0, AccessFlags.Write, mipLevel, (int)face);
    
                // 设置执行函数
                builder.SetRenderFunc(static (ConvolveMipPassData data, UnsafeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    RTHandle sourceRTHandle = data.sourceCubemap;
                    RTHandle destRTHandle = data.destinationCubemap;
                    
                    if (sourceRTHandle != null && destRTHandle != null && data.filterMaterial != null)
                    {
                        // 创建 MaterialPropertyBlock
                        var mpb = new MaterialPropertyBlock();
                        mpb.SetTexture("_SourceCubemap", sourceRTHandle.rt);
                        mpb.SetFloat("_MipLevel", data.mipLevel);
                        mpb.SetFloat("_FaceIndex", (int)data.face);
                        mpb.SetFloat("_InvOmegaP", data.invOmegaP);

                        // Create a command buffer for a list of rendering methods
                        CommandBuffer unsafeCommandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(cmd);

                        // 设置渲染目标为 cubemap 的特定 mip 级别和面
                        CoreUtils.SetRenderTarget(unsafeCommandBuffer, data.destinationCubemap, ClearFlag.None, data.mipLevel, data.face);
                        
                        // 使用 MaterialPropertyBlock 绘制全屏三角形
                        CoreUtils.DrawFullScreen(unsafeCommandBuffer, data.filterMaterial, mpb);
                    }
                });
            }
        }

        /// <summary>
        /// 记录 CopyMip0Pass - 复制 mip0 到最终 cubemap（所有6个面）
        /// </summary>
        private void RecordCopyMip0Pass(RenderGraph renderGraph, TextureHandle sourceHandle, TextureHandle destHandle)
        {
            using (var builder = renderGraph.AddUnsafePass<CopyFacePassData>("CopyMip0Pass", out var passData))
            {
                passData.sourceTexture = sourceHandle;
                passData.destinationTexture = destHandle;
                
                builder.UseTexture(passData.sourceTexture, AccessFlags.Read);
                builder.UseTexture(passData.destinationTexture, AccessFlags.Write);
                
                builder.SetRenderFunc(static (CopyFacePassData data, UnsafeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    RTHandle sourceRTHandle = data.sourceTexture;
                    RTHandle destRTHandle = data.destinationTexture;
                    
                    if (sourceRTHandle != null && destRTHandle != null && sourceRTHandle.rt != null && destRTHandle.rt != null)
                    {
                        // 复制所有6个面的 mip0
                        for (int face = 0; face < 6; face++)
                        {
                            cmd.CopyTexture(
                                sourceRTHandle.rt, face, 0,  // 源：m_IntermediumRT，面索引，mip0
                                destRTHandle.rt, face, 0      // 目标：cubemapTexture，面索引，mip0
                            );
                        }
                    }
                });
            }
        }

        /// <summary>
        /// 计算 invOmegaP 参数（用于 GGX 卷积）
        /// </summary>
        private float CalculateInvOmegaP(int cubemapWidth)
        {
            // invOmegaP = 1 / omegaP, where omegaP = FOUR_PI / (6.0 * cubemapWidth * cubemapWidth)
            float omegaP = (4.0f * Mathf.PI) / (6.0f * cubemapWidth * cubemapWidth);
            return 1.0f / omegaP;
        }


        /// <summary>
        /// 记录卷积指定的 mip 级别的 Render Graph Passes（支持 mipMask）
        /// </summary>
        /// <param name="renderGraph">Render Graph 实例</param>
        /// <param name="intermediumHandle">中间 Cubemap 纹理句柄</param>
        /// <param name="finalCubemapHandle">最终 Cubemap 纹理句柄</param>
        /// <param name="mipMask">mip 位掩码：bit 0-6 对应 mip 0-6</param>
        private void RecordConvolveMipsPasses(
            RenderGraph renderGraph,
            TextureHandle intermediumHandle,
            TextureHandle finalCubemapHandle,
            int mipMask)
        {
            // 检查是否需要处理 mip0（复制 mip0 并生成 mipmap）
            // 注意：CopyMip0 必须在 GenerateMips 之前， IndividualFaces 模式下 6 个面渲染完成后先复制 mip0
            if ((mipMask & 1) != 0)
            {
                RecordCopyMip0Pass(renderGraph, intermediumHandle, finalCubemapHandle);
                RecordGenerateMipsPass(renderGraph, intermediumHandle);
            }

            // mipMask > 1 表示需要处理 mip1-6 的 GGX 卷积
            if (mipMask > 1)
            {
                Material filterMaterial = filterCubemapMaterial;
                if (filterMaterial != null && m_IntermediumRT != null && m_IntermediumRT.rt != null)
                {
                    int cubemapWidth = m_IntermediumRT.rt.width;
                    float invOmegaP = CalculateInvOmegaP(cubemapWidth);

                    for (int mipLevel = 1; mipLevel <= 6; mipLevel++)
                    {
                        if ((mipMask & (1 << mipLevel)) == 0)
                            continue;

                        for (int face = 0; face < 6; face++)
                        {
                            RecordConvolveMipPass(
                                renderGraph,
                                intermediumHandle,
                                finalCubemapHandle,
                                mipLevel,
                                (CubemapFace)face,
                                filterMaterial,
                                invOmegaP);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 记录反射探针的后处理 Render Graph Passes（方案A）
        /// 相机渲染使用传统方式，后处理使用 Render Graph
        /// </summary>
        /// <param name="renderGraph">Render Graph 实例</param>
        /// <param name="faceRTHandle">面渲染纹理句柄</param>
        /// <param name="intermediumHandle">中间纹理句柄</param>
        /// <param name="finalCubemapHandle">最终 Cubemap 纹理句柄</param>
        private void RecordReflectionProbePasses(
            RenderGraph renderGraph,
            TextureHandle faceRTHandle,
            TextureHandle intermediumHandle,
            TextureHandle finalCubemapHandle)
        {
            // 先复制 mip0 到最终 cubemap（所有6个面），再生成 mipmap
            // 与 RecordConvolveMipsPasses 保持一致，便于 IndividualFaces 等分批渲染模式
            RecordCopyMip0Pass(renderGraph, intermediumHandle, finalCubemapHandle);
            RecordGenerateMipsPass(renderGraph, intermediumHandle);
            
            // 记录 ConvolveMipPass（每个 mip 级别和面）
            Material filterMaterial = filterCubemapMaterial;
            if (filterMaterial != null && m_IntermediumRT != null && m_IntermediumRT.rt != null)
            {
                int cubemapWidth = m_IntermediumRT.rt.width;
                float invOmegaP = CalculateInvOmegaP(cubemapWidth);
                
                // 遍历 mip1 到 mip6
                for (int mipLevel = 1; mipLevel <= 6; mipLevel++)
                {
                    // 遍历六个面
                    for (int face = 0; face < 6; face++)
                    {
                        RecordConvolveMipPass(
                            renderGraph, 
                            intermediumHandle, 
                            finalCubemapHandle, 
                            mipLevel, 
                            (CubemapFace)face, 
                            filterMaterial, 
                            invOmegaP
                        );
                    }
                }
            }
        }

        /// <summary>
        /// 使用 Render Graph 渲染反射探针（方案A）
        /// 相机渲染使用传统方式，后处理使用 Render Graph
        /// </summary>
        /// <param name="context">渲染上下文</param>
        /// <param name="probe">反射探针</param>
        public void RenderAllWithRenderGraph(ScriptableRenderContext context, ReflectionProbe probe)
        {
            if (probe == null)
                return;

            var probeData = probe.GetUniversalAdditionalReflectionProbeData();
            if (probeData == null)
                return;

            // 确保实时纹理已创建
            probeData.EnsureRealtimeTexture(probe);
            var cubemapTexture = probeData.customRealtimeTexture;
            
            if (cubemapTexture == null)
            {
                Debug.LogError($"Failed to create realtime texture for ReflectionProbe '{probe.name}'.");
                return;
            }

            SetupCameraForProbe(probe);

            // 准备用于渲染每个 cubemap 面的 RenderTextureDescriptor
            RenderTextureDescriptor faceRTDesc = cubemapTexture.rt.descriptor;
            faceRTDesc.dimension = TextureDimension.Tex2D;
            faceRTDesc.useMipMap = false;
            faceRTDesc.depthBufferBits = 0;
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_FaceColorRT, faceRTDesc, FilterMode.Point, TextureWrapMode.Clamp, 1, 0, "ReflectionProbe_FaceColorRT");

            // 创建中转纹理（如果尚未创建）
            RenderTextureDescriptor intermediumRTDesc = faceRTDesc;
            intermediumRTDesc.useMipMap = true;
            intermediumRTDesc.dimension = TextureDimension.Cube;
            intermediumRTDesc.depthBufferBits = 0;
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_IntermediumRT, intermediumRTDesc, FilterMode.Trilinear, TextureWrapMode.Clamp, 1, 0, "ReflectionProbe_IntermediumRT");

            faceRTDesc.graphicsFormat = GraphicsFormat.None;
            faceRTDesc.depthBufferBits = 24;
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_FaceDepthRT, faceRTDesc, FilterMode.Point, TextureWrapMode.Clamp, 1, 0, "ReflectionProbe_FaceDepthRT");

            //m_RenderFaceCamera.SetTargetBuffers(m_FaceColorRT.rt.colorBuffer, m_FaceDepthRT.rt.depthBuffer);

            // 步骤1：使用传统方式渲染所有面到 m_FaceRT
            // 注意：RenderFaces 内部会调用 UniversalRenderPipeline.RenderSingleCameraForReflectionProbe
            // 该方法会将每个面的渲染结果复制到 m_IntermediumRT，但根据方案A，所有后处理应在 Render Graph 中完成
            // 因此我们在 Render Graph 中重新复制和处理，确保后处理流程的统一管理
            RenderFaces(context, probe);

            // 初始化 Render Graph
            InitializeRenderGraph();

            // 准备 Render Graph 参数
            var cmd = CommandBufferPool.Get();
            cmd.name = "ReflectionProbeRenderGraph";
            
            try
            {
                // 开始录制
                RenderGraphParameters rgParams = new RenderGraphParameters
                {
                    commandBuffer = cmd,
                    scriptableRenderContext = context,
                    currentFrameIndex = Time.frameCount,
                };
                m_ReflectionProbeRenderGraph.BeginRecording(rgParams);
                
                // 导入外部纹理资源
                var faceRTHandle = m_ReflectionProbeRenderGraph.ImportTexture(m_FaceColorRT);
                var intermediumHandle = m_ReflectionProbeRenderGraph.ImportTexture(m_IntermediumRT);
                var finalCubemapHandle = m_ReflectionProbeRenderGraph.ImportTexture(cubemapTexture);
                
                // 步骤2：记录后处理 Pass（Copy, GenerateMips, Convolve）
                RecordReflectionProbePasses(
                    m_ReflectionProbeRenderGraph, 
                    faceRTHandle, 
                    intermediumHandle, 
                    finalCubemapHandle
                );
                
                // 统一执行整个 Render Graph
                m_ReflectionProbeRenderGraph.EndRecordingAndExecute();
            }
            catch (System.Exception e)
            {
                if (m_ReflectionProbeRenderGraph.ResetGraphAndLogException(e))
                    throw;
            }
            finally
            {
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                context.Submit();
            }

            probe.realtimeTexture = cubemapTexture;
        }

        /// <summary>
        /// 使用 Render Graph 卷积指定的 mip 级别（与 ConvolveMips 功能等价）
        /// </summary>
        /// <param name="context">渲染上下文</param>
        /// <param name="probe">反射探针</param>
        /// <param name="mipMask">mip 位掩码：bit 0-6 对应 mip 0-6。例如：1 = mip0, 2 = mip1, 124 (0x7C) = mip2-6, 126 (0x7E) = mip1-6</param>
        public void ConvolveMipsWithRenderGraph(ScriptableRenderContext context, ReflectionProbe probe, int mipMask = 127)
        {
            if (probe == null || mipMask == 0)
                return;

            var probeData = probe.GetUniversalAdditionalReflectionProbeData();
            if (probeData == null)
                return;

            var cubemapTexture = probeData.customRealtimeTexture;
            if (cubemapTexture == null || m_IntermediumRT == null || cubemapTexture.rt == null || m_IntermediumRT.rt == null)
                return;

            InitializeRenderGraph();

            var cmd = CommandBufferPool.Get();
            cmd.name = "ConvolveMipsRenderGraph";

            try
            {
                RenderGraphParameters rgParams = new RenderGraphParameters
                {
                    commandBuffer = cmd,
                    scriptableRenderContext = context,
                    currentFrameIndex = Time.frameCount,
                };
                m_ReflectionProbeRenderGraph.BeginRecording(rgParams);

                var intermediumHandle = m_ReflectionProbeRenderGraph.ImportTexture(m_IntermediumRT);
                var finalCubemapHandle = m_ReflectionProbeRenderGraph.ImportTexture(cubemapTexture);

                RecordConvolveMipsPasses(m_ReflectionProbeRenderGraph, intermediumHandle, finalCubemapHandle, mipMask);

                m_ReflectionProbeRenderGraph.EndRecordingAndExecute();
            }
            catch (System.Exception e)
            {
                if (m_ReflectionProbeRenderGraph.ResetGraphAndLogException(e))
                    throw;
            }
            finally
            {
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                context.Submit();
            }

            probe.realtimeTexture = cubemapTexture;
        }

        /// <summary>
        /// 清理缓存的资源
        /// </summary>
        public void Cleanup()
        {
            if (m_FilterCubemapMaterial != null)
            {
                CoreUtils.Destroy(m_FilterCubemapMaterial);
                m_FilterCubemapMaterial = null;
            }

            // 清理 Render Graph
            if (m_ReflectionProbeRenderGraph != null)
            {
                m_ReflectionProbeRenderGraph.Cleanup();
                m_ReflectionProbeRenderGraph = null;
            }
        }
    }
}
