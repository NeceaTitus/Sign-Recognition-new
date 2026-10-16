using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;

public class ClaudeCodeLauncher
{
    [MenuItem("Tools/Claude Code/Start")]
    public static void StartClaudeCode()
    {
        string batPath = Path.Combine(Application.dataPath, "../start_claude_code.bat");

        if (!File.Exists(batPath))
        {
            UnityEngine.Debug.LogError("Nu am gasit start_claude_code.bat la: " + batPath);
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = batPath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(batPath)
        };

        Process.Start(startInfo);
        UnityEngine.Debug.Log("Claude Code a fost pornit.");
    }
}