// GfxPluginVRSPlugin.cpp
#include <windows.h>
#include <d3d12.h>
#include <dxgi1_6.h> 

#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D12.h"
#include "RenderAPI_D3D12.h"            

// ---- 全局状态：跨回调共享 ——
static IUnityInterfaces*      s_UnityInterfaces = nullptr;  // 注册表：Get<...>() 取接口
static IUnityGraphics*        s_Graphics        = nullptr;  // 图形接口：注册设备回调/判后端
static IUnityGraphicsD3D12v7* s_D3D12           = nullptr;  // D3D12 接口：ConfigureEvent / 拿 command list
static RenderAPI_D3D12*       s_API             = nullptr;  // VRS 实现类实例

// 前置声明：静态回调定义在本文件较后位置，UnityPluginLoad/Unload 需先见其声明
static void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType);

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
    s_UnityInterfaces = unityInterfaces;
    s_Graphics = unityInterfaces->Get<IUnityGraphics>();
    s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginUnload()
{
    if (s_Graphics) { s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent); s_Graphics = nullptr; }
}

#define PipelineShadingRate_EVENT_ID         0 //设置 pipeline shaidng rate 的渲染事件ID
#define SetAttachmentShadingRate_EVENT_ID         1 //设置 attachment shaidng rate 的渲染事件ID
#define ResetAttachmentShadingRate_EVENT_ID         2 //清楚 attachment shaidng rate 的渲染事件ID

// ---- 设备事件回调（设备创建完毕/销毁前）----
static void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    if (eventType == kUnityGfxDeviceEventInitialize)
    {
        if (s_Graphics->GetRenderer() == kUnityGfxRendererD3D12)
        {
            s_D3D12 = s_UnityInterfaces->Get<IUnityGraphicsD3D12v7>(); //

            UnityD3D12PluginEventConfig vrsCfg = {};
            vrsCfg.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_DontCare;
            vrsCfg.flags = kUnityD3D12EventConfigFlag_SyncWorkerThreads
                         | kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState
                         | kUnityD3D12EventConfigFlag_EnsurePreviousFrameSubmission;
            vrsCfg.ensureActiveRenderTextureIsBound = true;
            s_D3D12->ConfigureEvent(PipelineShadingRate_EVENT_ID, &vrsCfg);
            s_D3D12->ConfigureEvent(SetAttachmentShadingRate_EVENT_ID, &vrsCfg);
            s_D3D12->ConfigureEvent(ResetAttachmentShadingRate_EVENT_ID, &vrsCfg);

            s_API = new RenderAPI_D3D12();
            s_API->OnDeviceInit(s_UnityInterfaces, s_D3D12);
        }
    }
    else if (eventType == kUnityGfxDeviceEventShutdown)
    {
        if (s_API) { s_API->OnDeviceShutdown(); delete s_API; s_API = nullptr; }
    }
}

#define VRS_COMB0_SHIFT   4          
#define VRS_COMB1_SHIFT   7          
#define VRS_RATE_MASK     0xF        // rate 位宽（4bit） 0~3
#define VRS_COMB0_MASK    0x7        // combiner0 位宽（3bit） 3~5
#define VRS_COMB1_MASK    0x7        // combiner1 位宽（3bit） 6~8

static void UNITY_INTERFACE_API OnRenderEventData(int eventID, void* data)
{
    // [VRS-DIAG] 事件可达性：任何到达的插件事件都写日志。根因确认后删除
    VRSLog("[VRS-DIAG] OnRenderEventData id=%d data=%p api=%p\n", eventID, data, (void*)s_API);
    if (s_API == nullptr) return;

    switch (eventID)
    {
        case PipelineShadingRate_EVENT_ID:
        {
            int p = (int)(intptr_t)data;
            D3D12_SHADING_RATE rate = (D3D12_SHADING_RATE)(p & VRS_RATE_MASK);
            D3D12_SHADING_RATE_COMBINER c0 = (D3D12_SHADING_RATE_COMBINER)((p >> VRS_COMB0_SHIFT) & VRS_COMB0_MASK);
            D3D12_SHADING_RATE_COMBINER c1 = (D3D12_SHADING_RATE_COMBINER)((p >> VRS_COMB1_SHIFT) & VRS_COMB1_MASK);
            s_API->SetPipelineShadingRate(rate, c0, c1);
            break;
        }
        case SetAttachmentShadingRate_EVENT_ID:
        {
            s_API->SetShadingRateImage(data);
            break;
        }
        case ResetAttachmentShadingRate_EVENT_ID:
        {
            s_API->ClearShadingRateImage();
            break;
        }
    default:
        break;
    }
}

extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
GetRenderEventFunc()
{
    return OnRenderEventData; 
}

// 能力查询：C# 侧据此判断设备是否支持 Pipeline VRS，不支持则跳过整个 feature
extern "C" int UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
IsPipelineVRSSupported()
{
    return (s_API && s_API->IsPipelineVRSSupported()) ? 1 : 0;
}

extern "C" int UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
IsAttachmentVRSSupported()
{
    return (s_API && s_API->IsAttachmentVRSSupported()) ? 1 : 0;
}

extern "C" int UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
GetShadingRateImageTileSize()
{
    return s_API ? (int)s_API->GetShadingRateImageTileSize() : 16;
}