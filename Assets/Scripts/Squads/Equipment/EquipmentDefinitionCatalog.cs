using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentDefinitionCatalog",
    menuName = "Game/Equipment/Definition Catalog")]
public sealed class EquipmentDefinitionCatalog : ScriptableObject
{
    [SerializeField] private List<Weapon> weapons = new List<Weapon>();
    [SerializeField] private List<ArmorDefinition> armors = new List<ArmorDefinition>();
    [SerializeField] private List<AccessoryDefinition> accessories =
        new List<AccessoryDefinition>();
    public IReadOnlyList<Weapon> Weapons => weapons;
    public IReadOnlyList<ArmorDefinition> Armors => armors;
    public IReadOnlyList<AccessoryDefinition> Accessories => accessories;
    public int DefinitionCount => weapons.Count + armors.Count + accessories.Count;

    public bool TryGetWeapon(string stableId, out Weapon definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(stableId))
            return false;
        for (int i = 0; i < weapons.Count; i++)
        {
            Weapon candidate = weapons[i];
            if (candidate != null && string.Equals(candidate.StableId, stableId,
                    StringComparison.Ordinal))
            {
                definition = candidate;
                return true;
            }
        }
        return false;
    }

    public bool TryGetDefinition(string stableId, out EquipmentItemDefinition definition)
    {
        definition = null;
        if (TryGetWeapon(stableId, out Weapon weapon))
        {
            definition = weapon;
            return true;
        }
        for (int i = 0; i < armors.Count; i++)
        {
            ArmorDefinition candidate = armors[i];
            if (candidate != null && string.Equals(candidate.StableId, stableId,
                    StringComparison.Ordinal))
            {
                definition = candidate;
                return true;
            }
        }
        for (int i = 0; i < accessories.Count; i++)
        {
            AccessoryDefinition candidate = accessories[i];
            if (candidate != null && string.Equals(candidate.StableId, stableId,
                    StringComparison.Ordinal))
            {
                definition = candidate;
                return true;
            }
        }
        return false;
    }

    public IEnumerable<EquipmentItemDefinition> EnumerateDefinitions()
    {
        for (int i = 0; i < weapons.Count; i++) yield return weapons[i];
        for (int i = 0; i < armors.Count; i++) yield return armors[i];
        for (int i = 0; i < accessories.Count; i++) yield return accessories[i];
    }

    public bool Validate(out string reason)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (EquipmentItemDefinition definition in EnumerateDefinitions())
        {
            if (definition == null)
            {
                reason = $"Equipment definition at index {index} is missing.";
                return false;
            }
            if (!definition.Validate(out reason))
                return false;
            if (!ids.Add(definition.StableId))
            {
                reason = $"Duplicate equipment definition ID '{definition.StableId}'.";
                return false;
            }
            index++;
        }
        reason = null;
        return true;
    }

#if UNITY_EDITOR
    public void ReplaceDevelopmentWeapons(List<Weapon> replacements) =>
        weapons = replacements ?? new List<Weapon>();

    public void ReplaceDevelopmentDefinitions(List<Weapon> configuredWeapons,
        List<ArmorDefinition> configuredArmors,
        List<AccessoryDefinition> configuredAccessories)
    {
        weapons = configuredWeapons ?? new List<Weapon>();
        armors = configuredArmors ?? new List<ArmorDefinition>();
        accessories = configuredAccessories ?? new List<AccessoryDefinition>();
    }
#endif
}
