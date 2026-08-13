using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EquipmentFoundationTests
{
    private const string CatalogPath =
        "Assets/GameData/Equipment/DEV_EquipmentDefinitionCatalog.asset";
    private EquipmentDefinitionCatalog catalog;

    [SetUp]
    public void SetUp()
    {
        catalog = AssetDatabase.LoadAssetAtPath<EquipmentDefinitionCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);
    }

    [Test]
    public void CanonicalCatalogContainsTwelveUniqueValidatedWeaponsAndSharedDefinitions()
    {
        Assert.That(catalog.Validate(out string reason), Is.True, reason);
        Assert.That(catalog.Weapons.Count, Is.EqualTo(12));
        Assert.That(catalog.Armors.Count, Is.EqualTo(3));
        Assert.That(catalog.Accessories.Count, Is.EqualTo(3));
        Assert.That(catalog.DefinitionCount, Is.EqualTo(18));
        Assert.That(catalog.Weapons.Select(weapon => weapon.StableId).Distinct().Count(),
            Is.EqualTo(12));
        foreach (Weapon weapon in catalog.Weapons)
        {
            Assert.That(weapon.PreviewSprite, Is.Not.Null, weapon.StableId);
            Assert.That(weapon.WeaponPrefab, Is.Not.Null, weapon.StableId);
            StringAssert.EndsWith("_Preview.png",
                AssetDatabase.GetAssetPath(weapon.PreviewSprite));
            StringAssert.EndsWith(".fbx", AssetDatabase.GetAssetPath(weapon.WeaponPrefab));
            Assert.That(weapon.SupportsSlot(EquipmentSlotKind.SquadWeapon), Is.True);
            Assert.That(weapon.SupportsSlot(EquipmentSlotKind.CommanderWeapon), Is.True);
        }
    }

    [Test]
    public void UniqueInstanceCannotOccupyTwoSlotsAndInvalidEquipIsAtomic()
    {
        SquadData squad = CreateSquad("atomic");
        SquadEquipmentService service = new SquadEquipmentService(catalog);
        Weapon weapon = catalog.Weapons[0];
        Assert.That(service.GrantOwnedWeapon(squad, "instance-1", weapon.StableId).Success,
            Is.True);
        Assert.That(service.TryEquip(squad, "instance-1",
            EquipmentSlotKind.SquadWeapon).Success, Is.True);

        EquipmentOperationResult duplicate = service.TryEquip(squad, "instance-1",
            EquipmentSlotKind.CommanderWeapon);
        Assert.That(duplicate.Failure,
            Is.EqualTo(EquipmentOperationFailure.AlreadyEquippedElsewhere));
        Assert.That(squad.Equipment.SquadWeaponInstanceId, Is.EqualTo("instance-1"));
        Assert.That(squad.Equipment.CommanderWeaponInstanceId, Is.Null.Or.Empty);

        EquipmentOperationResult missing = service.TryEquip(squad, "missing",
            EquipmentSlotKind.SquadWeapon);
        Assert.That(missing.Success, Is.False);
        Assert.That(squad.Equipment.SquadWeaponInstanceId, Is.EqualTo("instance-1"));
    }

    [Test]
    public void WeaponCannotBeEquippedIntoFunctionalArmorOrAccessorySlots()
    {
        SquadData squad = CreateSquad("contracts");
        SquadEquipmentService service = new SquadEquipmentService(catalog);
        Assert.That(service.GrantOwnedWeapon(squad, "weapon", catalog.Weapons[0].StableId).Success,
            Is.True);
        Assert.That(service.TryEquip(squad, "weapon", EquipmentSlotKind.Armor).Failure,
            Is.EqualTo(EquipmentOperationFailure.IncompatibleSlot));
        Assert.That(service.TryEquip(squad, "weapon", EquipmentSlotKind.Accessory).Failure,
            Is.EqualTo(EquipmentOperationFailure.IncompatibleSlot));
    }

    [Test]
    public void EquipmentOwnershipAndAssignmentsSurviveSaveRoundTrip()
    {
        GameObject sourceObject = new GameObject("source-repository");
        GameObject restoredObject = new GameObject("restored-repository");
        try
        {
            SquadSaveParticipant source = sourceObject.AddComponent<SquadSaveParticipant>();
            SquadData squad = CreateSquad("save");
            Assert.That(source.TryAddSquad(squad, out string error), Is.True, error);
            SquadEquipmentService service = new SquadEquipmentService(catalog);
            service.GrantOwnedWeapon(squad, "save-squad-weapon", catalog.Weapons[0].StableId);
            service.GrantOwnedWeapon(squad, "save-commander-weapon", catalog.Weapons[1].StableId);
            service.TryEquip(squad, "save-squad-weapon", EquipmentSlotKind.SquadWeapon);
            service.TryEquip(squad, "save-commander-weapon", EquipmentSlotKind.CommanderWeapon);

            SquadSaveParticipant restored =
                restoredObject.AddComponent<SquadSaveParticipant>();
            restored.RestoreState(source.CaptureState());
            SquadData loaded = restored.GetSquad(squad.Id);
            Assert.That(loaded.Equipment.OwnedItems.Count, Is.EqualTo(2));
            Assert.That(loaded.Equipment.SquadWeaponInstanceId,
                Is.EqualTo("save-squad-weapon"));
            Assert.That(loaded.Equipment.CommanderWeaponInstanceId,
                Is.EqualTo("save-commander-weapon"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceObject);
            UnityEngine.Object.DestroyImmediate(restoredObject);
        }
    }

    [Test]
    public void ExplicitDevelopmentMigrationSeedsLegacyEmptyEquipmentOnce()
    {
        GameObject repositoryObject = new GameObject("legacy-migration-repository");
        try
        {
            SquadSaveParticipant repository =
                repositoryObject.AddComponent<SquadSaveParticipant>();
            SquadData legacy = CreateSquad("legacy");
            Assert.That(repository.TryAddSquad(legacy, out string error), Is.True, error);
            repository.ConfigureEquipmentMigration(catalog, true);
            Assert.That(legacy.Equipment.OwnedItems.Count, Is.EqualTo(18));
            Assert.That(legacy.Equipment.SquadWeaponInstanceId, Is.Not.Empty);
            Assert.That(legacy.Equipment.CommanderWeaponInstanceId, Is.Not.Empty);
            string once = repository.CaptureState();
            repository.ConfigureEquipmentMigration(catalog, true);
            Assert.That(repository.CaptureState(), Is.EqualTo(once));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(repositoryObject);
        }
    }

    [Test]
    public void EquippedWeaponsModifyCanonicalCalculatedStats()
    {
        SquadData squad = CreateSquad("stats");
        SquadEquipmentService service = new SquadEquipmentService(catalog);
        Weapon weapon = catalog.Weapons.First(candidate => candidate.StrengthBonus > 0f);
        service.GrantOwnedWeapon(squad, "stat-weapon", weapon.StableId);
        SquadCalculatedStats before = SquadStatsCalculator.Calculate(squad, catalog);
        service.TryEquip(squad, "stat-weapon", EquipmentSlotKind.SquadWeapon);
        SquadCalculatedStats after = SquadStatsCalculator.Calculate(squad, catalog);
        Assert.That(after.Strength - before.Strength,
            Is.EqualTo(weapon.StrengthBonus).Within(.0001f));
        Assert.That(after.Accuracy - before.Accuracy,
            Is.EqualTo(weapon.AccuracyBonus).Within(.0001f));
    }

    [Test]
    public void BattleSnapshotCopiesValuesAndAttackCalculatorUsesWeaponProfile()
    {
        SquadData squad = CreateSquad("snapshot");
        SquadEquipmentService service = new SquadEquipmentService(catalog);
        Weapon weapon = catalog.Weapons.First(candidate => candidate.BaseDamageBonus > 0);
        service.GrantOwnedWeapon(squad, "snapshot-weapon", weapon.StableId);
        service.TryEquip(squad, "snapshot-weapon", EquipmentSlotKind.SquadWeapon);
        Assert.That(BattleEquipmentSnapshot.TryCreate(squad, catalog,
            out BattleEquipmentSnapshot snapshot, out string reason), Is.True, reason);

        AttackDefinition basic = AssetDatabase.LoadAssetAtPath<AttackDefinition>(
            "Assets/GameData/Combat/DEV_BasicPhysicalMeleeAttack.asset");
        BattleCombatRules rules = AssetDatabase.LoadAssetAtPath<BattleCombatRules>(
            "Assets/GameData/Combat/DEV_BattleCombatRules.asset");
        BattleAttackCalculator calculator = new BattleAttackCalculator(rules);
        SquadCalculatedStats stats = SquadStatsCalculator.Calculate(
            squad, null, snapshot.StatModifiers);
        BattleDamageCalculation unarmed = calculator.CalculateDamage(
            stats, stats, basic, false, null);
        BattleDamageCalculation armed = calculator.CalculateDamage(
            stats, stats, basic, false, snapshot.SquadWeapon);
        Assert.That(armed.RawDamage, Is.GreaterThan(unarmed.RawDamage));
        Assert.That(snapshot.SquadWeapon.BaseDamageBonus,
            Is.EqualTo(weapon.BaseDamageBonus));
    }

    [Test]
    public void AttackAssetsUseExplicitEquipmentSlotsAndRallyIsIndependent()
    {
        AttackDefinition basic = Load<AttackDefinition>(
            "Assets/GameData/Combat/DEV_BasicPhysicalMeleeAttack.asset");
        AttackDefinition power = Load<AttackDefinition>(
            "Assets/GameData/Abilities/DEV_PowerStrike_Attack.asset");
        AttackDefinition sweep = Load<AttackDefinition>(
            "Assets/GameData/Abilities/DEV_SweepingBlow_Attack.asset");
        AbilityDefinition rally = Load<AbilityDefinition>(
            "Assets/GameData/Abilities/DEV_Rally.asset");
        Assert.That(basic.WeaponSlot, Is.EqualTo(EquipmentSlotKind.SquadWeapon));
        Assert.That(power.WeaponSlot, Is.EqualTo(EquipmentSlotKind.CommanderWeapon));
        Assert.That(sweep.WeaponSlot, Is.EqualTo(EquipmentSlotKind.SquadWeapon));
        Assert.That(rally.EffectType, Is.EqualTo(BattleAbilityEffectType.RestoreMorale));
        Assert.That(rally.AttackEffect, Is.Null);
    }

    [Test]
    public void PostBattleApplicationPreservesEquipmentAssignments()
    {
        GameObject repositoryObject = new GameObject("post-battle-repository");
        try
        {
            SquadSaveParticipant repository =
                repositoryObject.AddComponent<SquadSaveParticipant>();
            SquadData squad = CreateSquad("post");
            Assert.That(repository.TryAddSquad(squad, out string error), Is.True, error);
            SquadEquipmentService service = new SquadEquipmentService(catalog);
            service.GrantOwnedWeapon(squad, "post-weapon", catalog.Weapons[0].StableId);
            service.TryEquip(squad, "post-weapon", EquipmentSlotKind.SquadWeapon);
            BattleOutcome outcome = new BattleOutcome { battleId = "equipment-post-battle" };
            SquadBattleResult result = new SquadBattleResult
            {
                squadId = squad.Id,
                commanderId = squad.Commander.id,
                side = BattleSide.Player,
                initialCommanderHP = 10,
                finalCommanderHP = 10,
                commanderDefeatedInBattle = false,
                initialMorale = 50,
                finalMorale = 50
            };
            string warriorId = squad.Warriors[0].id;
            result.initialWarriorIds.Add(warriorId);
            result.survivingWarriorIds.Add(warriorId);
            outcome.participantResults.Add(result);
            PostBattleRules rules = Load<PostBattleRules>(
                "Assets/GameData/BattleLifecycle/DEV_PostBattleRules.asset");
            BattleResultApplicationResult applied = new BattleResultApplier(
                repository, rules, _ => new SeededPostBattleRandomSource(1)).Apply(outcome);
            Assert.That(applied.Success, Is.True, applied.Error);
            Assert.That(squad.Equipment.SquadWeaponInstanceId, Is.EqualTo("post-weapon"));
            Assert.That(squad.Equipment.OwnedItems.Count, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(repositoryObject);
        }
    }

    [Test]
    public void ProductionScenesContainExplicitCatalogAndSingleEquipmentV2Owner()
    {
        string first = File.ReadAllText(Path.GetFullPath("Assets/Scenes/first_try.unity"));
        string battle = File.ReadAllText(
            Path.GetFullPath("Assets/Scenes/Raw_Alpha_BattleMode.unity"));
        string guid = AssetDatabase.AssetPathToGUID(CatalogPath);
        Assert.That(Count(first, "m_Name: EquipmentV2Root"), Is.EqualTo(1));
        StringAssert.Contains($"guid: {guid}", first);
        StringAssert.Contains($"guid: {guid}", battle);
        Assert.That(Count(first, "m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.EventSystems.EventSystem"),
            Is.EqualTo(1));
    }

    private static SquadData CreateSquad(string suffix) => new SquadData(
        $"squad-{suffix}",
        new CommanderData
        {
            id = $"commander-{suffix}",
            baseStats = new SquadBaseStats
            {
                hp = 10,
                actionPoints = 8,
                strength = 5,
                accuracy = .1f,
                criticalChance = .1f,
                criticalDamage = 1.5f,
                morale = 50
            }
        },
        new[] { new WarriorData { id = $"warrior-{suffix}", maxHP = 5,
            strength = 1, dexterity = 1 } });

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, path);
        return asset;
    }

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty).Length) / value.Length;
}
