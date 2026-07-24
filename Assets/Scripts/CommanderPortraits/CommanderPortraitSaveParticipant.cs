using UnityEngine;

public sealed class CommanderPortraitSaveParticipant : MonoBehaviour, ISaveable
{
    [SerializeField] private CommanderPortraitDatabase database;
    [SerializeField] private int optionalRandomSeed;
    [SerializeField] private bool useFixedRandomSeed;

    private CommanderPortraitService service;

    public string SaveKey => "commander-portrait-pools";
    public CommanderPortraitService Service => service ??= CreateService();

    private void Awake()
    {
        service = CreateService();
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(Service.CaptureState());
    }

    public void RestoreState(string json)
    {
        CommanderPortraitPoolState state = string.IsNullOrWhiteSpace(json)
            ? new CommanderPortraitPoolState()
            : JsonUtility.FromJson<CommanderPortraitPoolState>(json);
        Service.RestoreState(state);
    }

    private CommanderPortraitService CreateService()
    {
        if (database == null)
            throw new MissingReferenceException("CommanderPortraitSaveParticipant requires a portrait database.");

        return new CommanderPortraitService(
            database,
            useFixedRandomSeed ? optionalRandomSeed : (int?)null);
    }
}
