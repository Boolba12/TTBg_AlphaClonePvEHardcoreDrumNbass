using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GridOccupancyService : MonoBehaviour
{
    private readonly Dictionary<Vector2Int, SquadBattleController> occupied =
        new Dictionary<Vector2Int, SquadBattleController>();
    private readonly Dictionary<Vector2Int, SquadBattleController> reserved =
        new Dictionary<Vector2Int, SquadBattleController>();
    private readonly Dictionary<string, Vector2Int> occupiedBySquad =
        new Dictionary<string, Vector2Int>();
    private readonly Dictionary<string, Vector2Int> reservedBySquad =
        new Dictionary<string, Vector2Int>();
    private readonly Dictionary<string, SquadBattleController> controllersBySquad =
        new Dictionary<string, SquadBattleController>();
    private readonly Dictionary<string, Action> defeatHandlers =
        new Dictionary<string, Action>();

    public bool IsInitialized { get; private set; }
    public int OccupiedCellCount => occupied.Count;
    public int ReservationCount => reserved.Count;

    public event Action OnOccupancyChanged;

    public bool Initialize(IReadOnlyList<SquadBattleController> controllers)
    {
        if (IsInitialized || controllers == null || controllers.Count == 0)
            return false;

        Dictionary<Vector2Int, SquadBattleController> proposed =
            new Dictionary<Vector2Int, SquadBattleController>();
        for (int i = 0; i < controllers.Count; i++)
        {
            SquadBattleController controller = controllers[i];
            if (controller == null || !controller.IsInitialized ||
                controller.GridAnchor == null || !controller.GridAnchor.IsPlaced ||
                string.IsNullOrWhiteSpace(controller.SquadId))
            {
                return false;
            }

            Vector2Int cell = controller.GridAnchor.CurrentCell;
            if (proposed.ContainsKey(cell))
                return false;
            proposed.Add(cell, controller);
        }

        foreach (KeyValuePair<Vector2Int, SquadBattleController> entry in proposed)
        {
            occupied.Add(entry.Key, entry.Value);
            occupiedBySquad.Add(entry.Value.SquadId, entry.Key);
            controllersBySquad.Add(entry.Value.SquadId, entry.Value);
            string squadId = entry.Value.SquadId;
            Action handler = () => Release(squadId);
            defeatHandlers.Add(squadId, handler);
            entry.Value.Runtime.OnSquadDefeated += handler;
        }

        IsInitialized = true;
        OnOccupancyChanged?.Invoke();
        return true;
    }

    public bool IsOccupied(Vector2Int cell) => occupied.ContainsKey(cell);
    public bool IsReserved(Vector2Int cell) => reserved.ContainsKey(cell);

    public bool TryGetOccupant(
        Vector2Int cell,
        out SquadBattleController controller)
    {
        return occupied.TryGetValue(cell, out controller);
    }

    public bool TryGetOccupiedCell(
        SquadBattleController controller,
        out Vector2Int cell)
    {
        cell = default;
        return controller != null &&
               !string.IsNullOrWhiteSpace(controller.SquadId) &&
               occupiedBySquad.TryGetValue(controller.SquadId, out cell);
    }

    public bool CanEnter(SquadBattleController controller, Vector2Int cell)
    {
        if (!IsInitialized || controller == null)
            return false;

        if (occupied.TryGetValue(cell, out SquadBattleController occupant) &&
            occupant != controller)
        {
            return false;
        }

        return !reserved.TryGetValue(cell, out SquadBattleController reserver) ||
               reserver == controller;
    }

    public bool TryReserve(SquadBattleController controller, Vector2Int destination)
    {
        if (!CanEnter(controller, destination) ||
            !TryGetOccupiedCell(controller, out Vector2Int current) ||
            current == destination ||
            reservedBySquad.ContainsKey(controller.SquadId))
        {
            return false;
        }

        reserved.Add(destination, controller);
        reservedBySquad.Add(controller.SquadId, destination);
        OnOccupancyChanged?.Invoke();
        return true;
    }

    public bool CanCommitMove(
        SquadBattleController controller,
        Vector2Int destination)
    {
        return IsInitialized && controller != null &&
               occupiedBySquad.ContainsKey(controller.SquadId) &&
               reservedBySquad.TryGetValue(controller.SquadId, out Vector2Int reservedCell) &&
               reservedCell == destination &&
               reserved.TryGetValue(destination, out SquadBattleController reserver) &&
               reserver == controller;
    }

    public bool TryCommitMove(
        SquadBattleController controller,
        Vector2Int destination)
    {
        if (!CanCommitMove(controller, destination))
            return false;

        Vector2Int previous = occupiedBySquad[controller.SquadId];
        occupied.Remove(previous);
        occupied[destination] = controller;
        occupiedBySquad[controller.SquadId] = destination;
        reserved.Remove(destination);
        reservedBySquad.Remove(controller.SquadId);
        OnOccupancyChanged?.Invoke();
        return true;
    }

    public void CancelReservation(SquadBattleController controller)
    {
        if (controller == null || string.IsNullOrWhiteSpace(controller.SquadId) ||
            !reservedBySquad.TryGetValue(controller.SquadId, out Vector2Int cell))
        {
            return;
        }

        reservedBySquad.Remove(controller.SquadId);
        reserved.Remove(cell);
        OnOccupancyChanged?.Invoke();
    }

    public void Release(string squadId)
    {
        if (string.IsNullOrWhiteSpace(squadId))
            return;

        bool changed = false;
        if (occupiedBySquad.TryGetValue(squadId, out Vector2Int occupiedCell))
        {
            occupiedBySquad.Remove(squadId);
            occupied.Remove(occupiedCell);
            changed = true;
        }
        if (reservedBySquad.TryGetValue(squadId, out Vector2Int reservedCell))
        {
            reservedBySquad.Remove(squadId);
            reserved.Remove(reservedCell);
            changed = true;
        }
        if (changed)
            OnOccupancyChanged?.Invoke();
    }

    public void Clear()
    {
        foreach (KeyValuePair<string, Action> entry in defeatHandlers)
        {
            if (controllersBySquad.TryGetValue(
                    entry.Key,
                    out SquadBattleController controller) &&
                controller?.Runtime != null)
            {
                controller.Runtime.OnSquadDefeated -= entry.Value;
            }
        }

        defeatHandlers.Clear();
        occupied.Clear();
        reserved.Clear();
        occupiedBySquad.Clear();
        reservedBySquad.Clear();
        controllersBySquad.Clear();
        IsInitialized = false;
    }

    private void OnDestroy() => Clear();
}
