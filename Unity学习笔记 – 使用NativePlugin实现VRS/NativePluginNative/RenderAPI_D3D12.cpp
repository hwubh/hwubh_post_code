// RenderAPI_D3D12.cpp
#include "RenderAPI_D3D12.h"

#include <cstring> // memset
#include <cstdio>
#include <cstdarg>

#ifndef SAFE_RELEASE
#define SAFE_RELEASE(x) if (x) { x->Release(); x = nullptr; }
#endif

// [VRS-DIAG] 临时诊断日志：OutputDebugStringA + 文件双写（无 DebugView 时读文件）。根因确认后整块删除
void VRSLog(const char* fmt, ...)
{
    char buf[512];
    va_list args;
    va_start(args, fmt);
    vsnprintf_s(buf, sizeof(buf), _TRUNCATE, fmt, args);
    va_end(args);
    OutputDebugStringA(buf);
    FILE* f = nullptr;
    fopen_s(&f, "C:\\UNITY6\\NativePluginVRS\\vrs_diag.log", "a");
    if (f) { fputs(buf, f); fclose(f); }
}

void RenderAPI_D3D12::OnDeviceInit(IUnityInterfaces*, IUnityGraphicsD3D12v7* d3d12)
{
    m_D3D12 = d3d12;
    InitializeVRSCapabilities();
}

void RenderAPI_D3D12::InitializeVRSCapabilities()
{
    ID3D12Device* device = m_D3D12 != nullptr ? m_D3D12->GetDevice() : nullptr;
    if (!device)
    {
        VRSLog("[VRS-DIAG] caps: device null, VRS disabled\n");
        m_PipelineVRSSupported = false;
        m_AttachmentVRSSupported = false;
        m_ShadingRateImageTileSize = 16;
        return;
    }

    D3D12_FEATURE_DATA_D3D12_OPTIONS6 options6 = {};
    HRESULT hr = device->CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS6, &options6, sizeof(options6));
    if (SUCCEEDED(hr))
    {
        D3D12_VARIABLE_SHADING_RATE_TIER m_VRSTier = options6.VariableShadingRateTier;
        m_PipelineVRSSupported = (m_VRSTier >= D3D12_VARIABLE_SHADING_RATE_TIER_1);
        m_AttachmentVRSSupported    = (m_VRSTier >= D3D12_VARIABLE_SHADING_RATE_TIER_2);
        m_ShadingRateImageTileSize = options6.ShadingRateImageTileSize; 
        
    }
    else
    {
        VRSLog("[VRS-DIAG] caps: CheckFeatureSupport(OPTIONS6) failed hr=0x%08X\n", (unsigned)hr);
        m_PipelineVRSSupported = false;
        m_AttachmentVRSSupported = false;
        m_ShadingRateImageTileSize = 16;
    }

    VRSLog("[VRS-DIAG] caps: pipeline=%d attachment=%d tileSize=%u maxTexels=%ux%u\n",
           m_PipelineVRSSupported ? 1 : 0, m_AttachmentVRSSupported ? 1 : 0,
           m_ShadingRateImageTileSize, m_SriMaxTexelsW, m_SriMaxTexelsH);
}

void RenderAPI_D3D12::OnDeviceShutdown()
{
    VRSLog("[VRS-DIAG] OnDeviceShutdown\n");
    ReleaseShadingRateImage();
    m_D3D12 = nullptr;
}

#pragma region pipeline_shading_rate

bool RenderAPI_D3D12::SetPipelineShadingRate(
    D3D12_SHADING_RATE rate,
    D3D12_SHADING_RATE_COMBINER combiner0,
    D3D12_SHADING_RATE_COMBINER combiner1)
{
    if (!m_PipelineVRSSupported)
        return false;

    UnityGraphicsD3D12RecordingState rec;
    if (!m_D3D12->CommandRecordingState(&rec))
        return false;

    // ID3D12GraphicsCommandList5 为 RSSetShadingRate 要求的最低版本
    ID3D12GraphicsCommandList5* cmd5 = nullptr;
    if (FAILED(rec.commandList->QueryInterface(IID_PPV_ARGS(&cmd5))))
        return false;

    D3D12_SHADING_RATE_COMBINER comb[2] = { combiner0, combiner1 };
    cmd5->RSSetShadingRate(rate, comb);   // 设置 Pipeline Shading Rate 和 CombinerOps
    cmd5->Release();

    return true;
}
#pragma endregion pipeline_shading_rate

#pragma region attachment_shading_rate

bool RenderAPI_D3D12::CreateShadingRateImage()
{
    ID3D12Device* device = m_D3D12 ? m_D3D12->GetDevice() : nullptr;
    if (!device || m_SriMaxTexelsW == 0 || m_SriMaxTexelsH == 0)
    {
        VRSLog("[VRS-DIAG] CreateShadingRateImage fail: device=%p max=%ux%u\n",
               (void*)device, m_SriMaxTexelsW, m_SriMaxTexelsH);
        return false;
    }

    // 1) SRI：R8_UINT max 尺寸纹理，初始 COMMON（堆 DEFAULT，flags=NONE）
    //    超配合法：DirectX-Specs "If the screen space image is larger than it needs
    //    to be for a given render target, the extra portions at the right and/or
    //    bottom are not used."
    D3D12_HEAP_PROPERTIES heapProps = {};
    heapProps.Type = D3D12_HEAP_TYPE_DEFAULT; // GPU-local

    D3D12_RESOURCE_DESC desc = {};
    desc.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D;
    desc.Width = m_SriMaxTexelsW;
    desc.Height = m_SriMaxTexelsH;
    desc.DepthOrArraySize = 1;
    desc.MipLevels = 1;
    desc.Format = DXGI_FORMAT_R8_UINT;
    desc.SampleDesc.Count = 1;

    HRESULT hr = device->CreateCommittedResource(
        &heapProps, D3D12_HEAP_FLAG_NONE, &desc,
        D3D12_RESOURCE_STATE_COMMON, nullptr,
        IID_PPV_ARGS(&m_SriResource));
    if (FAILED(hr) || !m_SriResource)
    {
        VRSLog("[VRS-DIAG] CreateShadingRateImage fail: SRI texture hr=0x%08X\n", (unsigned)hr);
        return false;
    }

    // 未使用区域不被采样（spec: "the extra portions at the right and/or bottom are not used"），
    // 无需零填充。直接停在 COMMON，首次 SetShadingRateImage 的 barrier 会转到 COPY_DEST。
    m_SRIState = D3D12_RESOURCE_STATE_COMMON;

    VRSLog("[VRS-DIAG] CreateShadingRateImage OK: %ux%u\n", m_SriMaxTexelsW, m_SriMaxTexelsH);
    return true;
}

void RenderAPI_D3D12::ReleaseShadingRateImage()
{
    // 仅 OnDeviceShutdown 调用：设备销毁时 GPU 工作已终止，立即释放安全
    SAFE_RELEASE(m_SriResource);
    m_SRIState = D3D12_RESOURCE_STATE_COMMON;
}

bool RenderAPI_D3D12::SetShadingRateImage(void* nativeTexPtr)
{
    // [VRS-DIAG]
    VRSLog("[VRS-DIAG] SetShadingRateImage enter, nativeTexPtr=%p\n", nativeTexPtr);
    if (!m_AttachmentVRSSupported)
    {
        VRSLog("[VRS-DIAG] SetShadingRateImage early-return: attachment VRS unsupported\n");
        return false;
    }

    m_SriSource = static_cast<ID3D12Resource*>(nativeTexPtr);
    if (!m_SriSource)
    {
        VRSLog("[VRS-DIAG] SetShadingRateImage early-return: source null\n");
        return false;
    }

    // 源必须落在 SRI 覆盖范围内（超限跳过并告警，绝不中途重建——见 Fix Plan §3.2）
    const D3D12_RESOURCE_DESC srcDesc = m_SriSource->GetDesc();
    if (srcDesc.Width > m_SriMaxTexelsW || srcDesc.Height > m_SriMaxTexelsH)
    {
        OutputDebugStringA("[VRS] source exceeds max SRI coverage, skipped\n");
        VRSLog("[VRS-DIAG] SetShadingRateImage early-return: src %ux%u > max %ux%u\n",
               (unsigned)srcDesc.Width, (unsigned)srcDesc.Height, m_SriMaxTexelsW, m_SriMaxTexelsH);
        return false;
    }

    UnityGraphicsD3D12RecordingState recordingState;
    if (!m_D3D12->CommandRecordingState(&recordingState))
    {
        VRSLog("[VRS-DIAG] SetShadingRateImage early-return: CommandRecordingState false\n");
        return false;
    }

    ID3D12GraphicsCommandList5* cmd5 = nullptr;
    HRESULT hr = recordingState.commandList->QueryInterface(
        IID_PPV_ARGS(&cmd5));
    if (FAILED(hr) || !cmd5)
    {
        VRSLog("[VRS-DIAG] SetShadingRateImage early-return: QI cmd5 failed hr=0x%08X\n", (unsigned)hr);
        return false;
    }

    // 惰性创建（一次性）：COMMON → [零填充] → 停留在 COPY_DEST，
    // 下方"目标 → COPY_DEST"的 barrier 会因状态一致而自然跳过
    if (!m_SriResource && !CreateShadingRateImage())
    {
        VRSLog("[VRS-DIAG] SetShadingRateImage early-return: CreateShadingRateImage failed\n");
        cmd5->Release();
        return false;
    }

    // 0. 源贴图（Unity 的 R8_UInt RenderTexture）barrier：COMMON → COPY_SOURCE
    //    用 COMMON 作为 StateBefore：Unity 用 SetRandomWriteTarget 管理 UAV 状态，
    //    COMMON 是通用状态，可安全进入 COPY_SOURCE，避免与 Unity 状态冲突。
    D3D12_RESOURCE_BARRIER srcBarrier = {};
    srcBarrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
    srcBarrier.Transition.pResource = m_SriSource;
    srcBarrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
    srcBarrier.Transition.StateBefore = D3D12_RESOURCE_STATE_COMMON;
    srcBarrier.Transition.StateAfter  = D3D12_RESOURCE_STATE_COPY_SOURCE;
    cmd5->ResourceBarrier(1, &srcBarrier);

    // 1. 目标（SRI）barrier → COPY_DEST
    if (m_SRIState != D3D12_RESOURCE_STATE_COPY_DEST)
    {
        D3D12_RESOURCE_BARRIER dstBarrier = {};
        dstBarrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        dstBarrier.Transition.pResource = m_SriResource;
        dstBarrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
        dstBarrier.Transition.StateBefore = m_SRIState;
        dstBarrier.Transition.StateAfter  = D3D12_RESOURCE_STATE_COPY_DEST;
        cmd5->ResourceBarrier(1, &dstBarrier);
        m_SRIState = D3D12_RESOURCE_STATE_COPY_DEST;
    }

    // 2. 拷贝：Unity 贴图 → SRI（GPU→GPU，全程无 CPU 往返）
    D3D12_TEXTURE_COPY_LOCATION dst = {};
    dst.pResource = m_SriResource;
    dst.Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX;
    dst.SubresourceIndex = 0;

    D3D12_TEXTURE_COPY_LOCATION src = {};
    src.pResource = m_SriSource;
    src.Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX;
    src.SubresourceIndex = 0;

    cmd5->CopyTextureRegion(&dst, 0, 0, 0, &src, nullptr);

    // 3. 源贴图 barrier 回 COMMON（Unity 状态一致）
    D3D12_RESOURCE_BARRIER srcBack = {};
    srcBack.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
    srcBack.Transition.pResource = m_SriSource;
    srcBack.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
    srcBack.Transition.StateBefore = D3D12_RESOURCE_STATE_COPY_SOURCE;
    srcBack.Transition.StateAfter  = D3D12_RESOURCE_STATE_COMMON;
    cmd5->ResourceBarrier(1, &srcBack);

    // 4. SRI barrier → SHADING_RATE_SOURCE
    if (m_SRIState != D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE)
    {
        D3D12_RESOURCE_BARRIER transitionBarrier = {};
        transitionBarrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        transitionBarrier.Transition.pResource = m_SriResource;
        transitionBarrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
        transitionBarrier.Transition.StateBefore = m_SRIState;
        transitionBarrier.Transition.StateAfter  = D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE;
        cmd5->ResourceBarrier(1, &transitionBarrier);
        m_SRIState = D3D12_RESOURCE_STATE_SHADING_RATE_SOURCE;
    }

    // 6. 绑定 SRI
    cmd5->RSSetShadingRateImage(m_SriResource);
    cmd5->Release();

    VRSLog("[VRS-DIAG] SetShadingRateImage OK: sri=%p src=%p\n", (void*)m_SriResource, (void*)m_SriSource);
    return true;
}

bool RenderAPI_D3D12::ClearShadingRateImage()
{
    // [VRS-DIAG]
    VRSLog("[VRS-DIAG] ClearShadingRateImage enter\n");
    if (!m_AttachmentVRSSupported || !m_SriResource)
    {
        VRSLog("[VRS-DIAG] ClearShadingRateImage early-return: unsupported or SRI not created\n");
        return false;
    }

    UnityGraphicsD3D12RecordingState recordingState;
    if (!m_D3D12->CommandRecordingState(&recordingState))
        return false;

    // 统一使用 cmd5，所有操作都用它。
    ID3D12GraphicsCommandList5* cmd5 = nullptr;
    HRESULT hr = recordingState.commandList->QueryInterface(
        IID_PPV_ARGS(&cmd5));
    if (FAILED(hr) || !cmd5)
    {
        OutputDebugStringA("[VRS] ClearShadingRateImage: QueryInterface for ID3D12GraphicsCommandList5 failed\n");
        return false;
    }

    // 解除 SRI 绑定（只解绑，不转回 COMMON——下一帧 set 事件直接
    // SHADING_RATE_SOURCE → COPY_DEST，每帧省一对 barrier）
    cmd5->RSSetShadingRateImage(nullptr);

    cmd5->Release();

    OutputDebugStringA("[VRS] ClearShadingRateImage: RSSet(nullptr)\n");
    VRSLog("[VRS-DIAG] ClearShadingRateImage OK (resource=%p)\n", (void*)m_SriResource);
    return true;
}

#pragma endregion attachment_shading_rate