namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Contains the reflection probe data for the current frame.
    /// </summary>
    public class UniversalReflectionProbeData : ContextItem
    {
        /// <summary>
        /// The reflection probe data associated with the current rendering.
        /// </summary>
        public UniversalAdditionalReflectionProbeData probeData;

        /// <inheritdoc/>
        public override void Reset()
        {
            probeData = null;
        }
    }
}
