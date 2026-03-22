using UnityEditor;
using UnityEngine;

public class BuildScript
{
    public static void BuildWindows()
    {
        var scenes = new[] { "Assets/Scenes/ResonanceTestScene.unity" };
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/SoundResonationSystem.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError($"Build failed: {report.summary.totalErrors} errors");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log("Build succeeded!");
            EditorApplication.Exit(0);
        }
    }
}
