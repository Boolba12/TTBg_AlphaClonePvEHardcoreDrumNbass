using System;
using UnityEngine;

public enum BattleCommandMode
{
    None,
    Move,
    Attack,
    Ability
}

public sealed class BattleCommandModeController : MonoBehaviour
{
    public BattleCommandMode ActiveMode { get; private set; }
    public bool IsLocked { get; private set; }

    public event Action<BattleCommandMode> OnModeChanged;

    public bool TryEnter(BattleCommandMode mode)
    {
        if (mode == BattleCommandMode.None || IsLocked)
            return false;
        if (ActiveMode == mode)
            return true;

        ActiveMode = mode;
        OnModeChanged?.Invoke(ActiveMode);
        return true;
    }

    public bool Cancel()
    {
        if (ActiveMode == BattleCommandMode.None)
            return false;
        ActiveMode = BattleCommandMode.None;
        OnModeChanged?.Invoke(ActiveMode);
        return true;
    }

    public void CancelAndLock()
    {
        Cancel();
        IsLocked = true;
    }

    public void ResetForBattle()
    {
        IsLocked = false;
        Cancel();
    }

    private void OnDisable() => Cancel();
}
