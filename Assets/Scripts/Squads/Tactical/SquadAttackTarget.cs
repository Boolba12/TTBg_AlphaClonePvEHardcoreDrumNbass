using System;
using UnityEngine;

public sealed class SquadAttackTarget : MonoBehaviour
{
    [SerializeField] private SquadBattleController controller;
    [SerializeField] private SquadAttackTargetView targetingView;

    public SquadBattleController Controller => controller;
    public SquadAttackTargetView TargetingView => targetingView;

    public event Action<SquadAttackTarget> OnConfirmRequested;

    public void Configure(
        SquadBattleController configuredController,
        SquadAttackTargetView configuredView)
    {
        controller = configuredController;
        targetingView = configuredView;
    }

    public void Bind(SquadBattleController configuredController)
    {
        if (controller == null)
            controller = configuredController;
    }

    public void RequestConfirm() => OnConfirmRequested?.Invoke(this);
}
