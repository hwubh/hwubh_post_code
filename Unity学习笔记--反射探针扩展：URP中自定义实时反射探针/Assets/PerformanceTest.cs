using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class OctahedralmapTest : MonoBehaviour
{
    #region Inspector - UI

    public TMP_Dropdown m_ResoluionDropDown;
    public Button m_StopEveryFrame;
    public GameObject m_RealtimeReflectionProbe;
    public GameObject m_GroupOctahedralmap;
    public GameObject m_GroupCubeMap3Times;
    public GameObject m_GroupOctahedralmap3Times;

    #endregion

    #region Realtime Probe

    internal ReflectionProbe probe;

    #endregion

    #region Frame Stats

    GUIStyle m_Style;
    readonly FrameTiming[] m_FrameTimings = new FrameTiming[10];
    readonly float[] m_Fps = new float[10];
    int m_FrameCount;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        m_Style = new GUIStyle
        {
            fontSize = 40,
            normal = { textColor = Color.white }
        };
        m_FrameCount = 0;
        Application.targetFrameRate = 30;

        if (m_RealtimeReflectionProbe != null)
            probe = m_RealtimeReflectionProbe.GetComponent<ReflectionProbe>();
    }

    void Start()
    {
        if (m_StopEveryFrame != null)
        {
            m_StopEveryFrame.onClick.AddListener(SwitchCustomNativeRealtimeProbe);
            UpdateCustomNativeButtonText();
        }
    }

    void OnGUI()
    {
        CaptureTimings();

        double avgFps = 0, avgCpu = 0, avgGpu = 0;
        for (int i = 0; i < m_FrameTimings.Length; i++)
        {
            avgFps += m_Fps[i];
            avgCpu += m_FrameTimings[i].cpuFrameTime;
            avgGpu += m_FrameTimings[i].gpuFrameTime;
        }
        int n = m_FrameTimings.Length;
        avgFps /= n;
        avgCpu /= n;
        avgGpu /= n;

        var msg =
            $"\nResolution : {Screen.currentResolution}" +
            $"\nGraphics API : {SystemInfo.graphicsDeviceType}" +
            $"\nFrame rate: {(1f / Time.unscaledDeltaTime):00.00} FPS" +
            $"\nCPU: {m_FrameTimings[0].cpuFrameTime:00.00}ms" +
            $"\nMain Thread: {m_FrameTimings[0].cpuMainThreadFrameTime:00.00}ms" +
            $"\nRender Thread: {m_FrameTimings[0].cpuRenderThreadFrameTime:00.00} ms" +
            $"\nGPU: {m_FrameTimings[0].gpuFrameTime:00.00} ms" +
            $"\nAverage FPS (10 frame): {avgFps:00.00} FPS" +
            $"\nAverage CPU (10 frame): {avgCpu:00.00} ms" +
            $"\nAverage GPU (10 frame): {avgGpu:00.00} ms";

        var oldColor = GUI.color;
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(32, 50, 700, 700), "Frame Stats", GUI.skin.window);
        GUILayout.Label(msg, m_Style);
        GUILayout.EndArea();
        GUI.color = oldColor;
    }

    #endregion

    #region Custom / Native Realtime Probe

    /// <summary>
    /// 切换「自定义实时反射探针」与「Unity 原生」模式。
    /// 自定义：由 URP RealtimeReflectionProbeManager 更新；原生：探针 EveryFrame 由 Unity 更新。
    /// </summary>
    void SwitchCustomNativeRealtimeProbe()
    {
        var asset = UniversalRenderPipeline.asset;
        if (asset == null || probe == null || m_StopEveryFrame == null) return;

        if (asset.useCustomRealtimeReflectionProbe)
        {
            asset.useCustomRealtimeReflectionProbe = false;
            probe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;
        }
        else
        {
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            asset.useCustomRealtimeReflectionProbe = true;
        }
        UpdateCustomNativeButtonText();
    }

    void UpdateCustomNativeButtonText()
    {
        if (m_StopEveryFrame == null) return;
        var asset = UniversalRenderPipeline.asset;
        var text = m_StopEveryFrame.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.text = (asset != null && asset.useCustomRealtimeReflectionProbe) ? "Custom" : "Unity";
    }

    #endregion

    #region Frame Timing

    void CaptureTimings()
    {
        FrameTimingManager.CaptureFrameTimings();
        FrameTimingManager.GetLatestTimings((uint)m_FrameTimings.Length, m_FrameTimings);
        m_Fps[m_FrameCount++ % 10] = 1f / Time.unscaledDeltaTime;
    }

    #endregion
}
