using UnityEngine;
using UnityEditor;
using System.IO;

namespace SoundResonance.Editor
{
    /// <summary>
    /// Editor tool that generates MaterialProfileSO assets from the built-in preset data.
    /// Creates one .asset file per material with real measured physical properties,
    /// plus a MaterialDatabase asset referencing all of them.
    /// </summary>
    public static class MaterialPresetGenerator
    {
        private const string PresetPath = "Assets/SoundResonance/Runtime/ScriptableObjects/Presets";
        private const string DatabasePath = "Assets/SoundResonance/Runtime/ScriptableObjects";

        [MenuItem("Sound Resonance/Generate Material Presets")]
        public static void GeneratePresets()
        {
            if (!Directory.Exists(PresetPath))
                Directory.CreateDirectory(PresetPath);

            var presets = MaterialDatabase.GetPresetData();
            var database = ScriptableObject.CreateInstance<MaterialDatabase>();

            foreach (var preset in presets)
            {
                string assetPath = $"{PresetPath}/{preset.Name}.asset";

                var existing = AssetDatabase.LoadAssetAtPath<MaterialProfileSO>(assetPath);
                if (existing != null)
                {
                    // Update existing asset
                    existing.youngsModulus = preset.YoungsModulus;
                    existing.density = preset.Density;
                    existing.lossFactor = preset.LossFactor;
                    existing.poissonRatio = preset.PoissonRatio;
                    EditorUtility.SetDirty(existing);
                    database.materials.Add(existing);
                }
                else
                {
                    var profile = ScriptableObject.CreateInstance<MaterialProfileSO>();
                    profile.youngsModulus = preset.YoungsModulus;
                    profile.density = preset.Density;
                    profile.lossFactor = preset.LossFactor;
                    profile.poissonRatio = preset.PoissonRatio;

                    AssetDatabase.CreateAsset(profile, assetPath);
                    database.materials.Add(profile);
                }
            }

            string dbPath = $"{DatabasePath}/MaterialDatabase.asset";
            var existingDb = AssetDatabase.LoadAssetAtPath<MaterialDatabase>(dbPath);
            if (existingDb != null)
            {
                existingDb.materials = database.materials;
                EditorUtility.SetDirty(existingDb);
                Object.DestroyImmediate(database);
            }
            else
            {
                AssetDatabase.CreateAsset(database, dbPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Sound Resonance] Generated {presets.Length} material presets and MaterialDatabase at {dbPath}");
        }
    }
}
