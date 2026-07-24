using System;
using UnityEditor;

public sealed class CommanderPortraitAssetPostprocessor : AssetPostprocessor
{
    private static bool rebuildScheduled;

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (CommanderPortraitDatabaseBuilder.IsRebuilding || rebuildScheduled)
            return;

        if (!ContainsPortraitImage(importedAssets) &&
            !ContainsPortraitImage(deletedAssets) &&
            !ContainsPortraitImage(movedAssets) &&
            !ContainsPortraitImage(movedFromAssetPaths))
        {
            return;
        }

        rebuildScheduled = true;
        EditorApplication.delayCall += RebuildDelayed;
    }

    private static void RebuildDelayed()
    {
        rebuildScheduled = false;
        CommanderPortraitDatabaseBuilder.RebuildDatabase();
    }

    private static bool ContainsPortraitImage(string[] paths)
    {
        if (paths == null)
            return false;

        foreach (string path in paths)
        {
            if (CommanderPortraitDatabaseBuilder.IsPortraitImagePath(path))
                return true;
        }
        return false;
    }
}
