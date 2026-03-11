using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace SoundResonance.Editor
{
    /// <summary>
    /// Editor tool that creates a test scene with 2-3 resonant objects of different materials.
    /// The scene uses NO SubScene -- objects live directly in the scene hierarchy with
    /// standard BoxColliders for Physics.Raycast picking.
    ///
    /// The scene is designed for manual verification of the complete ECS pipeline:
    ///   Click (StrikeInputManager) -> StrikeEvent -> EmitterActivationSystem ->
    ///   ExponentialDecaySystem -> EmitterDeactivationSystem
    ///
    /// Objects:
    ///   - SteelBar: Elongated bar shape, very slow decay (Q=10000)
    ///   - GlassPlate: Flat plate shape, medium decay (Q=1000)
    ///   - WoodBar: Smaller bar shape, fast decay (Q=100)
    ///
    /// These three materials provide clearly distinguishable decay rates for visual testing.
    /// </summary>
    public static class TestSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/ResonanceTestScene.unity";
        private const string PresetPath = "Assets/SoundResonance/Runtime/ScriptableObjects/Presets";

        [MenuItem("Sound Resonance/Create Test Scene")]
        public static void CreateTestScene()
        {
            // Confirm if the scene already exists
            if (File.Exists(ScenePath))
            {
                if (!EditorUtility.DisplayDialog(
                    "Overwrite Test Scene?",
                    $"A test scene already exists at:\n{ScenePath}\n\nOverwrite it?",
                    "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            // Ensure the Scenes directory exists
            string scenesDir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(scenesDir))
                Directory.CreateDirectory(scenesDir);

            // Create a new empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Position the default camera to see all objects
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 1.5f, -3f);
                camera.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            }

            // Load material profiles
            var steel = LoadOrCreatePreset("Steel");
            var glass = LoadOrCreatePreset("Glass");
            var woodOak = LoadOrCreatePreset("Wood_Oak");

            // Create StrikeInputManager
            var inputManagerGO = new GameObject("StrikeInputManager");
            inputManagerGO.AddComponent<StrikeInputManager>();

            // Create SteelBar -- elongated bar shape
            CreateResonantObject(
                name: "SteelBar",
                position: new Vector3(-1.0f, 0.5f, 0f),
                scale: new Vector3(0.1f, 0.1f, 0.5f),
                material: steel);

            // Create GlassPlate -- flat plate shape
            CreateResonantObject(
                name: "GlassPlate",
                position: new Vector3(0f, 0.5f, 0f),
                scale: new Vector3(0.3f, 0.01f, 0.3f),
                material: glass);

            // Create WoodBar -- smaller elongated bar
            CreateResonantObject(
                name: "WoodBar",
                position: new Vector3(1.0f, 0.5f, 0f),
                scale: new Vector3(0.08f, 0.08f, 0.4f),
                material: woodOak);

            // Create a simple ground plane for visual reference
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(1f, 1f, 1f);

            // Save the scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Sound Resonance] Test scene created at {ScenePath} with 3 resonant objects " +
                      "(SteelBar, GlassPlate, WoodBar) and StrikeInputManager.");
        }

        /// <summary>
        /// Creates a resonant object GameObject with the required components:
        /// MeshFilter (Cube), MeshRenderer, BoxCollider, and ResonantObjectAuthoring.
        /// </summary>
        private static void CreateResonantObject(string name, Vector3 position, Vector3 scale,
            MaterialProfileSO material)
        {
            // CreatePrimitive gives us MeshFilter, MeshRenderer, and BoxCollider automatically
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;

            // Add the authoring component (it requires MeshFilter and MeshRenderer, which already exist)
            var authoring = go.AddComponent<ResonantObjectAuthoring>();
            authoring.materialProfile = material;

            // Verify BoxCollider is present (CreatePrimitive adds it for Cube, but be safe)
            if (go.GetComponent<BoxCollider>() == null)
            {
                go.AddComponent<BoxCollider>();
            }
        }

        /// <summary>
        /// Loads a MaterialProfileSO preset by name, or creates it from MaterialDatabase
        /// preset data if it doesn't exist.
        /// </summary>
        private static MaterialProfileSO LoadOrCreatePreset(string presetName)
        {
            string assetPath = $"{PresetPath}/{presetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<MaterialProfileSO>(assetPath);

            if (existing != null) return existing;

            // Preset doesn't exist -- create from MaterialDatabase preset data
            Debug.Log($"[Sound Resonance] Material preset '{presetName}' not found at {assetPath}. " +
                      "Creating from MaterialDatabase preset data.");

            var presets = MaterialDatabase.GetPresetData();
            MaterialPresetData? matchedPreset = null;

            foreach (var p in presets)
            {
                if (p.Name == presetName)
                {
                    matchedPreset = p;
                    break;
                }
            }

            if (!matchedPreset.HasValue)
            {
                Debug.LogError($"[Sound Resonance] No preset data found for '{presetName}' in MaterialDatabase.");
                return null;
            }

            // Ensure the Presets directory exists
            if (!Directory.Exists(PresetPath))
                Directory.CreateDirectory(PresetPath);

            var profile = ScriptableObject.CreateInstance<MaterialProfileSO>();
            var data = matchedPreset.Value;
            profile.youngsModulus = data.YoungsModulus;
            profile.density = data.Density;
            profile.lossFactor = data.LossFactor;
            profile.poissonRatio = data.PoissonRatio;

            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<MaterialProfileSO>(assetPath);
        }
    }
}
