using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleUIVisualPassTests
{
    private const string HudPath = "Assets/UI/Prefabs/Battle/BattleHUD.prefab";
    private const string ThemePath = "Assets/UI/Themes/PurgatoryUITheme.asset";
    private const string CatalogPath =
        "Assets/UI/Presentation/DevelopmentItemPresentationCatalog.asset";

    [Test]
    public void RealPortraitFoldersMapToExplicitRaces()
    {
        Assert.That(
            CommanderPortraitDatabaseBuilder.TryGetRaceFromAssetPath(
                CommanderPortraitDatabaseBuilder.ImportedHumanRoot + "/MHanzoDorian1.png",
                out CommanderRace humanRace),
            Is.True);
        Assert.That(humanRace, Is.EqualTo(CommanderRace.Human));
        Assert.That(
            CommanderPortraitDatabaseBuilder.TryGetRaceFromAssetPath(
                CommanderPortraitDatabaseBuilder.ImportedElfRoot + "/WNobleElf1.png",
                out CommanderRace elfRace),
            Is.True);
        Assert.That(elfRace, Is.EqualTo(CommanderRace.Elf));
    }

    [Test]
    public void PortraitDatabaseRebuildIsStableCompleteAndDuplicateFree()
    {
        CommanderPortraitDatabaseBuilder.RebuildDatabase();
        CommanderPortraitDatabase database = AssetDatabase.LoadAssetAtPath<CommanderPortraitDatabase>(
            CommanderPortraitDatabaseBuilder.DatabasePath);
        Assert.That(database, Is.Not.Null);
        string[] firstIds = database.Entries.Select(entry => entry.Id).ToArray();

        Assert.That(database.GetEntries(CommanderRace.Human).Count, Is.EqualTo(47));
        Assert.That(database.GetEntries(CommanderRace.Elf).Count, Is.EqualTo(30));
        Assert.That(firstIds.Length, Is.EqualTo(77));
        Assert.That(firstIds.Distinct().Count(), Is.EqualTo(firstIds.Length));
        Assert.That(database.Entries.All(entry => entry.Sprite != null), Is.True);

        CommanderPortraitDatabaseBuilder.RebuildDatabase();
        string[] secondIds = database.Entries.Select(entry => entry.Id).ToArray();
        Assert.That(secondIds, Is.EqualTo(firstIds));
    }

    [Test]
    public void RealPortraitImportSettingsAreUiAppropriate()
    {
        AssertPortraitImporter(
            CommanderPortraitDatabaseBuilder.ImportedHumanRoot + "/MHanzoDorian1.png");
        AssertPortraitImporter(
            CommanderPortraitDatabaseBuilder.ImportedElfRoot + "/WNobleElf1.png");
    }

    [Test]
    public void ValidPortraitLookupNeverUsesFallbackAndSameFilenameDoesNotCollide()
    {
        CommanderPortraitDatabase database = AssetDatabase.LoadAssetAtPath<CommanderPortraitDatabase>(
            CommanderPortraitDatabaseBuilder.DatabasePath);
        List<CommanderPortraitEntry> sharedNames = database.Entries
            .Where(entry => entry.ResourceName == "WPaladin1")
            .ToList();
        Assert.That(sharedNames.Count, Is.EqualTo(2));
        Assert.That(sharedNames.Select(entry => entry.Race),
            Is.EquivalentTo(new[] { CommanderRace.Human, CommanderRace.Elf }));
        Assert.That(sharedNames[0].Id, Is.Not.EqualTo(sharedNames[1].Id));

        CommanderPortraitService service = new CommanderPortraitService(database, 17);
        foreach (CommanderPortraitEntry entry in sharedNames)
        {
            Sprite resolved = service.GetDisplaySprite(entry.Id);
            Assert.That(resolved, Is.SameAs(entry.Sprite));
            Assert.That(resolved, Is.Not.SameAs(database.FallbackPortrait));
        }
        Assert.That(service.GetDisplaySprite("missing-id"), Is.SameAs(database.FallbackPortrait));
    }

    [Test]
    public void ThemeContainsVisualPassTokensAndDevSprites()
    {
        PurgatoryUITheme theme = AssetDatabase.LoadAssetAtPath<PurgatoryUITheme>(ThemePath);
        Assert.That(theme, Is.Not.Null);
        Assert.That(theme.OuterFrameSprite, Is.Not.Null);
        Assert.That(theme.InsetPanelSprite, Is.Not.Null);
        Assert.That(theme.SectionHeaderSprite, Is.Not.Null);
        Assert.That(theme.SelectedFrameSprite, Is.Not.Null);
        Assert.That(theme.ButtonHoverSprite, Is.Not.Null);
        Assert.That(theme.ButtonPressedSprite, Is.Not.Null);
        Assert.That(theme.ButtonDisabledSprite, Is.Not.Null);
        Assert.That(theme.PortraitFrameSprite, Is.Not.Null);
        Assert.That(theme.InitiativeCardSprite, Is.Not.Null);
        Assert.That(theme.EquipmentSlotSprite, Is.Not.Null);
        Assert.That(theme.EmptySlotSprite, Is.Not.Null);
        Assert.That(theme.IconPlaceholderSprite, Is.Not.Null);
        Assert.That(theme.SafeMargin, Is.EqualTo(20f));
        Assert.That(theme.MinimumButtonHeight, Is.GreaterThanOrEqualTo(48f));
    }

    [Test]
    public void ValidationIsReadOnlyAndAutomationRefusesMissingDestructiveFlag()
    {
        string absoluteHudPath = Path.GetFullPath(HudPath);
        byte[] before = File.ReadAllBytes(absoluteHudPath);
        BattleUIInstaller.ValidateExistingFoundation();
        byte[] after = File.ReadAllBytes(absoluteHudPath);
        Assert.That(after, Is.EqualTo(before));
        Assert.That(
            () => BattleUIInstaller.InstallForAutomation(),
            Throws.TypeOf<System.InvalidOperationException>());
        Assert.That(File.ReadAllBytes(absoluteHudPath), Is.EqualTo(before));
    }

    [Test]
    public void CompactHudFootprintRemainsBelowDocumentedBaselineWithoutGlobalScaling()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        Transform layer = prefab.transform.Find("HUDLayer");
        float newArea = AnchorArea(layer.Find("TopBar") as RectTransform) +
                        AnchorArea(layer.Find("TopRight_MinimapContainer") as RectTransform) +
                        AnchorArea(layer.Find("SelectedSquadPanel") as RectTransform) +
                        AnchorArea(layer.Find("BottomActionBar") as RectTransform);
        const float documentedFirstPhaseArea = 0.5431f;
        float reduction = 1f - newArea / documentedFirstPhaseArea;

        Assert.That(newArea, Is.EqualTo(0.3880f).Within(0.005f));
        Assert.That(reduction, Is.InRange(0.27f, 0.31f));
        Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(layer.localScale, Is.EqualTo(Vector3.one));
    }

    [Test]
    public void StatusPanelIsTenPercentSmallerRaisedAndAlignedWithMinimap()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        Transform layer = prefab.transform.Find("HUDLayer");
        RectTransform status = layer.Find("SelectedSquadPanel") as RectTransform;
        RectTransform minimap = layer.Find("TopRight_MinimapContainer") as RectTransform;
        const float previousStatusArea = 0.27f * 0.47f;
        float currentStatusArea = AnchorArea(status);
        float reduction = 1f - currentStatusArea / previousStatusArea;

        Assert.That(reduction, Is.InRange(0.09f, 0.12f));
        Assert.That(status.anchorMax.y, Is.EqualTo(minimap.anchorMax.y).Within(0.001f));
        Assert.That(status.anchorMin.y, Is.GreaterThan(0.18f));
        Assert.That(status.localScale, Is.EqualTo(Vector3.one));
        Assert.That(status.Find("Content").GetComponent<CanvasGroup>(), Is.Not.Null);
    }

    [Test]
    public void InitiativePrefabHasSeparateActiveAndSelectedVisualsAndControlLabel()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/UI/Prefabs/Components/InitiativeEntry.prefab");
        Assert.That(prefab.transform.Find("ActiveIndicator")?.GetComponent<Image>(),
            Is.Not.Null);
        Assert.That(prefab.transform.Find("SelectedHighlight")
            ?.GetComponent<SelectionHighlightView>(), Is.Not.Null);
        Assert.That(prefab.transform.Find("ControlType")
            ?.GetComponent<TMPro.TMP_Text>(), Is.Not.Null);
    }

    [Test]
    public void PortraitsPreserveAspectAndActionBarUsesOneFrameWithSeparators()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        Image statusPortrait = hud.transform.Find(
                "HUDLayer/SelectedSquadPanel/Content/CommanderPortraitFrame/Portrait")
            ?.GetComponent<Image>();
        GameObject initiativePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/UI/Prefabs/Components/InitiativeEntry.prefab");
        Image initiativePortrait = initiativePrefab.transform.Find("Portrait")?.GetComponent<Image>();
        Assert.That(statusPortrait, Is.Not.Null);
        Assert.That(statusPortrait.preserveAspect, Is.True);
        Assert.That(initiativePortrait, Is.Not.Null);
        Assert.That(initiativePortrait.preserveAspect, Is.True);

        Transform bottom = hud.transform.Find("HUDLayer/BottomActionBar");
        Assert.That(bottom.GetComponentsInChildren<PanelFrameView>(true).Length, Is.EqualTo(1));
        Assert.That(
            bottom.GetComponentsInChildren<Image>(true)
                .Count(image => image.gameObject.name == "VerticalSeparator"),
            Is.EqualTo(3));
        Assert.That(bottom.GetComponentsInChildren<BattleActionControlView>(true).Length,
            Is.EqualTo(9));
        Assert.That(bottom.GetComponentsInChildren<BattleActionControlView>(true)
            .Count(action => action.gameObject.name == "Ranged"), Is.EqualTo(1));
        Assert.That(
            bottom.GetComponentsInChildren<BattleActionControlView>(true)
                .Count(action => action.gameObject.name == "EndTurn"),
            Is.EqualTo(1));
        Assert.That(
            bottom.GetComponentsInChildren<BattleActionControlView>(true)
                .Count(action => action.gameObject.name == "PowerStrike" ||
                                 action.gameObject.name == "SweepingBlow" ||
                                 action.gameObject.name == "Rally"),
            Is.EqualTo(3));
        foreach (BattleActionControlView action in
                 bottom.GetComponentsInChildren<BattleActionControlView>(true))
        {
            Assert.That(action.Button, Is.Not.Null);
            Assert.That(action.Button.interactable, Is.False);
            Assert.That(action.DisplayedIcon, Is.Not.Null);
            Assert.That(action.GetComponent<TooltipAnchor>(), Is.Not.Null);
            Assert.That(action.GetComponent<LayoutElement>().minHeight, Is.GreaterThanOrEqualTo(48f));
        }

        BattleActionControlView attack = bottom
            .GetComponentsInChildren<BattleActionControlView>(true)
            .Single(action => action.gameObject.name == "Attack");
        Assert.That(attack.Button, Is.Not.Null);
        Assert.That(attack.DisplayedIcon, Is.Not.Null);
        AbilityDetailsPanelView abilityDetails =
            hud.GetComponentInChildren<AbilityDetailsPanelView>(true);
        Assert.That(abilityDetails, Is.Not.Null);
        Image targetPortrait =
            abilityDetails.transform.Find("TargetPortrait")?.GetComponent<Image>();
        Assert.That(targetPortrait, Is.Not.Null);
        Assert.That(targetPortrait.preserveAspect, Is.True);
        Assert.That(targetPortrait.raycastTarget, Is.False);
        SerializedObject abilitySerialized = new SerializedObject(abilityDetails);
        Assert.That(abilitySerialized.FindProperty("icon").objectReferenceValue,
            Is.SameAs(targetPortrait));
    }

    [Test]
    public void ItemPresentationCatalogUsesStableIdsAndControlledPlaceholder()
    {
        ItemPresentationCatalog catalog =
            AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.Entries.Count, Is.EqualTo(13));
        Assert.That(catalog.Entries.Select(entry => entry.StableId).Distinct().Count(),
            Is.EqualTo(catalog.Entries.Count));

        ItemPresentationRecord record = catalog.Entries[0];
        Assert.That(record.StableId, Is.EqualTo("dev-legacy-test-weapon"));
        Assert.That(record.Category, Is.EqualTo(ItemPresentationCategory.UnknownTest));
        Assert.That(record.IsPlaceholder, Is.True);
        Assert.That(record.PreviewSprite, Is.Null,
            "No unrelated raster should be presented as a verified model preview.");
        Assert.That(record.ModelPrefab, Is.Not.Null);
        Assert.That(EditorUtility.IsPersistent(record.ModelPrefab), Is.True);
        Assert.That(PrefabUtility.GetPrefabAssetType(record.ModelPrefab),
            Is.Not.EqualTo(PrefabAssetType.NotAPrefab));
        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(record.ModelPrefab),
            Is.EqualTo(0));
        foreach (Renderer renderer in record.ModelPrefab.GetComponentsInChildren<Renderer>(true))
            Assert.That(renderer.sharedMaterials.All(material => material != null), Is.True);

        ItemPresentationRecord[] auditedWeapons = catalog.Entries
            .Where(entry => entry.Category == ItemPresentationCategory.Weapon)
            .ToArray();
        Assert.That(auditedWeapons.Length, Is.EqualTo(12));
        Assert.That(auditedWeapons.All(entry => !entry.IsPlaceholder), Is.True);
        Assert.That(auditedWeapons.All(entry => entry.PreviewSprite != null), Is.True);
        Assert.That(auditedWeapons.All(entry => entry.ModelPrefab != null), Is.True);
    }

    [TestCase("Assets/Prefabs/1_voxtree.fbx", "Assets/Prefabs/1_voxtree.png")]
    [TestCase("Assets/Prefabs/deadtree.fbx", "Assets/Prefabs/deadtree.png")]
    [TestCase("Assets/Prefabs/Cactus Prop-0.obj", "Assets/Prefabs/Cactus Prop-0.png")]
    [TestCase("Assets/Prefabs/test.obj", "Assets/Prefabs/test.png")]
    public void LegacyVoxelEnvironmentModelsAreValidMeshesButPaletteTexturesAreNotItemPreviews(
        string modelPath,
        string palettePath)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Assert.That(model, Is.Not.Null);
        Assert.That(EditorUtility.IsPersistent(model), Is.True);
        Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().ToArray();
        Assert.That(meshes.Length, Is.GreaterThan(0));
        Assert.That(meshes.Any(mesh => mesh.vertexCount > 0), Is.True);
        Assert.That(meshes.Any(mesh => mesh.uv != null && mesh.uv.Length > 0), Is.True);

        Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath);
        Assert.That(palette, Is.Not.Null);
        Assert.That(palette.height, Is.EqualTo(1));
        TextureImporter importer = AssetImporter.GetAtPath(palettePath) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.textureType, Is.Not.EqualTo(TextureImporterType.Sprite));

        ItemPresentationCatalog catalog =
            AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(CatalogPath);
        Assert.That(catalog.Entries.All(entry =>
            AssetDatabase.GetAssetPath(entry.ModelPrefab) != modelPath), Is.True);
    }

    [Test]
    public void ItemPreviewCardShowsEmptyStateWithoutException()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/UI/Prefabs/Components/ItemPreviewCard.prefab");
        ItemPresentationCatalog catalog =
            AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(CatalogPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            ItemPreviewCardView view = instance.GetComponent<ItemPreviewCardView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(
                () => view.Render(
                    catalog.Entries[0],
                    new ItemPreviewCardState(true, true, true)),
                Throws.Nothing);
            Assert.That(view.IsEmpty, Is.True);
            Assert.That(view.DisplayedPreview, Is.Not.Null);
            Assert.That(view.IsSelected, Is.True);
            Assert.That(view.IsEquipped, Is.True);
            Assert.That(view.IsDisabled, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static void AssertPortraitImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.sRGBTexture, Is.True);
        Assert.That(importer.alphaIsTransparency, Is.True);
        Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
    }

    private static float AnchorArea(RectTransform rect)
    {
        Assert.That(rect, Is.Not.Null);
        Vector2 size = rect.anchorMax - rect.anchorMin;
        return size.x * size.y;
    }
}
