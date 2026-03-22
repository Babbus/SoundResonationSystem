using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace SoundResonance
{
    /// <summary>
    /// Authoring component for resonant objects. Attach this to any GameObject with a
    /// MeshFilter in a SubScene to make it participate in the resonance simulation.
    ///
    /// The Baker reads the MeshFilter's bounding box extents and the assigned material
    /// profile, then uses ShapeClassifier and FrequencyCalculator to compute the object's
    /// natural frequency, Q-factor, and shape classification — all baked into ECS at
    /// edit-time so there's zero runtime cost for these calculations.
    ///
    /// The designer's workflow:
    /// 1. Create/import a mesh (e.g., elongated cube for a bar, flat disc for a cymbal)
    /// 2. Add ResonantObjectAuthoring component
    /// 3. Assign a MaterialProfileSO (e.g., Steel, Glass)
    /// 4. The custom inspector immediately shows the computed frequency, musical note, and Q
    /// 5. Place in a SubScene — the Baker handles everything else
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ResonantObjectAuthoring : MonoBehaviour
    {
        [Tooltip("Physical material profile determining resonant properties. " +
                 "Select from preset materials (Steel, Glass, Wood, etc.) or create custom.")]
        public MaterialProfileSO materialProfile;

        /// <summary>
        /// Baker that converts authoring data into ECS components at build time.
        ///
        /// Why a Baker (not a conversion system):
        /// Unity Entities 1.x uses Bakers for SubScene serialization. Bakers run in the
        /// Editor when you modify the SubScene, and the result is serialized to disk.
        /// At runtime, the baked data loads directly — no conversion step needed.
        /// This means our ShapeClassifier/FrequencyCalculator math runs once in the Editor,
        /// not every time the game starts.
        /// </summary>
        private class ResonantObjectBaker : Baker<ResonantObjectAuthoring>
        {
            public override void Bake(ResonantObjectAuthoring authoring)
            {
                if (authoring.materialProfile == null)
                {
                    Debug.LogWarning(
                        $"ResonantObjectAuthoring on '{authoring.name}' has no material profile assigned. " +
                        "Skipping bake.", authoring);
                    return;
                }

                // DependsOn ensures re-baking when the material asset changes
                DependsOn(authoring.materialProfile);

                var meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    Debug.LogWarning(
                        $"ResonantObjectAuthoring on '{authoring.name}' has no MeshFilter or mesh. " +
                        "Skipping bake.", authoring);
                    return;
                }

                // Read mesh bounds for shape classification
                var bounds = meshFilter.sharedMesh.bounds;
                float3 extents = new float3(bounds.extents.x, bounds.extents.y, bounds.extents.z);

                // Account for GameObject scale — bounds are in local space.
                // Use GetComponent<Transform>() for proper Baker dependency tracking.
                var transform = GetComponent<Transform>();
                float3 scale = new float3(
                    math.abs(transform.lossyScale.x),
                    math.abs(transform.lossyScale.y),
                    math.abs(transform.lossyScale.z));
                extents *= scale;

                // Classify shape from bounding box aspect ratios
                var classification = ShapeClassifier.Classify(extents);

                // Get blittable material data and compute natural frequency
                var material = authoring.materialProfile.GetBlittableData();
                float f0 = FrequencyCalculator.CalculateNaturalFrequency(classification, material);

                // Get the entity — uses Dynamic because resonant objects may move
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Bake the core resonant data
                AddComponent(entity, new ResonantObjectData
                {
                    NaturalFrequency = f0,
                    QFactor = material.QFactor,
                    Shape = classification.Type,
                    CurrentAmplitude = 0f,
                    Phase = 0f,
                    DeactivationThreshold = material.DeactivationThreshold
                });

                // Add EmitterTag (disabled) — will be enabled when struck
                AddComponent<EmitterTag>(entity);
                SetComponentEnabled<EmitterTag>(entity, false);

                // Add StrikeEvent (disabled) — will be enabled by input system
                AddComponent<StrikeEvent>(entity);
                SetComponentEnabled<StrikeEvent>(entity, false);

                // Add base color override for amplitude visualization
                AddComponent(entity, new URPMaterialPropertyBaseColor
                {
                    Value = new float4(0.5f, 0.5f, 0.5f, 1f)
                });
            }
        }
    }
}
