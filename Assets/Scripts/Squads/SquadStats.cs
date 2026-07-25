using System;
using System.Collections.Generic;

[Serializable]
public sealed class SquadBaseStats
{
    public int hp = 1;
    public int actionPoints = 1;
    public float initiative;
    public float physicalSpeed;
    public float magicalSpeed;
    public float strength;
    public float dexterity;
    public float magicalMastery;
    public float accuracy;
    public float evasion;
    public float criticalChance;
    public float criticalDamage;
    public float physicalArmor;
    public float magicalResistance;
    public float morale;
    [UnityEngine.Tooltip("Reduces incoming morale loss before it is applied.")]
    public float resolve;
    public float visionRange;
    [UnityEngine.Tooltip("Normalized 0..1 chance to increase one used primary stat by +1.")]
    public float experienceMultiplier;

    public void Validate(string owner, List<string> errors)
    {
        if (hp < 0 || actionPoints < 0 || initiative < 0 || physicalSpeed < 0 ||
            magicalSpeed < 0 || strength < 0 || dexterity < 0 || magicalMastery < 0 ||
            accuracy < 0 || evasion < 0 || criticalChance < 0 || criticalDamage < 0 ||
            physicalArmor < 0 || magicalResistance < 0 || morale < 0 || resolve < 0 ||
            visionRange < 0 || experienceMultiplier < 0)
        {
            errors.Add($"{owner} has negative base stats.");
        }
    }
}

[Serializable]
public sealed class SquadStatModifiers
{
    public int hp;
    public int actionPoints;
    public float initiative;
    public float physicalSpeed;
    public float magicalSpeed;
    public float strength;
    public float dexterity;
    public float magicalMastery;
    public float accuracy;
    public float evasion;
    public float criticalChance;
    public float criticalDamage;
    public float physicalArmor;
    public float magicalResistance;
    public float morale;
    public float resolve;
    public float visionRange;
    public float experienceMultiplier;

    public static SquadStatModifiers Combine(SquadStatModifiers first, SquadStatModifiers second)
    {
        first ??= new SquadStatModifiers();
        second ??= new SquadStatModifiers();
        return new SquadStatModifiers
        {
            hp = first.hp + second.hp,
            actionPoints = first.actionPoints + second.actionPoints,
            initiative = first.initiative + second.initiative,
            physicalSpeed = first.physicalSpeed + second.physicalSpeed,
            magicalSpeed = first.magicalSpeed + second.magicalSpeed,
            strength = first.strength + second.strength,
            dexterity = first.dexterity + second.dexterity,
            magicalMastery = first.magicalMastery + second.magicalMastery,
            accuracy = first.accuracy + second.accuracy,
            evasion = first.evasion + second.evasion,
            criticalChance = first.criticalChance + second.criticalChance,
            criticalDamage = first.criticalDamage + second.criticalDamage,
            physicalArmor = first.physicalArmor + second.physicalArmor,
            magicalResistance = first.magicalResistance + second.magicalResistance,
            morale = first.morale + second.morale,
            resolve = first.resolve + second.resolve,
            visionRange = first.visionRange + second.visionRange,
            experienceMultiplier = first.experienceMultiplier + second.experienceMultiplier
        };
    }
}

public readonly struct SquadCalculatedStats
{
    public int MaxHP { get; }
    public int ActionPoints { get; }
    public float Initiative { get; }
    public float PhysicalSpeed { get; }
    public float MagicalSpeed { get; }
    public float Strength { get; }
    public float Dexterity { get; }
    public float MagicalMastery { get; }
    public float Accuracy { get; }
    public float Evasion { get; }
    public float CriticalChance { get; }
    public float CriticalDamage { get; }
    public float PhysicalArmor { get; }
    public float MagicalResistance { get; }
    public float Morale { get; }
    public float Resolve { get; }
    public float VisionRange { get; }
    public float ExperienceMultiplier { get; }

    public SquadCalculatedStats(SquadBaseStats commander, int warriorHP, float warriorStrength,
        float warriorDexterity, SquadStatModifiers modifiers)
    {
        modifiers ??= new SquadStatModifiers();
        MaxHP = Math.Max(0, commander.hp + warriorHP + modifiers.hp);
        ActionPoints = Math.Max(0, commander.actionPoints + modifiers.actionPoints);
        Initiative = Math.Max(0, commander.initiative + modifiers.initiative);
        PhysicalSpeed = Math.Max(0, commander.physicalSpeed + modifiers.physicalSpeed);
        MagicalSpeed = Math.Max(0, commander.magicalSpeed + modifiers.magicalSpeed);
        Strength = Math.Max(0, commander.strength + warriorStrength + modifiers.strength);
        Dexterity = Math.Max(0, commander.dexterity + warriorDexterity + modifiers.dexterity);
        MagicalMastery = Math.Max(0, commander.magicalMastery + modifiers.magicalMastery);
        Accuracy = Math.Max(0, commander.accuracy + modifiers.accuracy);
        Evasion = Math.Max(0, commander.evasion + modifiers.evasion);
        CriticalChance = Math.Max(0, commander.criticalChance + modifiers.criticalChance);
        CriticalDamage = Math.Max(0, commander.criticalDamage + modifiers.criticalDamage);
        PhysicalArmor = Math.Max(0, commander.physicalArmor + modifiers.physicalArmor);
        MagicalResistance = Math.Max(0, commander.magicalResistance + modifiers.magicalResistance);
        Morale = Math.Max(0, commander.morale + modifiers.morale);
        Resolve = Math.Max(0, commander.resolve + modifiers.resolve);
        VisionRange = Math.Max(0, commander.visionRange + modifiers.visionRange);
        ExperienceMultiplier = Math.Clamp(
            commander.experienceMultiplier + modifiers.experienceMultiplier, 0f, 1f);
    }
}
