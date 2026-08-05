#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleUIInstaller
{
    private const string ThemePath = "Assets/UI/Themes/PurgatoryUITheme.asset";
    private const string SpriteLibraryPath =
        "Assets/UI/Art/DEV/DevelopmentUISprites.asset";
    private const string PortraitDatabasePath =
        "Assets/Art/CommanderPortraits/CommanderPortraitDatabase.asset";
    private const string BattleHudPrefabPath =
        "Assets/UI/Prefabs/Battle/BattleHUD.prefab";
    private const string InitiativeEntryPrefabPath =
        "Assets/UI/Prefabs/Components/InitiativeEntry.prefab";
    private const string ItemPreviewCardPrefabPath =
        "Assets/UI/Prefabs/Components/ItemPreviewCard.prefab";
    private const string ItemPresentationFolder = "Assets/UI/Presentation";
    private const string ItemPresentationCatalogPath =
        ItemPresentationFolder + "/DevelopmentItemPresentationCatalog.asset";
    private const string BattleScenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
    private const string DestructiveRebuildFlag = "-purgatoryUiConfirmDestructiveRebuild";

    private static readonly List<(TooltipAnchor Anchor, TooltipContent Content)> TooltipAnchors =
        new List<(TooltipAnchor, TooltipContent)>();

    [MenuItem("Tools/Purgatory UI/Validate Existing Battle HUD")]
    public static void ValidateExistingFoundation()
    {
        PurgatoryUITheme theme = AssetDatabase.LoadAssetAtPath<PurgatoryUITheme>(ThemePath);
        DevelopmentUISpriteLibrary sprites =
            AssetDatabase.LoadAssetAtPath<DevelopmentUISpriteLibrary>(SpriteLibraryPath);
        GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleHudPrefabPath);
        if (theme == null || sprites == null || hudPrefab == null)
            throw new InvalidOperationException("Battle HUD theme, DEV sprites, or prefab is missing.");
        if (sprites.MonolithOuterFrame == null || sprites.ButtonDisabled == null ||
            sprites.InitiativeCard == null || sprites.IconPlaceholder == null)
        {
            throw new InvalidOperationException("DevelopmentUISprites is not a complete visual-pass library.");
        }
        if (hudPrefab.GetComponent<BattleHUDController>() == null ||
            hudPrefab.GetComponentInChildren<BattleSquadStatusPresenter>(true) == null ||
            hudPrefab.GetComponentInChildren<InitiativeQueuePresenter>(true) == null)
        {
            throw new InvalidOperationException("BattleHUD prefab is missing required production presenters.");
        }
        Debug.Log("BattleUIInstaller: existing Battle HUD assets validated without modifying files.");
    }

    [MenuItem("Tools/Purgatory UI/Wire Existing Battle HUD Into Raw Scene")]
    public static void WireExistingBattleHud()
    {
        bool allowReplacement = EditorUtility.DisplayDialog(
            "Wire existing Battle HUD",
            "This only wires the existing BattleHUD.prefab. If Raw contains a different HUD root, " +
            "it may be replaced. No DEV visual asset or prefab will be rebuilt.",
            "Continue",
            "Cancel");
        if (!allowReplacement)
            return;
        WireExistingBattleHudInternal(true);
    }

    [MenuItem("Tools/Purgatory UI/Rebuild DEV Visual Assets And Battle HUD (Destructive)...")]
    public static void RebuildVisualAssetsWithConfirmation()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Destructive Battle HUD rebuild",
            "This overwrites DevelopmentUISprites.asset, component prefabs, and BattleHUD.prefab. " +
            "The Raw scene is not rebuilt. Continue only when those prefab changes are intended.",
            "Rebuild",
            "Cancel");
        if (!confirmed)
            return;
        RebuildVisualAssets("explicit Editor confirmation");
    }

    public static void InstallForAutomation()
    {
        ApplySecondStageForAutomation();
    }

    public static void ApplySecondStageForAutomation()
    {
        RequireCommandLineDestructiveConfirmation();
        CommanderPortraitDatabaseBuilder.RebuildDatabase();
        RebuildVisualAssets("explicit command-line confirmation flag");
        ConfigureDevelopmentPortraitsInBattleScene();
        WireExistingBattleHudInternal(false);
        ValidateExistingFoundation();
    }

    private static void RebuildVisualAssets(string confirmationSource)
    {
        try
        {
            TooltipAnchors.Clear();
            DevelopmentUISpriteLibrary sprites = GetOrCreateDevelopmentSprites(true);
            PurgatoryUITheme theme = GetOrCreateTheme(sprites);
            ConfigureExistingPortraitFallback(sprites.PortraitFallback);
            BuildReusablePrefabs(theme);
            BuildBattleHudPrefab(theme);
            BuildDevelopmentItemPresentationCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"BattleUIInstaller: destructive visual rebuild completed after {confirmationSource}. " +
                "Raw scene wiring was not replaced by the rebuild.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static void RequireCommandLineDestructiveConfirmation()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), DestructiveRebuildFlag) < 0)
        {
            throw new InvalidOperationException(
                $"Destructive UI automation refused. Pass {DestructiveRebuildFlag} explicitly.");
        }
    }

    private static DevelopmentUISpriteLibrary GetOrCreateDevelopmentSprites(bool forceRebuild)
    {
        DevelopmentUISpriteLibrary existing =
            AssetDatabase.LoadAssetAtPath<DevelopmentUISpriteLibrary>(SpriteLibraryPath);
        if (!forceRebuild && existing != null && existing.Panel != null && existing.PortraitFallback != null)
            return existing;

        if (existing != null)
            AssetDatabase.DeleteAsset(SpriteLibraryPath);

        DevelopmentUISpriteLibrary library =
            ScriptableObject.CreateInstance<DevelopmentUISpriteLibrary>();
        AssetDatabase.CreateAsset(library, SpriteLibraryPath);

        Color outer = new Color32(12, 15, 16, 255);
        Color inset = new Color32(24, 29, 30, 255);
        Color raised = new Color32(43, 49, 50, 255);
        Color bronze = new Color32(132, 89, 44, 255);
        Color gold = new Color32(194, 147, 68, 255);
        Color emerald = new Color32(37, 132, 91, 255);
        Color disabled = new Color32(59, 63, 62, 255);

        Sprite monolithOuterFrame = AddMonolithSprite(
            library, "DEV_MonolithOuterFrame", outer, inset, bronze, gold, 7, true);
        Sprite insetPanel = AddMonolithSprite(
            library, "DEV_InsetPanel", inset, outer, new Color32(72, 75, 70, 255), bronze, 6, false);
        Sprite sectionHeader = AddMonolithSprite(
            library, "DEV_SectionHeader", raised, inset, bronze, gold, 5, true);
        Sprite separator = AddBorderedSprite(
            library, "DEV_BronzeSeparator", bronze, gold, 2);
        Sprite selectedFrame = AddMonolithSprite(
            library, "DEV_SelectedFrame", new Color32(19, 36, 31, 255), inset, emerald,
            new Color32(80, 183, 132, 255), 6, true);
        Sprite buttonNormal = AddMonolithSprite(
            library, "DEV_ButtonNormal", raised, inset, bronze, new Color32(163, 116, 57, 255), 5, true);
        Sprite buttonHover = AddMonolithSprite(
            library, "DEV_ButtonHover", new Color32(53, 61, 61, 255), inset, gold,
            new Color32(218, 174, 87, 255), 5, true);
        Sprite buttonPressed = AddMonolithSprite(
            library, "DEV_ButtonPressed", new Color32(31, 37, 38, 255), outer, bronze, gold, 7, true);
        Sprite buttonDisabled = AddMonolithSprite(
            library, "DEV_ButtonDisabled", disabled, outer, new Color32(82, 84, 80, 255),
            new Color32(102, 102, 95, 255), 5, false);
        Sprite portraitFrame = AddMonolithSprite(
            library, "DEV_PortraitFrame", inset, outer, bronze, gold, 7, true);
        Sprite initiativeCard = AddMonolithSprite(
            library, "DEV_InitiativeCard", inset, outer, new Color32(87, 75, 55, 255), bronze, 5, true);
        Sprite equipmentSlot = AddMonolithSprite(
            library, "DEV_EquipmentSlot", inset, outer, bronze, gold, 6, true);
        Sprite emptySlot = AddMonolithSprite(
            library, "DEV_EmptySlot", outer, inset, new Color32(65, 68, 64, 255), bronze, 6, false);
        Sprite iconPlaceholder = AddIconPlaceholderSprite(library);
        Sprite portrait = AddPortraitSprite(library);
        library.Configure(
            monolithOuterFrame,
            insetPanel,
            sectionHeader,
            separator,
            selectedFrame,
            buttonNormal,
            buttonHover,
            buttonPressed,
            buttonDisabled,
            portraitFrame,
            initiativeCard,
            equipmentSlot,
            emptySlot,
            iconPlaceholder,
            portrait);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        return library;
    }

    private static Sprite AddBorderedSprite(
        UnityEngine.Object owner,
        string name,
        Color fill,
        Color border,
        int borderPixels)
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name + "_Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x < borderPixels || x >= size - borderPixels ||
                            y < borderPixels || y >= size - borderPixels;
                pixels[y * size + x] = edge ? border : fill;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.AddObjectToAsset(texture, owner);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(borderPixels, borderPixels, borderPixels, borderPixels));
        sprite.name = name;
        AssetDatabase.AddObjectToAsset(sprite, owner);
        return sprite;
    }

    private static Sprite AddPortraitSprite(UnityEngine.Object owner)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "DEV_CommanderFallback_Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color background = new Color32(31, 36, 37, 255);
        Color silhouette = new Color32(133, 88, 42, 255);
        Color edge = new Color32(207, 164, 70, 255);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - 31.5f;
                float dy = y - 42f;
                bool head = dx * dx + dy * dy <= 12f * 12f;
                bool shoulders = y < 31 && y > 6 && Mathf.Abs(dx) < 24f - (y - 6) * 0.25f;
                bool border = x < 3 || x >= size - 3 || y < 3 || y >= size - 3;
                pixels[y * size + x] = border ? edge : (head || shoulders ? silhouette : background);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.AddObjectToAsset(texture, owner);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(3, 3, 3, 3));
        sprite.name = "DEV_CommanderFallback";
        AssetDatabase.AddObjectToAsset(sprite, owner);
        return sprite;
    }

    private static Sprite AddMonolithSprite(
        UnityEngine.Object owner,
        string name,
        Color fill,
        Color inset,
        Color border,
        Color accent,
        int borderPixels,
        bool cutCorners)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name + "_Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int edgeDistance = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                bool cornerCut = cutCorners &&
                                 ((x + y < borderPixels + 2) ||
                                  ((size - 1 - x) + y < borderPixels + 2) ||
                                  (x + (size - 1 - y) < borderPixels + 2) ||
                                  ((size - 1 - x) + (size - 1 - y) < borderPixels + 2));
                Color pixel;
                if (cornerCut)
                    pixel = new Color(0f, 0f, 0f, 0f);
                else if (edgeDistance < 2)
                    pixel = accent;
                else if (edgeDistance < borderPixels)
                    pixel = border;
                else if (edgeDistance < borderPixels + 2)
                    pixel = inset;
                else
                {
                    bool wear = ((x * 17 + y * 31 + x * y) % 47) == 0;
                    pixel = wear ? Color.Lerp(fill, accent, 0.12f) : fill;
                }
                pixels[y * size + x] = pixel;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.AddObjectToAsset(texture, owner);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(borderPixels + 2, borderPixels + 2, borderPixels + 2, borderPixels + 2));
        sprite.name = name;
        AssetDatabase.AddObjectToAsset(sprite, owner);
        return sprite;
    }

    private static Sprite AddIconPlaceholderSprite(UnityEngine.Object owner)
    {
        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "DEV_IconPlaceholder_Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color bronze = new Color32(145, 98, 49, 255);
        Color stone = new Color32(191, 186, 168, 255);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - 23.5f);
                float dy = Mathf.Abs(y - 23.5f);
                bool diamond = Mathf.Abs(dx + dy - 16f) < 1.7f;
                bool cross = (dx < 2f && dy < 9f) || (dy < 2f && dx < 9f);
                pixels[y * size + x] = cross ? stone : diamond ? bronze : clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.AddObjectToAsset(texture, owner);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        sprite.name = "DEV_IconPlaceholder";
        AssetDatabase.AddObjectToAsset(sprite, owner);
        return sprite;
    }

    private static PurgatoryUITheme GetOrCreateTheme(DevelopmentUISpriteLibrary sprites)
    {
        PurgatoryUITheme theme = AssetDatabase.LoadAssetAtPath<PurgatoryUITheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<PurgatoryUITheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        theme.ConfigureVisualPassDefaults(font, sprites);
        EditorUtility.SetDirty(theme);
        return theme;
    }

    private static void ConfigureExistingPortraitFallback(Sprite fallback)
    {
        CommanderPortraitDatabase database =
            AssetDatabase.LoadAssetAtPath<CommanderPortraitDatabase>(PortraitDatabasePath);
        if (database == null)
            throw new InvalidOperationException("Existing CommanderPortraitDatabase asset is missing.");

        SerializedObject serializedDatabase = new SerializedObject(database);
        serializedDatabase.FindProperty("fallbackPortrait").objectReferenceValue = fallback;
        serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(database);
    }

    private static void BuildDevelopmentItemPresentationCatalog()
    {
        EnsureAssetFolder(ItemPresentationFolder);
        ItemPresentationCatalog catalog =
            AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(ItemPresentationCatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ItemPresentationCatalog>();
            AssetDatabase.CreateAsset(catalog, ItemPresentationCatalogPath);
        }

        BattleWeaponDefinition sourceWeapon =
            AssetDatabase.LoadAssetAtPath<BattleWeaponDefinition>(
                "Assets/Prefabs/ScriptableObjects/BattleWeaponDefinition.asset");
        ItemPresentationRecord legacyTestWeapon = new ItemPresentationRecord();
        legacyTestWeapon.ConfigureDevelopment(
            "dev-legacy-test-weapon",
            "Legacy Test Weapon",
            null,
            null,
            ItemPresentationCategory.UnknownTest,
            "Existing Unity primitive weapon reference. No matching Blender item model or " +
            "verified preview image is present in the project, so the gallery shows a controlled placeholder.",
            true,
            sourceWeapon);
        catalog.ReplaceDevelopmentEntries(new List<ItemPresentationRecord> { legacyTestWeapon });
        EditorUtility.SetDirty(catalog);
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        string[] parts = assetFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void BuildReusablePrefabs(PurgatoryUITheme theme)
    {
        SaveTemporaryPrefab(BuildButton(theme, "PrimaryButton", ThemedButtonStyle.Primary),
            "Assets/UI/Prefabs/Components/PrimaryButton.prefab");
        SaveTemporaryPrefab(BuildButton(theme, "SecondaryButton", ThemedButtonStyle.Secondary),
            "Assets/UI/Prefabs/Components/SecondaryButton.prefab");
        SaveTemporaryPrefab(BuildButton(theme, "IconButton", ThemedButtonStyle.Icon),
            "Assets/UI/Prefabs/Components/IconButton.prefab");
        SaveTemporaryPrefab(BuildActionControlPrefab(theme),
            "Assets/UI/Prefabs/Components/BattleActionControl.prefab");
        SaveTemporaryPrefab(BuildPanelPrefab(theme),
            "Assets/UI/Prefabs/Components/PanelFrame.prefab");
        SaveTemporaryPrefab(BuildSectionHeaderPrefab(theme),
            "Assets/UI/Prefabs/Components/SectionHeader.prefab");
        SaveTemporaryPrefab(BuildStatRowPrefab(theme),
            "Assets/UI/Prefabs/Components/StatRow.prefab");
        SaveTemporaryPrefab(BuildProgressBarPrefab(theme),
            "Assets/UI/Prefabs/Components/ProgressBar.prefab");
        SaveTemporaryPrefab(BuildEquipmentSlotPrefab(theme),
            "Assets/UI/Prefabs/Components/EquipmentSlot.prefab");
        SaveTemporaryPrefab(BuildPortraitFramePrefab(theme),
            "Assets/UI/Prefabs/Components/PortraitFrame.prefab");
        SaveTemporaryPrefab(BuildTooltipAnchorPrefab(),
            "Assets/UI/Prefabs/Components/TooltipAnchor.prefab");
        SaveTemporaryPrefab(BuildSelectionHighlightPrefab(theme),
            "Assets/UI/Prefabs/Components/SelectionHighlight.prefab");
        SaveTemporaryPrefab(BuildInitiativeEntryPrefab(theme), InitiativeEntryPrefabPath);
        SaveTemporaryPrefab(BuildItemPreviewCardPrefab(theme), ItemPreviewCardPrefabPath);
    }

    private static GameObject BuildPanelPrefab(PurgatoryUITheme theme)
    {
        GameObject root = NewUIObject("PanelFrame", null);
        SetSize(root.GetComponent<RectTransform>(), 300, 160);
        AddPanelFrame(root, theme);
        return root;
    }

    private static GameObject BuildSectionHeaderPrefab(PurgatoryUITheme theme)
    {
        GameObject root = NewUIObject("SectionHeader", null);
        SetSize(root.GetComponent<RectTransform>(), 300, 30);
        CreateSectionHeader(root.transform, theme, "Section");
        return root;
    }

    private static GameObject BuildStatRowPrefab(PurgatoryUITheme theme)
    {
        GameObject root = NewUIObject("StatRow", null);
        SetSize(root.GetComponent<RectTransform>(), 300, 30);
        CreateStatRow(root.transform, theme, "Stat");
        return root;
    }

    private static GameObject BuildProgressBarPrefab(PurgatoryUITheme theme)
    {
        GameObject root = NewUIObject("ProgressBar", null);
        SetSize(root.GetComponent<RectTransform>(), 300, 34);
        CreateProgressBar(root.transform, theme, theme.Emerald);
        return root;
    }

    private static GameObject BuildEquipmentSlotPrefab(PurgatoryUITheme theme)
    {
        GameObject root = NewUIObject("EquipmentSlot", null);
        SetSize(root.GetComponent<RectTransform>(), 88, 88);
        Image frame = root.AddComponent<Image>();
        Button button = root.AddComponent<Button>();
        button.targetGraphic = frame;
        Image icon = CreateImage("Icon", root.transform, null, Color.white);
        Stretch(icon.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -12));
        TMP_Text label = CreateText("EmptyLabel", root.transform, theme, "Empty", TextAlignmentOptions.Center);
        Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
        EquipmentSlotView view = root.AddComponent<EquipmentSlotView>();
        view.Configure(theme, frame, icon, label, button);
        view.Render(new EquipmentSlotPresentationModel
        {
            label = "Empty",
            occupied = false,
            interactable = false
        });
        TooltipAnchor tooltipAnchor = root.AddComponent<TooltipAnchor>();
        tooltipAnchor.Configure(null, new TooltipContent
        {
            title = "Equipment slot",
            body = "Presentation placeholder. Equipment logic is not implemented in this phase.",
            values = new List<TooltipValueLine>()
        });
        return root;
    }

    private static GameObject BuildPortraitFramePrefab(PurgatoryUITheme theme)
    {
        GameObject root = NewUIObject("PortraitFrame", null);
        SetSize(root.GetComponent<RectTransform>(), theme.PortraitSize, theme.PortraitSize);
        Image frame = root.AddComponent<Image>();
        Image portrait = CreateImage(
            "Portrait", root.transform, theme.DevelopmentPortraitFallback, Color.white);
        Stretch(portrait.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
        portrait.preserveAspect = true;
        PortraitFrameView view = root.AddComponent<PortraitFrameView>();
        view.Configure(theme, frame, portrait);
        view.SetPortrait(null);
        return root;
    }

    private static GameObject BuildTooltipAnchorPrefab()
    {
        GameObject root = NewUIObject("TooltipAnchor", null);
        SetSize(root.GetComponent<RectTransform>(), 180, 48);
        Image image = root.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.01f);
        root.AddComponent<TooltipAnchor>();
        return root;
    }

    private static GameObject BuildSelectionHighlightPrefab(PurgatoryUITheme theme)
    {
        GameObject root = NewUIObject("SelectionHighlight", null);
        SetSize(root.GetComponent<RectTransform>(), theme.PortraitSize, theme.PortraitSize);
        Image image = root.AddComponent<Image>();
        image.sprite = theme.SelectedFrameSprite;
        image.type = Image.Type.Sliced;
        SelectionHighlightView view = root.AddComponent<SelectionHighlightView>();
        view.Configure(theme, image);
        return root;
    }

    private static GameObject BuildButton(
        PurgatoryUITheme theme,
        string name,
        ThemedButtonStyle style)
    {
        GameObject root = NewUIObject(name, null);
        SetSize(root.GetComponent<RectTransform>(), style == ThemedButtonStyle.Icon ? 56 : 220, 54);
        Image background = root.AddComponent<Image>();
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.minHeight = theme.MinimumButtonHeight;
        layout.preferredHeight = 54;
        TMP_Text label = CreateText("Label", root.transform, theme, name, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 5), new Vector2(-10, -5));
        Image icon = null;
        if (style == ThemedButtonStyle.Icon)
        {
            label.gameObject.SetActive(false);
            icon = CreateImage("Icon", root.transform, theme.DevelopmentPortraitFallback, Color.white);
            Stretch(icon.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
            icon.preserveAspect = true;
        }
        ThemedButtonView view = root.AddComponent<ThemedButtonView>();
        view.Configure(theme, style, button, background, label, icon);
        return root;
    }

    private static GameObject BuildActionControlPrefab(PurgatoryUITheme theme)
    {
        GameObject root = NewUIObject("BattleActionControl", null);
        SetSize(root.GetComponent<RectTransform>(), 176f, theme.ActionControlHeight);
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.minHeight = theme.MinimumButtonHeight;
        layout.preferredHeight = theme.ActionControlHeight;
        layout.flexibleWidth = 1f;
        Image background = root.AddComponent<Image>();
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;

        Image icon = CreateImage(
            "Icon", root.transform, theme.IconPlaceholderSprite, theme.TextPrimary);
        Stretch(icon.rectTransform, new Vector2(0.035f, 0.16f), new Vector2(0.28f, 0.84f),
            Vector2.zero, Vector2.zero);
        icon.preserveAspect = true;
        TMP_Text label = CreateText(
            "Label", root.transform, theme, "Action", TextAlignmentOptions.MidlineLeft);
        Stretch(label.rectTransform, new Vector2(0.31f, 0.49f), new Vector2(0.97f, 0.94f),
            Vector2.zero, Vector2.zero);
        TMP_Text state = CreateText(
            "State", root.transform, theme, theme.UnavailableLabel, TextAlignmentOptions.MidlineLeft);
        Stretch(state.rectTransform, new Vector2(0.31f, 0.08f), new Vector2(0.69f, 0.48f),
            Vector2.zero, Vector2.zero);
        TMP_Text hotkey = CreateText(
            "Hotkey", root.transform, theme, "—", TextAlignmentOptions.Center);
        Stretch(hotkey.rectTransform, new Vector2(0.70f, 0.08f), new Vector2(0.82f, 0.48f),
            Vector2.zero, Vector2.zero);
        TMP_Text cost = CreateText(
            "Cost", root.transform, theme, "AP —", TextAlignmentOptions.Center);
        Stretch(cost.rectTransform, new Vector2(0.82f, 0.08f), new Vector2(0.98f, 0.48f),
            Vector2.zero, Vector2.zero);

        BattleActionControlView view = root.AddComponent<BattleActionControlView>();
        view.Configure(theme, button, background, icon, label, hotkey, cost, state);
        view.RenderPlaceholder("Action", "—", "AP —");
        return root;
    }

    private static GameObject BuildItemPreviewCardPrefab(PurgatoryUITheme theme)
    {
        GameObject root = NewUIObject("ItemPreviewCard", null);
        SetSize(root.GetComponent<RectTransform>(), 220f, 280f);
        Image frame = root.AddComponent<Image>();

        Image preview = CreateImage(
            "Preview", root.transform, theme.IconPlaceholderSprite, Color.white);
        Stretch(preview.rectTransform, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.94f),
            Vector2.zero, Vector2.zero);
        preview.preserveAspect = true;
        TMP_Text empty = CreateText(
            "EmptyState", root.transform, theme, "Preview unavailable", TextAlignmentOptions.Center);
        Stretch(empty.rectTransform, new Vector2(0.12f, 0.47f), new Vector2(0.88f, 0.67f),
            Vector2.zero, Vector2.zero);
        TMP_Text title = CreateText(
            "Title", root.transform, theme, "No item selected", TextAlignmentOptions.MidlineLeft);
        Stretch(title.rectTransform, new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.32f),
            Vector2.zero, Vector2.zero);
        TMP_Text category = CreateText(
            "Category", root.transform, theme, "Empty", TextAlignmentOptions.MidlineLeft);
        Stretch(category.rectTransform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.17f),
            Vector2.zero, Vector2.zero);

        Image selected = CreateImage(
            "SelectionFrame", root.transform, theme.SelectedFrameSprite, Color.white);
        Stretch(selected.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        selected.type = selected.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        Image disabled = CreateImage(
            "DisabledOverlay", root.transform, theme.EmptySlotSprite, theme.Overlay);
        Stretch(disabled.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
        disabled.type = disabled.sprite != null ? Image.Type.Sliced : Image.Type.Simple;

        ItemPreviewCardView view = root.AddComponent<ItemPreviewCardView>();
        view.Configure(theme, frame, preview, title, category, empty, selected, disabled);
        TooltipAnchor tooltip = root.AddComponent<TooltipAnchor>();
        tooltip.Configure(null, new TooltipContent
        {
            title = "Item preview",
            body = "Presentation-only card. Inventory and equipment logic are not implemented.",
            values = new List<TooltipValueLine>()
        });
        return root;
    }

    private static GameObject BuildInitiativeEntryPrefab(PurgatoryUITheme theme)
    {
        GameObject root = NewUIObject("InitiativeEntry", null);
        SetSize(root.GetComponent<RectTransform>(), theme.InitiativeCardWidth, theme.InitiativeCardHeight);
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredWidth = theme.InitiativeCardWidth;
        layout.minWidth = theme.InitiativeCardWidth;
        layout.preferredHeight = theme.InitiativeCardHeight;
        Image background = root.AddComponent<Image>();

        Image portrait = CreateImage(
            "Portrait", root.transform, theme.DevelopmentPortraitFallback, Color.white);
        Stretch(portrait.rectTransform, Vector2.zero, new Vector2(0.39f, 1f),
            new Vector2(5, 5), new Vector2(-3, -5));
        portrait.preserveAspect = true;
        GameObject accentObject = NewUIObject("SideAccent", root.transform);
        Image sideAccent = accentObject.AddComponent<Image>();
        sideAccent.raycastTarget = false;
        Stretch(accentObject.GetComponent<RectTransform>(), new Vector2(0f, 0f),
            new Vector2(0.025f, 1f), Vector2.zero, Vector2.zero);
        TMP_Text squad = CreateText(
            "SquadId", root.transform, theme, "squad-id", TextAlignmentOptions.MidlineLeft);
        Stretch(squad.rectTransform, new Vector2(0.41f, 0.39f), new Vector2(0.84f, 0.92f),
            new Vector2(2, 0), new Vector2(-2, 0));
        squad.enableAutoSizing = true;
        squad.fontSizeMin = 12;
        squad.fontSizeMax = theme.CaptionSize;
        TMP_Text initiative = CreateText(
            "Initiative", root.transform, theme, "0", TextAlignmentOptions.Center);
        Stretch(initiative.rectTransform, new Vector2(0.84f, 0.1f), new Vector2(0.98f, 0.9f),
            Vector2.zero, Vector2.zero);

        GameObject activeObject = NewUIObject("ActiveIndicator", root.transform);
        Image activeIndicator = activeObject.AddComponent<Image>();
        activeIndicator.raycastTarget = false;
        Stretch(activeObject.GetComponent<RectTransform>(), new Vector2(0.405f, 0.16f),
            new Vector2(0.425f, 0.84f), Vector2.zero, Vector2.zero);
        TMP_Text controlLabel = CreateText(
            "ControlType", root.transform, theme, "HUMAN", TextAlignmentOptions.MidlineLeft);
        Stretch(controlLabel.rectTransform, new Vector2(0.43f, 0.08f),
            new Vector2(0.82f, 0.40f), Vector2.zero, Vector2.zero);

        GameObject highlightObject = NewUIObject("SelectedHighlight", root.transform);
        Stretch(highlightObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        Image highlightImage = highlightObject.AddComponent<Image>();
        highlightImage.sprite = theme.SelectedFrameSprite;
        highlightImage.type = Image.Type.Sliced;
        highlightImage.raycastTarget = false;
        SelectionHighlightView highlight = highlightObject.AddComponent<SelectionHighlightView>();
        highlight.Configure(theme, highlightImage);

        InitiativeEntryView view = root.AddComponent<InitiativeEntryView>();
        view.Configure(theme, background, sideAccent, portrait, squad, initiative, highlight);
        view.ConfigureStateVisuals(activeIndicator, controlLabel);
        return root;
    }

    private static GameObject BuildBattleHudPrefab(PurgatoryUITheme theme)
    {
        CommanderPortraitDatabase portraitDatabase =
            AssetDatabase.LoadAssetAtPath<CommanderPortraitDatabase>(PortraitDatabasePath);
        InitiativeEntryView initiativeEntryPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(InitiativeEntryPrefabPath)
                ?.GetComponent<InitiativeEntryView>();
        if (portraitDatabase == null || initiativeEntryPrefab == null)
            throw new InvalidOperationException("Battle HUD dependencies are missing.");

        GameObject root = NewUIObject("BattleUIRoot", null);
        Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        BattleHUDController hudController = root.AddComponent<BattleHUDController>();
        // Adding a root Canvas can reset its RectTransform while the object is still temporary.
        // Restore the transform value the prefab instance must own; the Canvas drives its size.
        root.GetComponent<RectTransform>().localScale = Vector3.one;

        GameObject hudLayer = NewUIObject("HUDLayer", root.transform);
        Stretch(hudLayer.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(theme.SafeMargin, theme.SafeMargin),
            new Vector2(-theme.SafeMargin, -theme.SafeMargin));

        InitiativeQueuePresenter initiativePresenter = CreateTopBar(
            hudLayer.transform, theme, portraitDatabase, initiativeEntryPrefab);
        CreateMinimapPlaceholder(hudLayer.transform, theme);
        BattleSquadStatusPresenter statusPresenter = CreateSquadStatusPanel(
            hudLayer.transform, theme, portraitDatabase);
        (BattleActionBarView actionBar, AbilityDetailsPanelView abilityDetails) =
            CreateActionBar(hudLayer.transform, theme);

        GameObject modalLayer = NewUIObject("ModalLayer", root.transform);
        Stretch(modalLayer.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        modalLayer.SetActive(false);

        TooltipController tooltipController = CreateTooltipLayer(root.transform, theme, canvas);
        foreach ((TooltipAnchor anchor, TooltipContent content) in TooltipAnchors)
            anchor.Configure(tooltipController, content);
        TooltipAnchors.Clear();

        hudController.Configure(
            null,
            statusPresenter,
            initiativePresenter,
            actionBar,
            abilityDetails,
            hudLayer);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BattleHudPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static InitiativeQueuePresenter CreateTopBar(
        Transform parent,
        PurgatoryUITheme theme,
        CommanderPortraitDatabase portraitDatabase,
        InitiativeEntryView initiativeEntryPrefab)
    {
        GameObject topBar = NewUIObject("TopBar", parent);
        Stretch(topBar.GetComponent<RectTransform>(), new Vector2(0.27f, 0.885f),
            new Vector2(0.73f, 0.99f), Vector2.zero, Vector2.zero);
        AddPanelFrame(topBar, theme);
        CreateSectionHeader(topBar.transform, theme, theme.InitiativeLabel);

        GameObject queueContainerObject = NewUIObject("InitiativeQueue", topBar.transform);
        Stretch(queueContainerObject.GetComponent<RectTransform>(), new Vector2(0.025f, 0.06f),
            new Vector2(0.975f, 0.70f), Vector2.zero, Vector2.zero);
        HorizontalLayoutGroup layout = queueContainerObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = theme.CompactSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        TMP_Text emptyLabel = CreateText(
            "EmptyState", topBar.transform, theme, "No initiative entries", TextAlignmentOptions.Center);
        Stretch(emptyLabel.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.68f),
            Vector2.zero, Vector2.zero);

        InitiativeQueueView view = topBar.AddComponent<InitiativeQueueView>();
        view.Configure(queueContainerObject.GetComponent<RectTransform>(), initiativeEntryPrefab, emptyLabel.gameObject);
        InitiativeQueuePresenter presenter = topBar.AddComponent<InitiativeQueuePresenter>();
        presenter.Configure(view, portraitDatabase);
        return presenter;
    }

    private static void CreateMinimapPlaceholder(Transform parent, PurgatoryUITheme theme)
    {
        GameObject minimap = NewUIObject("TopRight_MinimapContainer", parent);
        Stretch(minimap.GetComponent<RectTransform>(), new Vector2(0.83f, 0.72f),
            new Vector2(1f, 0.99f), Vector2.zero, Vector2.zero);
        AddPanelFrame(minimap, theme);
        CreateSectionHeader(minimap.transform, theme, "Minimap");
        TMP_Text label = CreateText(
            "EmptyState", minimap.transform, theme,
            "Not available in this build", TextAlignmentOptions.Center);
        label.color = theme.Disabled;
        Stretch(label.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.68f),
            Vector2.zero, Vector2.zero);
    }

    private static BattleSquadStatusPresenter CreateSquadStatusPanel(
        Transform parent,
        PurgatoryUITheme theme,
        CommanderPortraitDatabase portraitDatabase)
    {
        GameObject panel = NewUIObject("SelectedSquadPanel", parent);
        Stretch(panel.GetComponent<RectTransform>(), new Vector2(0f, 0.535f),
            new Vector2(0.25f, 0.99f), Vector2.zero, Vector2.zero);
        AddPanelFrame(panel, theme);
        CreateSectionHeader(panel.transform, theme, theme.SquadLabel);

        GameObject content = NewUIObject("Content", panel.transform);
        CanvasGroup contentCanvasGroup = content.AddComponent<CanvasGroup>();
        Stretch(content.GetComponent<RectTransform>(), new Vector2(0.045f, 0.045f),
            new Vector2(0.955f, 0.78f), Vector2.zero, Vector2.zero);
        GameObject empty = NewUIObject("EmptyState", panel.transform);
        Stretch(empty.GetComponent<RectTransform>(), new Vector2(0.08f, 0.1f),
            new Vector2(0.92f, 0.75f), Vector2.zero, Vector2.zero);
        TMP_Text emptyLabel = CreateText(
            "Message", empty.transform, theme, theme.EmptySquadLabel, TextAlignmentOptions.Center);
        Stretch(emptyLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        empty.SetActive(false);

        GameObject portraitObject = NewUIObject("CommanderPortraitFrame", content.transform);
        Stretch(portraitObject.GetComponent<RectTransform>(), new Vector2(0f, 0.58f),
            new Vector2(0.32f, 1f), Vector2.zero, Vector2.zero);
        Image portraitFrameImage = portraitObject.AddComponent<Image>();
        Image portraitImage = CreateImage(
            "Portrait", portraitObject.transform, theme.DevelopmentPortraitFallback, Color.white);
        Stretch(portraitImage.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(7, 7), new Vector2(-7, -7));
        portraitImage.preserveAspect = true;
        PortraitFrameView portraitFrame = portraitObject.AddComponent<PortraitFrameView>();
        portraitFrame.Configure(theme, portraitFrameImage, portraitImage);

        TMP_Text squadLabel = CreateText(
            "SquadId", content.transform, theme, "squad-id", TextAlignmentOptions.MidlineLeft);
        Stretch(squadLabel.rectTransform, new Vector2(0.35f, 0.77f), new Vector2(1f, 1f),
            Vector2.zero, Vector2.zero);
        squadLabel.enableAutoSizing = true;
        squadLabel.fontSizeMin = 16;
        squadLabel.fontSizeMax = theme.HeadingSize;
        TMP_Text commanderLabel = CreateText(
            "CommanderId", content.transform, theme, "commander-id", TextAlignmentOptions.MidlineLeft);
        Stretch(commanderLabel.rectTransform, new Vector2(0.35f, 0.58f), new Vector2(1f, 0.78f),
            Vector2.zero, Vector2.zero);
        commanderLabel.enableAutoSizing = true;
        commanderLabel.fontSizeMin = 14;
        commanderLabel.fontSizeMax = theme.BodySize;

        ProgressBarView health = CreateLabeledBar(
            content.transform, theme, "HealthBar", theme.HealthLabel, 0.44f, 0.55f, theme.Danger,
            "Current and maximum squad HP from SquadBattleRuntime.");
        ProgressBarView actionPoints = CreateLabeledBar(
            content.transform, theme, "ActionPointsBar", theme.ActionPointsLabel, 0.29f, 0.40f, theme.Emerald,
            "Current and maximum action points from SquadBattleRuntime.");
        ProgressBarView morale = CreateLabeledBar(
            content.transform, theme, "MoraleBar", theme.MoraleLabel, 0.14f, 0.25f, theme.Gold,
            "Current and maximum morale from SquadBattleRuntime.");

        GameObject warriorRowObject = NewUIObject("WarriorCount", content.transform);
        Stretch(warriorRowObject.GetComponent<RectTransform>(), new Vector2(0f, 0f),
            new Vector2(1f, 0.11f), Vector2.zero, Vector2.zero);
        StatRowView warriors = ConfigureStatRow(warriorRowObject, theme, theme.WarriorsLabel);
        AddTooltip(
            warriorRowObject,
            "Warriors",
            "Living and maximum warrior count from the bound squad runtime.");

        BattleSquadStatusView view = panel.AddComponent<BattleSquadStatusView>();
        view.Configure(
            theme,
            content,
            empty,
            emptyLabel,
            squadLabel,
            commanderLabel,
            portraitFrame,
            health,
            actionPoints,
            morale,
            warriors,
            contentCanvasGroup);
        BattleSquadStatusPresenter presenter = panel.AddComponent<BattleSquadStatusPresenter>();
        presenter.Configure(view, portraitDatabase);
        return presenter;
    }

    private static (BattleActionBarView, AbilityDetailsPanelView) CreateActionBar(
        Transform parent,
        PurgatoryUITheme theme)
    {
        GameObject bar = NewUIObject("BottomActionBar", parent);
        Stretch(bar.GetComponent<RectTransform>(), new Vector2(0f, 0f),
            new Vector2(1f, 0.18f), Vector2.zero, Vector2.zero);
        AddPanelFrame(bar, theme);

        GameObject sections = NewUIObject("Sections", bar.transform);
        Stretch(sections.GetComponent<RectTransform>(), new Vector2(0.012f, 0.10f),
            new Vector2(0.988f, 0.94f), Vector2.zero, Vector2.zero);
        HorizontalLayoutGroup horizontal = sections.AddComponent<HorizontalLayoutGroup>();
        horizontal.padding = new RectOffset(0, 0, 0, 0);
        horizontal.spacing = theme.CompactSpacing;
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;

        List<Button> buttons = new List<Button>();
        CreateActionSection(sections.transform, theme, "BasicActions", "Basic Actions",
            new[] { "Move", "Attack", "End Turn" }, 1.25f, buttons);
        CreateVerticalSeparator(sections.transform, theme);
        CreateActionSection(sections.transform, theme, "CommanderPerks", "Commander Perks",
            new[] { "Perk I", "Perk II" }, 0.85f, buttons);
        CreateVerticalSeparator(sections.transform, theme);
        CreateActionSection(sections.transform, theme, "Consumables", "Consumables",
            new[] { "Item I", "Item II" }, 0.85f, buttons);
        CreateVerticalSeparator(sections.transform, theme);
        GameObject detailsSection = CreateSectionPanel(
            sections.transform, theme, "AbilityInfo", "Ability Info", 1.3f);
        TMP_Text title = CreateText(
            "AbilityTitle", detailsSection.transform, theme, string.Empty, TextAlignmentOptions.MidlineLeft);
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
        TMP_Text description = CreateText(
            "AbilityDescription", detailsSection.transform, theme, string.Empty, TextAlignmentOptions.TopLeft);
        description.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        TMP_Text empty = CreateText(
            "EmptyState", detailsSection.transform, theme, theme.UnavailableLabel, TextAlignmentOptions.Center);
        empty.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        AbilityDetailsPanelView abilityDetails = detailsSection.AddComponent<AbilityDetailsPanelView>();
        abilityDetails.Configure(theme, null, title, description, empty);

        TMP_Text unavailable = CreateText(
            "UnavailableNotice", bar.transform, theme, theme.UnavailableLabel, TextAlignmentOptions.Center);
        Stretch(unavailable.rectTransform, new Vector2(0.25f, 0f), new Vector2(0.75f, 0.09f),
            Vector2.zero, Vector2.zero);
        unavailable.color = theme.Disabled;
        BattleActionBarView actionBar = bar.AddComponent<BattleActionBarView>();
        actionBar.Configure(theme, buttons.ToArray(), unavailable);
        return (actionBar, abilityDetails);
    }

    private static void CreateActionSection(
        Transform parent,
        PurgatoryUITheme theme,
        string name,
        string heading,
        string[] labels,
        float flexibleWidth,
        ICollection<Button> buttons)
    {
        GameObject section = CreateSectionPanel(parent, theme, name, heading, flexibleWidth);
        GameObject controls = NewUIObject("Controls", section.transform);
        LayoutElement controlsElement = controls.AddComponent<LayoutElement>();
        controlsElement.flexibleHeight = 1f;
        controlsElement.minHeight = theme.MinimumButtonHeight;
        HorizontalLayoutGroup controlsLayout = controls.AddComponent<HorizontalLayoutGroup>();
        controlsLayout.spacing = theme.CompactSpacing;
        controlsLayout.childAlignment = TextAnchor.MiddleCenter;
        controlsLayout.childControlWidth = true;
        controlsLayout.childControlHeight = true;
        controlsLayout.childForceExpandWidth = true;
        controlsLayout.childForceExpandHeight = true;
        foreach (string label in labels)
        {
            GameObject buttonObject = BuildActionControlPrefab(theme);
            buttonObject.name = label.Replace(" ", string.Empty);
            buttonObject.transform.SetParent(controls.transform, false);
            BattleActionControlView actionControl = buttonObject.GetComponent<BattleActionControlView>();
            actionControl.RenderPlaceholder(label, "—", "AP —");
            Button button = actionControl.Button;
            buttons.Add(button);
            AddTooltip(
                buttonObject,
                label,
                "Development placeholder. The gameplay command contract is not implemented in this phase.");
        }
    }

    private static GameObject CreateSectionPanel(
        Transform parent,
        PurgatoryUITheme theme,
        string name,
        string heading,
        float flexibleWidth)
    {
        GameObject section = NewUIObject(name, parent);
        LayoutElement element = section.AddComponent<LayoutElement>();
        element.flexibleWidth = flexibleWidth;
        element.minWidth = 168;
        VerticalLayoutGroup vertical = section.AddComponent<VerticalLayoutGroup>();
        int padding = Mathf.RoundToInt(theme.CompactPadding);
        vertical.padding = new RectOffset(padding, padding, padding, padding);
        vertical.spacing = theme.CompactSpacing;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlHeight = true;
        vertical.childControlWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.childForceExpandWidth = true;
        TMP_Text title = CreateText("Header", section.transform, theme, heading, TextAlignmentOptions.Center);
        title.color = theme.TextPrimary;
        LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 24;
        titleLayout.minHeight = 22;
        return section;
    }

    private static void CreateVerticalSeparator(Transform parent, PurgatoryUITheme theme)
    {
        Image separator = CreateImage(
            "VerticalSeparator", parent, theme.SeparatorSprite, Color.white);
        separator.type = separator.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        LayoutElement layout = separator.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = Mathf.Max(2f, theme.BorderWidth);
        layout.preferredWidth = Mathf.Max(2f, theme.BorderWidth);
        layout.flexibleWidth = 0f;
    }

    private static TooltipController CreateTooltipLayer(
        Transform parent,
        PurgatoryUITheme theme,
        Canvas canvas)
    {
        GameObject layer = NewUIObject("TooltipLayer", parent);
        Stretch(layer.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TooltipController controller = layer.AddComponent<TooltipController>();

        GameObject panel = NewUIObject("TooltipPanel", layer.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(390, 230);
        AddPanelFrame(panel, theme);
        CanvasGroup group = panel.AddComponent<CanvasGroup>();
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 16, 16);
        layout.spacing = theme.SpaceSmall;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text title = CreateText("Title", panel.transform, theme, string.Empty, TextAlignmentOptions.MidlineLeft);
        title.color = theme.Gold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
        TMP_Text body = CreateText("Body", panel.transform, theme, string.Empty, TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;
        TMP_Text values = CreateText("Values", panel.transform, theme, string.Empty, TextAlignmentOptions.TopLeft);
        values.color = theme.Emerald;
        values.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
        controller.Configure(canvas, panelRect, group, title, body, values, null);
        return controller;
    }

    private static void AddTooltip(GameObject target, string title, string body)
    {
        TooltipAnchor anchor = target.GetComponent<TooltipAnchor>() ?? target.AddComponent<TooltipAnchor>();
        TooltipAnchors.Add((anchor, new TooltipContent
        {
            title = title,
            body = body,
            values = new List<TooltipValueLine>()
        }));
    }

    private static ProgressBarView CreateLabeledBar(
        Transform parent,
        PurgatoryUITheme theme,
        string name,
        string label,
        float minY,
        float maxY,
        Color fill,
        string tooltip)
    {
        GameObject row = NewUIObject(name, parent);
        Stretch(row.GetComponent<RectTransform>(), new Vector2(0f, minY), new Vector2(1f, maxY),
            Vector2.zero, Vector2.zero);
        TMP_Text rowLabel = CreateText(
            "Label", row.transform, theme, label, TextAlignmentOptions.MidlineLeft);
        Stretch(rowLabel.rectTransform, Vector2.zero, new Vector2(0.34f, 1f), Vector2.zero, Vector2.zero);
        GameObject barObject = NewUIObject("Bar", row.transform);
        Stretch(barObject.GetComponent<RectTransform>(), new Vector2(0.35f, 0.12f),
            new Vector2(1f, 0.88f), Vector2.zero, Vector2.zero);
        ProgressBarView bar = ConfigureProgressBar(barObject, theme, fill);
        AddTooltip(row, label, tooltip);
        return bar;
    }

    private static ProgressBarView CreateProgressBar(
        Transform parent,
        PurgatoryUITheme theme,
        Color fillColor)
    {
        GameObject root = parent.gameObject;
        return ConfigureProgressBar(root, theme, fillColor);
    }

    private static ProgressBarView ConfigureProgressBar(
        GameObject root,
        PurgatoryUITheme theme,
        Color fillColor)
    {
        Image background = root.GetComponent<Image>() ?? root.AddComponent<Image>();
        background.color = theme.Granite;
        Image fill = CreateImage("Fill", root.transform, null, fillColor);
        Stretch(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
        TMP_Text value = CreateText("Value", root.transform, theme, "0 / 0", TextAlignmentOptions.Center);
        Stretch(value.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        ProgressBarView bar = root.AddComponent<ProgressBarView>();
        bar.Configure(theme, background, fill, value, fillColor);
        bar.SetValue(0, 0, "0 / 0");
        return bar;
    }

    private static StatRowView CreateStatRow(Transform parent, PurgatoryUITheme theme, string label)
    {
        return ConfigureStatRow(parent.gameObject, theme, label);
    }

    private static StatRowView ConfigureStatRow(
        GameObject root,
        PurgatoryUITheme theme,
        string label)
    {
        TMP_Text labelText = CreateText(
            "Label", root.transform, theme, label, TextAlignmentOptions.MidlineLeft);
        Stretch(labelText.rectTransform, Vector2.zero, new Vector2(0.65f, 1f), Vector2.zero, Vector2.zero);
        TMP_Text valueText = CreateText(
            "Value", root.transform, theme, "0 / 0", TextAlignmentOptions.MidlineRight);
        Stretch(valueText.rectTransform, new Vector2(0.65f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        StatRowView row = root.AddComponent<StatRowView>();
        row.Configure(theme, labelText, valueText, label);
        return row;
    }

    private static SectionHeaderView CreateSectionHeader(
        Transform parent,
        PurgatoryUITheme theme,
        string label)
    {
        GameObject root = parent.gameObject.name == "SectionHeader"
            ? parent.gameObject
            : NewUIObject("SectionHeader", parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        if (root != parent.gameObject)
            Stretch(rect, new Vector2(0.035f, 0.72f), new Vector2(0.965f, 0.98f), Vector2.zero, Vector2.zero);
        Image headerBackground = root.GetComponent<Image>() ?? root.AddComponent<Image>();
        headerBackground.sprite = theme.SectionHeaderSprite;
        headerBackground.type = headerBackground.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        headerBackground.color = Color.white;
        headerBackground.raycastTarget = false;
        TMP_Text labelText = CreateText(
            "Label", root.transform, theme, label, TextAlignmentOptions.MidlineLeft);
        Stretch(labelText.rectTransform, new Vector2(0.025f, 0.18f), new Vector2(0.975f, 1f),
            Vector2.zero, Vector2.zero);
        Image separator = CreateImage("Separator", root.transform, theme.SeparatorSprite, Color.white);
        Stretch(separator.rectTransform, Vector2.zero, new Vector2(1f, 0.10f), Vector2.zero, Vector2.zero);
        separator.type = Image.Type.Sliced;
        SectionHeaderView view = root.AddComponent<SectionHeaderView>();
        view.Configure(theme, labelText, separator, label);
        return view;
    }

    private static PanelFrameView AddPanelFrame(GameObject root, PurgatoryUITheme theme)
    {
        Image image = root.GetComponent<Image>() ?? root.AddComponent<Image>();
        PanelFrameView frame = root.GetComponent<PanelFrameView>() ?? root.AddComponent<PanelFrameView>();
        frame.Configure(theme, image);
        return frame;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        PurgatoryUITheme theme,
        string text,
        TextAlignmentOptions alignment)
    {
        GameObject child = NewUIObject(name, parent);
        TextMeshProUGUI label = child.AddComponent<TextMeshProUGUI>();
        label.font = theme.PrimaryFont;
        label.fontSize = theme.BodySize;
        label.color = theme.Marble;
        label.text = text ?? string.Empty;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Sprite sprite,
        Color color)
    {
        GameObject child = NewUIObject(name, parent);
        Image image = child.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject NewUIObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static void SetSize(RectTransform rect, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private static void SaveTemporaryPrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void ConfigureDevelopmentPortraitsInBattleScene()
    {
        const string humanPath =
            CommanderPortraitDatabaseBuilder.ImportedHumanRoot + "/MHanzoDorian1.png";
        const string elfPath =
            CommanderPortraitDatabaseBuilder.ImportedElfRoot + "/WNobleElf1.png";
        Sprite humanPortrait = AssetDatabase.LoadAssetAtPath<Sprite>(humanPath);
        Sprite elfPortrait = AssetDatabase.LoadAssetAtPath<Sprite>(elfPath);
        if (humanPortrait == null || elfPortrait == null)
        {
            throw new InvalidOperationException(
                "The explicit Human and Elf development portrait Sprites are unavailable.");
        }

        Scene scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
        SquadBattleBootstrap[] bootstraps =
            UnityEngine.Object.FindObjectsByType<SquadBattleBootstrap>(FindObjectsInactive.Include);
        if (bootstraps.Length != 1)
            throw new InvalidOperationException(
                $"Expected exactly one SquadBattleBootstrap in {BattleScenePath}; found {bootstraps.Length}.");

        SerializedObject serializedBootstrap = new SerializedObject(bootstraps[0]);
        SerializedProperty player = serializedBootstrap.FindProperty("developmentPlayerSquad");
        SerializedProperty enemy = serializedBootstrap.FindProperty("developmentEnemySquad");
        SerializedProperty playerCommander = player?.FindPropertyRelative("commander");
        SerializedProperty enemyCommander = enemy?.FindPropertyRelative("commander");
        if (playerCommander == null || enemyCommander == null)
            throw new InvalidOperationException("Development squad commander data is not serialized as expected.");

        playerCommander.FindPropertyRelative("race").enumValueIndex = (int)CommanderRace.Human;
        playerCommander.FindPropertyRelative("commanderPortraitId").stringValue =
            AssetDatabase.AssetPathToGUID(humanPath);
        enemyCommander.FindPropertyRelative("race").enumValueIndex = (int)CommanderRace.Elf;
        enemyCommander.FindPropertyRelative("commanderPortraitId").stringValue =
            AssetDatabase.AssetPathToGUID(elfPath);
        serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bootstraps[0]);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            "BattleUIInstaller: Raw development squads received explicit Human and Elf portrait GUIDs; " +
            "race was not inferred from battle side.");
    }

    private static void WireExistingBattleHudInternal(bool allowReplacement)
    {
        GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleHudPrefabPath);
        if (hudPrefab == null)
            throw new InvalidOperationException("Existing BattleHUD.prefab is missing.");

        Scene scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
        List<GameObject> hudRoots = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<BattleHUDController>() != null)
                hudRoots.Add(root);
        }

        SquadBattleBootstrap[] bootstraps =
            UnityEngine.Object.FindObjectsByType<SquadBattleBootstrap>(
                FindObjectsInactive.Include);
        if (bootstraps.Length != 1)
            throw new InvalidOperationException(
                $"Expected exactly one SquadBattleBootstrap in {BattleScenePath}; found {bootstraps.Length}.");

        GameObject instance = null;
        if (hudRoots.Count == 1 &&
            PrefabUtility.GetCorrespondingObjectFromSource(hudRoots[0]) == hudPrefab)
        {
            instance = hudRoots[0];
        }
        else if (hudRoots.Count > 0)
        {
            if (!allowReplacement)
            {
                throw new InvalidOperationException(
                    $"Raw scene contains {hudRoots.Count} non-canonical or duplicate Battle HUD root(s); " +
                    "automation refused to replace them.");
            }
            foreach (GameObject root in hudRoots)
                UnityEngine.Object.DestroyImmediate(root);
        }

        if (instance == null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene);
            instance.name = "BattleUIRoot";
        }

        BattleHUDController hud = instance.GetComponent<BattleHUDController>();
        BattleSquadStatusPresenter status = instance.GetComponentInChildren<BattleSquadStatusPresenter>(true);
        InitiativeQueuePresenter initiative = instance.GetComponentInChildren<InitiativeQueuePresenter>(true);
        BattleActionBarView actionBar = instance.GetComponentInChildren<BattleActionBarView>(true);
        AbilityDetailsPanelView details = instance.GetComponentInChildren<AbilityDetailsPanelView>(true);
        Transform hudLayer = instance.transform.Find("HUDLayer");
        if (hud == null || status == null || initiative == null || actionBar == null ||
            details == null || hudLayer == null)
        {
            throw new InvalidOperationException("BattleHUD prefab is missing required serialized components.");
        }

        hud.Configure(bootstraps[0], status, initiative, actionBar, details, hudLayer.gameObject);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            "BattleUIInstaller: existing BattleHUD.prefab wired to Raw scene without rebuilding visual assets.");
    }
}
#endif
