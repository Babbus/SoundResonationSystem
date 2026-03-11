using UnityEngine;

namespace SoundResonance
{
    /// <summary>
    /// ScriptableObject holding real physical material properties for acoustic simulation.
    ///
    /// The sound designer selects a material (e.g., "Steel") from the preset database.
    /// The system uses the material's measured physical constants to compute:
    /// - Natural frequency (from material + mesh geometry)
    /// - Q-factor / resonance selectivity (from loss factor)
    /// - Decay rate (from Q-factor)
    ///
    /// These are NOT artistic parameters — they are real values from material science
    /// reference data (ASM International, Kinsler & Frey "Fundamentals of Acoustics").
    /// </summary>
    [CreateAssetMenu(fileName = "NewMaterial", menuName = "Sound Resonance/Material Profile")]
    public class MaterialProfileSO : ScriptableObject
    {
        [Header("Physical Properties")]
        [Tooltip("Young's Modulus in Pascals (Pa). Measures material stiffness. " +
                 "Higher = stiffer = higher natural frequency. Steel: 200e9, Glass: 70e9, Wood: 10e9")]
        public float youngsModulus = 200e9f;

        [Tooltip("Density in kg/m^3. Higher = heavier = lower natural frequency. " +
                 "Steel: 7800, Glass: 2500, Wood: 500")]
        public float density = 7800f;

        [Tooltip("Internal friction / loss factor (dimensionless). " +
                 "Determines how quickly vibrations die out. Q = 1/eta. " +
                 "Steel: 0.0001 (rings long), Rubber: 0.1 (thuds)")]
        [Range(0.00001f, 1f)]
        public float lossFactor = 0.0001f;

        [Tooltip("Poisson's Ratio (dimensionless, 0.0-0.5). " +
                 "Ratio of lateral to axial strain. Affects plate/shell vibration modes. " +
                 "Steel: 0.30, Glass: 0.22, Rubber: 0.49")]
        [Range(0f, 0.5f)]
        public float poissonRatio = 0.30f;

        [Tooltip("Amplitude below which the object stops vibrating and EmitterTag is disabled. " +
                 "Lower values let the object ring longer. Steel: 0.0001, Rubber: 0.005")]
        [Range(0.00001f, 0.01f)]
        public float deactivationThreshold = 0.0001f;

        [Header("Derived (Read-Only)")]
        [Tooltip("Quality factor = 1 / lossFactor. Higher Q = narrower resonance peak = longer ring time.")]
        [SerializeField] private float qFactor;

        [Tooltip("Speed of sound through this material in m/s.")]
        [SerializeField] private float speedOfSound;

        /// <summary>Returns a Burst-safe blittable copy of this material's data.</summary>
        public BlittableMaterialData GetBlittableData()
        {
            var data = new BlittableMaterialData
            {
                YoungsModulus = youngsModulus,
                Density = density,
                LossFactor = lossFactor,
                PoissonRatio = poissonRatio
            };
            data.Validate();
            data.DeactivationThreshold = deactivationThreshold;
            return data;
        }

        private void OnValidate()
        {
            if (lossFactor <= 0f) lossFactor = 0.00001f;
            if (density <= 0f) density = 1f;
            if (youngsModulus <= 0f) youngsModulus = 1f;

            qFactor = 1f / lossFactor;
            speedOfSound = Mathf.Sqrt(youngsModulus / density);
        }
    }
}
