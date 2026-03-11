using Unity.Mathematics;

namespace SoundResonance
{
    /// <summary>
    /// Classifies a 3D mesh into a simplified vibration shape category based on its
    /// bounding box aspect ratios.
    ///
    /// Why we need shape classification:
    /// The natural frequency formula for a vibrating object depends fundamentally on its
    /// geometry. A long thin bar vibrates differently than a flat plate or a curved shell.
    /// Each shape type has a different relationship between dimensions, material properties,
    /// and resonant frequency.
    ///
    /// Why bounding box approximation (not FEA):
    /// Computing exact vibration modes of arbitrary 3D meshes requires Finite Element Analysis —
    /// a computationally expensive numerical method used in engineering software like ANSYS/COMSOL.
    /// For real-time simulation, we approximate by classifying the mesh into one of three
    /// canonical shapes and using the corresponding analytical formula. This is accurate for
    /// objects that roughly match these shapes (tuning forks, plates, bells) and provides
    /// reasonable estimates for others.
    ///
    /// Classification logic:
    /// We sort the bounding box extents from largest to smallest: dims[0] >= dims[1] >= dims[2].
    /// - Bar: One dimension dominates (dims[0]/dims[1] > threshold). Think: tuning fork prong,
    ///   rod, beam. Vibration is primarily along the long axis.
    /// - Shell: Two large dimensions, one thin, with curvature implied (dims[1]/dims[2] > threshold
    ///   AND dims[0]/dims[1] < threshold). Think: bell, bowl, cup.
    /// - Plate: Two large dimensions, one thin, roughly flat (everything else).
    ///   Think: cymbal, panel, tabletop.
    /// </summary>
    public enum ShapeType
    {
        Bar,
        Plate,
        Shell
    }

    /// <summary>
    /// Result of shape classification containing the shape type and the characteristic
    /// dimensions needed for the appropriate frequency formula.
    /// </summary>
    public struct ShapeClassification
    {
        public ShapeType Type;

        /// <summary>
        /// For Bar: the longest dimension (vibrating length).
        /// For Plate: the larger of the two dominant dimensions (effective radius).
        /// For Shell: the larger of the two dominant dimensions (effective radius).
        /// </summary>
        public float CharacteristicLength;

        /// <summary>
        /// For Bar: the cross-section thickness perpendicular to length.
        /// For Plate: the thin dimension (plate thickness).
        /// For Shell: the thin dimension (wall thickness).
        /// </summary>
        public float Thickness;
    }

    public static class ShapeClassifier
    {
        /// <summary>
        /// Aspect ratio threshold for bar classification.
        /// If the longest dimension is more than 3x the second longest, it's bar-like.
        /// Value of 3.0 comes from engineering practice: structural members with L/d > 3
        /// are typically analyzed using beam theory (Euler-Bernoulli).
        /// </summary>
        private const float BarAspectThreshold = 3.0f;

        /// <summary>
        /// Aspect ratio thresholds for shell classification.
        /// A shell has two large dimensions that differ somewhat (implying curvature,
        /// like a bell being taller than wide), plus one thin dimension (wall thickness).
        /// If the two large dims are nearly equal AND one is thin, it's a flat plate
        /// (like a cymbal), not a shell. The symmetry threshold range [1.2, 2.0] captures
        /// objects that have some asymmetry (curved) but aren't bar-like.
        /// </summary>
        private const float ShellThinThreshold = 3.0f;
        private const float ShellSymmetryMin = 1.2f;
        private const float ShellSymmetryMax = 2.0f;

        /// <summary>
        /// Classifies a mesh based on its bounding box extents.
        /// </summary>
        /// <param name="extents">Half-extents of the bounding box (as returned by Bounds.extents).</param>
        /// <returns>Shape classification with type and characteristic dimensions.</returns>
        public static ShapeClassification Classify(float3 extents)
        {
            // Use full dimensions, not half-extents
            float3 dims = extents * 2f;

            // Sort dimensions descending: x >= y >= z
            SortDescending(ref dims);

            float ratio01 = dims.y > 0f ? dims.x / dims.y : float.MaxValue;
            float ratio12 = dims.z > 0f ? dims.y / dims.z : float.MaxValue;

            if (ratio01 > BarAspectThreshold)
            {
                // One axis dominates — bar-like (tuning fork prong, rod, beam)
                return new ShapeClassification
                {
                    Type = ShapeType.Bar,
                    CharacteristicLength = dims.x,
                    Thickness = dims.y // cross-section thickness
                };
            }

            if (ratio12 > ShellThinThreshold && ratio01 > ShellSymmetryMin && ratio01 < ShellSymmetryMax)
            {
                // Two large similar dimensions, one thin — shell-like (bell, bowl)
                return new ShapeClassification
                {
                    Type = ShapeType.Shell,
                    CharacteristicLength = dims.x, // effective radius
                    Thickness = dims.z // wall thickness
                };
            }

            // Default: plate-like (cymbal, panel, flat surface)
            return new ShapeClassification
            {
                Type = ShapeType.Plate,
                CharacteristicLength = dims.x, // largest planar dimension
                Thickness = dims.z // plate thickness (thinnest dimension)
            };
        }

        private static void SortDescending(ref float3 v)
        {
            // Simple 3-element sort (descending)
            if (v.x < v.y) Swap(ref v.x, ref v.y);
            if (v.y < v.z) Swap(ref v.y, ref v.z);
            if (v.x < v.y) Swap(ref v.x, ref v.y);
        }

        private static void Swap(ref float a, ref float b)
        {
            float temp = a;
            a = b;
            b = temp;
        }
    }
}
