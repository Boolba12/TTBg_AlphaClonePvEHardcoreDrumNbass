using UnityEngine;

public static class BattleSetupContext
{
    public static bool HasSelection { get; private set; }
    public static bool IsConfirmed { get; private set; }
    public static int PlayerUnitCount { get; private set; }
    public static BattleWeaponDefinition SelectedWeapon { get; private set; }

    public static void Reset(int defaultPlayerUnitCount = 1, BattleWeaponDefinition defaultWeapon = null)
    {
        HasSelection = true;
        IsConfirmed = false;
        PlayerUnitCount = Mathf.Max(1, defaultPlayerUnitCount);
        SelectedWeapon = defaultWeapon;
    }

    public static void SetSelection(int playerUnitCount, BattleWeaponDefinition selectedWeapon)
    {
        HasSelection = true;
        PlayerUnitCount = Mathf.Max(1, playerUnitCount);
        SelectedWeapon = selectedWeapon;
    }

    public static void Confirm()
    {
        HasSelection = true;
        IsConfirmed = true;
    }

    public static void ClearConfirmation()
    {
        IsConfirmed = false;
    }
}