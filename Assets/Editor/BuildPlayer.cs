using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildPlayer
{
    public static void Windows()
    {
        var args = System.Environment.GetCommandLineArgs();
        string output = args[System.Array.IndexOf(args, "-out") + 1];
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        Debug.Log("BUILD RESULT: " + report.summary.result + " errors=" + report.summary.totalErrors);
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
