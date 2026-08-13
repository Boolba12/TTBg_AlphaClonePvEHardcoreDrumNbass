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

public static class EquipmentFoundationInstaller
{
    private const string FirstScene = "Assets/Scenes/first_try.unity";
    private const string BattleScene = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
    private const string TestRoot = "Assets/3DModel/Test";
    private const string DataRoot = "Assets/GameData/Equipment";
    private const string WeaponRoot = DataRoot + "/Weapons";
    private const string CatalogPath = DataRoot + "/DEV_EquipmentDefinitionCatalog.asset";
    private const string ThemePath = "Assets/UI/Themes/PurgatoryUITheme.asset";
    private const string SlotPrefabPath = "Assets/UI/Prefabs/Components/EquipmentSlot.prefab";
    private const string CardPrefabPath = "Assets/UI/Prefabs/Components/ItemPreviewCard.prefab";
    private const string OwnedUiName = "EquipmentV2Root";

    private static readonly Entry[] Entries =
    {
        new("Weapon_07", "test-weapon-07", "Weapon 07", WeaponClass.Other, 2, .05f, .5f, .01f, .01f, .05f),
        new("WP_Dagger_01", "test-wp-dagger-01", "Dagger 01", WeaponClass.Dagger, 1, .10f, 0f, .04f, .04f, .05f),
        new("WP_Estoc_03", "test-wp-estoc-03", "Estoc 03", WeaponClass.Estoc, 2, .10f, 0f, .03f, .02f, .10f),
        new("WP_Falchion_04", "test-wp-falchion-04", "Falchion 04", WeaponClass.Falchion, 3, .05f, .5f, .01f, .03f, .10f),
        new("WP_Greatsword_02", "test-wp-greatsword-02", "Greatsword 02", WeaponClass.Greatsword, 5, .15f, 1f, 0f, .01f, .20f),
        new("WP_Longsword_05", "test-wp-longsword-05", "Longsword 05", WeaponClass.Sword, 3, .10f, .5f, .02f, .02f, .10f),
        new("WP_Mace_04", "test-wp-mace-04", "Mace 04", WeaponClass.Mace, 4, .05f, 1f, 0f, .01f, .10f),
        new("WP_Mace_10", "test-wp-mace-10", "Mace 10", WeaponClass.Mace, 4, .10f, .5f, .01f, .02f, .15f),
        new("WP_Sword_01", "test-wp-sword-01", "Sword 01", WeaponClass.Sword, 2, .10f, .5f, .02f, .02f, .05f),
        new("WP_Sword_02", "test-wp-sword-02", "Sword 02", WeaponClass.Sword, 3, .05f, .5f, .03f, .01f, .10f),
        new("WP_Sword_03", "test-wp-sword-03", "Sword 03", WeaponClass.Sword, 3, .10f, 1f, .01f, .03f, .10f),
        new("WP_Sword_04", "test-wp-sword-04", "Sword 04", WeaponClass.Sword, 4, .05f, .5f, .02f, .02f, .15f)
    };

    [MenuItem("Tools/Purgatory UI/Apply Equipment Foundation Stage")]
    public static void ApplyStage()
    {
        EnsureFolder("Assets/GameData", "Equipment");
        EnsureFolder(DataRoot, "Weapons");
        EquipmentDefinitionCatalog catalog = BuildDefinitions();
        UpgradeFirstScene(catalog);
        UpgradeBattleScene(catalog);
        ConfigureAttackSlotAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("EquipmentFoundationInstaller: 12 canonical weapons, persistent ownership, " +
                  "Pre-Battle Equipment v2, and battle snapshot references configured.");
    }

    private static EquipmentDefinitionCatalog BuildDefinitions()
    {
        List<Weapon> weapons = new List<Weapon>();
        foreach (Entry entry in Entries)
        {
            string path = $"{WeaponRoot}/DEV_{entry.Stem}.asset";
            Weapon weapon = AssetDatabase.LoadAssetAtPath<Weapon>(path);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<Weapon>();
                AssetDatabase.CreateAsset(weapon, path);
            }
            Sprite preview = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{TestRoot}/{entry.Stem}_Preview.png");
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{TestRoot}/{entry.Stem}.fbx");
            Require(preview != null && model != null,
                $"Canonical preview or FBX is missing for {entry.Stem}.");
            weapon.ConfigureDevelopment(entry.Id, entry.Name,
                "Development weapon profile for persistent equipment and tactical combat.",
                entry.Class, preview, model, entry.Damage, entry.Scaling,
                entry.Strength, entry.Accuracy, entry.CritChance, entry.CritDamage);
            EditorUtility.SetDirty(weapon);
            weapons.Add(weapon);
        }

        EquipmentDefinitionCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EquipmentDefinitionCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EquipmentDefinitionCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.ReplaceDevelopmentWeapons(weapons);
        Require(catalog.Validate(out string reason), reason);
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void UpgradeFirstScene(EquipmentDefinitionCatalog catalog)
    {
        Scene scene = EditorSceneManager.OpenScene(FirstScene, OpenSceneMode.Single);
        PreBattlePreparationController controller = FindSingle<PreBattlePreparationController>();
        PreBattlePreparationView view = FindSingle<PreBattlePreparationView>();
        SquadSaveParticipant repository = FindSingle<SquadSaveParticipant>();
        TurnSystem turnSystem = FindSingle<TurnSystem>();
        CommanderPortraitDatabase portraits = GetReference<CommanderPortraitDatabase>(
            controller, "portraitDatabase");
        PurgatoryUITheme theme = AssetDatabase.LoadAssetAtPath<PurgatoryUITheme>(ThemePath);
        Require(portraits != null && theme != null, "Pre-Battle dependencies are missing.");

        EnsureOwnership(repository, catalog);
        repository.ConfigureEquipmentMigration(catalog, true);
        ConfigureEquipmentUi(view, theme);
        controller.Configure(repository, portraits, view, turnSystem, catalog);
        EditorUtility.SetDirty(repository);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void UpgradeBattleScene(EquipmentDefinitionCatalog catalog)
    {
        Scene scene = EditorSceneManager.OpenScene(BattleScene, OpenSceneMode.Single);
        SquadBattleBootstrap bootstrap = FindSingle<SquadBattleBootstrap>();
        SerializedObject serialized = new SerializedObject(bootstrap);
        SerializedProperty property = serialized.FindProperty("equipmentCatalog");
        Require(property != null, "SquadBattleBootstrap equipment catalog field is missing.");
        property.objectReferenceValue = catalog;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        bootstrap.ConfigureDevelopmentFallbackEquipment(catalog);
        EditorUtility.SetDirty(bootstrap);
        foreach (SquadSaveParticipant repository in
                 UnityEngine.Object.FindObjectsByType<SquadSaveParticipant>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            repository.ConfigureEquipmentMigration(catalog, true);
            EditorUtility.SetDirty(repository);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureAttackSlotAssets()
    {
        AttackDefinition basic = AssetDatabase.LoadAssetAtPath<AttackDefinition>(
            "Assets/GameData/Combat/DEV_BasicPhysicalMeleeAttack.asset");
        AttackDefinition power = AssetDatabase.LoadAssetAtPath<AttackDefinition>(
            "Assets/GameData/Abilities/DEV_PowerStrike_Attack.asset");
        AttackDefinition sweep = AssetDatabase.LoadAssetAtPath<AttackDefinition>(
            "Assets/GameData/Abilities/DEV_SweepingBlow_Attack.asset");
        Require(basic != null && power != null && sweep != null,
            "Weapon-aware attack assets are missing.");
        basic.SetDevelopmentWeaponSlot(EquipmentSlotKind.SquadWeapon);
        power.SetDevelopmentWeaponSlot(EquipmentSlotKind.CommanderWeapon);
        sweep.SetDevelopmentWeaponSlot(EquipmentSlotKind.SquadWeapon);
        EditorUtility.SetDirty(basic);
        EditorUtility.SetDirty(power);
        EditorUtility.SetDirty(sweep);
    }

    private static void EnsureOwnership(SquadSaveParticipant repository,
        EquipmentDefinitionCatalog catalog)
    {
        SquadEquipmentService service = new SquadEquipmentService(catalog);
        foreach (SquadData squad in repository.Squads)
        {
            if (squad == null) continue;
            foreach (Weapon weapon in catalog.Weapons)
            {
                if (squad.Equipment.OwnedItems.Any(item =>
                        item != null && item.DefinitionId == weapon.StableId))
                    continue;
                EquipmentOperationResult granted = service.GrantOwnedWeapon(squad,
                    $"{squad.Id}-dev-item-{weapon.StableId}", weapon.StableId);
                Require(granted.Success, granted.Reason);
            }
            if (string.IsNullOrWhiteSpace(squad.Equipment.SquadWeaponInstanceId))
                Require(service.TryEquip(squad,
                    $"{squad.Id}-dev-item-test-wp-sword-01",
                    EquipmentSlotKind.SquadWeapon).Success,
                    "Default Squad Weapon could not be equipped.");
            if (string.IsNullOrWhiteSpace(squad.Equipment.CommanderWeaponInstanceId))
                Require(service.TryEquip(squad,
                    $"{squad.Id}-dev-item-test-wp-dagger-01",
                    EquipmentSlotKind.CommanderWeapon).Success,
                    "Default Commander Weapon could not be equipped.");
            squad.MarkEquipmentSchemaCurrent();
        }
    }

    private static void ConfigureEquipmentUi(PreBattlePreparationView view,
        PurgatoryUITheme theme)
    {
        Transform frame = FindByName(view.transform, "PreparationFrame");
        Require(frame != null, "PreparationFrame is missing.");
        Transform old = frame.Find(OwnedUiName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old.gameObject);

        GameObject root = NewUi(OwnedUiName, frame);
        Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        EquipmentSlotView squad = CreateSlot(root.transform, "SquadWeaponSlot",
            new Vector2(.37f, .20f), new Vector2(.515f, .34f));
        EquipmentSlotView commander = CreateSlot(root.transform, "CommanderWeaponSlot",
            new Vector2(.525f, .20f), new Vector2(.67f, .34f));
        EquipmentSlotView armor = CreateSlot(root.transform, "ArmorSlot",
            new Vector2(.37f, .055f), new Vector2(.515f, .19f));
        EquipmentSlotView accessory = CreateSlot(root.transform, "AccessorySlot",
            new Vector2(.525f, .055f), new Vector2(.67f, .19f));

        GameObject inventory = NewUi("AvailableEquipment", root.transform);
        SetRect(inventory.GetComponent<RectTransform>(), new Vector2(.72f, .20f),
            new Vector2(.955f, .65f));
        Image background = inventory.AddComponent<Image>();
        background.sprite = theme.InsetPanelSprite;
        background.type = Image.Type.Sliced;
        TMP_Text header = CreateText("Header", inventory.transform, theme,
            "AVAILABLE WEAPONS", TextAlignmentOptions.Center, theme.CaptionSize, theme.Gold);
        SetRect(header.rectTransform, new Vector2(.04f, .90f), new Vector2(.96f, .99f));
        RectTransform content = CreateItemScroll(inventory.transform, out ItemPreviewCardView template);
        TMP_Text details = CreateText("EquipmentDetails", root.transform, theme,
            "Select a functional weapon slot.", TextAlignmentOptions.TopLeft,
            theme.CaptionSize - 2f, theme.Marble, true);
        SetRect(details.rectTransform, new Vector2(.72f, .07f), new Vector2(.87f, .19f));
        Button unequip = CreateButton(root.transform, theme, "UnequipButton", "UNEQUIP");
        SetRect(unequip.GetComponent<RectTransform>(), new Vector2(.875f, .08f),
            new Vector2(.955f, .17f));

        view.ConfigureEquipment(squad, commander, armor, accessory, content, template,
            details, unequip);
    }

    private static EquipmentSlotView CreateSlot(Transform parent, string name,
        Vector2 min, Vector2 max)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        Require(prefab != null, "EquipmentSlot prefab is missing.");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = name;
        SetRect(instance.GetComponent<RectTransform>(), min, max);
        EquipmentSlotView view = instance.GetComponent<EquipmentSlotView>();
        Require(view != null, $"{name} has no EquipmentSlotView.");
        return view;
    }

    private static RectTransform CreateItemScroll(Transform parent,
        out ItemPreviewCardView template)
    {
        GameObject scrollRoot = NewUi("WeaponScroll", parent);
        SetRect(scrollRoot.GetComponent<RectTransform>(), new Vector2(.04f, .04f),
            new Vector2(.96f, .88f));
        ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        GameObject viewport = NewUi("Viewport", scrollRoot.transform);
        Stretch(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, .01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        GameObject contentObject = NewUi("Content", viewport.transform);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content;

        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        Require(cardPrefab != null, "ItemPreviewCard prefab is missing.");
        GameObject card = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, content);
        card.name = "EquipmentItemTemplate";
        card.AddComponent<LayoutElement>().preferredHeight = 112f;
        Button button = card.GetComponent<Button>() ?? card.AddComponent<Button>();
        button.targetGraphic = card.GetComponent<Image>();
        template = card.GetComponent<ItemPreviewCardView>();
        SerializedObject serialized = new SerializedObject(template);
        serialized.FindProperty("button").objectReferenceValue = button;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        card.SetActive(false);
        return content;
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
            TextAlignmentOptions.Center, theme.CaptionSize - 2f, theme.Marble);
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
        text.fontSize = size;
        text.color = color;
        text.text = value;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
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

    private static Transform FindByName(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    private static T FindSingle<T>() where T : UnityEngine.Object
    {
        T[] values = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Require(values.Length == 1, $"Expected one {typeof(T).Name}, found {values.Length}.");
        return values[0];
    }

    private static T GetReference<T>(UnityEngine.Object owner, string field)
        where T : UnityEngine.Object
    {
        SerializedObject serialized = new SerializedObject(owner);
        return serialized.FindProperty(field)?.objectReferenceValue as T;
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

    private sealed class Entry
    {
        public Entry(string stem, string id, string name, WeaponClass weaponClass,
            int damage, float scaling, float strength, float accuracy,
            float critChance, float critDamage)
        {
            Stem = stem; Id = id; Name = name; Class = weaponClass; Damage = damage;
            Scaling = scaling; Strength = strength; Accuracy = accuracy;
            CritChance = critChance; CritDamage = critDamage;
        }
        public string Stem { get; }
        public string Id { get; }
        public string Name { get; }
        public WeaponClass Class { get; }
        public int Damage { get; }
        public float Scaling { get; }
        public float Strength { get; }
        public float Accuracy { get; }
        public float CritChance { get; }
        public float CritDamage { get; }
    }
}
#endif
