using Unity.Mathematics;

namespace SoundResonance
{
    /// <summary>
    /// Computes the natural (resonant) frequency of an object from its material properties
    /// and geometry using analytical formulas from structural vibration theory.
    ///
    /// Why natural frequency matters:
    /// Every physical object has frequencies at which it "wants" to vibrate — these are
    /// determined entirely by what it's made of and its shape. When driven at these
    /// frequencies, amplitude builds up dramatically (resonance). At other frequencies,
    /// the object barely responds.
    ///
    /// The general pattern across all shapes:
    ///   f0 increases with stiffness (stiffer = faster restoring force = higher pitch)
    ///   f0 decreases with density (heavier = more inertia = lower pitch)
    ///   f0 increases with thickness (thicker = stiffer cross-section)
    ///   f0 decreases with size (longer/larger = more mass to move)
    ///
    /// All formulas here are standard results from vibration theory textbooks
    /// (Blevins "Formulas for Natural Frequency and Mode Shape", Leissa "Vibration of Plates").
    /// </summary>
    public static class FrequencyCalculator
    {
        /// <summary>
        /// Coefficient for the fundamental mode of a free-free bar (both ends free to vibrate).
        /// This comes from solving the Euler-Bernoulli beam equation with free-free boundary
        /// conditions. The eigenvalue equation gives lambda_1 * L = 4.730, and the coefficient
        /// C_1 = (lambda_1)^2 / (2*pi) = 22.373 / (2*pi) ≈ 3.5608.
        ///
        /// However, the standard simplified form for a rectangular cross-section bar is:
        ///   f_n = (C_n / (2*pi)) * (lambda_n^2 / L^2) * sqrt(E*I / (rho*A))
        /// which simplifies (for rectangular cross-section) to:
        ///   f_1 = 1.028 * (t / L^2) * sqrt(E / rho)
        ///
        /// where the 1.028 factor absorbs the eigenvalue, 2*pi, and the geometry factor
        /// for a rectangular cross-section (I/A = t^2/12).
        ///
        /// Reference: Blevins, "Formulas for Natural Frequency and Mode Shape", Table 8-1.
        /// </summary>
        private const float FreeFreeBarCoefficient = 1.028f;

        /// <summary>
        /// Coefficient for the fundamental mode of a circular plate clamped at the edge.
        /// From the solution to the biharmonic equation in polar coordinates:
        ///   f = (alpha^2 / (2*pi*R^2)) * sqrt(D / (rho*h))
        /// where D = E*h^3 / (12*(1-nu^2)) is the flexural rigidity, alpha ≈ 10.21 for
        /// the fundamental mode (0,0).
        ///
        /// Simplified: f = C * (t / R^2) * sqrt(E / (12 * rho * (1 - nu^2)))
        /// where C ≈ 0.469 absorbs the eigenvalue and 2*pi.
        ///
        /// We use a simpler approximation treating the characteristic length as diameter,
        /// appropriate for our bounding-box-derived dimensions.
        ///
        /// Reference: Leissa, "Vibration of Plates", NASA SP-160, Chapter 2.
        /// </summary>
        private const float PlateCoefficient = 0.469f;

        /// <summary>
        /// Coefficient for the fundamental mode of a thin cylindrical shell.
        /// From Donnell's shell theory, the lowest frequency mode for a cylinder is:
        ///   f = C * (h / R^2) * sqrt(E / rho)
        /// where C ≈ 0.559 for the breathing mode (n=0).
        ///
        /// For bell-like shells (open at one end), the actual modes are more complex,
        /// but this provides a reasonable first approximation.
        ///
        /// Reference: Blevins, "Formulas for Natural Frequency and Mode Shape", Table 12-1.
        /// </summary>
        private const float ShellCoefficient = 0.559f;

        /// <summary>
        /// Computes the fundamental natural frequency for a bar (beam) with free-free
        /// boundary conditions (both ends free to vibrate, like a tuning fork prong).
        ///
        /// Formula: f0 = 1.028 * (thickness / length^2) * sqrt(E / rho)
        ///
        /// Physical intuition:
        /// - Longer bar → lower frequency (f ~ 1/L^2, not 1/L, because bending stiffness
        ///   depends on length squared)
        /// - Thicker bar → higher frequency (thicker cross-section is stiffer)
        /// - Stiffer material → higher frequency (stronger restoring force)
        /// - Denser material → lower frequency (more mass to accelerate)
        /// </summary>
        /// <param name="length">Bar length in meters.</param>
        /// <param name="thickness">Cross-section thickness in meters.</param>
        /// <param name="youngsModulus">Young's Modulus in Pascals.</param>
        /// <param name="density">Density in kg/m^3.</param>
        /// <returns>Fundamental frequency in Hz.</returns>
        public static float CalculateBarFrequency(float length, float thickness,
            float youngsModulus, float density)
        {
            if (length <= 0f || density <= 0f) return 0f;
            return FreeFreeBarCoefficient * (thickness / (length * length))
                   * math.sqrt(youngsModulus / density);
        }

        /// <summary>
        /// Computes the fundamental natural frequency for a flat plate.
        ///
        /// Formula: f0 = C * (thickness / R^2) * sqrt(E / (12 * rho * (1 - nu^2)))
        ///
        /// Physical intuition:
        /// - The (1 - nu^2) term appears because plates deform in 2D. When you bend a plate
        ///   in one direction, Poisson's ratio causes it to also bend in the perpendicular
        ///   direction, effectively making it stiffer than a simple beam.
        /// - Higher Poisson's ratio → slightly higher frequency (more coupling between axes).
        /// - The 1/12 factor comes from the moment of inertia of a rectangular cross-section.
        /// </summary>
        /// <param name="characteristicLength">Plate diameter/length in meters.</param>
        /// <param name="thickness">Plate thickness in meters.</param>
        /// <param name="youngsModulus">Young's Modulus in Pascals.</param>
        /// <param name="density">Density in kg/m^3.</param>
        /// <param name="poissonRatio">Poisson's Ratio (dimensionless).</param>
        /// <returns>Fundamental frequency in Hz.</returns>
        public static float CalculatePlateFrequency(float characteristicLength, float thickness,
            float youngsModulus, float density, float poissonRatio)
        {
            if (characteristicLength <= 0f || density <= 0f) return 0f;
            float stiffnessTerm = youngsModulus / (12f * density * (1f - poissonRatio * poissonRatio));
            return PlateCoefficient * (thickness / (characteristicLength * characteristicLength))
                   * math.sqrt(stiffnessTerm);
        }

        /// <summary>
        /// Computes the fundamental natural frequency for a thin shell (bell-like).
        ///
        /// Formula: f0 = C * (wallThickness / R^2) * sqrt(E / rho)
        ///
        /// Physical intuition:
        /// - Shells are inherently stiffer than flat plates of the same thickness because
        ///   curvature provides geometric stiffness (like how a curved sheet of paper can
        ///   support weight that a flat one cannot).
        /// - The formula is simpler than the plate formula (no Poisson's ratio term) because
        ///   the dominant stiffness comes from the curvature, not from bending.
        /// </summary>
        /// <param name="radius">Shell radius in meters.</param>
        /// <param name="wallThickness">Wall thickness in meters.</param>
        /// <param name="youngsModulus">Young's Modulus in Pascals.</param>
        /// <param name="density">Density in kg/m^3.</param>
        /// <returns>Fundamental frequency in Hz.</returns>
        public static float CalculateShellFrequency(float radius, float wallThickness,
            float youngsModulus, float density)
        {
            if (radius <= 0f || density <= 0f) return 0f;
            return ShellCoefficient * (wallThickness / (radius * radius))
                   * math.sqrt(youngsModulus / density);
        }

        /// <summary>
        /// Computes the natural frequency of an object by dispatching to the appropriate
        /// formula based on its shape classification.
        /// </summary>
        /// <param name="classification">Shape classification from ShapeClassifier.</param>
        /// <param name="material">Physical material properties.</param>
        /// <returns>Fundamental frequency in Hz.</returns>
        public static float CalculateNaturalFrequency(ShapeClassification classification,
            BlittableMaterialData material)
        {
            switch (classification.Type)
            {
                case ShapeType.Bar:
                    return CalculateBarFrequency(
                        classification.CharacteristicLength,
                        classification.Thickness,
                        material.YoungsModulus,
                        material.Density);

                case ShapeType.Plate:
                    return CalculatePlateFrequency(
                        classification.CharacteristicLength,
                        classification.Thickness,
                        material.YoungsModulus,
                        material.Density,
                        material.PoissonRatio);

                case ShapeType.Shell:
                    return CalculateShellFrequency(
                        classification.CharacteristicLength,
                        classification.Thickness,
                        material.YoungsModulus,
                        material.Density);

                default:
                    return 0f;
            }
        }
    }
}
