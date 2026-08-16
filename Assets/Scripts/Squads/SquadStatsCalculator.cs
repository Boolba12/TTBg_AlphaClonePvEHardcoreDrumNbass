public static class SquadStatsCalculator
{
    public static SquadCalculatedStats Calculate(
        SquadData data,
        SquadBattleState battleState = null,
        SquadStatModifiers equipmentModifiers = null)
    {
        return CalculateCore(data, data?.Warriors, battleState, equipmentModifiers);
    }

    public static SquadCalculatedStats CalculateComposition(
        SquadData data,
        System.Collections.Generic.IReadOnlyList<WarriorData> warriors,
        EquipmentDefinitionCatalog equipmentCatalog = null)
    {
        SquadStatModifiers equipment = equipmentCatalog != null && data != null
            ? new SquadEquipmentService(equipmentCatalog).BuildEquippedStatModifiers(data)
            : null;
        return CalculateCore(data, warriors, null, equipment);
    }

    private static SquadCalculatedStats CalculateCore(
        SquadData data,
        System.Collections.Generic.IReadOnlyList<WarriorData> warriors,
        SquadBattleState battleState,
        SquadStatModifiers equipmentModifiers)
    {
        if (data?.Commander?.baseStats == null)
            return default;

        int warriorHP = 0;
        float warriorStrength = 0;
        float warriorDexterity = 0;

        for (int i = 0; warriors != null && i < warriors.Count; i++)
        {
            WarriorData warrior = warriors[i];
            if (warrior == null || !IsAlive(warrior.id, battleState))
                continue;

            warriorHP += warrior.maxHP;
            warriorStrength += warrior.strength;
            warriorDexterity += warrior.dexterity;
        }

        SquadStatModifiers modifiers = SquadStatModifiers.Combine(
            data.PermanentModifiers,
            battleState?.temporaryModifiers);
        modifiers = SquadStatModifiers.Combine(modifiers, equipmentModifiers);

        return new SquadCalculatedStats(
            data.Commander.baseStats,
            warriorHP,
            warriorStrength,
            warriorDexterity,
            modifiers);
    }

    public static SquadCalculatedStats Calculate(
        SquadData data,
        EquipmentDefinitionCatalog equipmentCatalog,
        SquadBattleState battleState = null)
    {
        SquadStatModifiers equipment = equipmentCatalog != null && data != null
            ? new SquadEquipmentService(equipmentCatalog).BuildEquippedStatModifiers(data)
            : null;
        return Calculate(data, battleState, equipment);
    }

    private static bool IsAlive(string warriorId, SquadBattleState state)
    {
        if (state?.warriors == null)
            return true;

        WarriorBattleState battleWarrior =
            state.warriors.Find(candidate => candidate != null && candidate.warriorId == warriorId);
        return battleWarrior == null || (!battleWarrior.defeated && battleWarrior.currentHP > 0);
    }

    public static float CalculateMoraleLoss(float incomingLoss, float resolve)
    {
        return System.Math.Max(0, incomingLoss - System.Math.Max(0, resolve));
    }

    public static int ComparePhysicalSpeed(SquadCalculatedStats left, SquadCalculatedStats right) =>
        left.PhysicalSpeed.CompareTo(right.PhysicalSpeed);

    public static int CompareMagicalSpeed(SquadCalculatedStats left, SquadCalculatedStats right) =>
        left.MagicalSpeed.CompareTo(right.MagicalSpeed);
}
