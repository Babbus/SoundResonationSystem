using Unity.Entities;

namespace SoundResonance
{
    /// <summary>
    /// Core ECS component holding all physics-derived data for a resonant object.
    ///
    /// Baked at edit-time from mesh geometry + material properties:
    /// - NaturalFrequency: computed via ShapeClassifier + FrequencyCalculator
    /// - QFactor: from material loss factor (Q = 1/eta)
    /// - Shape: classified from bounding box aspect ratios
    ///
    /// Updated at runtime by ResonanceSystem:
    /// - CurrentAmplitude: driven oscillator output, read by audio bridge
    /// - Phase: accumulator for glitch-free sine generation
    ///
    /// This is an unmanaged, blittable struct — safe for Burst jobs and
    /// parallel iteration in IJobEntity.
    /// </summary>
    public struct ResonantObjectData : IComponentData
    {
        /// <summary>Fundamental natural frequency in Hz. Baked from geometry + material.</summary>
        public float NaturalFrequency;

        /// <summary>Quality factor (1/lossFactor). Baked from material.</summary>
        public float QFactor;

        /// <summary>Classified vibration shape. Baked from mesh bounding box.</summary>
        public ShapeType Shape;

        /// <summary>
        /// Current vibration amplitude [0, 1]. Updated per-frame by ResonanceSystem.
        /// Read by ResonanceAudioBridge in LateUpdate for audio synthesis.
        /// </summary>
        public float CurrentAmplitude;

        /// <summary>
        /// Phase accumulator in radians [0, 2*pi). Updated per-frame for
        /// glitch-free sine wave generation in the audio callback.
        /// </summary>
        public float Phase;

        /// <summary>
        /// Per-material amplitude cutoff below which EmitterTag is disabled.
        /// Baked from MaterialProfileSO.deactivationThreshold.
        /// Steel uses a lower threshold (rings longer), rubber uses higher (stops quickly).
        /// Default conceptually matches ResonanceMath.AmplitudeThreshold (0.0001f)
        /// but the actual runtime value comes from baking.
        /// </summary>
        public float DeactivationThreshold;
    }
}
