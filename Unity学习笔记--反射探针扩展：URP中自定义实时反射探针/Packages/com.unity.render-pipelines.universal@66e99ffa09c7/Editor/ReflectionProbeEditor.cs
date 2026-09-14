// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.AnimatedValues;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace UnityEditor
{
#region ReflectionCode
    /// <summary>
    /// 反射包装类，用于访问 Unity Editor 内部类型和方法
    /// </summary>
    internal static class ReflectionProbeEditorReflection
    {
        private static Assembly s_EditorAssembly;
        private static Assembly EditorAssembly
        {
            get
            {
                if (s_EditorAssembly == null)
                    s_EditorAssembly = typeof(Editor).Assembly;
                return s_EditorAssembly;
            }
        }

        // SavedBool 反射包装
        internal static class SavedBoolReflection
        {
            private static Type s_Type;
            private static ConstructorInfo s_Constructor;
            private static PropertyInfo s_ValueProperty;

            public static Type UnderlyingType
            {
                get
                {
                    if (s_Type != null)
                        return s_Type;
                    s_Type = EditorAssembly.GetType("UnityEditor.SavedBool");
                    if (s_Type == null)
                        throw new Exception("Failed to locate SavedBool type");
                    return s_Type;
                }
            }

            public static object CreateInstance(string key, bool defaultValue)
            {
                if (s_Constructor == null)
                {
                    s_Constructor = UnderlyingType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null,
                        new Type[] { typeof(string), typeof(bool) }, null);
                    if (s_Constructor == null)
                        throw new Exception("Failed to locate SavedBool constructor");
                }
                return s_Constructor.Invoke(new object[] { key, defaultValue });
            }

            public static bool GetValue(object instance)
            {
                if (s_ValueProperty == null)
                {
                    s_ValueProperty = UnderlyingType.GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
                    if (s_ValueProperty == null)
                        throw new Exception("Failed to locate SavedBool.value property");
                }
                return (bool)s_ValueProperty.GetValue(instance);
            }

            public static void SetValue(object instance, bool value)
            {
                if (s_ValueProperty == null)
                {
                    s_ValueProperty = UnderlyingType.GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
                    if (s_ValueProperty == null)
                        throw new Exception("Failed to locate SavedBool.value property");
                }
                s_ValueProperty.SetValue(instance, value);
            }
        }

        // TextureInspector 反射包装
        internal static class TextureInspectorReflection
        {
            private static Type s_Type;
            private static PropertyInfo s_MipLevelProperty;
            private static MethodInfo s_GetMipLevelForRenderingMethod;
            private static MethodInfo s_SetCubemapIntensityMethod;
            private static MethodInfo s_OnPreviewSettingsMethod;
            private static MethodInfo s_OnPreviewGUIMethod;

            public static Type UnderlyingType
            {
                get
                {
                    if (s_Type != null)
                        return s_Type;
                    s_Type = EditorAssembly.GetType("UnityEditor.TextureInspector");
                    if (s_Type == null)
                        throw new Exception("Failed to locate TextureInspector type");
                    return s_Type;
                }
            }

            public static float GetMipLevel(object instance)
            {
                if (s_MipLevelProperty == null)
                {
                    s_MipLevelProperty = UnderlyingType.GetProperty("mipLevel", BindingFlags.Public | BindingFlags.Instance);
                    if (s_MipLevelProperty == null)
                        throw new Exception("Failed to locate TextureInspector.mipLevel property");
                }
                return (float)s_MipLevelProperty.GetValue(instance);
            }

            public static void SetMipLevel(object instance, float value)
            {
                if (s_MipLevelProperty == null)
                {
                    s_MipLevelProperty = UnderlyingType.GetProperty("mipLevel", BindingFlags.Public | BindingFlags.Instance);
                    if (s_MipLevelProperty == null)
                        throw new Exception("Failed to locate TextureInspector.mipLevel property");
                }
                s_MipLevelProperty.SetValue(instance, value);
            }

            public static float GetMipLevelForRendering(object instance)
            {
                if (s_GetMipLevelForRenderingMethod == null)
                {
                    s_GetMipLevelForRenderingMethod = UnderlyingType.GetMethod("GetMipLevelForRendering", BindingFlags.Public | BindingFlags.Instance);
                    if (s_GetMipLevelForRenderingMethod == null)
                        throw new Exception("Failed to locate TextureInspector.GetMipLevelForRendering method");
                }
                return (float)s_GetMipLevelForRenderingMethod.Invoke(instance, null);
            }

            public static void SetCubemapIntensity(object instance, float intensity)
            {
                if (s_SetCubemapIntensityMethod == null)
                {
                    s_SetCubemapIntensityMethod = UnderlyingType.GetMethod("SetCubemapIntensity", BindingFlags.NonPublic | BindingFlags.Instance, null,
                        new Type[] { typeof(float) }, null);
                }
                if (s_SetCubemapIntensityMethod != null)
                    s_SetCubemapIntensityMethod.Invoke(instance, new object[] { intensity });
            }

            public static void OnPreviewSettings(object instance)
            {
                if (s_OnPreviewSettingsMethod == null)
                {
                    s_OnPreviewSettingsMethod = UnderlyingType.GetMethod("OnPreviewSettings", BindingFlags.Public | BindingFlags.Instance);
                }
                if (s_OnPreviewSettingsMethod != null)
                    s_OnPreviewSettingsMethod.Invoke(instance, null);
            }

            public static void OnPreviewGUI(object instance, Rect r, GUIStyle background)
            {
                if (s_OnPreviewGUIMethod == null)
                {
                    s_OnPreviewGUIMethod = UnderlyingType.GetMethod("OnPreviewGUI", BindingFlags.Public | BindingFlags.Instance, null,
                        new Type[] { typeof(Rect), typeof(GUIStyle) }, null);
                }
                if (s_OnPreviewGUIMethod != null)
                    s_OnPreviewGUIMethod.Invoke(instance, new object[] { r, background });
            }
        }

        // 其他 Editor 类型的反射包装
        internal static class EditorTypeReflection
        {
            public static Type GetCubemapInspectorType()
            {
                return EditorAssembly.GetType("UnityEditor.CubemapInspector");
            }

            public static Type GetCustomRenderTextureEditorType()
            {
                return EditorAssembly.GetType("UnityEditor.CustomRenderTextureEditor");
            }

            public static Type GetRenderTextureEditorType()
            {
                return EditorAssembly.GetType("UnityEditor.RenderTextureEditor");
            }
        }

        // FileUtil 反射包装
        internal static class FileUtilReflection
        {
            private static MethodInfo s_GetPathWithoutExtensionMethod;

            public static string GetPathWithoutExtension(string path)
            {
                if (s_GetPathWithoutExtensionMethod == null)
                {
                    Type fileUtilType = typeof(FileUtil);
                    s_GetPathWithoutExtensionMethod = fileUtilType.GetMethod("GetPathWithoutExtension", BindingFlags.Public | BindingFlags.Static);
                    if (s_GetPathWithoutExtensionMethod == null)
                    {
                        // 如果方法不存在，使用 Path 类的方法
                        return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
                    }
                }
                if (s_GetPathWithoutExtensionMethod != null)
                    return (string)s_GetPathWithoutExtensionMethod.Invoke(null, new object[] { path });
                return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
            }
        }

        // Lightmapping 反射包装
        internal static class LightmappingReflection
        {
            private static MethodInfo s_BakeAllReflectionProbesSnapshotsMethod;
            private static MethodInfo s_BakeReflectionProbeSnapshotMethod;

            public static void BakeAllReflectionProbesSnapshots()
            {
                if (s_BakeAllReflectionProbesSnapshotsMethod == null)
                {
                    Type lightmappingType = typeof(Lightmapping);
                    s_BakeAllReflectionProbesSnapshotsMethod = lightmappingType.GetMethod("BakeAllReflectionProbesSnapshots", BindingFlags.Public | BindingFlags.Static);
                }
                if (s_BakeAllReflectionProbesSnapshotsMethod != null)
                    s_BakeAllReflectionProbesSnapshotsMethod.Invoke(null, null);
            }

            public static void BakeReflectionProbeSnapshot(ReflectionProbe probe)
            {
                if (s_BakeReflectionProbeSnapshotMethod == null)
                {
                    Type lightmappingType = typeof(Lightmapping);
                    s_BakeReflectionProbeSnapshotMethod = lightmappingType.GetMethod("BakeReflectionProbeSnapshot", BindingFlags.Public | BindingFlags.Static, null,
                        new Type[] { typeof(ReflectionProbe) }, null);
                }
                if (s_BakeReflectionProbeSnapshotMethod != null)
                    s_BakeReflectionProbeSnapshotMethod.Invoke(null, new object[] { probe });
            }
        }

        // EditMode 反射包装
        internal static class EditModeReflection
        {
            private static MethodInfo s_DoInspectorToolbarMethod;

            public static void DoInspectorToolbar(EditMode.SceneViewEditMode[] modes, GUIContent[] contents, Editor editor)
            {
                if (s_DoInspectorToolbarMethod == null)
                {
                    Type editModeType = typeof(EditMode);
                    // 尝试不同的方法签名
                    s_DoInspectorToolbarMethod = editModeType.GetMethod("DoInspectorToolbar", BindingFlags.Public | BindingFlags.Static, null,
                        new Type[] { typeof(EditMode.SceneViewEditMode[]), typeof(GUIContent[]), typeof(Editor) }, null);
                    if (s_DoInspectorToolbarMethod == null)
                    {
                        // 尝试 2 个参数的重载
                        s_DoInspectorToolbarMethod = editModeType.GetMethod("DoInspectorToolbar", BindingFlags.Public | BindingFlags.Static, null,
                            new Type[] { typeof(EditMode.SceneViewEditMode[]), typeof(GUIContent[]) }, null);
                    }
                }
                if (s_DoInspectorToolbarMethod != null)
                {
                    if (s_DoInspectorToolbarMethod.GetParameters().Length == 3)
                        s_DoInspectorToolbarMethod.Invoke(null, new object[] { modes, contents, editor });
                    else
                        s_DoInspectorToolbarMethod.Invoke(null, new object[] { modes, contents });
                }
            }
        }

        // Toolbar 反射包装
        internal static class ToolbarReflection
        {
            private static Type s_Type;
            private static PropertyInfo s_GetProperty;

            public static object GetToolbar()
            {
                if (s_Type == null)
                {
                    s_Type = EditorAssembly.GetType("UnityEditor.Toolbar");
                    if (s_Type == null)
                        return null;
                }
                if (s_GetProperty == null)
                {
                    s_GetProperty = s_Type.GetProperty("get", BindingFlags.Public | BindingFlags.Static);
                }
                if (s_GetProperty != null)
                    return s_GetProperty.GetValue(null);
                return null;
            }

            public static void Repaint(object toolbar)
            {
                if (toolbar == null || s_Type == null)
                    return;
                MethodInfo repaintMethod = s_Type.GetMethod("Repaint", BindingFlags.Public | BindingFlags.Instance);
                if (repaintMethod != null)
                    repaintMethod.Invoke(toolbar, null);
            }
        }

        // EditorGraphicsSettings 反射包装
        internal static class EditorGraphicsSettingsReflection
        {
            private static MethodInfo s_GetCurrentTierSettingsMethod;

            public static object GetCurrentTierSettings()
            {
                if (s_GetCurrentTierSettingsMethod == null)
                {
                    Type type = EditorAssembly.GetType("UnityEditor.EditorGraphicsSettings");
                    if (type != null)
                        s_GetCurrentTierSettingsMethod = type.GetMethod("GetCurrentTierSettings", BindingFlags.Public | BindingFlags.Static);
                }
                if (s_GetCurrentTierSettingsMethod != null)
                    return s_GetCurrentTierSettingsMethod.Invoke(null, null);
                return null;
            }

            public static bool GetReflectionProbeBoxProjection(object tierSettings)
            {
                if (tierSettings == null)
                    return false;
                PropertyInfo prop = tierSettings.GetType().GetProperty("reflectionProbeBoxProjection", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                    return (bool)prop.GetValue(tierSettings);
                return false;
            }
        }

        // SceneView 反射包装
        internal static class SceneViewReflection
        {
            private static MethodInfo s_IsUsingDeferredRenderingPathMethod;

            public static bool IsUsingDeferredRenderingPath()
            {
                if (s_IsUsingDeferredRenderingPathMethod == null)
                {
                    Type sceneViewType = typeof(SceneView);
                    s_IsUsingDeferredRenderingPathMethod = sceneViewType.GetMethod("IsUsingDeferredRenderingPath", BindingFlags.Public | BindingFlags.Static);
                }
                if (s_IsUsingDeferredRenderingPathMethod != null)
                    return (bool)s_IsUsingDeferredRenderingPathMethod.Invoke(null, null);
                return false;
            }
        }

        // EditorGUI 反射包装
        internal static class EditorGUIReflection
        {
            private static FieldInfo s_ClipingPlanesLabelField;
            private static FieldInfo s_NearAndFarLabelsField;
            private static FieldInfo s_NearFarLabelsWidthField;

            public static GUIContent GetClipingPlanesLabel()
            {
                if (s_ClipingPlanesLabelField == null)
                {
                    Type editorGUIType = typeof(EditorGUI);
                    s_ClipingPlanesLabelField = editorGUIType.GetField("s_ClipingPlanesLabel", BindingFlags.NonPublic | BindingFlags.Static);
                }
                if (s_ClipingPlanesLabelField != null)
                    return (GUIContent)s_ClipingPlanesLabelField.GetValue(null);
                return EditorGUIUtility.TrTextContent("Clipping Planes");
            }

            public static GUIContent[] GetNearAndFarLabels()
            {
                if (s_NearAndFarLabelsField == null)
                {
                    Type editorGUIType = typeof(EditorGUI);
                    s_NearAndFarLabelsField = editorGUIType.GetField("s_NearAndFarLabels", BindingFlags.NonPublic | BindingFlags.Static);
                }
                if (s_NearAndFarLabelsField != null)
                    return (GUIContent[])s_NearAndFarLabelsField.GetValue(null);
                return new GUIContent[] { EditorGUIUtility.TrTextContent("Near"), EditorGUIUtility.TrTextContent("Far") };
            }

            public static float GetNearFarLabelsWidth()
            {
                if (s_NearFarLabelsWidthField == null)
                {
                    Type editorGUIType = typeof(EditorGUI);
                    s_NearFarLabelsWidthField = editorGUIType.GetField("kNearFarLabelsWidth", BindingFlags.NonPublic | BindingFlags.Static);
                }
                if (s_NearFarLabelsWidthField != null)
                    return (float)s_NearFarLabelsWidthField.GetValue(null);
                return 30f;
            }
        }

        // EditorGUILayout 反射包装
        internal static class EditorGUILayoutReflection
        {
            private static MethodInfo s_PropertiesFieldMethod;

            public static void PropertiesField(GUIContent label, SerializedProperty[] properties, GUIContent[] propertyLabels, float labelWidth)
            {
                if (s_PropertiesFieldMethod == null)
                {
                    Type editorGUILayoutType = typeof(EditorGUILayout);
                    s_PropertiesFieldMethod = editorGUILayoutType.GetMethod("PropertiesField", BindingFlags.Public | BindingFlags.Static, null,
                        new Type[] { typeof(GUIContent), typeof(SerializedProperty[]), typeof(GUIContent[]), typeof(float) }, null);
                }
                if (s_PropertiesFieldMethod != null)
                {
                    s_PropertiesFieldMethod.Invoke(null, new object[] { label, properties, propertyLabels, labelWidth });
                }
                else
                {
                    // Fallback: 手动绘制属性
                    EditorGUILayout.LabelField(label);
                    EditorGUI.indentLevel++;
                    foreach (var prop in properties)
                    {
                        EditorGUILayout.PropertyField(prop);
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        // EditorApplication 反射包装
        internal static class EditorApplicationReflection
        {
            private static MethodInfo s_SetSceneRepaintDirtyMethod;

            public static void SetSceneRepaintDirty()
            {
                if (s_SetSceneRepaintDirtyMethod == null)
                {
                    Type editorApplicationType = typeof(EditorApplication);
                    s_SetSceneRepaintDirtyMethod = editorApplicationType.GetMethod("SetSceneRepaintDirty", BindingFlags.Public | BindingFlags.Static);
                }
                if (s_SetSceneRepaintDirtyMethod != null)
                    s_SetSceneRepaintDirtyMethod.Invoke(null, null);
                else
                    SceneView.RepaintAll();
            }
        }

        // TextureUtil 反射包装
        internal static class TextureUtilReflection
        {
            private static Type s_Type;
            private static MethodInfo s_GetTextureColorSpaceStringMethod;

            public static string GetTextureColorSpaceString(Texture texture)
            {
                if (s_Type == null)
                {
                    s_Type = EditorAssembly.GetType("UnityEditor.TextureUtil");
                    if (s_Type == null)
                        return "Gamma"; // 默认值
                }
                if (s_GetTextureColorSpaceStringMethod == null && s_Type != null)
                {
                    s_GetTextureColorSpaceStringMethod = s_Type.GetMethod("GetTextureColorSpaceString", BindingFlags.Public | BindingFlags.Static, null,
                        new Type[] { typeof(Texture) }, null);
                }
                if (s_GetTextureColorSpaceStringMethod != null)
                    return (string)s_GetTextureColorSpaceStringMethod.Invoke(null, new object[] { texture });
                return "Gamma"; // 默认值
            }
        }

        // StageUtility 反射包装
        internal static class StageUtilityReflection
        {
            private static MethodInfo s_IsGizmoCulledBySceneCullingMasksOrFocusedSceneMethod;

            public static bool IsGizmoCulledBySceneCullingMasksOrFocusedScene(GameObject go, Camera camera)
            {
                if (s_IsGizmoCulledBySceneCullingMasksOrFocusedSceneMethod == null)
                {
                    Type stageUtilityType = EditorAssembly.GetType("UnityEditor.Experimental.SceneManagement.StageUtility");
                    if (stageUtilityType == null)
                        stageUtilityType = typeof(StageUtility);
                    s_IsGizmoCulledBySceneCullingMasksOrFocusedSceneMethod = stageUtilityType.GetMethod("IsGizmoCulledBySceneCullingMasksOrFocusedScene", BindingFlags.Public | BindingFlags.Static);
                }
                if (s_IsGizmoCulledBySceneCullingMasksOrFocusedSceneMethod != null)
                    return (bool)s_IsGizmoCulledBySceneCullingMasksOrFocusedSceneMethod.Invoke(null, new object[] { go, camera });
                return false; // 默认不剔除
            }
        }
    }
    #endregion

    [CustomEditor(typeof(ReflectionProbe))]
    [CanEditMultipleObjects]
    internal class ReflectionProbeEditor : Editor
    {
        static ReflectionProbeEditor s_LastInteractedEditor;
        static HashSet<ReflectionProbe> s_CurrentlyEditedProbes = new HashSet<ReflectionProbe>();

        SerializedProperty m_Mode;
        SerializedProperty m_RefreshMode;
        SerializedProperty m_TimeSlicingMode;
        SerializedProperty m_Resolution;
        SerializedProperty m_ShadowDistance;
        SerializedProperty m_Importance;
        SerializedProperty m_BoxSize;
        SerializedProperty m_BoxOffset;
        SerializedProperty m_CullingMask;
        SerializedProperty m_ClearFlags;
        SerializedProperty m_BackgroundColor;
        SerializedProperty m_HDR;
        SerializedProperty m_BoxProjection;
        SerializedProperty m_IntensityMultiplier;
        SerializedProperty m_BlendDistance;
        SerializedProperty m_CustomBakedTexture;
        SerializedProperty m_RenderDynamicObjects;
        SerializedProperty m_UseOcclusionCulling;

        SerializedProperty[] m_NearAndFarProperties;

        // UniversalAdditionalReflectionProbeData 的 SerializedObject，用于访问其所有序列化属性
        SerializedObject m_ProbeDataSerializedObject;
        SerializedProperty m_CustomRefreshMode; // 自定义 RefreshMode（来自 UniversalAdditionalReflectionProbeData）
        SerializedProperty m_RenderMainLightShadows; // 是否渲染主光源阴影
        SerializedProperty m_MainLightShadowmapResolution; // 主光源阴影贴图分辨率
        SerializedProperty m_RenderAdditionalLightShadows; // 是否渲染附加光源阴影
        SerializedProperty m_AdditionalLightShadowmapResolution; // 附加光源阴影贴图分辨率
        SerializedProperty m_DrawGizmosPass; // 是否启用 Draw Gizmos Pass
        SerializedProperty m_FinalBlitPass; // 是否启用 Final Blit Pass
        SerializedProperty m_SSAO; // 是否启用 SSAO

        private static Mesh s_SphereMesh;
        private Material m_ReflectiveMaterial;
        private Matrix4x4 m_OldLocalSpace = Matrix4x4.identity;
        private float m_MipLevelPreview = 0.0F;

        private BoxBoundsHandle m_BoundsHandle = new BoxBoundsHandle();

        private Hashtable m_CachedGizmoMaterials = new Hashtable();

        public static void GetResolutionArray(ref int[] resolutionList, ref GUIContent[] resolutionStringList)
        {
            if (Styles.reflectionResolutionValuesArray == null && Styles.reflectionResolutionTextArray == null)
            {
                int cubemapResolution = Mathf.Max(1, ReflectionProbe.minBakedCubemapResolution);

                List<int> envReflectionResolutionValues = new List<int>();
                List<GUIContent> envReflectionResolutionText = new List<GUIContent>();

                do
                {
                    envReflectionResolutionValues.Add(cubemapResolution);
                    envReflectionResolutionText.Add(new GUIContent(cubemapResolution.ToString()));
                    cubemapResolution *= 2;
                }
                while (cubemapResolution <= ReflectionProbe.maxBakedCubemapResolution);

                Styles.reflectionResolutionValuesArray = envReflectionResolutionValues.ToArray();
                Styles.reflectionResolutionTextArray = envReflectionResolutionText.ToArray();
            }

            resolutionList = Styles.reflectionResolutionValuesArray;
            resolutionStringList = Styles.reflectionResolutionTextArray;
        }

        static internal class Styles
        {
            static Styles()
            {
                richTextMiniLabel.richText = true;
            }

            public static GUIStyle richTextMiniLabel = new GUIStyle(EditorStyles.miniLabel);

            public static GUIContent bakeButtonText = EditorGUIUtility.TrTextContent("Bake");
            public static string[] bakeCustomOptionText = { "Bake as new Cubemap..." };
            public static string[] bakeButtonsText = { "Bake All Reflection Probes" };

            public static GUIContent bakeCustomButtonText = EditorGUIUtility.TrTextContent("Bake", "Bakes Reflection Probe's cubemap, overwriting the existing cubemap texture asset (if any).");
            public static GUIContent runtimeSettingsHeader = EditorGUIUtility.TrTextContent("Runtime Settings", "These settings determine this Probe's priority, blending, intensity, and zone of effect and works in conjunction with the cubemap of this probe when it is rendered.");
            public static GUIContent backgroundColorText = EditorGUIUtility.TrTextContent("Background Color", "Camera clears the screen to this color before rendering.");
            public static GUIContent clearFlagsText = EditorGUIUtility.TrTextContent("Clear Flags", "Specify how to fill empty areas of the cubemap.");
            public static GUIContent intensityText = EditorGUIUtility.TrTextContent("Intensity", "The intensity modifier the Editor applies to this probe's texture in its shader.");
            public static GUIContent resolutionText = EditorGUIUtility.TrTextContent("Resolution", "The resolution of the cubemap.");
            public static GUIContent captureCubemapHeader = EditorGUIUtility.TrTextContent("Cubemap Capture Settings", "Settings that determine how to render this probe's cubemap.");
            public static GUIContent boxProjectionText = EditorGUIUtility.TrTextContent("Box Projection", "When enabled, Unity assumes that the reflected light is originating from the inside of the probe's box, rather than from infinitely far away. This is useful for box-shaped indoor environments.");
            public static GUIContent blendDistanceText = EditorGUIUtility.TrTextContent("Blend Distance", "Area around the probe where it is blended with other probes. Only used in deferred probes.");
            public static GUIContent sizeText = EditorGUIUtility.TrTextContent("Box Size", "The size of the box in which the reflections will be applied to objects. The value is not affected by the Transform of the Game Object.");
            public static GUIContent centerText = EditorGUIUtility.TrTextContent("Box Offset", "The center of the box in which the reflections will be applied to objects. The value is relative to the position of the Game Object.");
            public static GUIContent customCubemapText = EditorGUIUtility.TrTextContent("Cubemap", "Sets a custom cubemap for this probe.");
            public static GUIContent importanceText = EditorGUIUtility.TrTextContent("Importance", "When reflection probes overlap, Unity uses Importance to determine which probe should take priority.");
            public static GUIContent renderDynamicObjects = EditorGUIUtility.TrTextContent("Dynamic Objects", "If enabled dynamic objects are also rendered into the cubemap");
            public static GUIContent timeSlicing = EditorGUIUtility.TrTextContent("Time Slicing", "If enabled this probe will update over several frames, to help reduce the impact on the frame rate");
            public static GUIContent refreshMode = EditorGUIUtility.TrTextContent("Refresh Mode", "Controls how this probe refreshes in the Player");
            public static GUIContent useOcclusionCulling = EditorGUIUtility.TrTextContent("Occlusion Culling", "If this property is enabled, geometries which are blocked from the probe's line of sight are skipped during rendering.");
            public static GUIContent hdrText = EditorGUIUtility.TrTextContent("HDR", "Enable High Dynamic Range rendering.");
            public static GUIContent shadowDistanceText = EditorGUIUtility.TrTextContent("Shadow Distance", "Maximum distance at which Unity renders shadows associated with this probe.");
            public static GUIContent cullingMaskText = EditorGUIUtility.TrTextContent("Culling Mask", "Allows objects on specified layers to be included or excluded in the reflection.");
            public static GUIContent customSettingsHeader = EditorGUIUtility.TrTextContent("Custom Settings", "Custom rendering settings for this reflection probe, including shadowmap configuration.");
            public static GUIContent enableMainLightShadowsText = EditorGUIUtility.TrTextContent("Render Main Light Shadows", "Enable rendering of main light shadows in reflection probe");
            public static GUIContent enableAdditionalLightShadowsText = EditorGUIUtility.TrTextContent("Additional Light Shadowmap Resolution", "Resolution of the additional light shadowmap. Use 'From Asset' to use the UniversalRenderPipelineAsset setting.");
            public static GUIContent drawGizmosPassText = EditorGUIUtility.TrTextContent("Draw Gizmos Pass", "Enable the Draw Gizmos Pass for this reflection probe");
            public static GUIContent finalBlitPassText = EditorGUIUtility.TrTextContent("Final Blit Pass", "Enable the Final Blit Pass for this reflection probe");
            public static GUIContent SSAOText = EditorGUIUtility.TrTextContent("SSAO", "Enable Screen Space Ambient Occlusion for this reflection probe");

            public static GUIContent typeText = EditorGUIUtility.TrTextContent("Type", "Specify the lighting setup for this probe: Baked, Custom, or Realtime.");
            public static GUIContent[] reflectionProbeMode = { EditorGUIUtility.TrTextContent("Baked"), EditorGUIUtility.TrTextContent("Custom"), EditorGUIUtility.TrTextContent("Realtime") };
            public static int[] reflectionProbeModeValues = { (int)ReflectionProbeMode.Baked, (int)ReflectionProbeMode.Custom, (int)ReflectionProbeMode.Realtime };

            public static int[] reflectionResolutionValuesArray = null;
            public static GUIContent[] reflectionResolutionTextArray = null;

            public static GUIContent[] clearFlags =
            {
                EditorGUIUtility.TrTextContent("Skybox"),
                EditorGUIUtility.TrTextContent("Solid Color")
            };
            public static int[] clearFlagsValues = { 1, 2 }; // taken from Camera.h

            private static GUIContent customPrivitiveBoundsHandleEditModeButton = new GUIContent(
                EditorGUIUtility.IconContent("EditShape").image,
                EditorGUIUtility.TrTextContent("Adjust the probe's zone of effect. Holding Alt or Shift and click the control handle to pin the center or scale the volume uniformly.").text
            );
            public static GUIContent[] toolContents =
            {
                customPrivitiveBoundsHandleEditModeButton,
                EditorGUIUtility.TrIconContent("CapturePosition", "Modify capture position.")
            };
            public static EditMode.SceneViewEditMode[] sceneViewEditModes = new[]
            {
                EditMode.SceneViewEditMode.ReflectionProbeBox,
                EditMode.SceneViewEditMode.ReflectionProbeOrigin
            };

            public static string baseSceneEditingToolText = "<color=grey>Probe Scene Editing Mode:</color> ";
            public static GUIContent[] toolNames =
            {
                new GUIContent(baseSceneEditingToolText + "Box Projection Bounds", ""),
                new GUIContent(baseSceneEditingToolText + "Probe Origin", "")
            };
        } // end of class Styles

        // Should match reflection probe gizmo color in GizmoDrawers.cpp!
        internal static Color kGizmoReflectionProbe = new Color(0xFF / 255f, 0xE5 / 255f, 0x94 / 255f, 0x80 / 255f);
        internal static Color kGizmoReflectionProbeDisabled = new Color(0x99 / 255f, 0x89 / 255f, 0x59 / 255f, 0x60 / 255f);
        internal static Color kGizmoHandleReflectionProbe = new Color(0xFF / 255f, 0xE5 / 255f, 0xAA / 255f, 0xFF / 255f);

        private object m_ShowRuntimeSettings;
        private object m_ShowCubemapCaptureSettings;
        private object m_ShowCustomSettings;

        readonly AnimBool m_ShowProbeModeRealtimeOptions = new AnimBool(); // p.mode == ReflectionProbeMode.Realtime; Will be brought back in 5.1
        readonly AnimBool m_ShowProbeModeCustomOptions = new AnimBool();
        readonly AnimBool m_ShowBoxOptions = new AnimBool();

        private object m_CubemapEditor = null;

        static bool IsReflectionProbeEditMode(EditMode.SceneViewEditMode editMode)
        {
            return editMode == EditMode.SceneViewEditMode.ReflectionProbeBox ||
                editMode == EditMode.SceneViewEditMode.ReflectionProbeOrigin;
        }

        bool sceneViewEditing
        {
            get { return IsReflectionProbeEditMode(EditMode.editMode) && EditMode.IsOwner(this); }
        }

        public void OnEnable()
        {
            m_Mode = serializedObject.FindProperty("m_Mode");
            m_RefreshMode = serializedObject.FindProperty("m_RefreshMode");
            m_TimeSlicingMode = serializedObject.FindProperty("m_TimeSlicingMode");

            m_Resolution = serializedObject.FindProperty("m_Resolution");
            m_NearAndFarProperties = new[] { serializedObject.FindProperty("m_NearClip"), serializedObject.FindProperty("m_FarClip") };
            m_ShadowDistance = serializedObject.FindProperty("m_ShadowDistance");
            m_Importance = serializedObject.FindProperty("m_Importance");
            m_BoxSize = serializedObject.FindProperty("m_BoxSize");
            m_BoxOffset = serializedObject.FindProperty("m_BoxOffset");
            m_CullingMask = serializedObject.FindProperty("m_CullingMask");
            m_ClearFlags = serializedObject.FindProperty("m_ClearFlags");
            m_BackgroundColor = serializedObject.FindProperty("m_BackGroundColor");
            m_HDR = serializedObject.FindProperty("m_HDR");
            m_BoxProjection = serializedObject.FindProperty("m_BoxProjection");
            m_IntensityMultiplier = serializedObject.FindProperty("m_IntensityMultiplier");
            m_BlendDistance = serializedObject.FindProperty("m_BlendDistance");
            m_CustomBakedTexture = serializedObject.FindProperty("m_CustomBakedTexture");
            m_RenderDynamicObjects = serializedObject.FindProperty("m_RenderDynamicObjects");
            m_UseOcclusionCulling = serializedObject.FindProperty("m_UseOcclusionCulling");

            ReflectionProbe p = target as ReflectionProbe;
            // 查找 UniversalAdditionalReflectionProbeData 组件并创建 SerializedObject
            if (p != null)
            {
                var probeData = p.GetUniversalAdditionalReflectionProbeData();
                if (probeData != null)
                {
                    // 创建 UniversalAdditionalReflectionProbeData 的 SerializedObject
                    m_ProbeDataSerializedObject = new SerializedObject(probeData);
                    m_CustomRefreshMode = m_ProbeDataSerializedObject.FindProperty("m_CustomRefreshMode");
                    m_RenderMainLightShadows = m_ProbeDataSerializedObject.FindProperty("m_RenderMainLightShadows");
                    m_MainLightShadowmapResolution = m_ProbeDataSerializedObject.FindProperty("m_MainLightShadowmapResolution");
                    m_RenderAdditionalLightShadows = m_ProbeDataSerializedObject.FindProperty("m_RenderAdditionalLightShadows");
                    m_AdditionalLightShadowmapResolution = m_ProbeDataSerializedObject.FindProperty("m_AdditionalLightShadowmapResolution");
                    m_DrawGizmosPass = m_ProbeDataSerializedObject.FindProperty("m_DrawGizmosPass");
                    m_FinalBlitPass = m_ProbeDataSerializedObject.FindProperty("m_FinalBlitPass");
                    m_SSAO = m_ProbeDataSerializedObject.FindProperty("m_SSAO");
                }
                else
                {
                    m_ProbeDataSerializedObject = null;
                    m_CustomRefreshMode = null;
                    m_RenderMainLightShadows = null;
                    m_MainLightShadowmapResolution = null;
                    m_RenderAdditionalLightShadows = null;
                    m_AdditionalLightShadowmapResolution = null;
                    m_DrawGizmosPass = null;
                    m_FinalBlitPass = null;
                    m_SSAO = null;
                }
            }

            m_ShowProbeModeRealtimeOptions.valueChanged.AddListener(Repaint);
            m_ShowProbeModeCustomOptions.valueChanged.AddListener(Repaint);
            m_ShowBoxOptions.valueChanged.AddListener(Repaint);
            m_ShowProbeModeRealtimeOptions.value = p.mode == ReflectionProbeMode.Realtime;
            m_ShowProbeModeCustomOptions.value = p.mode == ReflectionProbeMode.Custom;
            m_ShowBoxOptions.value = true;

            m_BoundsHandle.handleColor = kGizmoHandleReflectionProbe;
            m_BoundsHandle.wireframeColor = Color.clear;

            m_ShowRuntimeSettings = ReflectionProbeEditorReflection.SavedBoolReflection.CreateInstance("ReflectionProbeEditor.ShowRuntimeSettings", true);
            m_ShowCubemapCaptureSettings = ReflectionProbeEditorReflection.SavedBoolReflection.CreateInstance("ReflectionProbeEditor.ShowCubemapCaptureSettings", true);
            m_ShowCustomSettings = ReflectionProbeEditorReflection.SavedBoolReflection.CreateInstance("ReflectionProbeEditor.ShowCustomSettings", true);

            UpdateOldLocalSpace();
            SceneView.beforeSceneGui += OnPreSceneGUICallback;

            for (int i = 0; i < targets.Length; ++i)
            {
                s_CurrentlyEditedProbes.Add((ReflectionProbe)targets[i]);
            }
        }

        public void OnDisable()
        {
            SceneView.beforeSceneGui -= OnPreSceneGUICallback;

            DestroyImmediate(m_ReflectiveMaterial);
            if (m_CubemapEditor != null)
                DestroyImmediate(m_CubemapEditor as UnityEngine.Object);

            foreach (Material mat in m_CachedGizmoMaterials.Values)
                DestroyImmediate(mat);
            m_CachedGizmoMaterials.Clear();

            m_ProbeDataSerializedObject = null;

            for (int i = 0; i < targets.Length; ++i)
            {
                s_CurrentlyEditedProbes.Remove((ReflectionProbe)targets[i]);
            }
        }

        private bool IsCollidingWithOtherProbes(string targetPath, ReflectionProbe targetProbe, out ReflectionProbe collidingProbe)
        {
            ReflectionProbe[] probes = FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.InstanceID).ToArray();
            collidingProbe = null;
            foreach (var probe in probes)
            {
                if (probe == targetProbe || probe.customBakedTexture == null)
                    continue;
                string path = AssetDatabase.GetAssetPath(probe.customBakedTexture);
                if (path == targetPath)
                {
                    collidingProbe = probe;
                    return true;
                }
            }
            return false;
        }

        private void BakeCustomReflectionProbe(ReflectionProbe probe, bool usePreviousAssetPath)
        {
            string path = "";
            if (usePreviousAssetPath)
                path = AssetDatabase.GetAssetPath(probe.customBakedTexture);

            string targetExtension = probe.hdr ? "exr" : "png";
            if (string.IsNullOrEmpty(path) || Path.GetExtension(path) != "." + targetExtension)
            {
                // We use the path of the active scene as the target path
                string targetPath = ReflectionProbeEditorReflection.FileUtilReflection.GetPathWithoutExtension(SceneManager.GetActiveScene().path);
                if (string.IsNullOrEmpty(targetPath))
                    targetPath = "Assets";
                else if (Directory.Exists(targetPath) == false)
                    Directory.CreateDirectory(targetPath);

                string fileName = probe.name + (probe.hdr ? "-reflectionHDR" : "-reflection") + "." + targetExtension;
                fileName = Path.GetFileNameWithoutExtension(AssetDatabase.GenerateUniqueAssetPath(Path.Combine(targetPath, fileName)));

                path = EditorUtility.SaveFilePanelInProject("Save reflection probe's cubemap.", fileName, targetExtension, "", targetPath);
                if (string.IsNullOrEmpty(path))
                    return;

                ReflectionProbe collidingProbe;
                if (IsCollidingWithOtherProbes(path, probe, out collidingProbe))
                {
                    if (!EditorUtility.DisplayDialog("Cubemap is used by other reflection probe",
                        string.Format("'{0}' path is used by the game object '{1}', do you really want to overwrite it?",
                            path, collidingProbe.name), "Yes", "No"))
                    {
                        return;
                    }
                }
            }

            EditorUtility.DisplayProgressBar("Reflection Probes", "Baking " + path, 0.5f);
            if (!Lightmapping.BakeReflectionProbe(probe, path))
                Debug.LogError("Failed to bake reflection probe to " + path);
            EditorUtility.ClearProgressBar();
        }

        private void OnBakeCustomButton(object data)
        {
            int mode = (int)data;

            ReflectionProbe p = target as ReflectionProbe;
            if (mode == 0)
                BakeCustomReflectionProbe(p, false);
        }

        private void OnBakeButton(object data)
        {
            int mode = (int)data;
            if (mode == 0)
                ReflectionProbeEditorReflection.LightmappingReflection.BakeAllReflectionProbesSnapshots();
        }

        ReflectionProbe reflectionProbeTarget
        {
            get { return (ReflectionProbe)target; }
        }

        void DoBakeButton()
        {
            if (reflectionProbeTarget.mode == ReflectionProbeMode.Realtime)
            {
                EditorGUILayout.HelpBox("Baking of this reflection probe should be initiated from the scripting API because the type is 'Realtime'", MessageType.Info);

                if (!QualitySettings.realtimeReflectionProbes)
                    EditorGUILayout.HelpBox("Realtime reflection probes are disabled in Quality Settings", MessageType.Warning);
                return;
            }

            GUILayout.BeginHorizontal();

            switch (reflectionProbeMode)
            {
                case ReflectionProbeMode.Custom:
                    if (EditorGUI.LargeSplitButtonWithDropdownList(Styles.bakeCustomButtonText, Styles.bakeCustomOptionText, OnBakeCustomButton))
                    {
                        BakeCustomReflectionProbe(reflectionProbeTarget, true);
                        GUIUtility.ExitGUI();
                    }
                    break;

                case ReflectionProbeMode.Baked:
                    using (new EditorGUI.DisabledScope(!reflectionProbeTarget.enabled))
                    {
                        // Bake button in non-continous mode
                        if (EditorGUI.LargeSplitButtonWithDropdownList(Styles.bakeButtonText, Styles.bakeButtonsText, OnBakeButton))
                        {
                            ReflectionProbeEditorReflection.LightmappingReflection.BakeReflectionProbeSnapshot(reflectionProbeTarget);
                            GUIUtility.ExitGUI();
                        }
                    }

                    break;

                case ReflectionProbeMode.Realtime:
                    // Not showing bake button in realtime
                    break;
            }

            GUILayout.EndHorizontal();
        }

        void DoToolbar()
        {
            // Show the master tool selector
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.changed = false;
            var oldEditMode = EditMode.editMode;

            EditorGUI.BeginChangeCheck();
            ReflectionProbeEditorReflection.EditModeReflection.DoInspectorToolbar(Styles.sceneViewEditModes, Styles.toolContents, this);
            if (EditorGUI.EndChangeCheck())
                s_LastInteractedEditor = this;

            if (oldEditMode != EditMode.editMode)
            {
                switch (EditMode.editMode)
                {
                    case EditMode.SceneViewEditMode.ReflectionProbeOrigin:
                        UpdateOldLocalSpace();
                        break;
                }
                var toolbar = ReflectionProbeEditorReflection.ToolbarReflection.GetToolbar();
                if (toolbar != null)
                    ReflectionProbeEditorReflection.ToolbarReflection.Repaint(toolbar);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Info box for tools
            GUILayout.BeginVertical(EditorStyles.helpBox);
            string helpText = Styles.baseSceneEditingToolText;
            if (sceneViewEditing)
            {
                int index = ArrayUtility.IndexOf(Styles.sceneViewEditModes, EditMode.editMode);
                if (index >= 0)
                    helpText = Styles.toolNames[index].text;
            }
            GUILayout.Label(helpText, Styles.richTextMiniLabel);
            GUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        ReflectionProbeMode reflectionProbeMode
        {
            get { return reflectionProbeTarget.mode; }
        }


        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // 更新 UniversalAdditionalReflectionProbeData 的 SerializedObject（如果存在）
            if (m_ProbeDataSerializedObject != null)
            {
                m_ProbeDataSerializedObject.Update();
            }

            if (targets.Length == 1)
                DoToolbar();

            m_ShowProbeModeRealtimeOptions.target = reflectionProbeMode == ReflectionProbeMode.Realtime;
            m_ShowProbeModeCustomOptions.target = reflectionProbeMode == ReflectionProbeMode.Custom;

            // Bake/custom/realtime type
            EditorGUILayout.IntPopup(m_Mode, Styles.reflectionProbeMode, Styles.reflectionProbeModeValues, Styles.typeText);

            // We cannot show multiple different type controls
            if (!m_Mode.hasMultipleDifferentValues)
            {
                EditorGUI.indentLevel++;
                {
                    // Custom cubemap UI (Bake button and manual cubemap assignment)
                    if (EditorGUILayout.BeginFadeGroup(m_ShowProbeModeCustomOptions.faded))
                    {
                        EditorGUILayout.PropertyField(m_RenderDynamicObjects, Styles.renderDynamicObjects);

                        EditorGUI.BeginChangeCheck();
                        EditorGUI.showMixedValue = m_CustomBakedTexture.hasMultipleDifferentValues;
                        var newCubemap = EditorGUILayout.ObjectField(Styles.customCubemapText, m_CustomBakedTexture.objectReferenceValue, typeof(Texture), false);
                        EditorGUI.showMixedValue = false;
                        if (EditorGUI.EndChangeCheck())
                            m_CustomBakedTexture.objectReferenceValue = newCubemap;
                    }
                    EditorGUILayout.EndFadeGroup();

                    // Realtime UI
                    if (EditorGUILayout.BeginFadeGroup(m_ShowProbeModeRealtimeOptions.faded))
                    {
                        // 显示自定义的 RefreshMode（来自 UniversalAdditionalReflectionProbeData）
                        // 而不是 ReflectionProbe 原生的 refreshMode（已固定为 ViaScripting）
                        ReflectionProbe probe = reflectionProbeTarget;
                        if (m_ProbeDataSerializedObject != null && m_CustomRefreshMode != null)
                        {
                            EditorGUI.BeginChangeCheck();
                            EditorGUILayout.PropertyField(m_CustomRefreshMode, Styles.refreshMode);
                            if (EditorGUI.EndChangeCheck())
                            {      
                                Undo.RecordObject(probe, "Set ReflectionProbe RefreshMode to ViaScripting");                  
                                // 确保 ReflectionProbe 的 refreshMode 保持为 ViaScripting
                                if (m_CustomRefreshMode.intValue != (int)ReflectionProbeRefreshMode.ViaScripting)
                                    probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                                else
                                    probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                                EditorUtility.SetDirty(probe);
                            }
                        }
                        else
                        {
                            // Fallback: 如果没有 UniversalAdditionalReflectionProbeData，显示原生的 RefreshMode
                            EditorGUILayout.PropertyField(m_RefreshMode, Styles.refreshMode);
                        }
                        
                        EditorGUILayout.PropertyField(m_TimeSlicingMode, Styles.timeSlicing);
                    }
                    EditorGUILayout.EndFadeGroup();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            bool showRuntimeSettings = ReflectionProbeEditorReflection.SavedBoolReflection.GetValue(m_ShowRuntimeSettings);
            ReflectionProbeEditorReflection.SavedBoolReflection.SetValue(m_ShowRuntimeSettings, EditorGUILayout.BeginFoldoutHeaderGroup(showRuntimeSettings, Styles.runtimeSettingsHeader));

            if (ReflectionProbeEditorReflection.SavedBoolReflection.GetValue(m_ShowRuntimeSettings))
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(m_Importance, Styles.importanceText);
                EditorGUILayout.PropertyField(m_IntensityMultiplier, Styles.intensityText);

                // Only take graphic Tier settings into account when built-in render pipeline is active.
                var tierSettings = ReflectionProbeEditorReflection.EditorGraphicsSettingsReflection.GetCurrentTierSettings();
                if (GraphicsSettings.currentRenderPipeline == null && tierSettings != null && 
                    ReflectionProbeEditorReflection.EditorGraphicsSettingsReflection.GetReflectionProbeBoxProjection(tierSettings) == false)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.Toggle(Styles.boxProjectionText, false);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(m_BoxProjection, Styles.boxProjectionText);
                }

                bool isDeferredRenderingPath = ReflectionProbeEditorReflection.SceneViewReflection.IsUsingDeferredRenderingPath();
                bool isDeferredReflections = isDeferredRenderingPath && (UnityEngine.Rendering.GraphicsSettings.GetShaderMode(BuiltinShaderType.DeferredReflections) != BuiltinShaderMode.Disabled);
                using (new EditorGUI.DisabledScope(!isDeferredReflections && !SupportedRenderingFeatures.active.reflectionProbesBlendDistance))
                {
                    EditorGUILayout.PropertyField(m_BlendDistance, Styles.blendDistanceText);
                }

                // Bounds editing (box projection bounds + the bounds that objects use to check if they should be affected by this reflection probe)
                if (EditorGUILayout.BeginFadeGroup(m_ShowBoxOptions.faded))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(m_BoxSize, Styles.sizeText);
                    EditorGUILayout.PropertyField(m_BoxOffset, Styles.centerText);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Vector3 center = m_BoxOffset.vector3Value;
                        Vector3 size = m_BoxSize.vector3Value;
                        if (ValidateAABB(ref center, ref size))
                        {
                            m_BoxOffset.vector3Value = center;
                            m_BoxSize.vector3Value = size;
                        }
                    }
                }
                EditorGUILayout.EndFadeGroup();

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            bool showCubemapCaptureSettings = ReflectionProbeEditorReflection.SavedBoolReflection.GetValue(m_ShowCubemapCaptureSettings);
            ReflectionProbeEditorReflection.SavedBoolReflection.SetValue(m_ShowCubemapCaptureSettings, EditorGUILayout.BeginFoldoutHeaderGroup(showCubemapCaptureSettings, Styles.captureCubemapHeader));

            if (ReflectionProbeEditorReflection.SavedBoolReflection.GetValue(m_ShowCubemapCaptureSettings))
            {
                EditorGUI.indentLevel++;

                int[] reflectionResolutionValuesArray = null;
                GUIContent[] reflectionResolutionTextArray = null;
                GetResolutionArray(ref reflectionResolutionValuesArray, ref reflectionResolutionTextArray);

                EditorGUILayout.IntPopup(m_Resolution, reflectionResolutionTextArray, reflectionResolutionValuesArray, Styles.resolutionText, GUILayout.MinWidth(40));
                EditorGUILayout.PropertyField(m_HDR, Styles.hdrText);
                EditorGUILayout.PropertyField(m_ShadowDistance, Styles.shadowDistanceText);
                EditorGUILayout.IntPopup(m_ClearFlags, Styles.clearFlags, Styles.clearFlagsValues, Styles.clearFlagsText);
                EditorGUILayout.PropertyField(m_BackgroundColor, Styles.backgroundColorText);
                EditorGUILayout.PropertyField(m_CullingMask, Styles.cullingMaskText);
                EditorGUILayout.PropertyField(m_UseOcclusionCulling, Styles.useOcclusionCulling);
                ReflectionProbeEditorReflection.EditorGUILayoutReflection.PropertiesField(
                    ReflectionProbeEditorReflection.EditorGUIReflection.GetClipingPlanesLabel(), 
                    m_NearAndFarProperties, 
                    ReflectionProbeEditorReflection.EditorGUIReflection.GetNearAndFarLabels(), 
                    ReflectionProbeEditorReflection.EditorGUIReflection.GetNearFarLabelsWidth());

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space();

            // Custom Settings 折叠面板（包含Shadowmap设置）
            if (m_ProbeDataSerializedObject != null)
            {
                bool showCustomSettings = ReflectionProbeEditorReflection.SavedBoolReflection.GetValue(m_ShowCustomSettings);
                ReflectionProbeEditorReflection.SavedBoolReflection.SetValue(m_ShowCustomSettings, EditorGUILayout.BeginFoldoutHeaderGroup(showCustomSettings, Styles.customSettingsHeader));

                if (ReflectionProbeEditorReflection.SavedBoolReflection.GetValue(m_ShowCustomSettings))
                {
                    EditorGUI.indentLevel++;

                    // 主光源阴影设置
                    if (m_RenderMainLightShadows != null)
                    {
                        EditorGUILayout.PropertyField(m_RenderMainLightShadows, Styles.enableMainLightShadowsText);
                        
                        if (m_RenderMainLightShadows.boolValue && m_MainLightShadowmapResolution != null)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(m_MainLightShadowmapResolution, Styles.enableMainLightShadowsText);
                            EditorGUI.indentLevel--;
                        }
                    }

                    EditorGUILayout.Space();

                    // 附加光源阴影设置
                    if (m_RenderAdditionalLightShadows != null)
                    {
                        EditorGUILayout.PropertyField(m_RenderAdditionalLightShadows, Styles.enableAdditionalLightShadowsText);
                        
                        if (m_RenderAdditionalLightShadows.boolValue && m_AdditionalLightShadowmapResolution != null)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(m_AdditionalLightShadowmapResolution, Styles.enableAdditionalLightShadowsText);
                            EditorGUI.indentLevel--;
                        }
                    }

                    EditorGUILayout.Space();

                    // 渲染优化选项
                    if (m_DrawGizmosPass != null)
                    {
                        EditorGUILayout.PropertyField(m_DrawGizmosPass, Styles.drawGizmosPassText);
                    }

                    if (m_FinalBlitPass != null)
                    {
                        EditorGUILayout.PropertyField(m_FinalBlitPass, Styles.finalBlitPassText);
                    }

                    if (m_SSAO != null)
                    {
                        EditorGUILayout.PropertyField(m_SSAO, Styles.SSAOText);
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            EditorGUILayout.Space();

            if (targets.Length == 1)
            {
                ReflectionProbe probe = (ReflectionProbe)target;
                if (probe.mode == ReflectionProbeMode.Custom && probe.customBakedTexture != null)
                {
                    Cubemap cubemap = probe.customBakedTexture as Cubemap;
                    if (cubemap && cubemap.mipmapCount == 1)
                        EditorGUILayout.HelpBox("No mipmaps in the cubemap, Smoothness value in Standard shader will be ignored.", MessageType.Warning);
                }
            }

            DoBakeButton();
            EditorGUILayout.Space();           
            serializedObject.ApplyModifiedProperties();

            // 应用 UniversalAdditionalReflectionProbeData 的修改（如果存在）
            if (m_ProbeDataSerializedObject != null)
            {
                m_ProbeDataSerializedObject.ApplyModifiedProperties();
            }
        }

        // 注意：GetWorldBoundsOfTarget 在某些 Unity 版本中可能不存在
        // 如果编译错误，可以注释掉这个方法
        // internal override Bounds GetWorldBoundsOfTarget(UnityEngine.Object targetObject)
        // {
        //     return ((ReflectionProbe)targetObject).bounds;
        // }

        bool ValidPreviewSetup()
        {
            ReflectionProbe p = (ReflectionProbe)target;
            return (p != null && p.texture != null);
        }

        void CreateTextureInspector(Texture texture, ref Editor previousEditor)
        {
            switch (texture)
            {
                case Cubemap _:
                    {
                        Type cubemapInspectorType = ReflectionProbeEditorReflection.EditorTypeReflection.GetCubemapInspectorType();
                        if (cubemapInspectorType != null)
                            Editor.CreateCachedEditor(texture, cubemapInspectorType, ref previousEditor);
                        else
                            Editor.CreateCachedEditor(texture, ReflectionProbeEditorReflection.TextureInspectorReflection.UnderlyingType, ref previousEditor);
                    }
                    break;
                case CustomRenderTexture _:
                    {
                        Type customRenderTextureEditorType = ReflectionProbeEditorReflection.EditorTypeReflection.GetCustomRenderTextureEditorType();
                        if (customRenderTextureEditorType != null)
                            Editor.CreateCachedEditor(texture, customRenderTextureEditorType, ref previousEditor);
                        else
                            Editor.CreateCachedEditor(texture, ReflectionProbeEditorReflection.TextureInspectorReflection.UnderlyingType, ref previousEditor);
                    }
                    break;
                case RenderTexture _:
                    {
                        Type renderTextureEditorType = ReflectionProbeEditorReflection.EditorTypeReflection.GetRenderTextureEditorType();
                        if (renderTextureEditorType != null)
                            Editor.CreateCachedEditor(texture, renderTextureEditorType, ref previousEditor);
                        else
                            Editor.CreateCachedEditor(texture, ReflectionProbeEditorReflection.TextureInspectorReflection.UnderlyingType, ref previousEditor);
                    }
                    break;
                default:
                    Editor.CreateCachedEditor(texture, ReflectionProbeEditorReflection.TextureInspectorReflection.UnderlyingType, ref previousEditor);
                    break;
            }
        }

        public override bool HasPreviewGUI()
        {
            if (targets.Length > 1)
                return false;  // We only handle one preview for reflection probes

            // Ensure valid cube map editor (if possible)
            if (ValidPreviewSetup())
            {
                Editor editor = m_CubemapEditor as Editor;
                CreateTextureInspector(((ReflectionProbe)target).texture, ref editor);
                m_CubemapEditor = editor;
            }

            // If having one probe selected we always want preview (to prevent preview window from popping)
            return true;
        }

        public override void OnPreviewSettings()
        {
            if (!ValidPreviewSetup())
                return;

            if (m_CubemapEditor != null)
            {
                ReflectionProbeEditorReflection.TextureInspectorReflection.SetMipLevel(m_CubemapEditor, m_MipLevelPreview);

                EditorGUI.BeginChangeCheck();
                ReflectionProbeEditorReflection.TextureInspectorReflection.OnPreviewSettings(m_CubemapEditor);
                // Need to repaint, because mipmap value changes affect reflection probe preview in the scene
                if (EditorGUI.EndChangeCheck())
                {
                    ReflectionProbeEditorReflection.EditorApplicationReflection.SetSceneRepaintDirty();
                    m_MipLevelPreview = ReflectionProbeEditorReflection.TextureInspectorReflection.GetMipLevel(m_CubemapEditor);
                }
            }
        }

        public override void OnPreviewGUI(Rect position, GUIStyle style)
        {
            // Fix for case 939947 where we didn't get the Layout event if the texture was null when changing color
            if (!ValidPreviewSetup() && Event.current.type != EventType.ExecuteCommand)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Color prevColor = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.5f);
                GUILayout.Label("Reflection Probe not baked/ready yet");
                GUI.color = prevColor;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                return;
            }

            ReflectionProbe p = target as ReflectionProbe;
            if (p != null && p.texture != null && targets.Length == 1)
            {
                Editor editor = m_CubemapEditor as Editor;
                CreateTextureInspector(p.texture, ref editor);
                m_CubemapEditor = editor;
            }

            if (m_CubemapEditor != null)
            {
                ReflectionProbeEditorReflection.TextureInspectorReflection.SetCubemapIntensity(m_CubemapEditor, GetProbeIntensity((ReflectionProbe)target));
                ReflectionProbeEditorReflection.TextureInspectorReflection.OnPreviewGUI(m_CubemapEditor, position, style);
            }
        }

        private static Mesh sphereMesh
        {
            get { return s_SphereMesh ?? (s_SphereMesh = Resources.GetBuiltinResource(typeof(Mesh), "New-Sphere.fbx") as Mesh); }
        }

        private Material reflectiveMaterial
        {
            get
            {
                if (m_ReflectiveMaterial == null)
                {
                    m_ReflectiveMaterial = (Material)Instantiate(EditorGUIUtility.Load("Previews/PreviewCubemapMaterial.mat"));
                    m_ReflectiveMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                return m_ReflectiveMaterial;
            }
        }

        private float GetProbeIntensity(ReflectionProbe p)
        {
            if (p == null || p.texture == null)
                return 1.0f;

            float intensity = p.intensity;
            if (ReflectionProbeEditorReflection.TextureUtilReflection.GetTextureColorSpaceString(p.texture) == "Linear")
                intensity = Mathf.LinearToGammaSpace(intensity);
            return intensity;
        }

        // Draw Reflection probe preview sphere
        private void OnPreSceneGUICallback(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            foreach (var t in targets)
            {
                ReflectionProbe p = (ReflectionProbe)t;
                if (!reflectiveMaterial)
                    return;

                if (ReflectionProbeEditorReflection.StageUtilityReflection.IsGizmoCulledBySceneCullingMasksOrFocusedScene(p.gameObject, Camera.current))
                    return;

                Matrix4x4 m = new Matrix4x4();

                // @TODO: use MaterialPropertyBlock instead - once it actually works!
                // Tried to use MaterialPropertyBlock in 5.4.0b2, but would get incorrectly set parameters when using with Graphics.DrawMesh
                if (!m_CachedGizmoMaterials.ContainsKey(p))
                    m_CachedGizmoMaterials.Add(p, Instantiate(reflectiveMaterial));

                Material mat = m_CachedGizmoMaterials[p] as Material;
                if (!mat)
                    return;
                {
                    // Get mip level
                    float mipLevel = 0.0F;
                    if (m_CubemapEditor != null)
                        mipLevel = ReflectionProbeEditorReflection.TextureInspectorReflection.GetMipLevelForRendering(m_CubemapEditor);

                    mat.SetTexture("_MainTex", p.texture);
                    mat.SetMatrix("_CubemapRotation", Matrix4x4.identity);
                    mat.SetFloat("_Mip", mipLevel);
                    mat.SetFloat("_Alpha", 0.0f);
                    mat.SetFloat("_Intensity", GetProbeIntensity(p));
                    mat.SetVector("_MainTex_HDR", p.textureHDRDecodeValues);

                    if (PlayerSettings.colorSpace == ColorSpace.Linear)
                    {
                        mat.SetInt("_ColorspaceIsGamma", 0);
                    }
                    else
                    {
                        mat.SetInt("_ColorspaceIsGamma", 1);
                    }

                    // draw a preview sphere that scales with overall GO scale, but always uniformly
                    var scale = p.transform.lossyScale.magnitude * 0.5f;
                    m.SetTRS(p.transform.position, Quaternion.identity, new Vector3(scale, scale, scale));
                    Graphics.DrawMesh(sphereMesh, m, mat, 0, SceneView.currentDrawingSceneView.camera, 0);
                }
            }
        }

        // Ensures that probe's AABB encapsulates probe's position
        // Returns true, if center or size was modified
        private bool ValidateAABB(ref Vector3 center, ref Vector3 size)
        {
            ReflectionProbe p = (ReflectionProbe)target;

            Matrix4x4 localSpace = GetLocalSpace(p);
            Vector3 localTransformPosition = localSpace.inverse.MultiplyPoint3x4(p.transform.position);

            Bounds b = new Bounds(center, size);

            if (b.Contains(localTransformPosition)) return false;

            b.Encapsulate(localTransformPosition);

            center = b.center;
            size = b.size;
            return true;
        }

        [DrawGizmo(GizmoType.Active)]
        static void RenderBoxGizmo(ReflectionProbe reflectionProbe, GizmoType gizmoType)
        {
            if (s_LastInteractedEditor == null)
                return;

            if (s_LastInteractedEditor.sceneViewEditing && EditMode.editMode == EditMode.SceneViewEditMode.ReflectionProbeBox)
            {
                Color oldColor = Gizmos.color;
                Gizmos.color = kGizmoReflectionProbe;

                Gizmos.matrix = GetLocalSpace(reflectionProbe);
                Gizmos.DrawCube(reflectionProbe.center, -1f * reflectionProbe.size);
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = oldColor;
            }
        }

        [DrawGizmo(GizmoType.Selected)]
        static void RenderBoxOutline(ReflectionProbe reflectionProbe, GizmoType gizmoType)
        {
            if (!s_CurrentlyEditedProbes.Contains(reflectionProbe))
                return;

            Color oldColor = Gizmos.color;
            Gizmos.color = reflectionProbe.isActiveAndEnabled ? kGizmoReflectionProbe : kGizmoReflectionProbeDisabled;

            Gizmos.matrix = GetLocalSpace(reflectionProbe);
            Gizmos.DrawWireCube(reflectionProbe.center, reflectionProbe.size);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = oldColor;
        }

        public static bool IsSceneGUIEnabled()
        {
            return IsReflectionProbeEditMode(EditMode.editMode);
        }

        public void OnSceneGUI()
        {
            if (!sceneViewEditing)
                return;

            switch (EditMode.editMode)
            {
                case EditMode.SceneViewEditMode.ReflectionProbeBox:
                    DoBoxEditing();
                    break;
                case EditMode.SceneViewEditMode.ReflectionProbeOrigin:
                    DoOriginEditing();
                    break;
            }
        }

        void UpdateOldLocalSpace()
        {
            m_OldLocalSpace = GetLocalSpace((ReflectionProbe)target);
        }

        void DoOriginEditing()
        {
            ReflectionProbe p = (ReflectionProbe)target;
            Vector3 transformPosition = p.transform.position;
            Vector3 size = p.size;

            EditorGUI.BeginChangeCheck();
            Vector3 newPostion = Handles.PositionHandle(transformPosition, GetLocalSpaceRotation(p));

            bool changed = EditorGUI.EndChangeCheck();

            if (changed || m_OldLocalSpace != GetLocalSpace((ReflectionProbe)target))
            {
                Vector3 localNewPosition = m_OldLocalSpace.inverse.MultiplyPoint3x4(newPostion);

                Bounds b = new Bounds(p.center, size);
                localNewPosition = b.ClosestPoint(localNewPosition);

                Undo.RecordObject(p.transform, "Modified Reflection Probe Origin");
                p.transform.position = m_OldLocalSpace.MultiplyPoint3x4(localNewPosition);

                Undo.RecordObject(p, "Modified Reflection Probe Origin");
                p.center = GetLocalSpace(p).inverse.MultiplyPoint3x4(m_OldLocalSpace.MultiplyPoint3x4(p.center));

                EditorUtility.SetDirty(target);

                UpdateOldLocalSpace();
            }
        }

        static Matrix4x4 GetLocalSpace(ReflectionProbe probe)
        {
            Vector3 t = probe.transform.position;
            return Matrix4x4.TRS(t, GetLocalSpaceRotation(probe), Vector3.one);
        }

        static Quaternion GetLocalSpaceRotation(ReflectionProbe probe)
        {
            bool supportsRotation = (SupportedRenderingFeatures.active.reflectionProbeModes & SupportedRenderingFeatures.ReflectionProbeModes.Rotation) != 0;
            if (supportsRotation)
                return probe.transform.rotation;
            else
                return Quaternion.identity;
        }

        void DoBoxEditing()
        {
            // Drawing of the probe box is done from GizmoDrawers.cpp,
            // here we only want to show the box editing handles when needed.
            ReflectionProbe p = (ReflectionProbe)target;

            using (new Handles.DrawingScope(GetLocalSpace(p)))
            {
                m_BoundsHandle.center = p.center;
                m_BoundsHandle.size = p.size;

                EditorGUI.BeginChangeCheck();
                m_BoundsHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(p, "Modified Reflection Probe AABB");
                    Vector3 center = m_BoundsHandle.center;
                    Vector3 size = m_BoundsHandle.size;
                    ValidateAABB(ref center, ref size);
                    p.center = center;
                    p.size = size;
                    EditorUtility.SetDirty(target);
                }
            }
        }
    }
}