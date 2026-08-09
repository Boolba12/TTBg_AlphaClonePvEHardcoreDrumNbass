using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BattleTurnController : MonoBehaviour
{
    [SerializeField] private SquadBattleBootstrap squadBootstrap;
    [Header("Development AI placeholder")]
    [SerializeField] private bool autoSkipAITurns = true;
    [SerializeField, Min(0f)] private float aiTurnDelay = 0.2f;

    private readonly Dictionary<string, Action> defeatHandlers =
        new Dictionary<string, Action>();
    private SquadInitiativeOrder initiativeOrder;
    private Coroutine aiTurnRoutine;
    private int activeIndex;
    private bool changingTurn;

    public bool HasStarted { get; private set; }
    public bool IsBattleLocked { get; private set; }
    public int CurrentRound { get; private set; }
    public int CompletedTurnCount { get; private set; }
    public SquadBattleController ActiveSquad { get; private set; }
    public string ActiveSquadId => ActiveSquad?.SquadId;
    public SquadInitiativeOrder InitiativeOrder => initiativeOrder;
    public bool DevelopmentAutoSkipAIEnabled => autoSkipAITurns;

    public event Action OnBattleStarted;
    public event Action<int> OnRoundStarted;
    public event Action<SquadBattleController> OnTurnStarted;
    public event Action<SquadBattleController> OnTurnEnded;
    public event Action<SquadBattleController> OnActiveSquadChanged;
    public event Action OnBattleStopped;

    public void Configure(
        SquadBattleBootstrap bootstrap,
        bool developmentAutoSkipAI = true,
        float developmentAIDelay = 0.2f)
    {
        squadBootstrap = bootstrap;
        autoSkipAITurns = developmentAutoSkipAI;
        aiTurnDelay = Mathf.Max(0f, developmentAIDelay);
    }

    public bool StartBattle()
    {
        if (HasStarted || squadBootstrap == null || !squadBootstrap.HasBootstrapped ||
            squadBootstrap.InitiativeOrder.Entries.Count == 0)
        {
            return false;
        }

        initiativeOrder = squadBootstrap.InitiativeOrder;
        SubscribeToDefeats();
        IsBattleLocked = false;
        CompletedTurnCount = 0;
        HasStarted = true;
        CurrentRound = 1;
        activeIndex = 0;
        OnBattleStarted?.Invoke();
        OnRoundStarted?.Invoke(CurrentRound);
        StartTurnAt(activeIndex);
        return true;
    }

    public bool EndCurrentTurn()
    {
        if (!HasStarted || IsBattleLocked || changingTurn || ActiveSquad == null)
            return false;

        changingTurn = true;
        StopAITurnRoutine();
        SquadBattleController ended = ActiveSquad;
        ended.Runtime?.CompleteTurn();
        CompletedTurnCount++;
        OnTurnEnded?.Invoke(ended);

        int currentPosition = IndexOf(ended);
        int nextIndex = currentPosition >= 0 ? currentPosition + 1 : activeIndex;
        ActiveSquad = null;
        OnActiveSquadChanged?.Invoke(null);

        if (initiativeOrder == null || initiativeOrder.Entries.Count == 0)
        {
            changingTurn = false;
            return true;
        }

        if (nextIndex >= initiativeOrder.Entries.Count)
        {
            nextIndex = 0;
            CurrentRound++;
            OnRoundStarted?.Invoke(CurrentRound);
        }

        activeIndex = nextIndex;
        changingTurn = false;
        StartTurnAt(activeIndex);
        return true;
    }

    public bool IsActive(SquadBattleController controller)
    {
        return controller != null && ActiveSquad == controller;
    }

    public bool StopBattleLifecycle()
    {
        if (!HasStarted || IsBattleLocked)
            return false;

        IsBattleLocked = true;
        changingTurn = false;
        StopAITurnRoutine();
        ActiveSquad = null;
        OnActiveSquadChanged?.Invoke(null);
        OnBattleStopped?.Invoke();
        return true;
    }

    private void StartTurnAt(int index)
    {
        if (!HasStarted || IsBattleLocked || initiativeOrder == null || initiativeOrder.Entries.Count == 0)
            return;

        activeIndex = Mathf.Clamp(index, 0, initiativeOrder.Entries.Count - 1);
        SquadBattleController next = initiativeOrder.Entries[activeIndex];
        if (next == null || !next.CanAct)
        {
            EndInvalidEntry();
            return;
        }

        ActiveSquad = next;
        next.Runtime.BeginTurn();
        OnActiveSquadChanged?.Invoke(next);
        OnTurnStarted?.Invoke(next);

        if (next.ControlType == SquadControlType.AI && autoSkipAITurns)
            aiTurnRoutine = StartCoroutine(AutoSkipAITurn(next));
    }

    private void EndInvalidEntry()
    {
        ActiveSquad = null;
        OnActiveSquadChanged?.Invoke(null);
        if (initiativeOrder.Entries.Count > 0)
            StartTurnAt(activeIndex % initiativeOrder.Entries.Count);
    }

    private IEnumerator AutoSkipAITurn(SquadBattleController expectedActive)
    {
        if (aiTurnDelay > 0f)
            yield return new WaitForSecondsRealtime(aiTurnDelay);

        aiTurnRoutine = null;
        if (!IsBattleLocked && ActiveSquad == expectedActive &&
            expectedActive.ControlType == SquadControlType.AI)
            EndCurrentTurn();
    }

    private void HandleSquadDefeated(SquadBattleController controller)
    {
        if (controller == ActiveSquad)
            EndCurrentTurn();
    }

    private int IndexOf(SquadBattleController controller)
    {
        if (initiativeOrder == null || controller == null)
            return -1;
        for (int i = 0; i < initiativeOrder.Entries.Count; i++)
        {
            if (initiativeOrder.Entries[i] == controller)
                return i;
        }
        return -1;
    }

    private void SubscribeToDefeats()
    {
        foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
        {
            if (controller?.Runtime == null || defeatHandlers.ContainsKey(controller.SquadId))
                continue;
            Action handler = () => HandleSquadDefeated(controller);
            defeatHandlers.Add(controller.SquadId, handler);
            controller.Runtime.OnSquadDefeated += handler;
        }
    }

    private void StopAITurnRoutine()
    {
        if (aiTurnRoutine != null)
            StopCoroutine(aiTurnRoutine);
        aiTurnRoutine = null;
    }

    private void OnDestroy()
    {
        StopAITurnRoutine();
        if (squadBootstrap != null)
        {
            foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
            {
                if (controller?.Runtime != null &&
                    defeatHandlers.TryGetValue(controller.SquadId, out Action handler))
                {
                    controller.Runtime.OnSquadDefeated -= handler;
                }
            }
        }
        defeatHandlers.Clear();
    }
}
