using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class AttackCommandController : MonoBehaviour
{
    [Header("Battle services")]
    [SerializeField] private SquadBattleBootstrap squadBootstrap;
    [SerializeField] private BattleSquadSelectionController selectionController;
    [SerializeField] private BattleTurnController turnController;
    [SerializeField] private SquadMovementService movementService;
    [SerializeField] private BattleCommandModeController commandMode;
    [SerializeField] private BattleAttackService attackService;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField, Min(1f)] private float raycastDistance = 10000f;

    [Header("HUD command")]
    [SerializeField] private BattleActionControlView attackAction;
    [SerializeField] private BattleActionControlView rangedAttackAction;
    [SerializeField] private BattleHUDController battleHud;
    [SerializeField] private AttackRangePreviewView rangePreview;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[24];
    private readonly Dictionary<string, Action> defeatHandlers =
        new Dictionary<string, Action>();
    private bool listenersBound;
    private SquadAttackTarget hoveredTarget;
    private bool battleCommandsEnabled = true;
    private AttackDefinition activeAttackDefinition;

    public bool IsInitialized { get; private set; }
    public bool IsAttackTargeting =>
        commandMode != null && commandMode.ActiveMode == BattleCommandMode.Attack;
    public SquadAttackTarget HoveredTarget => hoveredTarget;
    public int AttackCommandCount { get; private set; }
    public BattleAttackResult LastResult { get; private set; }
    public AttackDefinition ActiveAttackDefinition => activeAttackDefinition;

    public void Configure(
        SquadBattleBootstrap bootstrap,
        BattleSquadSelectionController selection,
        BattleTurnController turns,
        SquadMovementService movement,
        BattleCommandModeController modes,
        BattleAttackService attacks,
        Camera camera,
        BattleActionControlView attackControl,
        BattleHUDController hud)
    {
        UnbindListeners();
        squadBootstrap = bootstrap;
        selectionController = selection;
        turnController = turns;
        movementService = movement;
        commandMode = modes;
        attackService = attacks;
        inputCamera = camera;
        attackAction = attackControl;
        battleHud = hud;
        if (isActiveAndEnabled)
            BindListeners();
    }

    public void ConfigureRanged(BattleActionControlView rangedControl,
        AttackRangePreviewView configuredRangePreview)
    {
        UnbindListeners();
        rangedAttackAction = rangedControl;
        rangePreview = configuredRangePreview;
        if (isActiveAndEnabled)
            BindListeners();
    }

    public bool Initialize()
    {
        IsInitialized = squadBootstrap != null && squadBootstrap.HasBootstrapped &&
                        selectionController != null && selectionController.IsInitialized &&
                        turnController != null && turnController.HasStarted &&
                        movementService != null && movementService.IsInitialized &&
                        commandMode != null && attackService != null &&
                        attackService.IsInitialized && attackAction != null && battleHud != null &&
                        (attackService.RangedAttack == null ||
                         (rangedAttackAction != null && rangePreview != null));
        if (!IsInitialized)
            return false;

        BindTargets();
        BindListeners();
        RefreshAvailability();
        return true;
    }

    public bool TryBeginAttackTargeting()
    {
        return TryBeginAttackTargeting(attackService?.BasicAttack);
    }

    public bool TryBeginRangedTargeting()
    {
        return TryBeginAttackTargeting(attackService?.RangedAttack);
    }

    public bool TryBeginAttackTargeting(AttackDefinition definition)
    {
        if (!battleCommandsEnabled || !IsInitialized || definition == null ||
            attackService.IsExecuting || movementService.IsMoving)
            return false;

        SquadBattleController attacker = turnController.ActiveSquad;
        if (attacker != null && selectionController.SelectedSquad != attacker)
            selectionController.TrySelect(attacker);
        BattleAttackValidationResult validation = attackService.ValidateAvailability(
            attacker,
            definition,
            true,
            true);
        if (!validation.IsValid)
        {
            ResolveControl(definition)?.SetCommandState(
                false, false, validation.Reason, validation.Reason);
            return false;
        }

        activeAttackDefinition = definition;
        if (!commandMode.TryEnter(BattleCommandMode.Attack))
        {
            activeAttackDefinition = null;
            return false;
        }
        rangePreview?.ShowRange(
            attacker.GridAnchor.CurrentCell,
            activeAttackDefinition,
            movementService.AllowDiagonalMovement);
        RefreshTargetHighlights();
        RefreshAvailability();
        return true;
    }

    public void CancelAttackTargeting()
    {
        if (IsAttackTargeting)
            commandMode.Cancel();
        else
            ClearTargetingPresentation();
    }

    public BattleAttackPreview TryHoverTarget(SquadAttackTarget target)
    {
        if (!IsAttackTargeting || target == null || activeAttackDefinition == null)
            return default;

        if (hoveredTarget != target)
        {
            RestoreTargetState(hoveredTarget);
            hoveredTarget = target;
        }

        BattleAttackPreview preview = attackService.PreviewAttack(
            turnController.ActiveSquad,
            target.Controller,
            activeAttackDefinition);
        target.TargetingView?.SetState(
            preview.IsValid
                ? SquadAttackTargetVisualState.HoveredValid
                : SquadAttackTargetVisualState.HoveredInvalid);
        target.TargetingView?.ShowCoverIndicator(preview.CoverType);
        if (activeAttackDefinition.Delivery == BattleAttackDelivery.Ranged)
        {
            BattleAttackTargetEvaluation geometry =
                attackService.TargetingService.EvaluateTarget(
                    turnController.ActiveSquad,
                    target.Controller,
                    activeAttackDefinition);
            rangePreview?.ShowLine(geometry.LineOfSight);
        }
        else
        {
            rangePreview?.ClearLine();
        }
        battleHud.ShowAttackPreview(preview, target.Controller, activeAttackDefinition);
        return preview;
    }

    public bool TryConfirmTarget(SquadAttackTarget target)
    {
        if (!battleCommandsEnabled || !IsInitialized || !IsAttackTargeting || target == null)
            return false;

        SquadBattleController attacker = turnController.ActiveSquad;
        AttackDefinition definition = activeAttackDefinition;
        BattleAttackValidationResult validation = attackService.ValidateCommand(
            attacker,
            target.Controller,
            definition);
        if (!validation.IsValid)
        {
            TryHoverTarget(target);
            return false;
        }

        commandMode.Cancel();
        bool accepted = attackService.TryExecuteAttack(
            attacker,
            target.Controller,
            out BattleAttackResult result,
            definition);
        LastResult = result;
        if (result.WasExecuted)
        {
            AttackCommandCount++;
            attacker.AttackTarget?.TargetingView?.ShowAttackPulse();
            target.TargetingView?.ShowResult(result);
            battleHud.ShowAttackResult(result, target.Controller);
        }
        RefreshAvailability();
        return accepted;
    }

    public void RefreshAvailability()
    {
        RenderAvailability(attackAction, attackService?.BasicAttack, "Attack");
        RenderAvailability(rangedAttackAction, attackService?.RangedAttack, "Ranged");
    }

    private void RenderAvailability(BattleActionControlView control,
        AttackDefinition definition, string fallbackLabel)
    {
        if (control == null || definition == null)
            return;
        string hotkey = definition != null ? definition.Hotkey.ToString() : "A";
        string cost = definition != null ? $"{definition.ActionPointCost} AP" : "AP —";
        BattleAttackValidationResult validation = battleCommandsEnabled && IsInitialized
            ? attackService.ValidateAvailability(
                turnController.ActiveSquad,
                definition,
                false,
                true)
            : BattleAttackValidationResult.Reject(
                BattleAttackFailureReason.ServiceNotInitialized,
                "Attack command is not initialized.");
        bool selected = IsAttackTargeting && activeAttackDefinition == definition;
        bool interactable = validation.IsValid || selected;
        string state = selected
            ? "Choose an enemy squad"
            : validation.IsValid ? "Select a target" : validation.Reason;
        Sprite weaponPreview = definition.Delivery == BattleAttackDelivery.Melee
            ? turnController.ActiveSquad?.Runtime?.Equipment?.SquadWeapon?.PreviewSprite
            : null;
        control.RenderCommand(
            string.IsNullOrWhiteSpace(definition.DisplayName)
                ? fallbackLabel
                : definition.DisplayName,
            hotkey,
            cost,
            interactable,
            selected,
            validation.IsValid ? state : validation.Reason,
            weaponPreview != null ? weaponPreview : definition?.PreviewSprite);
    }

    public void SetBattleCommandsEnabled(bool enabled)
    {
        battleCommandsEnabled = enabled;
        if (!enabled)
            CancelAttackTargeting();
        RefreshAvailability();
    }

    private void Update()
    {
        if (!battleCommandsEnabled || !IsInitialized || Keyboard.current == null)
            return;

        AttackDefinition definition = attackService.BasicAttack;
        if (definition != null && Keyboard.current[definition.Hotkey].wasPressedThisFrame)
        {
            if (IsAttackTargeting)
                CancelAttackTargeting();
            else
                TryBeginAttackTargeting();
        }
        AttackDefinition ranged = attackService.RangedAttack;
        if (ranged != null && ranged.Hotkey != Key.None &&
            Keyboard.current[ranged.Hotkey].wasPressedThisFrame)
        {
            if (IsAttackTargeting && activeAttackDefinition == ranged)
                CancelAttackTargeting();
            else
                TryBeginAttackTargeting(ranged);
        }
        if (Keyboard.current.escapeKey.wasPressedThisFrame ||
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
        {
            CancelAttackTargeting();
        }

        if (!IsAttackTargeting || Mouse.current == null)
            return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearHoveredTarget();
            return;
        }

        SquadAttackTarget target = FindTargetAtPointer();
        if (target != null)
            TryHoverTarget(target);
        else
            ClearHoveredTarget();
        if (target != null && Mouse.current.leftButton.wasPressedThisFrame)
            target.RequestConfirm();
    }

    private SquadAttackTarget FindTargetAtPointer()
    {
        Camera camera = inputCamera != null ? inputCamera : Camera.main;
        if (camera == null)
            return null;
        Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        int count = Physics.RaycastNonAlloc(
            ray,
            hitBuffer,
            raycastDistance,
            targetLayers,
            QueryTriggerInteraction.Collide);
        SquadAttackTarget closest = null;
        float distance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            SquadAttackTarget candidate =
                hitBuffer[i].collider.GetComponentInParent<SquadAttackTarget>();
            if (candidate == null || hitBuffer[i].distance >= distance)
                continue;
            closest = candidate;
            distance = hitBuffer[i].distance;
        }
        return closest;
    }

    private void RefreshTargetHighlights()
    {
        if (squadBootstrap == null || activeAttackDefinition == null)
            return;
        SquadBattleController attacker = turnController.ActiveSquad;
        foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
        {
            SquadAttackTarget target = controller?.AttackTarget;
            if (target?.TargetingView == null)
                continue;
            if (controller == attacker)
            {
                target.TargetingView.SetState(SquadAttackTargetVisualState.None);
                continue;
            }
            bool valid = attackService.TargetingService.ValidateTarget(
                attacker,
                controller,
                activeAttackDefinition).IsValid;
            target.TargetingView.SetState(valid
                ? SquadAttackTargetVisualState.Available
                : SquadAttackTargetVisualState.Unavailable);
        }
    }

    private void RestoreTargetState(SquadAttackTarget target)
    {
        if (target?.TargetingView == null || !IsAttackTargeting)
            return;
        BattleAttackValidationResult validation = attackService.TargetingService.ValidateTarget(
            turnController.ActiveSquad,
            target.Controller,
            activeAttackDefinition);
        target.TargetingView.SetState(validation.IsValid
            ? SquadAttackTargetVisualState.Available
            : SquadAttackTargetVisualState.Unavailable);
    }

    private void ClearHoveredTarget()
    {
        RestoreTargetState(hoveredTarget);
        hoveredTarget?.TargetingView?.ClearCoverIndicator();
        hoveredTarget = null;
        rangePreview?.ClearLine();
        battleHud?.ClearAttackPreview();
    }

    private void ClearTargetingPresentation()
    {
        hoveredTarget?.TargetingView?.ClearCoverIndicator();
        hoveredTarget = null;
        if (squadBootstrap != null)
        {
            foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
            {
                controller?.AttackTarget?.TargetingView?.SetState(
                    SquadAttackTargetVisualState.None);
                controller?.AttackTarget?.TargetingView?.ClearCoverIndicator();
            }
        }
        rangePreview?.Clear();
        activeAttackDefinition = null;
        battleHud?.ClearAttackPreview();
        RefreshAvailability();
    }

    private void BindTargets()
    {
        foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
        {
            if (controller?.AttackTarget == null)
                continue;
            controller.AttackTarget.Bind(controller);
            controller.AttackTarget.OnConfirmRequested -= HandleTargetConfirmRequested;
            controller.AttackTarget.OnConfirmRequested += HandleTargetConfirmRequested;
            if (controller.Runtime != null && !defeatHandlers.ContainsKey(controller.SquadId))
            {
                Action handler = () => HandleSquadDefeated(controller);
                defeatHandlers.Add(controller.SquadId, handler);
                controller.Runtime.OnSquadDefeated += handler;
            }
        }
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;
        if (attackAction?.Button != null)
            attackAction.Button.onClick.AddListener(HandleAttackClicked);
        if (rangedAttackAction?.Button != null)
            rangedAttackAction.Button.onClick.AddListener(HandleRangedAttackClicked);
        if (selectionController != null)
            selectionController.OnSelectedSquadChanged += HandleSelectionChanged;
        if (turnController != null)
            turnController.OnActiveSquadChanged += HandleActiveSquadChanged;
        if (commandMode != null)
            commandMode.OnModeChanged += HandleModeChanged;
        if (movementService != null)
        {
            movementService.OnMovementStarted += HandleMovementStarted;
            movementService.OnMovementCompleted += HandleMovementCompleted;
            movementService.OnMovementFailed += HandleMovementFailed;
        }
        if (attackService != null)
            attackService.OnAttackResolved += HandleAttackResolved;
        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (attackAction?.Button != null)
            attackAction.Button.onClick.RemoveListener(HandleAttackClicked);
        if (rangedAttackAction?.Button != null)
            rangedAttackAction.Button.onClick.RemoveListener(HandleRangedAttackClicked);
        if (selectionController != null)
            selectionController.OnSelectedSquadChanged -= HandleSelectionChanged;
        if (turnController != null)
            turnController.OnActiveSquadChanged -= HandleActiveSquadChanged;
        if (commandMode != null)
            commandMode.OnModeChanged -= HandleModeChanged;
        if (movementService != null)
        {
            movementService.OnMovementStarted -= HandleMovementStarted;
            movementService.OnMovementCompleted -= HandleMovementCompleted;
            movementService.OnMovementFailed -= HandleMovementFailed;
        }
        if (attackService != null)
            attackService.OnAttackResolved -= HandleAttackResolved;
        if (squadBootstrap != null)
        {
            foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
            {
                if (controller?.AttackTarget != null)
                    controller.AttackTarget.OnConfirmRequested -= HandleTargetConfirmRequested;
                if (controller?.Runtime != null &&
                    defeatHandlers.TryGetValue(controller.SquadId, out Action handler))
                {
                    controller.Runtime.OnSquadDefeated -= handler;
                }
            }
        }
        defeatHandlers.Clear();
        listenersBound = false;
    }

    private void HandleAttackClicked()
    {
        if (IsAttackTargeting && activeAttackDefinition == attackService.BasicAttack)
            CancelAttackTargeting();
        else
            TryBeginAttackTargeting();
    }

    private void HandleRangedAttackClicked()
    {
        if (IsAttackTargeting && activeAttackDefinition == attackService.RangedAttack)
            CancelAttackTargeting();
        else
            TryBeginRangedTargeting();
    }

    private void HandleTargetConfirmRequested(SquadAttackTarget target) =>
        TryConfirmTarget(target);

    private void HandleSelectionChanged(SquadBattleController selected)
    {
        if (IsAttackTargeting && selected != turnController.ActiveSquad)
            CancelAttackTargeting();
        RefreshAvailability();
    }

    private void HandleActiveSquadChanged(SquadBattleController _)
    {
        CancelAttackTargeting();
        RefreshAvailability();
    }

    private void HandleModeChanged(BattleCommandMode mode)
    {
        if (mode == BattleCommandMode.Attack)
            RefreshTargetHighlights();
        else
            ClearTargetingPresentation();
        RefreshAvailability();
    }

    private void HandleMovementStarted(SquadMovementPlan _) => RefreshAvailability();
    private void HandleMovementCompleted(SquadMovementPlan _) => RefreshAvailability();
    private void HandleMovementFailed(string _) => RefreshAvailability();
    private void HandleAttackResolved(BattleAttackResult _) => RefreshAvailability();

    private void HandleSquadDefeated(SquadBattleController controller)
    {
        if (controller == turnController.ActiveSquad ||
            hoveredTarget?.Controller == controller)
        {
            CancelAttackTargeting();
        }
        RefreshAvailability();
    }

    private void OnEnable() => BindListeners();

    private void OnDisable()
    {
        CancelAttackTargeting();
        UnbindListeners();
    }

    private BattleActionControlView ResolveControl(AttackDefinition definition)
    {
        return definition != null && definition == attackService?.RangedAttack
            ? rangedAttackAction
            : attackAction;
    }
}
