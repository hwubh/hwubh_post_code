// RenderAPI_D3D12.h
#pragma once

// include 顺序关键：IUnityGraphicsD3D12.h 使用 IDXGISwapChain/UINT32 等(来自 dxgi1_6.h 与 windows.h)，
// 以及 ID3D12Device/ID3D12GraphicsCommandList5 等(来自 d3d12.h)，但它本身不 include 这些，
// 必须由本头文件先引入。
#include <windows.h>
#include <d3d12.h>
#include <dxgi1_6.h>
#include "IUnityGraphicsD3D12.h"

// [VRS-DIAG] 临时诊断日志（定义在 RenderAPI_D3D12.cpp）：OutputDebugStringA + 文件双写。根因确认后删除
void VRSLog(const char* fmt, ...);

class RenderAPI_D3D12
{
public:
    void OnDeviceInit(IUnityInterfaces* interfaces, IUnityGraphicsD3D12v7* d3d12);
    void OnDeviceShutdown();

    bool SetPipelineShadingRate(
        D3D12_SHADING_RATE rate,
        D3D12_SHADING_RATE_COMBINER combiner0,
        D3D12_SHADING_RATE_COMBINER combiner1);

    bool IsPipelineVRSSupported() const { return m_PipelineVRSSupported; }
    bool IsAttachmentVRSSupported() const { return m_AttachmentVRSSupported; }
    UINT GetShadingRateImageTileSize() const { return m_ShadingRateImageTileSize; }
    
    ID3D12Resource* m_SriSource = nullptr; 
    bool SetShadingRateImage(void* nativeTexPtr); 
    bool ClearShadingRateImage();

private:
    void InitializeVRSCapabilities();

    IUnityGraphicsD3D12v7* m_D3D12 = nullptr;
    bool m_PipelineVRSSupported = false;
    bool m_AttachmentVRSSupported = false;
    UINT m_ShadingRateImageTileSize = 16; // 通常 16

    // SRI related parameters.
    // 方案 B（DX12_SRIResourceLifetime_Fix_Plan.md）：按最大 RT 尺寸一次性分配，终生不重建，
    // 从根本上消除"释放仍被在飞 command list 引用的资源"问题。
    ID3D12Resource* m_SriResource = nullptr; // SRI 图（max 尺寸）
    D3D12_RESOURCE_STATES m_SRIState = D3D12_RESOURCE_STATE_COMMON;
    static constexpr UINT m_SriMaxTexelsW = 512;
    static constexpr UINT m_SriMaxTexelsH = 256;

    bool CreateShadingRateImage();
    void ReleaseShadingRateImage();
};