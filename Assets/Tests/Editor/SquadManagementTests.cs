using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SquadManagementTests
{
    private const string CatalogPath =
        "Assets/GameData/Equipment/DEV_EquipmentDefinitionCatalog.asset";
    private EquipmentDefinitionCatalog catalog;

    [SetUp]
    public void SetUp()
    {
        catalog = AssetDatabase.LoadAssetAtPath<EquipmentDefinitionCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.Validate(out string reason), Is.True, reason);
    }

    [Test]
    public void ArmorAndAccessoryUseCanonicalCatalogAndSlots()
    {
        Assert.That(catalog.Armors.Count, Is.EqualTo(3));
        Assert.That(catalog.Accessories.Count, Is.EqualTo(3));
        Assert.That(catalog.EnumerateDefinitions().Select(item => item.StableId)
            .Distinct().Count(), Is.EqualTo(catalog.DefinitionCount));
        Assert.That(catalog.Armors.All(item => item.SupportsSlot(
            EquipmentSlotKind.Armor)), Is.True);
        Assert.That(catalog.Accessories.All(item => item.SupportsSlot(
            EquipmentSlotKind.Accessory)), Is.True);
    }

    [Test]
    public void ArmorAndAccessoryModifyOnlyTheirDocumentedCalculatedStats()
    {
        SquadData squad = CreateSquad("modifier-contract");
        SquadEquipmentService service = new SquadEquipmentService(catalog);
        ArmorDefinition armor = catalog.Armors[1];
        AccessoryDefinition accessory = catalog.Accessories[2];
        service.GrantOwnedItem(squad, "armor", armor.StableId);
        service.GrantOwnedItem(squad, "accessory", accessory.StableId);
        SquadCalculatedStats before = SquadStatsCalculator.Calculate(squad, catalog);
        Assert.That(service.TryEquip(squad, "armor", EquipmentSlotKind.Armor).Success,
            Is.True);
        Assert.That(service.TryEquip(squad, "accessory",
            EquipmentSlotKind.Accessory).Success, Is.True);
        SquadCalculatedStats after = SquadStatsCalculator.Calculate(squad, catalog);

        Assert.That(after.PhysicalArmor - before.PhysicalArmor,
            Is.EqualTo(armor.PhysicalArmorModifier).Within(.0001f));
        Assert.That(after.MagicalResistance - before.MagicalResistance,
            Is.EqualTo(armor.MagicalResistanceModifier).Within(.0001f));
        Assert.That(after.Accuracy - before.Accuracy,
            Is.EqualTo(accessory.AccuracyModifier).Within(.0001f));
        Assert.That(after.CriticalChance - before.CriticalChance,
            Is.EqualTo(accessory.CriticalChanceModifier).Within(.0001f));
        Assert.That(after.Strength, Is.EqualTo(before.Strength).Within(.0001f));
        Assert.That(after.ActionPoints, Is.EqualTo(before.ActionPoints));
    }

    [Test]
    public void StatPreviewIsCanonicalAndDoesNotMutatePersistentEquipment()
    {
        SquadData squad = CreateSquad("preview");
        SquadEquipmentService service = new SquadEquipmentService(catalog);
        ArmorDefinition armor = catalog.Armors[2];
        service.GrantOwnedItem(squad, "preview-armor", armor.StableId);
        string before = squad.Equipment.ArmorInstanceId;
        EquipmentOperationResult result = service.PreviewEquip(squad, "preview-armor",
            EquipmentSlotKind.Armor, out EquipmentStatComparison comparison);

        Assert.That(result.Success, Is.True, result.Reason);
        Assert.That(squad.Equipment.ArmorInstanceId, Is.EqualTo(before));
        Assert.That(comparison.CandidateStats.PhysicalArmor -
                    comparison.CurrentStats.PhysicalArmor,
            Is.EqualTo(armor.PhysicalArmorModifier).Within(.0001f));
    }

    [Test]
    public void ManagementInventoryUsesOwnedInstancesFiltersAndCompatibility()
    {
        GameObject owner = new GameObject("management-repository");
        try
        {
            SquadSaveParticipant repository = owner.AddComponent<SquadSaveParticipant>();
            SquadData squad = CreateSquad("inventory");
            Assert.That(repository.TryAddSquad(squad, out string reason), Is.True, reason);
            repository.ConfigureEquipmentMigration(catalog, true);
            SquadManagementService management = new SquadManagementService(
                repository, catalog);

            IReadOnlyList<SquadManagementInventoryEntry> armor =
                management.BuildInventory(squad.Id, SquadManagementInventoryFilter.Armor,
                    EquipmentSlotKind.Armor);
            IReadOnlyList<SquadManagementInventoryEntry> accessories =
                management.BuildInventory(squad.Id,
                    SquadManagementInventoryFilter.Accessories,
                    EquipmentSlotKind.Accessory);
            IReadOnlyList<SquadManagementInventoryEntry> weapons =
                management.BuildInventory(squad.Id,
                    SquadManagementInventoryFilter.Weapons, EquipmentSlotKind.Armor);

            Assert.That(armor.Count, Is.EqualTo(3));
            Assert.That(armor.All(entry => entry.Compatible), Is.True);
            Assert.That(accessories.Count, Is.EqualTo(3));
            Assert.That(accessories.All(entry => entry.Compatible), Is.True);
            Assert.That(weapons.Count, Is.EqualTo(12));
            Assert.That(weapons.All(entry => !entry.Compatible), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void PersistentDebuffIsProjectedWithoutInventingCommanderProgression()
    {
        GameObject owner = new GameObject("debuff-repository");
        try
        {
            SquadSaveParticipant repository = owner.AddComponent<SquadSaveParticipant>();
            SquadData squad = CreateSquad("scarred");
            squad.Commander.permanentDebuffs.Add(new PersistentDebuffRecord
            {
                debuffId = "DEV_BattleScar",
                sourceBattleId = "battle-source"
            });
            squad.Commander.permanentDebuffIds.Add("DEV_BattleScar");
            Assert.That(repository.TryAddSquad(squad, out string reason), Is.True, reason);
            PersistentDebuffDefinition scar =
                AssetDatabase.LoadAssetAtPath<PersistentDebuffDefinition>(
                    "Assets/GameData/BattleLifecycle/DEV_BattleScar.asset");
            SquadManagementDetails details = new SquadManagementService(repository,
                catalog, new[] { scar }).BuildDetails(squad.Id);

            Assert.That(details.CommanderId, Is.EqualTo(squad.Commander.id));
            Assert.That(details.Debuffs.Count, Is.EqualTo(1));
            Assert.That(details.Debuffs[0].DisplayName, Is.EqualTo(scar.DisplayName));
            Assert.That(details.Debuffs[0].SourceBattleId, Is.EqualTo("battle-source"));
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void ArmorAccessoryOwnershipAndAssignmentsSurviveSaveRoundTrip()
    {
        GameObject sourceObject = new GameObject("source");
        GameObject targetObject = new GameObject("target");
        try
        {
            SquadSaveParticipant source = sourceObject.AddComponent<SquadSaveParticipant>();
            SquadData squad = CreateSquad("save-management");
            source.TryAddSquad(squad, out _);
            SquadEquipmentService equipment = new SquadEquipmentService(catalog);
            equipment.GrantOwnedItem(squad, "saved-armor", catalog.Armors[0].StableId);
            equipment.GrantOwnedItem(squad, "saved-accessory",
                catalog.Accessories[0].StableId);
            equipment.TryEquip(squad, "saved-armor", EquipmentSlotKind.Armor);
            equipment.TryEquip(squad, "saved-accessory", EquipmentSlotKind.Accessory);

            SquadSaveParticipant target = targetObject.AddComponent<SquadSaveParticipant>();
            target.RestoreState(source.CaptureState());
            SquadData restored = target.GetSquad(squad.Id);
            Assert.That(restored.Equipment.ArmorInstanceId, Is.EqualTo("saved-armor"));
            Assert.That(restored.Equipment.AccessoryInstanceId,
                Is.EqualTo("saved-accessory"));
            Assert.That(SquadStatsCalculator.Calculate(restored, catalog).PhysicalArmor,
                Is.GreaterThan(SquadStatsCalculator.Calculate(CreateSquad("baseline"),
                    catalog).PhysicalArmor));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void BattleSnapshotCopiesArmorAccessoryAndAllPersistentModifiers()
    {
        SquadData squad = CreateSquad("battle-snapshot");
        SquadEquipmentService service = new SquadEquipmentService(catalog);
        ArmorDefinition armor = catalog.Armors[2];
        AccessoryDefinition accessory = catalog.Accessories[0];
        service.GrantOwnedItem(squad, "armor", armor.StableId);
        service.GrantOwnedItem(squad, "accessory", accessory.StableId);
        service.TryEquip(squad, "armor", EquipmentSlotKind.Armor);
        service.TryEquip(squad, "accessory", EquipmentSlotKind.Accessory);

        Assert.That(BattleEquipmentSnapshot.TryCreate(squad, catalog,
            out BattleEquipmentSnapshot snapshot, out string reason), Is.True, reason);
        Assert.That(snapshot.ArmorDefinitionId, Is.EqualTo(armor.StableId));
        Assert.That(snapshot.AccessoryDefinitionId, Is.EqualTo(accessory.StableId));
        Assert.That(snapshot.StatModifiers.physicalArmor,
            Is.EqualTo(armor.PhysicalArmorModifier).Within(.0001f));
        Assert.That(snapshot.StatModifiers.resolve,
            Is.EqualTo(accessory.ResolveModifier).Within(.0001f));
    }

    [Test]
    public void FirstTryContainsOneProductionManagementOwnerAndOneEventSystem()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/first_try.unity");
        SquadManagementController[] controllers = Object.FindObjectsByType<
            SquadManagementController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        SquadManagementView[] views = Object.FindObjectsByType<SquadManagementView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.That(controllers.Length, Is.EqualTo(1));
        Assert.That(views.Length, Is.EqualTo(1));
        Assert.That(eventSystems.Length, Is.EqualTo(1));
        Assert.That(controllers[0].OpenButton, Is.Not.Null);
        Assert.That(controllers[0].OpenButton.GetComponentInParent<Canvas>(), Is.Not.Null);
        Assert.That(views[0].IsVisible, Is.False);
    }

    [TestCase(1920, 1080)]
    [TestCase(2560, 1440)]
    [TestCase(1366, 768)]
    public void ManagementLayoutUsesResponsiveNonOverlappingAnchors(int width, int height)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/first_try.unity");
        SquadManagementView view = Object.FindAnyObjectByType<SquadManagementView>(
            FindObjectsInactive.Include);
        RectTransform root = view.GetComponent<RectTransform>();
        RectTransform frame = FindRect(root, "ManagementFrame");
        RectTransform left = FindRect(frame, "RosterPanel");
        RectTransform center = FindRect(frame, "SquadDetailPanel");
        RectTransform right = FindRect(frame, "InventoryPanel");

        Assert.That(root.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(root.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(left.anchorMax.x, Is.LessThan(center.anchorMin.x));
        Assert.That(center.anchorMax.x, Is.LessThan(right.anchorMin.x));
        Assert.That((left.anchorMax.x - left.anchorMin.x) * width,
            Is.GreaterThanOrEqualTo(320f));
        Assert.That((center.anchorMax.x - center.anchorMin.x) * width,
            Is.GreaterThanOrEqualTo(500f));
        Assert.That((right.anchorMax.x - right.anchorMin.x) * width,
            Is.GreaterThanOrEqualTo(430f));
        Assert.That((frame.anchorMax.y - frame.anchorMin.y) * height,
            Is.GreaterThan(690f));
    }

    private static RectTransform FindRect(Transform root, string name)
    {
        RectTransform result = root.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(candidate => candidate.name == name);
        Assert.That(result, Is.Not.Null, name);
        return result;
    }

    private static SquadData CreateSquad(string id)
    {
        CommanderData commander = new CommanderData
        {
            id = id + "-commander",
            race = CommanderRace.Human,
            commanderPortraitId = "human-commander-portrait-01",
            baseStats = new SquadBaseStats
            {
                hp = 20,
                actionPoints = 8,
                strength = 5,
                dexterity = 4,
                initiative = 6,
                accuracy = .1f,
                criticalChance = .05f,
                physicalArmor = .02f,
                magicalResistance = .03f,
                resolve = 3,
                criticalDamage = 1.5f,
                morale = 50
            }
        };
        return new SquadData(id, commander, new[]
        {
            new WarriorData { id = id + "-warrior", maxHP = 10,
                strength = 2, dexterity = 1 }
        });
    }
}
