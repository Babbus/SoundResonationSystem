using UnityEngine;
using System.Collections.Generic;

namespace SoundResonance
{
    /// <summary>
    /// Central database of acoustic material profiles.
    /// Contains preset materials with real measured physical properties from
    /// material science reference data.
    ///
    /// Sources for property values:
    /// - ASM International Materials Data
    /// - Kinsler & Frey, "Fundamentals of Acoustics" (loss factors)
    /// - Blevins, "Formulas for Natural Frequency and Mode Shape"
    /// - CES EduPack material property database
    ///
    /// Note on loss factors: Published values vary significantly depending on
    /// measurement conditions (frequency, temperature, surface treatment).
    /// The values here are representative mid-range values suitable for simulation.
    /// </summary>
    [CreateAssetMenu(fileName = "MaterialDatabase", menuName = "Sound Resonance/Material Database")]
    public class MaterialDatabase : ScriptableObject
    {
        [Tooltip("All available acoustic material profiles.")]
        public List<MaterialProfileSO> materials = new List<MaterialProfileSO>();

        /// <summary>
        /// Finds a material profile by name. Returns null if not found.
        /// </summary>
        public MaterialProfileSO FindByName(string materialName)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i] != null && materials[i].name == materialName)
                    return materials[i];
            }
            return null;
        }

        /// <summary>
        /// Returns preset data for all built-in materials.
        /// Used by the editor tool to create MaterialProfileSO assets.
        /// All values are from real material science reference data.
        /// </summary>
        public static MaterialPresetData[] GetPresetData()
        {
            return new[]
            {
                new MaterialPresetData
                {
                    Name = "Steel",
                    YoungsModulus = 200e9f,   // Pa — structural carbon steel
                    Density = 7800f,          // kg/m3
                    LossFactor = 0.0001f,     // Very low internal friction — rings for minutes
                    PoissonRatio = 0.30f
                    // Q = 10000. A steel tuning fork or bell rings for a very long time.
                    // The crystal structure of steel has very little internal friction.
                },
                new MaterialPresetData
                {
                    Name = "Aluminum",
                    YoungsModulus = 69e9f,    // Pa — 6061 alloy
                    Density = 2700f,          // kg/m3
                    LossFactor = 0.0001f,     // Similar to steel
                    PoissonRatio = 0.33f
                    // Q = 10000. Aluminum chimes ring cleanly. Lower density than steel
                    // means higher frequencies for the same geometry.
                },
                new MaterialPresetData
                {
                    Name = "Glass",
                    YoungsModulus = 70e9f,    // Pa — soda-lime glass
                    Density = 2500f,          // kg/m3
                    LossFactor = 0.001f,      // Slightly more damping than metals
                    PoissonRatio = 0.22f
                    // Q = 1000. Wine glasses ring clearly but not as long as steel.
                    // The amorphous structure has slightly more internal friction
                    // than crystalline metals.
                },
                new MaterialPresetData
                {
                    Name = "Brass",
                    YoungsModulus = 100e9f,   // Pa — yellow brass (C27000)
                    Density = 8500f,          // kg/m3
                    LossFactor = 0.001f,      // Slightly more than steel
                    PoissonRatio = 0.34f
                    // Q = 1000. Brass bells and cymbals have a warm, sustained tone.
                    // Higher density than steel gives a lower pitch for same dimensions.
                },
                new MaterialPresetData
                {
                    Name = "Copper",
                    YoungsModulus = 120e9f,   // Pa — pure copper
                    Density = 8900f,          // kg/m3
                    LossFactor = 0.002f,      // More damping than brass
                    PoissonRatio = 0.34f
                    // Q = 500. Copper has a warmer, shorter ring than brass.
                },
                new MaterialPresetData
                {
                    Name = "Wood_Oak",
                    YoungsModulus = 12e9f,    // Pa — along grain
                    Density = 600f,           // kg/m3
                    LossFactor = 0.01f,       // Significant internal friction
                    PoissonRatio = 0.35f
                    // Q = 100. Wood has much more internal damping than metals.
                    // Vibrations are absorbed by the cellular/fibrous structure.
                    // Marimba bars are tuned wood — they ring, but briefly.
                },
                new MaterialPresetData
                {
                    Name = "Wood_Spruce",
                    YoungsModulus = 10e9f,    // Pa — Sitka spruce (instrument wood)
                    Density = 400f,           // kg/m3
                    LossFactor = 0.008f,      // Slightly less than oak — prized for instruments
                    PoissonRatio = 0.37f
                    // Q = 125. Spruce is the gold standard for instrument soundboards
                    // (guitars, violins, pianos) because it has the best stiffness-to-weight
                    // ratio AND relatively low damping for a wood.
                },
                new MaterialPresetData
                {
                    Name = "Concrete",
                    YoungsModulus = 30e9f,    // Pa — normal strength concrete
                    Density = 2400f,          // kg/m3
                    LossFactor = 0.02f,       // High internal friction
                    PoissonRatio = 0.20f
                    // Q = 50. Concrete absorbs vibrations quickly.
                    // The aggregate structure has many internal friction surfaces.
                },
                new MaterialPresetData
                {
                    Name = "Rubber",
                    YoungsModulus = 0.01e9f,  // Pa — natural rubber (very soft)
                    Density = 1100f,          // kg/m3
                    LossFactor = 0.1f,        // Extremely high damping
                    PoissonRatio = 0.49f      // Nearly incompressible
                    // Q = 10. Rubber barely resonates at all.
                    // The polymer chains absorb nearly all vibrational energy as heat.
                    // This is why rubber is used as a vibration DAMPER.
                },
                new MaterialPresetData
                {
                    Name = "Ceramic",
                    YoungsModulus = 300e9f,   // Pa — alumina ceramic
                    Density = 3800f,          // kg/m3
                    LossFactor = 0.001f,      // Low damping (crystalline structure)
                    PoissonRatio = 0.22f
                    // Q = 1000. Ceramic bowls and tiles have a clear, bright ring.
                    // High stiffness + moderate density = high frequencies.
                }
            };
        }
    }

    /// <summary>
    /// Data container for material presets, used by the editor tool to create
    /// MaterialProfileSO assets with correct physical properties.
    /// </summary>
    public struct MaterialPresetData
    {
        public string Name;
        public float YoungsModulus;
        public float Density;
        public float LossFactor;
        public float PoissonRatio;
    }
}
