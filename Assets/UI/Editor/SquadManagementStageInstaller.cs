#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SquadManagementStageInstaller
{
    private const string ScenePath = "Assets/Scenes/first_try.unity";
    private const string CatalogPath =
        "Assets/GameData/Equipment/DEV_EquipmentDefinitionCatalog.asset";
    private const string ThemePath = "Assets/UI/Themes/PurgatoryUITheme.asset";
    private const string ScarPath = "Assets/GameData/BattleLifecycle/DEV_BattleScar.asset";
    private const string ArmorFolder = "Assets/GameData/Equipment/Armor";
    private const string AccessoryFolder = "Assets/GameData/Equipment/Accessories";
    private const string SlotPrefabPath =
        "Assets/UI/Prefabs/Components/EquipmentSlot.prefab";
    private const string ItemPrefabPath =
        "Assets/UI/Prefabs/Components/ItemPreviewCard.prefab";
    private const string RootName = "SquadManagementV1Root";
    private const string OwnerName = "SquadManagementOwner";
    private const string OpenButtonName = "OpenSquadsButton";

    [MenuItem("Tools/Purgatory UI/Apply Squad Management v1 Stage")]
    public static void ApplyStage()
    {
        EnsureFolder("Assets/GameData/Equipment", "Armor");
        EnsureFolder("Assets/GameData/Equipment", "Accessories");
        PurgatoryUITheme theme = AssetDatabase.LoadAssetAtPath<PurgatoryUITheme>(ThemePath);
        EquipmentDefinitionCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EquipmentDefinitionCatalog>(CatalogPath);
        Require(theme != null && catalog != null,
            "Theme or canonical equipment catalog is missing.");
        BuildDefinitions(catalog, theme.IconPlaceholderSprite);
        UpgradeScene(catalog, theme);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SquadManagementStageInstaller: persistent Armor/Accessory definitions, " +
                  "shared inventory and production Squad Management UI configured.");
    }

    private static void BuildDefinitions(EquipmentDefinitionCatalog catalog, Sprite fallback)
    {
        List<ArmorDefinition> armors = new()
        {
            BuildArmor("DEV_ScoutArmor", "DEV Scout Armor",
                "Light development armor: modest physical protection.", fallback, .08f, .02f),
            BuildArmor("DEV_WardenArmor", "DEV Warden Armor",
                "Balanced development armor for physical and magical mitigation.", fallback,
                .16f, .07f),
            BuildArmor("DEV_BastionArmor", "DEV Bastion Armor",
                "Heavy development armor with strong physical absorption.", fallback,
                .25f, .10f)
        };
        List<AccessoryDefinition> accessories = new()
        {
            BuildAccessory("DEV_IronVow", "DEV Iron Vow",
                "A persistent resolve accessory for foundation validation.", fallback,
                2f, 0f, 0f, 0f),
            BuildAccessory("DEV_ScoutSigil", "DEV Scout Sigil",
                "A development initiative accessory.", fallback,
                0f, 2f, 0f, 0f),
            BuildAccessory("DEV_HawkeyeCharm", "DEV Hawkeye Charm",
                "A development accuracy and critical-chance accessory.", fallback,
                0f, 0f, .05f, .03f)
        };
        catalog.ReplaceDevelopmentDefinitions(catalog.Weapons.ToList(), armors, accessories);
        Require(catalog.Validate(out string reason), reason);
        EditorUtility.SetDirty(catalog);
    }

    private static ArmorDefinition BuildArmor(string id, string label, string description,
        Sprite preview, float physicalArmor, float magicalResistance)
    {
        string path = $"{ArmorFolder}/{id}.asset";
        ArmorDefinition definition = AssetDatabase.LoadAssetAtPath<ArmorDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<ArmorDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }
        definition.ConfigureDevelopment(id, label, description, preview,
            physicalArmor, magicalResistance);
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static AccessoryDefinition BuildAccessory(string id, string label,
        string description, Sprite preview, float resolve, float initiative,
        float accuracy, float criticalChance)
    {
        string path = $"{AccessoryFolder}/{id}.asset";
        AccessoryDefinition definition =
            AssetDatabase.LoadAssetAtPath<AccessoryDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<AccessoryDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }
        definition.ConfigureDevelopment(id, label, description, preview, resolve,
            initiative, accuracy, criticalChance);
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static void UpgradeScene(EquipmentDefinitionCatalog catalog,
        PurgatoryUITheme theme)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        PreBattlePreparationView preBattleView = FindSingle<PreBattlePreparationView>();
        PreBattlePreparationController preBattleController =
            FindSingle<PreBattlePreparationController>();
        SquadSaveParticipant repository = FindSingle<SquadSaveParticipant>();
        TurnSystem turnSystem = FindSingle<TurnSystem>();
        SaveSystemBehaviour saveSystem = FindSingle<SaveSystemBehaviour>();
        Canvas canvas = preBattleView.GetComponentInParent<Canvas>(true);
        Require(canvas != null, "The existing Pre-Battle Canvas is missing.");
        CommanderPortraitDatabase portraits = GetReference<CommanderPortraitDatabase>(
            preBattleController, "portraitDatabase");
        PersistentDebuffDefinition scar =
            AssetDatabase.LoadAssetAtPath<PersistentDebuffDefinition>(ScarPath);
        Require(portraits != null && scar != null,
            "Portrait database or DEV_BattleScar is missing.");

        DestroyOwned(canvas.transform, RootName);
        DestroyOwned(canvas.transform, OwnerName);
        DestroyOwned(canvas.transform, OpenButtonName);
        EnsureOwnership(repository, catalog);
        repository.ConfigureEquipmentMigration(catalog, true);
        repository.ConfigureDevelopmentReserve(true, 8);

        Button openButton = CreateButton(canvas.transform, theme, OpenButtonName, "SQUADS");
        SetRect(openButton.GetComponent<RectTransform>(), new Vector2(.015f, .915f),
            new Vector2(.13f, .98f));

        GameObject root = NewUi(RootName, canvas.transform);
        Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        Image overlay = root.AddComponent<Image>();
        overlay.color = theme.Overlay;
        overlay.raycastTarget = true;
        CanvasGroup blocker = root.AddComponent<CanvasGroup>();
        SquadManagementView view = root.AddComponent<SquadManagementView>();

        GameObject frame = CreatePanel("ManagementFrame", root.transform, theme,
            new Vector2(.025f, .035f), new Vector2(.975f, .965f), true);
        TMP_Text title = CreateText("Title", frame.transform, theme,
            "SQUAD MANAGEMENT", TextAlignmentOptions.Center, theme.HeadingSize,
            theme.Gold);
        SetRect(title.rectTransform, new Vector2(.02f, .925f), new Vector2(.98f, .99f));
        TMP_Text subtitle = CreateText("Subtitle", frame.transform, theme,
            "Persistent roster, equipment and calculated campaign state",
            TextAlignmentOptions.Center, theme.CaptionSize - 2f, theme.TextSecondary);
        SetRect(subtitle.rectTransform, new Vector2(.02f, .89f), new Vector2(.98f, .93f));

        GameObject left = CreatePanel("RosterPanel", frame.transform, theme,
            new Vector2(.015f, .075f), new Vector2(.265f, .88f));
        GameObject center = CreatePanel("SquadDetailPanel", frame.transform, theme,
            new Vector2(.275f, .075f), new Vector2(.655f, .88f));
        GameObject right = CreatePanel("InventoryPanel", frame.transform, theme,
            new Vector2(.665f, .075f), new Vector2(.985f, .88f));

        BuildRoster(left.transform, preBattleView, theme, out RectTransform squadContent,
            out PreBattleSquadCardView squadTemplate, out TMP_Text emptyRoster,
            out Image portrait, out TMP_Text squadTitle, out TMP_Text commander,
            out TMP_Text status, out TMP_Text stats);
        BuildDetails(center.transform, theme, out TMP_Text composition,
            out TMP_Text debuffs, out RectTransform assignedContent,
            out WarriorRosterCardView assignedTemplate, out TMP_Text compositionPreview,
            out EquipmentSlotView squadWeapon,
            out EquipmentSlotView commanderWeapon, out EquipmentSlotView armor,
            out EquipmentSlotView accessory);
        BuildInventory(right.transform, theme, out RectTransform inventoryContent,
            out ItemPreviewCardView itemTemplate, out Button allFilter,
            out Button weaponsFilter, out Button armorFilter,
            out Button accessoriesFilter, out TMP_Text itemDetails,
            out TMP_Text comparison, out Button equip, out Button unequip,
            out Button save, out Button close, out TMP_Text operation,
            out RectTransform reserveContent, out WarriorRosterCardView reserveTemplate,
            out TMP_Text reserveCount, out Button addWarrior,
            out Button removeWarrior, out Button rotateWarrior);

        SerializedObject serialized = new SerializedObject(view);
        Set(serialized, "panelRoot", root);
        Set(serialized, "inputBlocker", blocker);
        Set(serialized, "squadListContent", squadContent);
        Set(serialized, "squadCardTemplate", squadTemplate);
        Set(serialized, "emptyRosterLabel", emptyRoster);
        Set(serialized, "commanderPortrait", portrait);
        Set(serialized, "squadTitle", squadTitle);
        Set(serialized, "commanderSummary", commander);
        Set(serialized, "statusSummary", status);
        Set(serialized, "calculatedStats", stats);
        Set(serialized, "compositionSummary", composition);
        Set(serialized, "debuffSummary", debuffs);
        Set(serialized, "assignedWarriorContent", assignedContent);
        Set(serialized, "assignedWarriorTemplate", assignedTemplate);
        Set(serialized, "reserveWarriorContent", reserveContent);
        Set(serialized, "reserveWarriorTemplate", reserveTemplate);
        Set(serialized, "reserveCountLabel", reserveCount);
        Set(serialized, "compositionStatPreview", compositionPreview);
        Set(serialized, "addWarriorButton", addWarrior);
        Set(serialized, "removeWarriorButton", removeWarrior);
        Set(serialized, "rotateWarriorButton", rotateWarrior);
        Set(serialized, "squadWeaponSlot", squadWeapon);
        Set(serialized, "commanderWeaponSlot", commanderWeapon);
        Set(serialized, "armorSlot", armor);
        Set(serialized, "accessorySlot", accessory);
        Set(serialized, "inventoryContent", inventoryContent);
        Set(serialized, "inventoryItemTemplate", itemTemplate);
        Set(serialized, "allFilterButton", allFilter);
        Set(serialized, "weaponsFilterButton", weaponsFilter);
        Set(serialized, "armorFilterButton", armorFilter);
        Set(serialized, "accessoriesFilterButton", accessoriesFilter);
        Set(serialized, "itemDetails", itemDetails);
        Set(serialized, "statComparison", comparison);
        Set(serialized, "equipButton", equip);
        Set(serialized, "unequipButton", unequip);
        Set(serialized, "saveButton", save);
        Set(serialized, "closeButton", close);
        Set(serialized, "operationStatus", operation);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject owner = new GameObject(OwnerName);
        owner.transform.SetParent(canvas.transform, false);
        SquadManagementController controller = owner.AddComponent<SquadManagementController>();
        controller.Configure(openButton, view, repository, catalog, portraits,
            new[] { scar }, turnSystem, saveSystem);
        root.SetActive(false);

        EditorUtility.SetDirty(repository);
        EditorUtility.SetDirty(view);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildRoster(Transform parent, PreBattlePreparationView preBattle,
        PurgatoryUITheme theme, out RectTransform content,
        out PreBattleSquadCardView template, out TMP_Text empty,
        out Image portrait, out TMP_Text squadTitle, out TMP_Text commander,
        out TMP_Text status, out TMP_Text stats)
    {
        CreateSectionHeader(parent, theme, "PLAYER SQUADS", .94f, 1f);
        content = CreateScroll(parent, theme, new Vector2(.035f, .50f),
            new Vector2(.965f, .935f));
        PreBattleSquadCardView source = GetReference<PreBattleSquadCardView>(
            preBattle, "squadCardTemplate");
        Require(source != null, "Existing Pre-Battle squad card template is missing.");
        GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, content);
        clone.name = "ManagementSquadCardTemplate";
        clone.AddComponent<LayoutElement>().preferredHeight = 122f;
        clone.SetActive(false);
        template = clone.GetComponent<PreBattleSquadCardView>();
        empty = CreateText("EmptyRoster", parent, theme, "No persistent squads",
            TextAlignmentOptions.Center, theme.CaptionSize, theme.TextSecondary);
        SetRect(empty.rectTransform, new Vector2(.05f, .68f), new Vector2(.95f, .76f));
        empty.gameObject.SetActive(false);

        portrait = CreateImage("CommanderPortrait", parent, theme.PortraitFrameSprite,
            theme.SurfaceRaised);
        SetRect(portrait.rectTransform, new Vector2(.055f, .33f),
            new Vector2(.36f, .485f));
        portrait.preserveAspect = true;
        squadTitle = CreateText("SquadTitle", parent, theme, "No squad selected",
            TextAlignmentOptions.TopLeft, theme.BodySize, theme.Gold);
        SetRect(squadTitle.rectTransform, new Vector2(.39f, .43f),
            new Vector2(.95f, .49f));
        commander = CreateText("CommanderSummary", parent, theme, "Commander -",
            TextAlignmentOptions.TopLeft, theme.CaptionSize - 2f, theme.TextPrimary, true);
        SetRect(commander.rectTransform, new Vector2(.39f, .33f),
            new Vector2(.95f, .43f));
        status = CreateText("StatusSummary", parent, theme, "Status -",
            TextAlignmentOptions.TopLeft, theme.CaptionSize - 2f, theme.TextSecondary);
        SetRect(status.rectTransform, new Vector2(.055f, .27f), new Vector2(.95f, .32f));
        stats = CreateText("CalculatedStats", parent, theme, "Calculated stats unavailable.",
            TextAlignmentOptions.TopLeft, theme.CaptionSize - 3f, theme.TextPrimary, true);
        SetRect(stats.rectTransform, new Vector2(.055f, .025f), new Vector2(.95f, .265f));
    }

    private static void BuildDetails(Transform parent, PurgatoryUITheme theme,
        out TMP_Text composition, out TMP_Text debuffs,
        out RectTransform assignedContent, out WarriorRosterCardView assignedTemplate,
        out TMP_Text compositionPreview,
        out EquipmentSlotView squadWeapon, out EquipmentSlotView commanderWeapon,
        out EquipmentSlotView armor, out EquipmentSlotView accessory)
    {
        CreateSectionHeader(parent, theme, "COMPOSITION & EQUIPMENT", .94f, 1f);
        composition = CreateText("Composition", parent, theme,
            "COMPOSITION\nNo squad selected.", TextAlignmentOptions.TopLeft,
            theme.CaptionSize - 3f, theme.TextPrimary, true);
        SetRect(composition.rectTransform, new Vector2(.035f, .84f),
            new Vector2(.965f, .925f));
        assignedContent = CreateScroll(parent, theme, new Vector2(.035f, .50f),
            new Vector2(.965f, .83f));
        assignedContent.parent.parent.name = "AssignedWarriorsScroll";
        assignedTemplate = CreateWarriorTemplate(assignedContent, theme,
            "AssignedWarriorTemplate");
        assignedTemplate.gameObject.SetActive(false);
        compositionPreview = CreateText("CompositionStatPreview", parent, theme,
            "Select an Assigned or Reserve Warrior to preview calculated changes.",
            TextAlignmentOptions.TopLeft, theme.CaptionSize - 4f,
            theme.TextSecondary, true);
        SetRect(compositionPreview.rectTransform, new Vector2(.035f, .385f),
            new Vector2(.965f, .49f));
        debuffs = CreateText("PersistentDebuffs", parent, theme,
            "PERSISTENT DEBUFFS\nNone", TextAlignmentOptions.TopLeft,
            theme.CaptionSize - 4f, theme.TextSecondary, true);
        SetRect(debuffs.rectTransform, new Vector2(.035f, .315f),
            new Vector2(.965f, .38f));
        squadWeapon = CreateSlot(parent, "SquadWeaponSlot",
            new Vector2(.035f, .17f), new Vector2(.49f, .305f));
        commanderWeapon = CreateSlot(parent, "CommanderWeaponSlot",
            new Vector2(.51f, .17f), new Vector2(.965f, .305f));
        armor = CreateSlot(parent, "ArmorSlot",
            new Vector2(.035f, .025f), new Vector2(.49f, .16f));
        accessory = CreateSlot(parent, "AccessorySlot",
            new Vector2(.51f, .025f), new Vector2(.965f, .16f));
    }

    private static void BuildInventory(Transform parent, PurgatoryUITheme theme,
        out RectTransform content, out ItemPreviewCardView itemTemplate,
        out Button all, out Button weapons, out Button armor, out Button accessories,
        out TMP_Text details, out TMP_Text comparison, out Button equip,
        out Button unequip, out Button save, out Button close, out TMP_Text operation,
        out RectTransform reserveContent, out WarriorRosterCardView reserveTemplate,
        out TMP_Text reserveCount, out Button addWarrior, out Button removeWarrior,
        out Button rotateWarrior)
    {
        CreateSectionHeader(parent, theme, "RESERVE & OWNED INVENTORY", .94f, 1f);
        reserveCount = CreateText("ReserveCount", parent, theme, "RESERVE  0",
            TextAlignmentOptions.TopLeft, theme.CaptionSize - 2f, theme.Gold);
        SetRect(reserveCount.rectTransform, new Vector2(.035f, .90f),
            new Vector2(.965f, .94f));
        reserveContent = CreateScroll(parent, theme, new Vector2(.03f, .61f),
            new Vector2(.97f, .895f));
        reserveContent.parent.parent.name = "ReserveWarriorsScroll";
        reserveTemplate = CreateWarriorTemplate(reserveContent, theme,
            "ReserveWarriorTemplate");
        reserveTemplate.gameObject.SetActive(false);

        addWarrior = CreateButton(parent, theme, "AddWarriorButton", "ADD");
        removeWarrior = CreateButton(parent, theme, "RemoveWarriorButton", "REMOVE");
        rotateWarrior = CreateButton(parent, theme, "RotateWarriorButton", "ROTATE");
        SetRect(addWarrior.GetComponent<RectTransform>(), new Vector2(.03f, .54f),
            new Vector2(.34f, .60f));
        SetRect(removeWarrior.GetComponent<RectTransform>(), new Vector2(.345f, .54f),
            new Vector2(.655f, .60f));
        SetRect(rotateWarrior.GetComponent<RectTransform>(), new Vector2(.66f, .54f),
            new Vector2(.97f, .60f));

        all = CreateButton(parent, theme, "FilterAll", "ALL");
        weapons = CreateButton(parent, theme, "FilterWeapons", "WEAPONS");
        armor = CreateButton(parent, theme, "FilterArmor", "ARMOR");
        accessories = CreateButton(parent, theme, "FilterAccessories", "ACCESSORIES");
        SetRect(all.GetComponent<RectTransform>(), new Vector2(.03f, .475f), new Vector2(.255f, .53f));
        SetRect(weapons.GetComponent<RectTransform>(), new Vector2(.265f, .475f), new Vector2(.50f, .53f));
        SetRect(armor.GetComponent<RectTransform>(), new Vector2(.51f, .475f), new Vector2(.735f, .53f));
        SetRect(accessories.GetComponent<RectTransform>(), new Vector2(.745f, .475f), new Vector2(.97f, .53f));

        content = CreateScroll(parent, theme, new Vector2(.03f, .255f),
            new Vector2(.97f, .465f));
        content.parent.parent.name = "OwnedInventoryScroll";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);
        Require(prefab != null, "ItemPreviewCard prefab is missing.");
        GameObject card = (GameObject)PrefabUtility.InstantiatePrefab(prefab, content);
        card.name = "ManagementInventoryItemTemplate";
        card.AddComponent<LayoutElement>().preferredHeight = 104f;
        Button itemButton = card.GetComponent<Button>() ?? card.AddComponent<Button>();
        itemButton.targetGraphic = card.GetComponent<Image>();
        itemTemplate = card.GetComponent<ItemPreviewCardView>();
        SerializedObject itemSerialized = new SerializedObject(itemTemplate);
        Set(itemSerialized, "button", itemButton);
        itemSerialized.ApplyModifiedPropertiesWithoutUndo();
        card.SetActive(false);

        details = CreateText("ItemDetails", parent, theme, "Select an owned item.",
            TextAlignmentOptions.TopLeft, theme.CaptionSize - 3f, theme.TextPrimary, true);
        SetRect(details.rectTransform, new Vector2(.035f, .19f), new Vector2(.965f, .25f));
        comparison = CreateText("StatComparison", parent, theme,
            "Select a compatible item to compare calculated stats.",
            TextAlignmentOptions.TopLeft, theme.CaptionSize - 4f, theme.TextSecondary, true);
        SetRect(comparison.rectTransform, new Vector2(.035f, .105f), new Vector2(.965f, .185f));

        equip = CreateButton(parent, theme, "EquipButton", "EQUIP");
        unequip = CreateButton(parent, theme, "UnequipButton", "UNEQUIP");
        save = CreateButton(parent, theme, "SaveButton", "SAVE");
        close = CreateButton(parent, theme, "CloseButton", "CLOSE");
        SetRect(equip.GetComponent<RectTransform>(), new Vector2(.03f, .045f), new Vector2(.265f, .10f));
        SetRect(unequip.GetComponent<RectTransform>(), new Vector2(.275f, .045f), new Vector2(.51f, .10f));
        SetRect(save.GetComponent<RectTransform>(), new Vector2(.52f, .045f), new Vector2(.745f, .10f));
        SetRect(close.GetComponent<RectTransform>(), new Vector2(.755f, .045f), new Vector2(.97f, .10f));
        operation = CreateText("OperationStatus", parent, theme, string.Empty,
            TextAlignmentOptions.Center, theme.CaptionSize - 4f, theme.TextSecondary, true);
        SetRect(operation.rectTransform, new Vector2(.035f, .005f), new Vector2(.965f, .042f));
    }

    private static WarriorRosterCardView CreateWarriorTemplate(
        Transform parent,
        PurgatoryUITheme theme,
        string name)
    {
        GameObject root = NewUi(name, parent);
        root.AddComponent<LayoutElement>().preferredHeight = 64f;
        Image background = root.AddComponent<Image>();
        background.sprite = theme.ButtonSprite;
        background.type = Image.Type.Sliced;
        background.color = theme.SurfaceRaised;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        TMP_Text identity = CreateText("Identity", root.transform, theme, "Warrior",
            TextAlignmentOptions.TopLeft, theme.CaptionSize - 3f, theme.Marble);
        SetRect(identity.rectTransform, new Vector2(.035f, .52f), new Vector2(.68f, .94f));
        TMP_Text stats = CreateText("Stats", root.transform, theme,
            "HP -  STR -  DEX -", TextAlignmentOptions.BottomLeft,
            theme.CaptionSize - 5f, theme.TextSecondary);
        SetRect(stats.rectTransform, new Vector2(.035f, .08f), new Vector2(.76f, .52f));
        TMP_Text status = CreateText("Status", root.transform, theme, "RESERVE",
            TextAlignmentOptions.Center, theme.CaptionSize - 5f, theme.Emerald);
        SetRect(status.rectTransform, new Vector2(.76f, .18f), new Vector2(.965f, .82f));
        Image selected = CreateImage("SelectedFrame", root.transform,
            theme.SelectedFrameSprite, Color.white);
        Stretch(selected.rectTransform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        selected.raycastTarget = false;
        selected.gameObject.SetActive(false);
        WarriorRosterCardView view = root.AddComponent<WarriorRosterCardView>();
        view.Configure(button, identity, stats, status, selected.gameObject);
        return view;
    }

    private static RectTransform CreateScroll(Transform parent, PurgatoryUITheme theme,
        Vector2 min, Vector2 max)
    {
        GameObject scrollRoot = NewUi("Scroll", parent);
        SetRect(scrollRoot.GetComponent<RectTransform>(), min, max);
        scrollRoot.AddComponent<Image>().color = new Color(0, 0, 0, .12f);
        ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        GameObject viewport = NewUi("Viewport", scrollRoot.transform);
        Stretch(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(4, 4), new Vector2(-4, -4));
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, .01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        GameObject contentObject = NewUi("Content", viewport.transform);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content;
        return content;
    }

    private static EquipmentSlotView CreateSlot(Transform parent, string name,
        Vector2 min, Vector2 max)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        Require(prefab != null, "EquipmentSlot prefab is missing.");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = name;
        SetRect(instance.GetComponent<RectTransform>(), min, max);
        return instance.GetComponent<EquipmentSlotView>();
    }

    private static void EnsureOwnership(SquadSaveParticipant repository,
        EquipmentDefinitionCatalog catalog)
    {
        SquadEquipmentService service = new SquadEquipmentService(catalog);
        foreach (SquadData squad in repository.Squads)
        {
            if (squad == null) continue;
            foreach (EquipmentItemDefinition definition in catalog.EnumerateDefinitions())
            {
                bool exists = squad.Equipment.OwnedItems.Any(item => item != null &&
                    item.DefinitionId == definition.StableId);
                if (exists) continue;
                EquipmentOperationResult result = service.GrantOwnedItem(squad,
                    $"{squad.Id}-dev-item-{definition.StableId}", definition.StableId);
                Require(result.Success, result.Reason);
            }
            squad.MarkEquipmentSchemaCurrent();
        }
    }

    private static GameObject CreatePanel(string name, Transform parent,
        PurgatoryUITheme theme, Vector2 min, Vector2 max, bool outer = false)
    {
        GameObject panel = NewUi(name, parent);
        SetRect(panel.GetComponent<RectTransform>(), min, max);
        Image image = panel.AddComponent<Image>();
        image.sprite = outer ? theme.OuterFrameSprite : theme.InsetPanelSprite;
        image.type = Image.Type.Sliced;
        image.color = outer ? theme.DarkSteel : theme.SurfaceInset;
        return panel;
    }

    private static void CreateSectionHeader(Transform parent, PurgatoryUITheme theme,
        string label, float minY, float maxY)
    {
        TMP_Text text = CreateText("Header", parent, theme, label,
            TextAlignmentOptions.Center, theme.CaptionSize, theme.Gold);
        SetRect(text.rectTransform, new Vector2(.03f, minY), new Vector2(.97f, maxY));
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject root = NewUi(name, parent);
        Image image = root.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        return image;
    }

    private static Button CreateButton(Transform parent, PurgatoryUITheme theme,
        string name, string label)
    {
        GameObject root = NewUi(name, parent);
        Image image = root.AddComponent<Image>();
        image.sprite = theme.ButtonSprite;
        image.type = Image.Type.Sliced;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText("Label", root.transform, theme, label,
            TextAlignmentOptions.Center, theme.CaptionSize - 3f, theme.Marble);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent,
        PurgatoryUITheme theme, string value, TextAlignmentOptions alignment,
        float size, Color color, bool wrap = false)
    {
        GameObject root = NewUi(name, parent);
        TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
        text.font = theme.PrimaryFont;
        text.fontSize = Mathf.Max(10f, size);
        text.color = color;
        text.text = value;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static GameObject NewUi(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.layer = LayerMask.NameToLayer("UI");
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max) =>
        Stretch(rect, min, max, Vector2.zero, Vector2.zero);

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static void DestroyOwned(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static void Set(SerializedObject serialized, string property,
        UnityEngine.Object value)
    {
        SerializedProperty target = serialized.FindProperty(property);
        Require(target != null, $"Serialized field '{property}' is missing.");
        target.objectReferenceValue = value;
    }

    private static T GetReference<T>(UnityEngine.Object owner, string field)
        where T : UnityEngine.Object
    {
        SerializedObject serialized = new SerializedObject(owner);
        return serialized.FindProperty(field)?.objectReferenceValue as T;
    }

    private static T FindSingle<T>() where T : UnityEngine.Object
    {
        T[] values = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Require(values.Length == 1,
            $"Expected one {typeof(T).Name}, found {values.Length}.");
        return values[0];
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
