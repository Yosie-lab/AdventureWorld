using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebGLBuilder
{
    [MenuItem("Build/Build WebGL for GitHub Pages")]
    public static void Build()
    {
        string projectDir = Directory.GetCurrentDirectory();
        string buildPath = Path.Combine(projectDir, "Build_WebGL");

        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        // Brotli圧縮で100MB以下に確実に圧縮
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        EditorUserBuildSettings.development = false;

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/AdventureWorld.unity" },
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        Debug.Log($"[WebGLBuilder] Brotli圧縮ビルドを開始します... 出力先: {buildPath}");
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[WebGLBuilder] ビルド成功! 出力先: {buildPath} (所要時間: {summary.totalTime.TotalMinutes:F1}分)");
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"[WebGLBuilder] ビルド失敗: エラー数 {summary.totalErrors}");
        }
        else if (summary.result == BuildResult.Cancelled)
        {
            Debug.LogWarning("[WebGLBuilder] ビルドがキャンセルされました");
        }
    }
}
