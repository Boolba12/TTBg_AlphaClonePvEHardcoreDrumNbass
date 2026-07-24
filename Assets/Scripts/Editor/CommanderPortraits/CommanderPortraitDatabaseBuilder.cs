using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CommanderPortraitDatabaseBuilder
{
    public const string PortraitRoot = "Assets/Art/CommanderPortraits";
    public const string DatabasePath = PortraitRoot + "/CommanderPortraitDatabase.asset";

    private static readonly Dictionary<string, CommanderRace> FolderRaces =
        new Dictionary<string, CommanderRace>(StringComparer.OrdinalIgnoreCase)
        {
            { PortraitRoot + "/Humans/", CommanderRace.Human },
            { PortraitRoot + "/Elves/", CommanderRace.Elf },
            { PortraitRoot + "/Dwarves/", CommanderRace.Dwarf },
            { PortraitRoot + "/Orcs/", CommanderRace.Orc },
            { PortraitRoot + "/Tieflings/", CommanderRace.Tiefling }
        };

    public static bool IsRebuilding { get; private set; }

    [MenuItem("Tools/Commander Portraits/Rebuild Database")]
    public static void RebuildDatabase()
    {
        if (IsRebuilding)
            return;

        IsRebuilding = true;
        try
        {
            EnsureFolders();
            CommanderPortraitDatabase database =
                AssetDatabase.LoadAssetAtPath<CommanderPortraitDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<CommanderPortraitDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            List<CommanderPortraitEntry> entries = ScanEntries();
            database.ReplaceEntries(entries);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"Commander portraits: database rebuilt with {entries.Count} portrait(s).");
        }
        finally
        {
            IsRebuilding = false;
        }
    }

    public static bool TryGetRaceFromAssetPath(string assetPath, out CommanderRace race)
    {
        string normalized = (assetPath ?? string.Empty).Replace('\\', '/');
        foreach (KeyValuePair<string, CommanderRace> pair in FolderRaces)
        {
            if (normalized.StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                race = pair.Value;
                return true;
            }
        }

        race = default;
        return false;
    }

    public static bool IsPortraitImagePath(string assetPath)
    {
        if (!TryGetRaceFromAssetPath(assetPath, out _))
            return false;

        string extension = Path.GetExtension(assetPath);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tga", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".psd", StringComparison.OrdinalIgnoreCase);
    }

    private static List<CommanderPortraitEntry> ScanEntries()
    {
        List<CommanderPortraitEntry> entries = new List<CommanderPortraitEntry>();
        HashSet<string> ids = new HashSet<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsPortraitImagePath(path) || !TryGetRaceFromAssetPath(path, out CommanderRace race))
                continue;

            EnsureSpriteImport(path);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"Commander portraits: '{path}' could not be loaded as a Sprite.");
                continue;
            }

            if (!ids.Add(guid))
            {
                Debug.LogWarning($"Commander portraits: duplicate asset GUID '{guid}' at '{path}' was ignored.");
                continue;
            }

            entries.Add(new CommanderPortraitEntry(guid, sprite, race, Path.GetFileNameWithoutExtension(path)));
        }

        entries.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        return entries;
    }

    private static void EnsureSpriteImport(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer ||
            importer.textureType == TextureImporterType.Sprite)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
        Debug.Log($"Commander portraits: changed '{path}' Texture Type to Sprite.");
    }

    private static void EnsureFolders()
    {
        string[] parts = PortraitRoot.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }

        foreach (string folderWithSlash in FolderRaces.Keys)
        {
            string folder = folderWithSlash.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(PortraitRoot, Path.GetFileName(folder));
        }
    }
}
