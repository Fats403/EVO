using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Post-build processor that ensures Steam-required files are in the correct location.
/// This is particularly important for macOS where the Steam overlay requires steam_appid.txt
/// to be placed alongside the executable.
/// </summary>
public class SteamBuildPostProcessor : IPostprocessBuildWithReport
{
    // Run after most other post-processors
    public int callbackOrder => 100;

    public void OnPostprocessBuild(BuildReport report)
    {
        string outputPath = report.summary.outputPath;
        BuildTarget target = report.summary.platform;

        Debug.Log($"SteamBuildPostProcessor: Processing build at {outputPath}");

        // Handle macOS builds
        if (target == BuildTarget.StandaloneOSX)
        {
            CopySteamAppIdForMac(outputPath);
        }
        // Handle Windows builds
        else if (
            target == BuildTarget.StandaloneWindows
            || target == BuildTarget.StandaloneWindows64
        )
        {
            CopySteamAppIdForWindows(outputPath);
        }
        // Handle Linux builds
        else if (target == BuildTarget.StandaloneLinux64)
        {
            CopySteamAppIdForLinux(outputPath);
        }
    }

    private void CopySteamAppIdForMac(string appPath)
    {
        // For macOS, outputPath is the .app bundle
        // steam_appid.txt should go next to the .app bundle AND inside Contents/MacOS/

        string steamAppIdSource = Path.Combine(Application.streamingAssetsPath, "steam_appid.txt");

        if (!File.Exists(steamAppIdSource))
        {
            Debug.LogWarning(
                $"SteamBuildPostProcessor: steam_appid.txt not found at {steamAppIdSource}"
            );
            return;
        }

        // Copy to the same directory as the .app bundle
        string appDirectory = Path.GetDirectoryName(appPath);
        string destOutside = Path.Combine(appDirectory, "steam_appid.txt");

        try
        {
            File.Copy(steamAppIdSource, destOutside, overwrite: true);
            Debug.Log($"SteamBuildPostProcessor: Copied steam_appid.txt to {destOutside}");
        }
        catch (IOException e)
        {
            Debug.LogError($"SteamBuildPostProcessor: Failed to copy steam_appid.txt: {e.Message}");
        }

        // Also copy inside the app bundle's MacOS directory for good measure
        string macosDir = Path.Combine(appPath, "Contents", "MacOS");
        if (Directory.Exists(macosDir))
        {
            string destInside = Path.Combine(macosDir, "steam_appid.txt");
            try
            {
                File.Copy(steamAppIdSource, destInside, overwrite: true);
                Debug.Log($"SteamBuildPostProcessor: Copied steam_appid.txt to {destInside}");
            }
            catch (IOException e)
            {
                Debug.LogError(
                    $"SteamBuildPostProcessor: Failed to copy steam_appid.txt inside bundle: {e.Message}"
                );
            }
        }
    }

    private void CopySteamAppIdForWindows(string exePath)
    {
        // For Windows, outputPath is the .exe file
        // steam_appid.txt should go in the same directory as the exe

        string steamAppIdSource = Path.Combine(Application.streamingAssetsPath, "steam_appid.txt");

        if (!File.Exists(steamAppIdSource))
        {
            Debug.LogWarning(
                $"SteamBuildPostProcessor: steam_appid.txt not found at {steamAppIdSource}"
            );
            return;
        }

        string exeDirectory = Path.GetDirectoryName(exePath);
        string dest = Path.Combine(exeDirectory, "steam_appid.txt");

        try
        {
            File.Copy(steamAppIdSource, dest, overwrite: true);
            Debug.Log($"SteamBuildPostProcessor: Copied steam_appid.txt to {dest}");
        }
        catch (IOException e)
        {
            Debug.LogError($"SteamBuildPostProcessor: Failed to copy steam_appid.txt: {e.Message}");
        }
    }

    private void CopySteamAppIdForLinux(string exePath)
    {
        // For Linux, similar to Windows - put it next to the executable
        CopySteamAppIdForWindows(exePath); // Same logic applies
    }
}
