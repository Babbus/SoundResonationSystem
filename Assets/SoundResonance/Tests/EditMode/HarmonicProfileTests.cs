using NUnit.Framework;

namespace SoundResonance.Tests
{
    /// <summary>
    /// Validates HarmonicProfile returns correct physics-derived frequency ratios
    /// and amplitude weights for each shape type.
    /// </summary>
    public class HarmonicProfileTests
    {
        private const float Delta = 0.001f;

        // --- GetRatios tests ---

        [Test]
        public void GetRatios_Bar_ReturnsEulerBernoulliModes()
        {
            float[] ratios = HarmonicProfile.GetRatios(ShapeType.Bar);
            Assert.AreEqual(HarmonicProfile.PartialCount, ratios.Length);
            Assert.AreEqual(1.0f, ratios[0], Delta);
            Assert.AreEqual(2.756f, ratios[1], Delta);
            Assert.AreEqual(5.404f, ratios[2], Delta);
            Assert.AreEqual(8.933f, ratios[3], Delta);
        }

        [Test]
        public void GetRatios_Plate_ReturnsKirchhoffModes()
        {
            float[] ratios = HarmonicProfile.GetRatios(ShapeType.Plate);
            Assert.AreEqual(HarmonicProfile.PartialCount, ratios.Length);
            Assert.AreEqual(1.0f, ratios[0], Delta);
            Assert.AreEqual(1.594f, ratios[1], Delta);
            Assert.AreEqual(2.136f, ratios[2], Delta);
            Assert.AreEqual(2.653f, ratios[3], Delta);
        }

        [Test]
        public void GetRatios_Shell_ReturnsDonnellModes()
        {
            float[] ratios = HarmonicProfile.GetRatios(ShapeType.Shell);
            Assert.AreEqual(HarmonicProfile.PartialCount, ratios.Length);
            Assert.AreEqual(1.0f, ratios[0], Delta);
            Assert.AreEqual(1.506f, ratios[1], Delta);
            Assert.AreEqual(1.927f, ratios[2], Delta);
            Assert.AreEqual(2.292f, ratios[3], Delta);
        }

        [Test]
        public void GetRatios_AllShapes_ReturnExactlyFourElements()
        {
            Assert.AreEqual(4, HarmonicProfile.GetRatios(ShapeType.Bar).Length);
            Assert.AreEqual(4, HarmonicProfile.GetRatios(ShapeType.Plate).Length);
            Assert.AreEqual(4, HarmonicProfile.GetRatios(ShapeType.Shell).Length);
        }

        // --- GetWeights tests ---

        [Test]
        public void GetWeights_Bar_DirectStrike_ReturnsFundamentalHeavy()
        {
            float[] weights = HarmonicProfile.GetWeights(ShapeType.Bar, isSympathetic: false);
            Assert.AreEqual(HarmonicProfile.PartialCount, weights.Length);
            Assert.AreEqual(1.0f, weights[0], Delta);
            Assert.AreEqual(0.5f, weights[1], Delta);
            Assert.AreEqual(0.25f, weights[2], Delta);
            Assert.AreEqual(0.12f, weights[3], Delta);
        }

        [Test]
        public void GetWeights_Plate_DirectStrike_ReturnsEvenDistribution()
        {
            float[] weights = HarmonicProfile.GetWeights(ShapeType.Plate, isSympathetic: false);
            Assert.AreEqual(HarmonicProfile.PartialCount, weights.Length);
            Assert.AreEqual(1.0f, weights[0], Delta);
            Assert.AreEqual(0.7f, weights[1], Delta);
            Assert.AreEqual(0.5f, weights[2], Delta);
            Assert.AreEqual(0.35f, weights[3], Delta);
        }

        [Test]
        public void GetWeights_Shell_DirectStrike_ReturnsMidHeavy()
        {
            float[] weights = HarmonicProfile.GetWeights(ShapeType.Shell, isSympathetic: false);
            Assert.AreEqual(HarmonicProfile.PartialCount, weights.Length);
            Assert.AreEqual(0.8f, weights[0], Delta);
            Assert.AreEqual(1.0f, weights[1], Delta);
            Assert.AreEqual(0.7f, weights[2], Delta);
            Assert.AreEqual(0.4f, weights[3], Delta);
        }

        [Test]
        public void GetWeights_Sympathetic_ReturnsPurerTone_RegardlessOfShape()
        {
            // All shapes should return the same sympathetic weights
            float[] barWeights = HarmonicProfile.GetWeights(ShapeType.Bar, isSympathetic: true);
            float[] plateWeights = HarmonicProfile.GetWeights(ShapeType.Plate, isSympathetic: true);
            float[] shellWeights = HarmonicProfile.GetWeights(ShapeType.Shell, isSympathetic: true);

            float[] expected = { 1.0f, 0.15f, 0.05f, 0.02f };

            for (int i = 0; i < HarmonicProfile.PartialCount; i++)
            {
                Assert.AreEqual(expected[i], barWeights[i], Delta,
                    $"Bar sympathetic weight[{i}] mismatch");
                Assert.AreEqual(expected[i], plateWeights[i], Delta,
                    $"Plate sympathetic weight[{i}] mismatch");
                Assert.AreEqual(expected[i], shellWeights[i], Delta,
                    $"Shell sympathetic weight[{i}] mismatch");
            }
        }

        [Test]
        public void GetWeights_AllShapes_ReturnExactlyFourElements()
        {
            Assert.AreEqual(4, HarmonicProfile.GetWeights(ShapeType.Bar, false).Length);
            Assert.AreEqual(4, HarmonicProfile.GetWeights(ShapeType.Plate, false).Length);
            Assert.AreEqual(4, HarmonicProfile.GetWeights(ShapeType.Shell, false).Length);
            Assert.AreEqual(4, HarmonicProfile.GetWeights(ShapeType.Bar, true).Length);
        }
    }
}
