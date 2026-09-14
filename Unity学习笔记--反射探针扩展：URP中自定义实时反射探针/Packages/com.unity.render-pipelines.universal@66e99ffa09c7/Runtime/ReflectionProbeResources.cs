using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// ScriptableObject for storing reflection probe related shader resources
    /// </summary>
    [CreateAssetMenu(fileName = "ReflectionProbeResources", menuName = "Rendering/Universal/Reflection Probe Resources", order = 100)]
    public class ReflectionProbeResources : ScriptableObject
    {
        [SerializeField]
        [ResourcePath("Shaders/Utils/FilterCubemap.shader")]
        private Shader m_FilterCubemapShader;

        /// <summary>
        /// FilterCubemap shader for GGX prefiltering
        /// </summary>
        public Shader filterCubemapShader
        {
            get => m_FilterCubemapShader;
            set => m_FilterCubemapShader = value;
        }

        private static ReflectionProbeResources s_Instance;

        /// <summary>
        /// Get the instance of ReflectionProbeResources
        /// </summary>
        public static ReflectionProbeResources instance
        {
            get
            {
                if (s_Instance == null)
                {
                    // Try to find the asset in the project
                    #if UNITY_EDITOR
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ReflectionProbeResources");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        s_Instance = UnityEditor.AssetDatabase.LoadAssetAtPath<ReflectionProbeResources>(path);
                    }
                    #endif

                    // Fallback: try to find in Resources folder
                    if (s_Instance == null)
                    {
                        s_Instance = Resources.Load<ReflectionProbeResources>("ReflectionProbeResources");
                    }
                }
                return s_Instance;
            }
        }
    }
}
