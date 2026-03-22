using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace SoundResonance.Editor
{
    /// <summary>
    /// Custom inspector for ResonantObjectAuthoring that shows the computed physics
    /// properties in real-time as the designer adjusts mesh scale or material.
    ///
    /// Displays:
    /// - Classified shape type (Bar/Plate/Shell) with explanation
    /// - Natural frequency in Hz and as a musical note name
    /// - Q-factor (resonance quality)
    /// - Characteristic dimensions used for the calculation
    ///
    /// All values update immediately when the material or transform changes,
    /// giving instant feedback on how geometry and material affect the sound.
    /// </summary>
    [CustomEditor(typeof(ResonantObjectAuthoring))]
    public class ResonantObjectAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default fields (materialProfile)
            DrawDefaultInspector();

            var authoring = (ResonantObjectAuthoring)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Computed Resonance Properties", EditorStyles.boldLabel);

            if (authoring.materialProfile == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Material Profile to see computed resonance properties.",
                    MessageType.Info);
                return;
            }

            var meshFilter = authoring.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                EditorGUILayout.HelpBox(
                    "No MeshFilter or mesh found. Add a mesh to compute resonance properties.",
                    MessageType.Warning);
                return;
            }

            // Compute shape classification (same logic as the Baker)
            var bounds = meshFilter.sharedMesh.bounds;
            float3 extents = new float3(bounds.extents.x, bounds.extents.y, bounds.extents.z);

            float3 scale = new float3(
                math.abs(authoring.transform.lossyScale.x),
                math.abs(authoring.transform.lossyScale.y),
                math.abs(authoring.transform.lossyScale.z));
            extents *= scale;

            var classification = ShapeClassifier.Classify(extents);
            var material = authoring.materialProfile.GetBlittableData();
            float f0 = FrequencyCalculator.CalculateNaturalFrequency(classification, material);
            string noteName = NoteNameHelper.FrequencyToNoteName(f0);

            // Display computed values in a disabled GUI group (read-only)
            EditorGUI.BeginDisabledGroup(true);

            EditorGUILayout.EnumPopup("Shape", classification.Type);
            EditorGUILayout.FloatField("Characteristic Length (m)", classification.CharacteristicLength);
            EditorGUILayout.FloatField("Thickness (m)", classification.Thickness);

            EditorGUILayout.Space(5);
            EditorGUILayout.FloatField("Natural Frequency (Hz)", f0);
            EditorGUILayout.TextField("Musical Note", noteName);
            EditorGUILayout.FloatField("Q-Factor", material.QFactor);

            EditorGUI.EndDisabledGroup();

            // Contextual help based on shape
            string shapeExplanation = classification.Type switch
            {
                ShapeType.Bar => "Bar-like: one dimension dominates. " +
                                 "Vibrates like a tuning fork prong or beam. " +
                                 "f0 ~ thickness / length^2.",
                ShapeType.Shell => "Shell-like: two large dimensions with curvature. " +
                                   "Vibrates like a bell or bowl. " +
                                   "f0 ~ wall_thickness / radius^2.",
                ShapeType.Plate => "Plate-like: two large dimensions, one thin. " +
                                   "Vibrates like a cymbal or panel. " +
                                   "f0 ~ thickness / diameter^2.",
                _ => ""
            };
            EditorGUILayout.HelpBox(shapeExplanation, MessageType.None);
        }
    }
}
