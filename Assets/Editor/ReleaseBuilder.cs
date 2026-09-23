#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class ReleaseBuilder
{
    private const string MarketingVersion = "1.15";
    private const string BuildNumber = "24";
    private const string ReleaseFolder = "Builds/Release-1.15-24";

    public static void BuildLinuxServer()
    {
        ValidateReleaseVersion();
        string output = ProjectPath("EdgegapServer/ServerBuild.x86_64");
        Build(output, BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server);
    }

    public static void BuildIos()
    {
        ValidateReleaseVersion();
        string output = ProjectPath("iOSClient");
        Build(output, BuildTarget.iOS, StandaloneBuildSubtarget.Player);
    }

    private static void Build(string output, BuildTarget target, StandaloneBuildSubtarget subtarget)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? output);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray(),
            locationPathName = output,
            target = target,
            subtarget = (int)subtarget,
            options = BuildOptions.CleanBuildCache
        });

        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException(
                $"Release build failed: target={target}, result={report.summary.result}, " +
                $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}");

        UnityEngine.Debug.Log(
            $"Release build succeeded: target={target}, output={output}, " +
            $"size={report.summary.totalSize}, warnings={report.summary.totalWarnings}");
        EditorApplication.Exit(0);
    }

    private static string ProjectPath(string childPath)
    {
        string root = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
        return Path.Combine(root, ReleaseFolder, childPath);
    }

    private static void ValidateReleaseVersion()
    {
        string configuredBuild = PlayerSettings.iOS.buildNumber;
        if (PlayerSettings.bundleVersion != MarketingVersion || configuredBuild != BuildNumber)
            throw new InvalidOperationException(
                $"Release version mismatch. Expected {MarketingVersion} ({BuildNumber}), " +
                $"found {PlayerSettings.bundleVersion} ({configuredBuild}).");
    }
}
#endif
