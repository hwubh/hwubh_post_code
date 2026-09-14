using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// 反射探针渲染任务类型
    /// </summary>
    public enum ReflectionProbeRenderTaskType
    {
        /// <summary>
        /// 渲染所有面（无时间切片）
        /// </summary>
        RenderALL,
        
        /// <summary>
        /// 渲染指定的面（使用位掩码，可以一次渲染多个面）
        /// </summary>
        RenderFace,
        
        /// <summary>
        /// 卷积指定的 mip 级别（使用位掩码，可以一次卷积多个 mip）
        /// </summary>
        ConvolutionMips
    }

    /// <summary>
    /// 反射探针渲染任务
    /// </summary>
    public struct ReflectionProbeRenderTask
    {
        public ReflectionProbe probe;
        public ReflectionProbeRenderTaskType taskType;
        public int faceMask; // 用于 RenderFace 任务，位掩码：bit 0-5 对应 face 0-5，0 表示不适用
        public int mipMask;  // 用于 ConvolutionMips 任务，位掩码：bit 0-6 对应 mip 0-6，0 表示不适用
        
        public ReflectionProbeRenderTask(ReflectionProbe probe, ReflectionProbeRenderTaskType taskType, int faceMask = 0, int mipMask = 0)
        {
            this.probe = probe;
            this.taskType = taskType;
            this.faceMask = faceMask;
            this.mipMask = mipMask;
        }
    }

    /// <summary>
    /// 管理实时反射探针的渲染（单例模式）
    /// </summary>
    public class RealtimeReflectionProbeManager
    {
        private static RealtimeReflectionProbeManager s_Instance;

        private static readonly object s_Lock = new object();

        private RealtimeReflectionProbeRenderer m_Renderer;
        
        private int m_LastProbeUpdateFrame = -1;

        // 使用 HashSet 来去重，避免同一个探针被多次处理
        private HashSet<ReflectionProbe> processedProbes = new HashSet<ReflectionProbe>();

        // 渲染任务队列
        private Queue<ReflectionProbeRenderTask> m_RenderTaskQueue = new Queue<ReflectionProbeRenderTask>();

        // 跟踪每个探针的最后一个任务的执行帧：探针 -> 最后一个任务的帧数
        // 如果当前帧 >= 这个值，说明该探针的所有任务已完成，可以添加新任务
        private Dictionary<ReflectionProbe, int> m_ProbeTaskCounts = new Dictionary<ReflectionProbe, int>();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static RealtimeReflectionProbeManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    lock (s_Lock)
                    {
                        if (s_Instance == null)
                        {
                            s_Instance = new RealtimeReflectionProbeManager();
                        }
                    }
                }
                return s_Instance;
            }
        }

        /// <summary>
        /// 私有构造函数，防止外部实例化
        /// </summary>
        private RealtimeReflectionProbeManager()
        {
            // 初始化资源
        }

        /// <summary>
        /// 获取或创建反射探针渲染器
        /// </summary>
        private RealtimeReflectionProbeRenderer renderer
        {
            get
            {
                if (m_Renderer == null)
                {
                    m_Renderer = new RealtimeReflectionProbeRenderer();
                }
                return m_Renderer;
            }
        }

        /// <summary>
        /// 在渲染循环中更新，遍历所有相机收集可见的反射探针并渲染
        /// </summary>
        /// <param name="context">渲染上下文</param>
        /// <param name="cameras">相机列表</param>
        public void Update(ScriptableRenderContext context, List<Camera> cameras)
        {
            if(cameras[0].cameraType != CameraType.Game)
                return;

            int currentFrame = Time.renderedFrameCount;
            
            // 每帧只更新一次
            if (m_LastProbeUpdateFrame == currentFrame)
                return;

            // 清空已处理的探针集合
            processedProbes.Clear();

            // 收集所有相机可见的反射探针
            CollectVisibleProbes(context, cameras);

            // 创建副本以避免在枚举期间集合被修改（例如递归渲染调用）
            var probesToRender = new List<ReflectionProbe>(processedProbes);

            // 为需要更新的探针添加渲染任务
            foreach (var probe in probesToRender)
            {
                if (probe != null && probe.mode == ReflectionProbeMode.Realtime)
                {
                    AddRenderTasksForProbe(probe, currentFrame);
                }
            }

            // 执行一个渲染任务（每帧只执行一个）
            ExecuteOneRenderTask(context, currentFrame);

            m_LastProbeUpdateFrame = currentFrame;
        }

        /// <summary>
        /// 根据探针的 timeSlicingMode 添加相应的渲染任务
        /// </summary>
        /// <param name="probe">反射探针</param>
        /// <param name="currentFrame">当前帧数</param>
        private void AddRenderTasksForProbe(ReflectionProbe probe, int currentFrame)
        {
            if (probe == null)
                return;

            // 检查该探针是否已有未完成的任务
            // m_ProbeTaskCounts 记录的是该探针最后一个任务的执行帧数
            if (m_ProbeTaskCounts.TryGetValue(probe, out int lastTaskFrame))
            {
                // 如果最后一个任务的执行帧 > 当前帧，说明还有未完成的任务，不允许添加
                // 如果最后一个任务的执行帧 == 当前帧，说明当前帧会执行最后一个任务，允许添加（因为执行完后就没有了）
                // 如果最后一个任务的执行帧 < 当前帧，说明所有任务已完成，允许添加
                if (lastTaskFrame > currentFrame)
                {
                    return;
                }
            }

            // 获取 timeSlicingMode（Unity 内置枚举）
            // ReflectionProbeTimeSlicingMode: NoTimeSlicing = 0, AllFacesAtOnce = 1, IndividualFaces = 2
            ReflectionProbeTimeSlicingMode timeSlicingMode = probe.timeSlicingMode;

            int taskCount = 0;

            switch (timeSlicingMode)
            {
                case ReflectionProbeTimeSlicingMode.NoTimeSlicing: // NoTimeSlicing
                    // 添加一个 RenderALL 任务
                    m_RenderTaskQueue.Enqueue(new ReflectionProbeRenderTask(probe, ReflectionProbeRenderTaskType.RenderALL));
                    taskCount = 1;
                    break;

                case ReflectionProbeTimeSlicingMode.AllFacesAtOnce: // AllFacesAtOnce
                    // 添加三个任务：RenderFace (所有面), ConvolutionMips (mip0), ConvolutionMips (mip1-6)
                    // faceMask: 63 (0x3F) = 所有6个面 (bit 0-5)
                    m_RenderTaskQueue.Enqueue(new ReflectionProbeRenderTask(probe, ReflectionProbeRenderTaskType.RenderFace, 63));
                    // mipMask: bit 0-1 = mip0-1 (3 = 0x11)
                    m_RenderTaskQueue.Enqueue(new ReflectionProbeRenderTask(probe, ReflectionProbeRenderTaskType.ConvolutionMips, 0, 3));
                    // mipMask: bit 2-6 = mip2-6 (124 = 0x7E = 0b1111100)
                    m_RenderTaskQueue.Enqueue(new ReflectionProbeRenderTask(probe, ReflectionProbeRenderTaskType.ConvolutionMips, 0, 124));
                    taskCount = 3;
                    break;

                case ReflectionProbeTimeSlicingMode.IndividualFaces: // IndividualFaces
                    // 添加 8 个任务：6 个 RenderFace（每个面一个，使用位掩码），ConvolutionMips (mip0), ConvolutionMips (mip1-6)
                    for (int face = 0; face < 6; face++)
                    {
                        // faceMask: bit 0-5 对应 face 0-5，例如 face 0 = 1 (0x01), face 1 = 2 (0x02), face 5 = 32 (0x20)
                        int faceMask = 1 << face;
                        m_RenderTaskQueue.Enqueue(new ReflectionProbeRenderTask(probe, ReflectionProbeRenderTaskType.RenderFace, faceMask));
                    }
                    // mipMask: bit 0-1 = mip0-1 (3 = 0x11)
                    m_RenderTaskQueue.Enqueue(new ReflectionProbeRenderTask(probe, ReflectionProbeRenderTaskType.ConvolutionMips, 0, 3));
                    // mipMask: bit 2-6 = mip2-6 (124 = 0x7E = 0b1111100)
                    m_RenderTaskQueue.Enqueue(new ReflectionProbeRenderTask(probe, ReflectionProbeRenderTaskType.ConvolutionMips, 0, 124));
                    taskCount = 8;
                    break;

                default:
                    // 未知模式，使用 NoTimeSlicing 作为默认
                    m_RenderTaskQueue.Enqueue(new ReflectionProbeRenderTask(probe, ReflectionProbeRenderTaskType.RenderALL));
                    taskCount = 1;
                    break;
            }

            // 计算该探针最后一个任务的执行帧数
            // 需要统计队列中该探针的任务前面有多少个任务（包括其他探针的任务）
            int tasksBeforeThisProbe = m_RenderTaskQueue.Count;
            
            // 最后一个任务的执行帧数 = 当前帧 + 前面的任务数 + 该探针的任务数 - 1
            // 例如：当前帧100，前面有3个任务，该探针有8个任务
            // 第一个任务在帧103执行，最后一个任务在帧110执行
            m_ProbeTaskCounts[probe] = currentFrame + tasksBeforeThisProbe + taskCount - 1;
        }

        /// <summary>
        /// 执行一个渲染任务（每帧只执行一个）
        /// </summary>
        /// <param name="context">渲染上下文</param>
        /// <param name="currentFrame">当前帧数</param>
        private void ExecuteOneRenderTask(ScriptableRenderContext context, int currentFrame)
        {
            if (m_RenderTaskQueue.Count == 0)
                return;

            // 取出队列中的第一个任务
            var task = m_RenderTaskQueue.Dequeue();

            if (task.probe == null || task.probe.mode != ReflectionProbeMode.Realtime)
            {
                // 探针无效（被释放或更改了模式），需要抛弃该探针的所有任务
                // 统计并移除队列中该探针的所有任务
                int removedTaskCount = RemoveProbeTasksFromQueue(task.probe);
                
                // 移除该探针的任务计数记录
                m_ProbeTaskCounts.Remove(task.probe);
                
                // 更新其他探针的结束时刻，提前的数量 = 被移除的任务数量
                UpdateOtherProbesLastTaskFrame(null, removedTaskCount);
                return;
            }

            // 执行任务
            ExecuteRenderTask(context, task);

            // 检查该探针的所有任务是否已完成
            if (m_ProbeTaskCounts.TryGetValue(task.probe, out int probeLastTaskFrame))
            {
                // 如果当前帧 >= 最后一个任务的帧数，说明所有任务已完成，移除记录
                if (currentFrame >= probeLastTaskFrame)
                {
                    m_ProbeTaskCounts.Remove(task.probe);
                }
            }
        }

        /// <summary>
        /// 从队列中移除指定探针的所有任务
        /// </summary>
        /// <param name="probe">要移除任务的探针</param>
        /// <returns>被移除的任务数量</returns>
        private int RemoveProbeTasksFromQueue(ReflectionProbe probe)
        {
            if (probe == null)
                return 0;

            int removedCount = 0;
            var tasksToKeep = new Queue<ReflectionProbeRenderTask>();

            // 遍历队列，保留其他探针的任务，移除指定探针的任务
            while (m_RenderTaskQueue.Count > 0)
            {
                var task = m_RenderTaskQueue.Dequeue();
                if (task.probe == probe)
                {
                    removedCount++;
                }
                else
                {
                    tasksToKeep.Enqueue(task);
                }
            }

            // 将保留的任务重新放回队列
            while (tasksToKeep.Count > 0)
            {
                m_RenderTaskQueue.Enqueue(tasksToKeep.Dequeue());
            }

            return removedCount;
        }

        /// <summary>
        /// 更新队列中其他探针的最后一个任务帧数
        /// </summary>
        /// <param name="excludedProbe">不更新的探针（如果为 null，则更新所有探针）</param>
        /// <param name="frameOffset">帧数偏移量（通常为1，表示提前一帧；如果移除了多个任务，则为移除的任务数量）</param>
        private void UpdateOtherProbesLastTaskFrame(ReflectionProbe excludedProbe, int frameOffset)
        {
            // 遍历队列，找出所有需要更新的探针
            HashSet<ReflectionProbe> probesToUpdate = new HashSet<ReflectionProbe>();
            foreach (var task in m_RenderTaskQueue)
            {
                if (task.probe != null && task.probe != excludedProbe)
                {
                    probesToUpdate.Add(task.probe);
                }
            }

            // 更新这些探针的最后一个任务帧数
            foreach (var probe in probesToUpdate)
            {
                if (m_ProbeTaskCounts.TryGetValue(probe, out int lastTaskFrame))
                {
                    // 减去 frameOffset，因为队列中少了任务，其他任务会提前执行
                    m_ProbeTaskCounts[probe] = lastTaskFrame - frameOffset;
                }
            }
        }

        /// <summary>
        /// 执行具体的渲染任务
        /// </summary>
        private void ExecuteRenderTask(ScriptableRenderContext context, ReflectionProbeRenderTask task)
        {
            var probe = task.probe;
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

            switch (task.taskType)
            {
                case ReflectionProbeRenderTaskType.RenderALL:
                    // 渲染所有面并完成所有后续处理
                    renderer.RenderAll(context, probe);
                    break;

                case ReflectionProbeRenderTaskType.RenderFace:
                    // 渲染指定的面（使用位掩码，可以一次渲染多个面）
                    renderer.RenderFaces(context, probe, task.faceMask);
                    break;

                case ReflectionProbeRenderTaskType.ConvolutionMips:
                    // 卷积指定的 mip 级别（使用 Render Graph，使用位掩码可一次卷积多个 mip）
                    renderer.ConvolveMips(context, probe, task.mipMask);
                    break;
            }
        }

        /// <summary>
        /// 从相机列表收集可见的反射探针
        /// </summary>
        /// <param name="context">渲染上下文</param>
        /// <param name="cameras">相机列表</param>
        private void CollectVisibleProbes(ScriptableRenderContext context, List<Camera> cameras)
        {
            // 遍历所有相机，收集可见的反射探针
            foreach (var camera in cameras)
            {
                if (camera == null)
                    continue;

                // 只处理游戏相机
                if (!UniversalRenderPipeline.IsGameCamera(camera) || camera.cameraType != CameraType.Game)
                    continue;

                // 为每个相机创建剔除参数以获取可见的反射探针
                UniversalAdditionalCameraData additionalCameraData = null;
                camera.gameObject.TryGetComponent(out additionalCameraData);
                
                var cameraRenderer = UniversalRenderPipeline.GetRenderer(camera, additionalCameraData);
                if (cameraRenderer == null)
                    continue;

                if (additionalCameraData != null && additionalCameraData.renderType != CameraRenderType.Base)
                {
                    Debug.LogWarning("Only Base cameras can be rendered with standalone RenderSingleCamera. Camera will be skipped.");
                    continue;
                }

                if (camera.targetTexture != null && (camera.targetTexture.width == 0 || camera.targetTexture.height == 0))
                {
                    Debug.LogWarning($"Camera '{camera.name}' has an invalid render target size (width: {camera.targetTexture.width}, height: {camera.targetTexture.height}). Camera will be skipped.");
                    continue;
                }

                if (camera.pixelWidth == 0 || camera.pixelHeight == 0)
                {
                    Debug.LogWarning($"Camera '{camera.name}' has invalid pixel dimensions (width: {camera.pixelWidth}, height: {camera.pixelHeight}). Camera will be skipped.");
                    continue;
                }

                // 直接使用相机的 TryGetCullingParameters 获取剔除参数
                // 这样可以避免创建 UniversalCameraData，因为它在后续渲染流程中会被创建
                // 注意：对于 XR 相机，可能需要特殊处理，但大部分情况下相机的 TryGetCullingParameters 可以正常工作
                if (!camera.TryGetCullingParameters(false, out var cullingParameters))
                    continue;

                var cullResults = context.Cull(ref cullingParameters);
                
                // 收集这个相机可见的探针
                var visibleProbes = cullResults.visibleReflectionProbes;
                for (int i = 0; i < visibleProbes.Length; i++)
                {
                    var probe = visibleProbes[i].reflectionProbe;
                    if (probe != null && probe.mode == ReflectionProbeMode.Realtime)
                    {                  
                        processedProbes.Add(probe);
                    }
                }
            }
        }

        /// <summary>
        /// 判断反射探针是否需要更新
        /// </summary>
        private bool ShouldUpdateProbe(ReflectionProbe probe)
        {
            if (probe == null)
                return false;

            // 使用自定义的 RefreshMode（来自 UniversalAdditionalReflectionProbeData）
            // 而不是 ReflectionProbe 原生的 refreshMode（已固定为 ViaScripting）
            var probeData = probe.GetUniversalAdditionalReflectionProbeData();
            ReflectionProbeRefreshMode refreshMode;
            
            if (probeData != null)
            {
                // 使用自定义的 RefreshMode
                refreshMode = probeData.customRefreshMode;
            }
            else
            {
                // Fallback: 如果没有 UniversalAdditionalReflectionProbeData，使用原生的 refreshMode
                refreshMode = probe.refreshMode;
            }

            // 根据刷新模式判断是否需要更新
            switch (refreshMode)
            {
                case ReflectionProbeRefreshMode.EveryFrame:
                    return true;

                case ReflectionProbeRefreshMode.OnAwake:
                    // 只在 Awake 时更新一次，这里简化处理，实际应该跟踪是否已更新
                    return true;

                case ReflectionProbeRefreshMode.ViaScripting:
                    // 通过脚本控制，这里不自动更新
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 判断指定探针是否正在渲染
        /// </summary>
        /// <param name="probe">要检查的反射探针</param>
        /// <returns>如果探针正在渲染（有未完成的任务），返回 true；否则返回 false</returns>
        public bool IsProbeRendering(ReflectionProbe probe)
        {
            if (probe == null)
                return false;

            // 检查该探针是否在任务计数字典中
            if (m_ProbeTaskCounts.TryGetValue(probe, out int lastTaskFrame))
            {
                int currentFrame = Time.renderedFrameCount;
                // 如果最后一个任务的执行帧 > 当前帧，说明还有未完成的任务，正在渲染
                return lastTaskFrame > currentFrame;
            }

            // 如果探针不在任务计数字典中，说明没有正在渲染
            return false;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            processedProbes.Clear();
            m_RenderTaskQueue.Clear();
            m_ProbeTaskCounts.Clear();
            if (m_Renderer != null)
            {
                m_Renderer.Cleanup();
            }
            m_Renderer = null;
        }

        /// <summary>
        /// 销毁单例实例（用于测试或重置）
        /// </summary>
        public static void DestroyInstance()
        {
            lock (s_Lock)
            {
                if (s_Instance != null)
                {
                    s_Instance.Cleanup();
                    s_Instance = null;
                }
            }
        }
    }
}
