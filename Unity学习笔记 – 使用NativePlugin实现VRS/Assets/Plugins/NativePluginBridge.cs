using System.Runtime.InteropServices;
using System;
public enum PipelineShadingRate
{
    Rate1x1 = 0x0, Rate1x2 = 0x1, Rate2x1 = 0x4,
    Rate2x2 = 0x5, Rate2x4 = 0x6, Rate4x2 = 0x9, Rate4x4 = 0xA,
}
public enum PipelineShadingRateCombiner
{
    Passthrough = 0, Override = 1, Min = 2, Max = 3, Sum = 4,
}

public static class NativePluginBridge
{
    const string PluginName = "GfxPluginVRSPlugin";

    [DllImport(PluginName)]
    static extern int IsPipelineVRSSupported();
    // 能力查询：设备是否支持 Pipeline VRS
    public static bool IsPipelineVRSSupportedQuery()
        => IsPipelineVRSSupported() != 0;

    // 与 native 的 EVENT ID 保持一致
    public const int PipelineShadingRate_EVENT_ID = 0;
    public const int SetAttachmentShadingRate_EVENT_ID = 1;
    public const int ResetAttachmentShadingRate_EVENT_ID = 2;

    // 位布局与 native 的 SHIFT/MASK 一致
    public const int RATE_SHIFT = 0;
    public const int COMB0_SHIFT = 4;
    public const int COMB1_SHIFT = 7;
    public const int RATE_MASK = 0xF;
    public const int COMB0_MASK = 0x7;
    public const int COMB1_MASK = 0x7;

    // 打包：rate/comb0/comb1 各占一段，位移用宏
    public static int PackRate(int rate, int c0, int c1)
        => ((rate & RATE_MASK) << RATE_SHIFT)
         | ((c0 & COMB0_MASK) << COMB0_SHIFT)
         | ((c1 & COMB1_MASK) << COMB1_SHIFT);

    [DllImport(PluginName, EntryPoint = "GetRenderEventFunc")]
    static extern IntPtr GetRenderEventDataFunc();

    static IntPtr f;
    public static IntPtr RenderEventAndData =>
        f == IntPtr.Zero ? (f = GetRenderEventDataFunc()) : f;

    // [VRS-DIAG] 临时诊断：Tier2 能力直查。根因确认后删除
    [DllImport(PluginName)]
    public static extern int IsAttachmentVRSSupported();

    [DllImport(PluginName)]
    private static extern int GetShadingRateImageTileSize();
    public static int GetShadingRateImageTileSizeQuery()
    {
        return GetShadingRateImageTileSize();
    }
}