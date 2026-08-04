using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class BattleSquadSelectionController : MonoBehaviour
{
    [SerializeField] private SquadBattleBootstrap squadBootstrap;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask selectionLayers = ~0;
    [SerializeField, Min(1f)] private float raycastDistance = 10000f;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[24];
    private readonly Dictionary<string, Action> defeatHandlers =
        new Dictionary<string, Action>();

    public bool IsInitialized { get; private set; }
    public SquadBattleController SelectedSquad { get; private set; }

    public event Action<SquadBattleController> OnSelectedSquadChanged;

    public void Configure(SquadBattleBootstrap bootstrap, Camera camera)
    {
        squadBootstrap = bootstrap;
        inputCamera = camera;
    }

    public bool Initialize()
    {
        if (IsInitialized || squadBootstrap == null || !squadBootstrap.HasBootstrapped)
            return false;

        foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
        {
            if (controller?.Runtime == null)
                return false;

            controller.SelectionTarget?.Bind(controller);
            string squadId = controller.SquadId;
            Action handler = () => HandleSquadDefeated(controller);
            defeatHandlers.Add(squadId, handler);
            controller.Runtime.OnSquadDefeated += handler;
        }

        IsInitialized = true;
        return true;
    }

    public bool TrySelectTarget(SquadSelectionTarget target)
    {
        return target != null && TrySelect(target.Controller);
    }

    public bool TrySelect(SquadBattleController controller)
    {
        if (!IsInspectable(controller))
            return false;
        if (SelectedSquad == controller)
            return true;

        SelectedSquad?.SelectionTarget?.SetSelected(false);
        SelectedSquad = controller;
        SelectedSquad.SelectionTarget?.SetSelected(true);
        OnSelectedSquadChanged?.Invoke(SelectedSquad);
        return true;
    }

    public void ClearSelection()
    {
        if (SelectedSquad == null)
            return;
        SelectedSquad.SelectionTarget?.SetSelected(false);
        SelectedSquad = null;
        OnSelectedSquadChanged?.Invoke(null);
    }

    public bool IsInspectable(SquadBattleController controller)
    {
        return IsInitialized && controller != null && controller.IsInitialized &&
               controller.CanAct && controller.Side == BattleSide.Player;
    }

    private void Update()
    {
        if (!IsInitialized || Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame ||
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            return;
        }

        Camera camera = inputCamera != null ? inputCamera : Camera.main;
        if (camera == null)
            return;

        Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            hitBuffer,
            raycastDistance,
            selectionLayers,
            QueryTriggerInteraction.Collide);
        SquadSelectionTarget closest = null;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            SquadSelectionTarget candidate =
                hitBuffer[i].collider.GetComponentInParent<SquadSelectionTarget>();
            if (candidate == null || hitBuffer[i].distance >= closestDistance)
                continue;
            closest = candidate;
            closestDistance = hitBuffer[i].distance;
        }

        if (closest != null)
            TrySelectTarget(closest);
    }

    private void HandleSquadDefeated(SquadBattleController controller)
    {
        if (SelectedSquad == controller)
            ClearSelection();
    }

    private void OnDestroy()
    {
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
