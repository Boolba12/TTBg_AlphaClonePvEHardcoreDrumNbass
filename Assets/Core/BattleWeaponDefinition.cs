using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Weapon Definition", fileName = "BattleWeaponDefinition")]
public class BattleWeaponDefinition : ScriptableObject
{
    public string weaponName;
    [TextArea]
    public string description;
    public Sprite icon;

    public GameObject weaponPrefab;
    public int attackBonus;
    public int rangeBonus;
}