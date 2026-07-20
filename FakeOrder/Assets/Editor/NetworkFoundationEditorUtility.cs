using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class NetworkFoundationEditorUtility
{
    public static void BuildNetworkSmokePlayerForCommandLine()
    {
        string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Builds/NetworkSmoke/FakeOrderNetworkSmoke.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/GamePrototype.unity" },
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.InvalidOperationException($"Network smoke player build failed: {report.summary.result}");
        Debug.Log($"Network smoke player built: {outputPath}");
    }
}
