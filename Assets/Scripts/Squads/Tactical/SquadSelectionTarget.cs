using UnityEngine;

public sealed class SquadSelectionTarget : MonoBehaviour
{
    [SerializeField] private SquadBattleController controller;
    [SerializeField] private SquadSelectionView selectionView;

    public SquadBattleController Controller => controller;
    public SquadSelectionView SelectionView => selectionView;

    public void Configure(
        SquadBattleController configuredController,
        SquadSelectionView configuredView)
    {
        controller = configuredController;
        selectionView = configuredView;
    }

    public void Bind(SquadBattleController configuredController)
    {
        if (controller == null)
            controller = configuredController;
    }

    public void SetSelected(bool selected) => selectionView?.SetSelected(selected);
}
