using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class TacticalCameraController : MonoBehaviour
{
    [SerializeField] private Camera controlledCamera;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private MapRenderer mapRenderer;
    [SerializeField] private BattleTurnController turnController;
    [SerializeField, Min(0.1f)] private float keyboardPanSpeed = 9f;
    [SerializeField, Min(0.1f)] private float zoomSensitivity = 4f;
    [SerializeField, Range(10f, 80f)] private float minimumFieldOfView = 32f;
    [SerializeField, Range(10f, 90f)] private float maximumFieldOfView = 70f;
    [SerializeField] private bool focusMapCenterOnInitialize = true;

    private readonly Vector3[] footprint = new Vector3[4];
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float lastFieldOfView;
    private bool turnEventsBound;
    private SquadBattleController pendingTurnFocus;

    public bool IsInitialized { get; private set; }
    public Camera ControlledCamera => controlledCamera;
    public Bounds MapBounds { get; private set; }
    public IReadOnlyList<Vector3> CurrentFootprint => footprint;
    public int PositionChangeCount { get; private set; }
    public int ViewportChangeCount { get; private set; }
    public int KeyboardPanCount { get; private set; }
    public int TurnFocusCount { get; private set; }
    public string LastTurnFocusSquadId { get; private set; }

    public event Action PositionChanged;
    public event Action ViewportChanged;

    private IEnumerator Start()
    {
        while (mapGenerator != null && !mapGenerator.HasGeneratedData)
            yield return null;
        Initialize();
    }

    public void Configure(
        Camera camera,
        MapGenerator generator,
        MapRenderer renderer,
        float panSpeed = 9f,
        float zoomStep = 4f)
    {
        Configure(camera, generator, renderer, null, panSpeed, zoomStep);
    }

    public void Configure(
        Camera camera,
        MapGenerator generator,
        MapRenderer renderer,
        BattleTurnController turns,
        float panSpeed = 9f,
        float zoomStep = 4f)
    {
        UnbindTurnEvents();
        controlledCamera = camera;
        mapGenerator = generator;
        mapRenderer = renderer;
        turnController = turns;
        keyboardPanSpeed = Mathf.Max(0.1f, panSpeed);
        zoomSensitivity = Mathf.Max(0.1f, zoomStep);
        IsInitialized = false;
        pendingTurnFocus = turnController?.ActiveSquad;
        BindTurnEvents();
    }

    public bool Initialize()
    {
        if (IsInitialized)
            return true;
        if (controlledCamera == null)
            controlledCamera = GetComponent<Camera>();
        if (controlledCamera == null || mapGenerator == null || mapRenderer == null ||
            !mapGenerator.HasGeneratedData ||
            !mapRenderer.TryGetGeneratedWorldBounds(out Bounds bounds, true))
        {
            return false;
        }

        BindTurnEvents();
        MapBounds = bounds;
        controlledCamera.fieldOfView = Mathf.Clamp(
            controlledCamera.fieldOfView,
            minimumFieldOfView,
            maximumFieldOfView);
        IsInitialized = true;
        SquadBattleController initialTurnSquad =
            pendingTurnFocus != null ? pendingTurnFocus : turnController?.ActiveSquad;
        if (initialTurnSquad != null && TryFocusTurnSquad(initialTurnSquad))
        {
            // The turn event can precede camera initialization; consume it exactly once here.
        }
        else if (focusMapCenterOnInitialize)
            FocusWorld(MapBounds.center);
        else
            ClampToMapBounds();
        RefreshFootprintAndNotify(true);
        return true;
    }

    public bool FocusGrid(Vector2Int cell)
    {
        if (!IsInitialized || !mapGenerator.GetIsPlayable(cell.x, cell.y))
            return false;
        return FocusWorld(mapRenderer.GetCellWorldCenter(cell));
    }

    public bool FocusWorld(Vector3 worldPosition)
    {
        if (!IsInitialized)
            return false;
        Vector3 position = controlledCamera.transform.position;
        if (TryCalculateFootprint(footprint))
        {
            Vector3 footprintCenter = Vector3.zero;
            for (int i = 0; i < footprint.Length; i++)
                footprintCenter += footprint[i];
            footprintCenter /= footprint.Length;
            position.x += worldPosition.x - footprintCenter.x;
            position.z += worldPosition.z - footprintCenter.z;
        }
        else
        {
            position.x = worldPosition.x;
            position.z = worldPosition.z;
        }
        controlledCamera.transform.position = position;
        ClampToMapBounds();
        RefreshFootprintAndNotify(true);
        return true;
    }

    public bool PanWorld(Vector2 horizontalDelta)
    {
        if (!IsInitialized || horizontalDelta == Vector2.zero)
            return false;
        controlledCamera.transform.position +=
            new Vector3(horizontalDelta.x, 0f, horizontalDelta.y);
        ClampToMapBounds();
        RefreshFootprintAndNotify(true);
        return true;
    }

    /// <summary>
    /// Shared bounded pan contract used by the production Arrow-key binding and tests.
    /// It deliberately has no WASD binding because A is the battle Attack hotkey.
    /// </summary>
    public bool PanFromKeyboard(Vector2 direction, float unscaledDeltaTime)
    {
        if (!IsInitialized || direction.sqrMagnitude <= 0f || unscaledDeltaTime <= 0f)
            return false;
        float zoomScale = controlledCamera.fieldOfView / 60f;
        bool accepted = PanWorld(
            direction.normalized *
            (keyboardPanSpeed * zoomScale * unscaledDeltaTime));
        if (accepted)
            KeyboardPanCount++;
        return accepted;
    }

    public bool ZoomBy(float steps)
    {
        if (!IsInitialized || Mathf.Approximately(steps, 0f))
            return false;
        float before = controlledCamera.fieldOfView;
        controlledCamera.fieldOfView = Mathf.Clamp(
            before - steps * zoomSensitivity,
            minimumFieldOfView,
            maximumFieldOfView);
        if (Mathf.Approximately(before, controlledCamera.fieldOfView))
            return false;
        ClampToMapBounds();
        RefreshFootprintAndNotify(true);
        return true;
    }

    public bool TryGetFootprint(IList<Vector3> destination)
    {
        if (!IsInitialized || destination == null || destination.Count < 4)
            return false;
        if (!TryCalculateFootprint(footprint))
            return false;
        for (int i = 0; i < footprint.Length; i++)
            destination[i] = footprint[i];
        return true;
    }

    private void Update()
    {
        if (!IsInitialized)
            return;
        if (Keyboard.current != null)
        {
            Vector2 direction = Vector2.zero;
            if (Keyboard.current.leftArrowKey.isPressed)
                direction.x -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed)
                direction.x += 1f;
            if (Keyboard.current.downArrowKey.isPressed)
                direction.y -= 1f;
            if (Keyboard.current.upArrowKey.isPressed)
                direction.y += 1f;
            if (direction.sqrMagnitude > 0f)
                PanFromKeyboard(direction, Time.unscaledDeltaTime);
        }

        if (Mouse.current != null &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f))
                ZoomBy(scroll / 120f);
        }
    }

    private void LateUpdate()
    {
        if (!IsInitialized)
            return;
        Transform cameraTransform = controlledCamera.transform;
        if (cameraTransform.position != lastPosition ||
            cameraTransform.rotation != lastRotation ||
            !Mathf.Approximately(controlledCamera.fieldOfView, lastFieldOfView))
        {
            ClampToMapBounds();
            RefreshFootprintAndNotify(true);
        }
    }

    private void ClampToMapBounds()
    {
        if (!IsInitialized || !TryCalculateFootprint(footprint))
            return;
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        for (int i = 0; i < footprint.Length; i++)
        {
            minX = Mathf.Min(minX, footprint[i].x);
            maxX = Mathf.Max(maxX, footprint[i].x);
            minZ = Mathf.Min(minZ, footprint[i].z);
            maxZ = Mathf.Max(maxZ, footprint[i].z);
        }

        Vector3 correction = Vector3.zero;
        correction.x = CalculateAxisCorrection(
            minX, maxX, MapBounds.min.x, MapBounds.max.x);
        correction.z = CalculateAxisCorrection(
            minZ, maxZ, MapBounds.min.z, MapBounds.max.z);
        controlledCamera.transform.position += correction;
    }

    private static float CalculateAxisCorrection(
        float footprintMin,
        float footprintMax,
        float boundsMin,
        float boundsMax)
    {
        if (footprintMax - footprintMin >= boundsMax - boundsMin)
        {
            return (boundsMin + boundsMax) * 0.5f -
                   (footprintMin + footprintMax) * 0.5f;
        }
        if (footprintMin < boundsMin)
            return boundsMin - footprintMin;
        if (footprintMax > boundsMax)
            return boundsMax - footprintMax;
        return 0f;
    }

    private bool TryCalculateFootprint(Vector3[] destination)
    {
        if (controlledCamera == null || destination == null || destination.Length < 4)
            return false;
        Plane mapPlane = new Plane(Vector3.up, new Vector3(0f, MapBounds.center.y, 0f));
        Vector2[] corners =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        for (int i = 0; i < corners.Length; i++)
        {
            Ray ray = controlledCamera.ViewportPointToRay(corners[i]);
            if (!mapPlane.Raycast(ray, out float distance))
                return false;
            destination[i] = ray.GetPoint(distance);
        }
        return true;
    }

    private void RefreshFootprintAndNotify(bool positionMayHaveChanged)
    {
        if (!IsInitialized)
            return;
        TryCalculateFootprint(footprint);
        Transform cameraTransform = controlledCamera.transform;
        bool positionChanged = positionMayHaveChanged &&
                               cameraTransform.position != lastPosition;
        bool viewportChanged = positionChanged ||
                               cameraTransform.rotation != lastRotation ||
                               !Mathf.Approximately(
                                   controlledCamera.fieldOfView,
                                   lastFieldOfView);
        lastPosition = cameraTransform.position;
        lastRotation = cameraTransform.rotation;
        lastFieldOfView = controlledCamera.fieldOfView;
        if (positionChanged)
        {
            PositionChangeCount++;
            PositionChanged?.Invoke();
        }
        if (viewportChanged)
        {
            ViewportChangeCount++;
            ViewportChanged?.Invoke();
        }
    }

    private void HandleTurnStarted(SquadBattleController activeSquad)
    {
        pendingTurnFocus = activeSquad;
        if (IsInitialized)
            TryFocusTurnSquad(activeSquad);
    }

    private bool TryFocusTurnSquad(SquadBattleController activeSquad)
    {
        if (activeSquad?.GridAnchor == null || !activeSquad.GridAnchor.IsPlaced ||
            !FocusGrid(activeSquad.GridAnchor.CurrentCell))
        {
            return false;
        }

        pendingTurnFocus = null;
        LastTurnFocusSquadId = activeSquad.SquadId;
        TurnFocusCount++;
        return true;
    }

    private void BindTurnEvents()
    {
        if (turnEventsBound || turnController == null)
            return;
        turnController.OnTurnStarted += HandleTurnStarted;
        turnEventsBound = true;
        if (turnController.ActiveSquad != null)
            pendingTurnFocus = turnController.ActiveSquad;
    }

    private void UnbindTurnEvents()
    {
        if (!turnEventsBound || turnController == null)
            return;
        turnController.OnTurnStarted -= HandleTurnStarted;
        turnEventsBound = false;
    }

    private void OnEnable() => BindTurnEvents();
    private void OnDisable() => UnbindTurnEvents();
}
