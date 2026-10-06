using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// A player has no UAS project folder, so the playable Ai and the ticker settings are copied into
/// StreamingAssets/UAS/ for the duration of a build and removed again afterwards. The runtime reads
/// them from there (see Consts.CurrentProjectPath).
/// </summary>
internal class UasBuildFiles : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string CopiedKey = "UAS.BuildFiles.Copied";
    private const string DestinationRoot = "Assets/StreamingAssets/UAS";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var copied = new System.Text.StringBuilder();
        Copy(Consts.FilePath_PlayAbleAiWithExtention, "PlAi", copied);
        Copy(Consts.FilePath_TickerSettingsWithExtention, "Settings", copied);
        SessionState.SetString(CopiedKey, copied.ToString());
        if (copied.Length == 0)
        {
            Debug.LogWarning("[UAS] No playable Ai file found to include in the build. Mark an Ai as playable in the Template Manager if the build uses UAS.");
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        var copied = SessionState.GetString(CopiedKey, "");
        SessionState.EraseString(CopiedKey);
        foreach (var assetPath in copied.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
        RemoveIfEmpty(DestinationRoot + "/PlAi");
        RemoveIfEmpty(DestinationRoot + "/Settings");
        RemoveIfEmpty(DestinationRoot);
        RemoveIfEmpty("Assets/StreamingAssets");
    }

    private static void Copy(string source, string subFolder, System.Text.StringBuilder copied)
    {
        if (string.IsNullOrEmpty(source) || !File.Exists(source)) return;
        var folder = DestinationRoot + "/" + subFolder;
        Directory.CreateDirectory(folder);
        var destination = folder + "/" + Path.GetFileName(source);
        File.Copy(source, destination, true);
        AssetDatabase.ImportAsset(destination);
        copied.Append(destination).Append('\n');
    }

    private static void RemoveIfEmpty(string assetFolder)
    {
        if (!AssetDatabase.IsValidFolder(assetFolder)) return;
        if (Directory.GetFileSystemEntries(assetFolder).Length > 0 &&
            !OnlyMetaLeft(assetFolder)) return;
        AssetDatabase.DeleteAsset(assetFolder);
    }

    private static bool OnlyMetaLeft(string folder)
    {
        foreach (var entry in Directory.GetFileSystemEntries(folder))
        {
            if (!entry.EndsWith(".meta")) return false;
        }
        return true;
    }
}
