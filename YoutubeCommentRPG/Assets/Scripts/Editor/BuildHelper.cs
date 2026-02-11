using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildHelper
{
    [MenuItem("Tools/Build Windows64")]
    public static void BuildWindows64()
    {
        string[] scenes = { "Assets/Scenes/KingsMarch.unity" };
        string buildPath = "D:/Git/YoutubeCommentRPG/Build/StreamerKing.exe";
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("[BuildHelper] Build succeeded: " + buildPath);
            CopyTikTokProxy(buildPath);
        }
        else
            Debug.LogError("[BuildHelper] Build failed: " + report.summary.result);
    }

    static void CopyTikTokProxy(string buildExePath)
    {
        string buildDir = System.IO.Path.GetDirectoryName(buildExePath);
        string proxySource = "D:/Git/YoutubeCommentRPG/tiktok-proxy/dist/tiktok-proxy.exe";
        string proxyDest = System.IO.Path.Combine(buildDir, "tiktok-proxy.exe");

        if (System.IO.File.Exists(proxySource))
        {
            System.IO.File.Copy(proxySource, proxyDest, true);
            Debug.Log($"[BuildHelper] tiktok-proxy.exe copied to {proxyDest}");
        }
        else
        {
            Debug.LogWarning($"[BuildHelper] tiktok-proxy.exe not found at {proxySource} - TikTok mode will not work");
        }
    }
}
