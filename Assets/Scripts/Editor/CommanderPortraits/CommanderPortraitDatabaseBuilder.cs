using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CommanderPortraitDatabaseBuilder
{
    public const string PortraitRoot = "Assets/Art/CommanderPortraits";
    public const string DatabasePath = PortraitRoot + "/CommanderPortraitDatabase.asset";
    public const string ImportedHumanRoot =
        "Assets/Scripts/CommanderPortraits/CommanderPortraitHuman";
    public const string ImportedElfRoot =
        "Assets/Scripts/CommanderPortraits/CommanderPortraitElf";

    private static readonly Dictionary<string, CommanderRace> CanonicalFolderRaces =
        new Dictionary<string, CommanderRace>(StringComparer.OrdinalIgnoreCase)
        {
            { PortraitRoot + "/Humans/", CommanderRace.Human },
            { PortraitRoot + "/Elves/", CommanderRace.Elf },
            { PortraitRoot + "/Dwarves/", CommanderRace.Dwarf },
            { PortraitRoot + "/Orcs/", CommanderRace.Orc },
            { PortraitRoot + "/Tieflings/", CommanderRace.Tiefling }
        };

    private static readonly Dictionary<string, CommanderRace> FolderRaces =
        new Dictionary<string, CommanderRace>(CanonicalFolderRaces, StringComparer.OrdinalIgnoreCase)
        {
            { ImportedHumanRoot + "/", CommanderRace.Human },
            { ImportedElfRoot + "/", CommanderRace.Elf }
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

        string[] searchRoots = GetExistingSearchRoots();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", searchRoots))
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
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;

        bool changed = false;
        changed |= SetIfDifferent(importer.textureType, TextureImporterType.Sprite,
            value => importer.textureType = value);
        changed |= SetIfDifferent(importer.spriteImportMode, SpriteImportMode.Single,
            value => importer.spriteImportMode = value);
        changed |= SetIfDifferent(importer.mipmapEnabled, false,
            value => importer.mipmapEnabled = value);
        changed |= SetIfDifferent(importer.sRGBTexture, true,
            value => importer.sRGBTexture = value);
        changed |= SetIfDifferent(importer.alphaIsTransparency, true,
            value => importer.alphaIsTransparency = value);
        changed |= SetIfDifferent(importer.npotScale, TextureImporterNPOTScale.None,
            value => importer.npotScale = value);
        changed |= SetIfDifferent(importer.wrapMode, TextureWrapMode.Clamp,
            value => importer.wrapMode = value);
        changed |= SetIfDifferent(importer.filterMode, FilterMode.Bilinear,
            value => importer.filterMode = value);
        changed |= SetIfDifferent(importer.maxTextureSize, 1024,
            value => importer.maxTextureSize = value);
        changed |= SetIfDifferent(
            importer.textureCompression,
            TextureImporterCompression.CompressedHQ,
            value => importer.textureCompression = value);

        if (!changed)
            return;

        importer.SaveAndReimport();
        Debug.Log($"Commander portraits: normalized Sprite import settings for '{path}'.");
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

        foreach (string folderWithSlash in CanonicalFolderRaces.Keys)
        {
            string folder = folderWithSlash.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(PortraitRoot, Path.GetFileName(folder));
        }
    }

    private static string[] GetExistingSearchRoots()
    {
        List<string> roots = new List<string>();
        foreach (string folderWithSlash in FolderRaces.Keys)
        {
            string folder = folderWithSlash.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
                roots.Add(folder);
        }
        return roots.ToArray();
    }

    private static bool SetIfDifferent<T>(T current, T expected, Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(current, expected))
            return false;
        setter(expected);
        return true;
    }
}
