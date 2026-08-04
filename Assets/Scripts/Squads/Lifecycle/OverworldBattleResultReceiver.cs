using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class OverworldBattleResultReceiver : MonoBehaviour
{
    [SerializeField] private SaveSystemBehaviour saveSystem;
    [SerializeField] private bool logDevelopmentSummary = true;

    public bool HasConsumedResult { get; private set; }
    public int ConsumeCount { get; private set; }
    public BattleOutcome LastOutcome { get; private set; }
    public string FailureReason { get; private set; }

    public void Configure(SaveSystemBehaviour configuredSaveSystem)
    {
        saveSystem = configuredSaveSystem;
    }

    private IEnumerator Start()
    {
        while (PendingSaveLoadContext.HasData || (saveSystem != null && saveSystem.IsBusy))
            yield return null;

        if (!BattleReturnContext.TryPeek(out BattleReturnData pending))
            yield break;
        if (pending.targetScene != SceneManager.GetActiveScene().name)
        {
            FailureReason =
                $"Battle return targets '{pending.targetScene}', not the active scene.";
            Debug.LogWarning($"OverworldBattleResultReceiver: {FailureReason}", this);
            yield break;
        }
        if (!pending.persistentMutationsApplied || !pending.autosaveSucceeded)
        {
            FailureReason = "Battle return data was not applied and saved.";
            Debug.LogError($"OverworldBattleResultReceiver: {FailureReason}", this);
            yield break;
        }
        if (!BattleReturnContext.TryConsume(out BattleReturnData consumed))
        {
            FailureReason = "Battle return data could not be consumed.";
            yield break;
        }

        LastOutcome = consumed.outcome;
        HasConsumedResult = LastOutcome != null;
        if (!HasConsumedResult)
        {
            FailureReason = "Consumed return data has no BattleOutcome.";
            yield break;
        }
        ConsumeCount++;
        if (LastOutcome.resultType == BattleResultType.Victory &&
            !string.IsNullOrWhiteSpace(LastOutcome.encounterId))
        {
            ResolvedEncounterRegistry.MarkResolved(LastOutcome.encounterId);
        }
        if (logDevelopmentSummary)
        {
            Debug.Log(
                $"OverworldBattleResultReceiver: consumed {LastOutcome.resultType} " +
                $"for battle '{LastOutcome.battleId}' with " +
                $"{LastOutcome.casualties.Count} warrior casualties.",
                this);
        }
    }
}
