public static class SquadStatsCalculator
{
    public static SquadCalculatedStats Calculate(SquadData data, SquadBattleState battleState = null)
    {
        if (data?.Commander?.baseStats == null)
            return default;

        int warriorHP = 0;
        float warriorStrength = 0;
        float warriorDexterity = 0;

        for (int i = 0; i < data.Warriors.Count; i++)
        {
            WarriorData warrior = data.Warriors[i];
            if (warrior == null || !IsAlive(warrior.id, battleState))
                continue;

            warriorHP += warrior.maxHP;
            warriorStrength += warrior.strength;
            warriorDexterity += warrior.dexterity;
        }

        SquadStatModifiers modifiers = SquadStatModifiers.Combine(
            data.PermanentModifiers,
            battleState?.temporaryModifiers);

        return new SquadCalculatedStats(
            data.Commander.baseStats,
            warriorHP,
            warriorStrength,
            warriorDexterity,
            modifiers);
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
