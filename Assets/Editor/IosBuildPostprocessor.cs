using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class IosBuildPostprocessor
{
    private const string MarketingVersion = "1.11";
    private const string BuildNumber = "18";

    [PostProcessBuild(999)]
    private static void ConfigureIosVersion(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
            return;

        string infoPlistPath = Path.Combine(buildPath, "Info.plist");
        PlistDocument infoPlist = new PlistDocument();
        infoPlist.ReadFromFile(infoPlistPath);
        infoPlist.root.SetString("CFBundleShortVersionString", MarketingVersion);
        infoPlist.root.SetString("CFBundleVersion", BuildNumber);
        infoPlist.WriteToFile(infoPlistPath);

        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);
        string mainTargetGuid = project.GetUnityMainTargetGuid();
        project.SetBuildProperty(mainTargetGuid, "MARKETING_VERSION", MarketingVersion);
        project.SetBuildProperty(mainTargetGuid, "CURRENT_PROJECT_VERSION", BuildNumber);
        project.WriteToFile(projectPath);

        Debug.Log($"iOS version enforced: {MarketingVersion} ({BuildNumber}) in {buildPath}");
    }
}
