using NUnit.Framework;
using Unity.Mathematics;

namespace SoundResonance.Tests
{
    /// <summary>
    /// Validates that ShapeClassifier correctly identifies object geometries
    /// from bounding box aspect ratios.
    /// </summary>
    public class ShapeClassifierTests
    {
        [Test]
        public void LongThinObject_ClassifiesAsBar()
        {
            // Tuning fork prong: 10cm long, 0.6cm wide, 0.4cm thick
            // Half-extents (as returned by Bounds.extents)
            var extents = new float3(0.05f, 0.003f, 0.002f);
            var result = ShapeClassifier.Classify(extents);

            Assert.AreEqual(ShapeType.Bar, result.Type,
                "A long thin object (10:0.6:0.4 ratio) should classify as Bar");
        }

        [Test]
        public void FlatWideObject_ClassifiesAsPlate()
        {
            // Cymbal: 40cm diameter, 40cm wide, 2mm thick
            // Half-extents
            var extents = new float3(0.2f, 0.2f, 0.001f);
            var result = ShapeClassifier.Classify(extents);

            Assert.AreEqual(ShapeType.Plate, result.Type,
                "A flat wide object (40:40:0.2 ratio) should classify as Plate");
        }

        [Test]
        public void TwoLargeOneThin_WithSymmetry_ClassifiesAsShell()
        {
            // Bell: 20cm tall, 15cm wide, 2mm wall (effectively)
            // The two large dims are similar, one is thin
            var extents = new float3(0.1f, 0.075f, 0.001f);
            var result = ShapeClassifier.Classify(extents);

            Assert.AreEqual(ShapeType.Shell, result.Type,
                "Two similar large dimensions with one thin should classify as Shell");
        }

        [Test]
        public void Cube_ClassifiesAsPlate()
        {
            // A cube has no dominant axis — defaults to plate (most general case)
            var extents = new float3(0.1f, 0.1f, 0.1f);
            var result = ShapeClassifier.Classify(extents);

            Assert.AreEqual(ShapeType.Plate, result.Type,
                "A cube (all equal dimensions) should classify as Plate (default)");
        }

        [Test]
        public void BarClassification_ReturnsCorrectDimensions()
        {
            // Long axis = 0.2m full length, cross-section = 0.02m full thickness
            var extents = new float3(0.1f, 0.01f, 0.005f);
            var result = ShapeClassifier.Classify(extents);

            Assert.AreEqual(ShapeType.Bar, result.Type);
            Assert.That(result.CharacteristicLength, Is.InRange(0.19f, 0.21f),
                "Characteristic length should be the full longest dimension");
            Assert.That(result.Thickness, Is.InRange(0.019f, 0.021f),
                "Thickness should be the full second-longest dimension");
        }

        [Test]
        public void DimensionOrder_DoesNotMatter()
        {
            // Same object, different axis orientations
            var result1 = ShapeClassifier.Classify(new float3(0.1f, 0.01f, 0.005f));
            var result2 = ShapeClassifier.Classify(new float3(0.005f, 0.1f, 0.01f));
            var result3 = ShapeClassifier.Classify(new float3(0.01f, 0.005f, 0.1f));

            Assert.AreEqual(result1.Type, result2.Type, "Classification should be rotation-independent");
            Assert.AreEqual(result1.Type, result3.Type, "Classification should be rotation-independent");
            Assert.AreEqual(result1.CharacteristicLength, result2.CharacteristicLength, 0.001f);
            Assert.AreEqual(result1.CharacteristicLength, result3.CharacteristicLength, 0.001f);
        }
    }
}
