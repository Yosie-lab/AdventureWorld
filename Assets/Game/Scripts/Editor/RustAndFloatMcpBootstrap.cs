using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using Microsoft.Unity.VisualStudio.Editor;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GUI 起動の Unity は ~/.local/bin を見ない。uvx の場所を渡し、MCP を起動して C# ソリューションを作る。
/// </summary>
[InitializeOnLoad]
static class RustAndFloatMcpBootstrap
{
    const string UvxPath = "/Users/user/.local/bin/uvx";
    static bool _kickStarted;

    static RustAndFloatMcpBootstrap()
    {
        if (File.Exists(UvxPath))
            EditorPrefs.SetString("MCPForUnity.UvxPath", UvxPath);

        EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
        AssemblyReloadEvents.afterAssemblyReload += OnReload;
        EditorApplication.update += KickOnce;
    }

    static void OnReload()
    {
        AssemblyReloadEvents.afterAssemblyReload -= OnReload;
        KickOnce();
    }

    static void KickOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (_kickStarted)
        {
            EditorApplication.update -= KickOnce;
            return;
        }

        _kickStarted = true;
        EditorApplication.update -= KickOnce;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string gameProject = Path.Combine(projectRoot, "Assembly-CSharp.csproj");
        if (!File.Exists(gameProject))
        {
            try
            {
                // Unity の検出は名前に Code を含む .app だけ。Cursor.app はそのままではソリューションを書けない。
                string alias = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                    "Applications",
                    "Cursor Code.app");
                if (Directory.Exists(alias))
                    CodeEditor.SetExternalScriptEditor(alias);

                var sdkType = typeof(ProjectGeneration).Assembly.GetType(
                    "Microsoft.Unity.VisualStudio.Editor.SdkStyleProjectGeneration");
                var gen = (ProjectGeneration)Activator.CreateInstance(sdkType, nonPublic: true);
                typeof(ProjectGeneration).GetMethod(
                    "RefreshCurrentInstallation",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(gen, null);
                gen.GenerateAndWriteSolutionAndProjects();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RustAndFloat] solution sync failed: {(e.InnerException ?? e).Message}");
            }
        }

        PruneMissingProjects(projectRoot);

        var server = MCPServiceLocator.Server;
        if (!server.IsLocalHttpServerReachable())
            server.StartLocalHttpServer(quiet: true);

        _ = ConnectWhenReady();
    }

    static void PruneMissingProjects(string projectRoot)
    {
        string slnPath = Path.Combine(projectRoot, "RustAndFloat.sln");
        if (!File.Exists(slnPath))
            return;

        string[] lines = File.ReadAllLines(slnPath);
        var keep = new List<string>(lines.Length);
        var drop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("Project(", StringComparison.Ordinal))
            {
                if (!LineDrops(lines[i], drop))
                    keep.Add(lines[i]);
                continue;
            }

            int end = i;
            while (end < lines.Length && lines[end] != "EndProject")
                end++;

            string csproj = Quoted(lines[i], 3);
            string guid = Quoted(lines[i], 4);
            bool exists = !string.IsNullOrEmpty(csproj)
                && File.Exists(Path.Combine(projectRoot, csproj));
            if (!exists)
            {
                if (!string.IsNullOrEmpty(guid))
                    drop.Add(guid);
            }
            else
            {
                for (int j = i; j <= end && j < lines.Length; j++)
                    keep.Add(lines[j]);
            }

            i = end;
        }

        File.WriteAllLines(slnPath, keep);
    }

    static bool LineDrops(string line, HashSet<string> drop)
    {
        foreach (string guid in drop)
        {
            if (line.IndexOf(guid, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static string Quoted(string line, int nth)
    {
        int seen = 0;
        int start = -1;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] != '"')
                continue;
            if (start < 0)
            {
                start = i + 1;
                continue;
            }

            seen++;
            if (seen == nth)
                return line.Substring(start, i - start);
            start = -1;
        }

        return null;
    }

    static async Task ConnectWhenReady()
    {
        var server = MCPServiceLocator.Server;
        for (int i = 0; i < 120; i++)
        {
            if (server.IsLocalHttpServerReachable())
            {
                if (!MCPServiceLocator.Bridge.IsRunning)
                    await MCPServiceLocator.Bridge.StartAsync();
                return;
            }

            await Task.Delay(500);
        }
    }
}
