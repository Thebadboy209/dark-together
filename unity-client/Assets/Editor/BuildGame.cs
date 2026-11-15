#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// One-click builds. Unity must have the requested platform support installed.
public static class DarkTogetherBuild
{
    private const string ScenePath = "Assets/Scenes/DarkTogether.unity";

    [MenuItem("Dark Together/Build/Windows x64")]
    public static void Windows()
    {
        Build(BuildTarget.StandaloneWindows64, "Builds/Windows/DarkTogether.exe");
    }

    [MenuItem("Dark Together/Build/Android APK")]
    public static void Android()
    {
        EditorUserBuildSettings.buildAppBundle = false;
        Build(BuildTarget.Android, "Builds/Android/DarkTogether.apk");
    }

    [MenuItem("Dark Together/Build/iOS Xcode Project")]
    public static void IOS()
    {
        Build(BuildTarget.iOS, "Builds/iOS");
    }

    private static void Build(BuildTarget platform, string output)
    {
        if (!File.Exists(ScenePath))
            CreateGameScene.Create();

        string folder = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        PlayerSettings.productName = "Dark Together";
        PlayerSettings.companyName = "Dark Together Community";
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = platform,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Dark Together build failed: " + report.summary.result +
                ". See Unity Console and install the required platform module.");

        Debug.Log("Dark Together build generated at: " + output);
    }
}
#endif
