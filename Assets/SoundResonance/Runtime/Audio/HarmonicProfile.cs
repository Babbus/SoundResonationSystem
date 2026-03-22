namespace SoundResonance
{
    /// <summary>
    /// Shape-to-harmonic-ratio and amplitude-weight lookups for additive synthesis.
    /// Each shape type has physically-derived partial frequency ratios and amplitude weights
    /// that determine the timbre of the synthesized sound.
    ///
    /// Ratio sources:
    /// - Bar: Euler-Bernoulli beam theory modes (1, 2.756, 5.404, 8.933)
    /// - Plate: Kirchhoff plate theory modes (1, 1.594, 2.136, 2.653)
    /// - Shell: Donnell shell theory modes (1, 1.506, 1.927, 2.292)
    ///
    /// Weight philosophy:
    /// - Direct strikes excite many partials (shape-specific distribution)
    /// - Sympathetic activation is purer (fundamental-dominated)
    /// </summary>
    public static class HarmonicProfile
    {
        /// <summary>Number of partials synthesized per voice.</summary>
        public const int PartialCount = 4;

        // Euler-Bernoulli beam modes
        private static readonly float[] BarRatios = { 1.0f, 2.756f, 5.404f, 8.933f };
        // Kirchhoff plate modes
        private static readonly float[] PlateRatios = { 1.0f, 1.594f, 2.136f, 2.653f };
        // Donnell shell modes
        private static readonly float[] ShellRatios = { 1.0f, 1.506f, 1.927f, 2.292f };

        // Direct strike weights per shape
        private static readonly float[] BarWeights = { 1.0f, 0.5f, 0.25f, 0.12f };       // fundamental-heavy, metallic ring
        private static readonly float[] PlateWeights = { 1.0f, 0.7f, 0.5f, 0.35f };       // even distribution, shimmery
        private static readonly float[] ShellWeights = { 0.8f, 1.0f, 0.7f, 0.4f };        // mid-heavy, bell-like

        // Sympathetic activation: purer tone, fundamental-dominated
        private static readonly float[] SympatheticWeights = { 1.0f, 0.15f, 0.05f, 0.02f };

        /// <summary>
        /// Returns the partial frequency ratios for the given shape type.
        /// Multiply each ratio by the fundamental frequency to get partial frequencies.
        /// </summary>
        public static float[] GetRatios(ShapeType shape)
        {
            switch (shape)
            {
                case ShapeType.Plate: return PlateRatios;
                case ShapeType.Shell: return ShellRatios;
                case ShapeType.Bar:
                default:
                    return BarRatios;
            }
        }

        /// <summary>
        /// Returns the amplitude weights for each partial.
        /// Sympathetic activation uses a purer tone profile regardless of shape.
        /// </summary>
        public static float[] GetWeights(ShapeType shape, bool isSympathetic)
        {
            if (isSympathetic) return SympatheticWeights;

            switch (shape)
            {
                case ShapeType.Plate: return PlateWeights;
                case ShapeType.Shell: return ShellWeights;
                case ShapeType.Bar:
                default:
                    return BarWeights;
            }
        }
    }
}
