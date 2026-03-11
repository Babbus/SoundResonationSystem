using Unity.Mathematics;

namespace SoundResonance
{
    /// <summary>
    /// Burst-safe, blittable struct containing real physical material properties.
    /// These values are measured constants from material science — not artistic parameters.
    ///
    /// Why these specific properties:
    /// - Young's Modulus (E): Determines how stiff the material is. Stiffer materials vibrate
    ///   at higher frequencies because they resist deformation more strongly, producing faster
    ///   restoring forces. Units: Pascals (Pa).
    /// - Density (rho): Mass per unit volume. Denser materials vibrate at lower frequencies
    ///   because more mass means more inertia to overcome. Units: kg/m^3.
    /// - Loss Factor (eta): The fraction of vibrational energy converted to heat per oscillation
    ///   cycle due to internal friction within the material's crystal/molecular structure.
    ///   This directly determines Q-factor (Q = 1/eta). Low eta = material rings a long time
    ///   (steel bell). High eta = material thuds and stops (rubber). Dimensionless.
    /// - Poisson's Ratio (nu): When you compress a material in one direction, it expands in the
    ///   perpendicular directions. This ratio affects the vibration modes of plates and shells
    ///   (2D/3D structures) but not bars (1D). Dimensionless, typically 0.2-0.5.
    /// </summary>
    public struct BlittableMaterialData
    {
        /// <summary>Young's Modulus in Pascals. Steel ~200e9, Glass ~70e9, Wood ~10e9.</summary>
        public float YoungsModulus;

        /// <summary>Density in kg/m^3. Steel ~7800, Glass ~2500, Wood ~500.</summary>
        public float Density;

        /// <summary>
        /// Internal friction / loss factor. Dimensionless.
        /// Directly determines Q-factor: Q = 1 / LossFactor.
        /// Steel ~0.0001 (rings forever), Rubber ~0.1 (thuds immediately).
        /// </summary>
        public float LossFactor;

        /// <summary>Poisson's Ratio. Dimensionless, typically 0.2-0.5.</summary>
        public float PoissonRatio;

        /// <summary>
        /// Cached Q-factor, computed as 1/LossFactor.
        /// Call Validate() after changing LossFactor to update this.
        /// </summary>
        public float QFactor;

        /// <summary>
        /// Speed of sound through this material in m/s.
        /// Derived from sqrt(E/rho). Cached for performance.
        /// </summary>
        public float SpeedOfSound;

        /// <summary>
        /// Per-material amplitude cutoff below which EmitterTag is disabled.
        /// Authored value from MaterialProfileSO, not computed.
        /// </summary>
        public float DeactivationThreshold;

        /// <summary>
        /// Recomputes derived values (QFactor, SpeedOfSound) from the primary properties.
        /// Must be called after modifying YoungsModulus, Density, or LossFactor.
        /// </summary>
        public void Validate()
        {
            QFactor = LossFactor > 0f ? 1f / LossFactor : 10000f;
            SpeedOfSound = Density > 0f ? math.sqrt(YoungsModulus / Density) : 0f;
        }
    }
}
