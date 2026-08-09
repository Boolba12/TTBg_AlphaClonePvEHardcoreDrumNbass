using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class AbilityCommandController : MonoBehaviour
{
    [SerializeField] private SquadBattleBootstrap squadBootstrap;
    [SerializeField] private BattleSquadSelectionController selectionController;
    [SerializeField] private BattleTurnController turnController;
    [SerializeField] private SquadMovementService movementService;
    [SerializeField] private BattleCommandModeController commandMode;
    [SerializeField] private BattleAbilityService abilityService;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField, Min(1f)] private float raycastDistance = 10000f;
    [SerializeField] private BattleHUDController battleHud;
    [SerializeField] private List<BattleActionControlView> abilityControls =
        new List<BattleActionControlView>();

    private readonly RaycastHit[] hitBuffer = new RaycastHit[24];
    private readonly Dictionary<ButtonKey, UnityAction> buttonHandlers =
        new Dictionary<ButtonKey, UnityAction>();
    private readonly Dictionary<string, Action> defeatHandlers =
        new Dictionary<string, Action>(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<float>> moraleHandlers =
        new Dictionary<string, Action<float>>(StringComparer.Ordinal);
    private bool listenersBound;
    private bool battleCommandsEnabled = true;
    private SquadAttackTarget hoveredTarget;
    private AbilityDefinition selectedAbility;

    public bool IsInitialized { get; private set; }
    public bool IsAbilityTargeting => commandMode != null &&
                                      commandMode.ActiveMode == BattleCommandMode.Ability;
    public AbilityDefinition SelectedAbility => selectedAbility;
    public SquadAttackTarget HoveredTarget => hoveredTarget;
    public BattleAbilityResult LastResult { get; private set; }
    public int AbilityCommandCount { get; private set; }
    public IReadOnlyList<BattleActionControlView> AbilityControls => abilityControls;

    public void Configure(
        SquadBattleBootstrap bootstrap,
        BattleSquadSelectionController selection,
        BattleTurnController turns,
        SquadMovementService movement,
        BattleCommandModeController modes,
        BattleAbilityService service,
        Camera camera,
        BattleHUDController hud,
        IEnumerable<BattleActionControlView> controls)
    {
        UnbindListeners();
        squadBootstrap = bootstrap;
        selectionController = selection;
        turnController = turns;
        movementService = movement;
        commandMode = modes;
        abilityService = service;
        inputCamera = camera;
        battleHud = hud;
        abilityControls = controls == null
            ? new List<BattleActionControlView>()
            : new List<BattleActionControlView>(controls);
        IsInitialized = false;
        if (isActiveAndEnabled && Application.isPlaying)
            BindListeners();
    }

    public bool Initialize()
    {
        IsInitialized = squadBootstrap != null && squadBootstrap.HasBootstrapped &&
                        selectionController != null && selectionController.IsInitialized &&
                        turnController != null && turnController.HasStarted &&
                        movementService != null && movementService.IsInitialized &&
                        commandMode != null && abilityService != null &&
                        abilityService.IsInitialized && battleHud != null &&
                        abilityControls.Count == abilityService.Abilities.Count;
        if (!IsInitialized)
            return false;
        for (int i = 0; i < abilityControls.Count; i++)
        {
            if (abilityControls[i]?.Button == null)
            {
                IsInitialized = false;
                return false;
            }
        }
        BindTargets();
        BindListeners();
        RefreshAvailability();
        return true;
    }

    public bool TryUseAbility(AbilityDefinition definition)
    {
        if (!battleCommandsEnabled || !IsInitialized || definition == null)
            return false;
        SquadBattleController caster = turnController.ActiveSquad;
        if (caster != null && selectionController.SelectedSquad != caster)
            selectionController.TrySelect(caster);
        BattleAbilityValidationResult validation = abilityService.ValidateAvailability(
            caster,
            definition,
            true);
        if (!validation.IsValid)
        {
            RenderControl(definition, validation, false);
            return false;
        }

        if (definition.TargetType == BattleAbilityTargetType.Self)
        {
            BattleAbilityPreview preview = abilityService.PreviewAbility(
                caster, caster, definition);
            battleHud.ShowAbilityPreview(preview, caster, definition);
            return Execute(caster, caster, definition);
        }

        if (IsAbilityTargeting && selectedAbility == definition)
        {
            CancelAbilityTargeting();
            return true;
        }
        selectedAbility = definition;
        if (!commandMode.TryEnter(BattleCommandMode.Ability))
        {
            selectedAbility = null;
            return false;
        }
        RefreshTargetHighlights();
        RefreshAvailability();
        return true;
    }

    public void CancelAbilityTargeting()
    {
        selectedAbility = null;
        if (IsAbilityTargeting)
            commandMode.Cancel();
        else
            ClearTargetingPresentation();
    }

    public BattleAbilityPreview TryHoverTarget(SquadAttackTarget target)
    {
        if (!IsAbilityTargeting || selectedAbility == null || target == null)
            return default;
        if (hoveredTarget != target)
        {
            RestoreTargetState(hoveredTarget);
            hoveredTarget = target;
        }
        BattleAbilityPreview preview = abilityService.PreviewAbility(
            turnController.ActiveSquad,
            target.Controller,
            selectedAbility);
        target.TargetingView?.SetState(preview.IsValid
            ? SquadAttackTargetVisualState.HoveredValid
            : SquadAttackTargetVisualState.HoveredInvalid);
        battleHud.ShowAbilityPreview(preview, target.Controller, selectedAbility);
        return preview;
    }

    public bool TryConfirmTarget(SquadAttackTarget target)
    {
        if (!battleCommandsEnabled || !IsInitialized || !IsAbilityTargeting ||
            selectedAbility == null || target == null)
            return false;
        AbilityDefinition definition = selectedAbility;
        SquadBattleController caster = turnController.ActiveSquad;
        BattleAbilityValidationResult validation = abilityService.ValidateCommand(
            caster,
            target.Controller,
            definition);
        if (!validation.IsValid)
        {
            TryHoverTarget(target);
            return false;
        }
        selectedAbility = null;
        commandMode.Cancel();
        return Execute(caster, target.Controller, definition);
    }

    public void SetBattleCommandsEnabled(bool enabled)
    {
        battleCommandsEnabled = enabled;
        if (!enabled)
            CancelAbilityTargeting();
        RefreshAvailability();
    }

    public void RefreshAvailability()
    {
        if (abilityService == null)
            return;
        SquadBattleController caster = turnController?.ActiveSquad;
        for (int i = 0; i < abilityService.Abilities.Count; i++)
        {
            AbilityDefinition definition = abilityService.Abilities[i];
            BattleAbilityValidationResult validation = battleCommandsEnabled && IsInitialized
                ? abilityService.ValidateAvailability(caster, definition, false)
                : BattleAbilityValidationResult.Reject(
                    BattleAbilityFailureReason.ServiceNotInitialized,
                    "Ability command is not initialized.");
            RenderControl(definition, validation,
                IsAbilityTargeting && selectedAbility == definition);
        }
    }

    private bool Execute(
        SquadBattleController caster,
        SquadBattleController target,
        AbilityDefinition definition)
    {
        bool accepted = abilityService.TryExecuteAbility(
            caster,
            target,
            definition,
            out BattleAbilityResult result);
        LastResult = result;
        if (result.WasExecuted)
        {
            AbilityCommandCount++;
            caster.AttackTarget?.TargetingView?.ShowAttackPulse();
            target?.AttackTarget?.TargetingView?.ShowAbilityResult(result);
            battleHud.ShowAbilityResult(result, definition);
        }
        RefreshAvailability();
        return accepted;
    }

    private void RenderControl(
        AbilityDefinition definition,
        BattleAbilityValidationResult validation,
        bool selected)
    {
        int index = -1;
        for (int i = 0; i < abilityService.Abilities.Count; i++)
        {
            if (abilityService.Abilities[i] == definition)
            {
                index = i;
                break;
            }
        }
        if (index < 0 || index >= abilityControls.Count || abilityControls[index] == null)
            return;
        BattleAbilityRuntimeState state = abilityService.GetRuntimeState(
            turnController?.ActiveSquadId,
            definition.StableId);
        string cooldown = state != null && state.remainingCooldown > 0
            ? $"CD {state.remainingCooldown}"
            : validation.IsValid ? "Ready" : validation.Reason;
        abilityControls[index].RenderCommand(
            definition.DisplayName,
            definition.Hotkey.ToString().Replace("Digit", string.Empty),
            $"{definition.ActionPointCost} AP",
            validation.IsValid || selected,
            selected,
            selected ? "Choose an enemy squad" : cooldown,
            definition.Icon);
    }

    private void Update()
    {
        if (!battleCommandsEnabled || !IsInitialized || Keyboard.current == null)
            return;
        foreach (AbilityDefinition definition in abilityService.Abilities)
        {
            if (Keyboard.current[definition.Hotkey].wasPressedThisFrame)
            {
                TryUseAbility(definition);
                break;
            }
        }
        if (Keyboard.current.escapeKey.wasPressedThisFrame ||
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
            CancelAbilityTargeting();
        if (!IsAbilityTargeting || Mouse.current == null)
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
            ray, hitBuffer, raycastDistance, targetLayers, QueryTriggerInteraction.Collide);
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
        if (!IsAbilityTargeting || selectedAbility == null)
            return;
        SquadBattleController caster = turnController.ActiveSquad;
        foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
        {
            SquadAttackTarget target = controller?.AttackTarget;
            if (target?.TargetingView == null)
                continue;
            if (controller == caster)
            {
                target.TargetingView.SetState(SquadAttackTargetVisualState.None);
                continue;
            }
            bool valid = abilityService.ValidateCommand(
                caster,
                controller,
                selectedAbility).IsValid;
            target.TargetingView.SetState(valid
                ? SquadAttackTargetVisualState.Available
                : SquadAttackTargetVisualState.Unavailable);
        }
    }

    private void RestoreTargetState(SquadAttackTarget target)
    {
        if (target?.TargetingView == null || !IsAbilityTargeting || selectedAbility == null)
            return;
        bool valid = abilityService.ValidateCommand(
            turnController.ActiveSquad,
            target.Controller,
            selectedAbility).IsValid;
        target.TargetingView.SetState(valid
            ? SquadAttackTargetVisualState.Available
            : SquadAttackTargetVisualState.Unavailable);
    }

    private void ClearHoveredTarget()
    {
        RestoreTargetState(hoveredTarget);
        hoveredTarget = null;
        battleHud?.ClearAbilityPreview();
    }

    private void ClearTargetingPresentation()
    {
        hoveredTarget = null;
        if (squadBootstrap != null)
        {
            foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
                controller?.AttackTarget?.TargetingView?.SetState(SquadAttackTargetVisualState.None);
        }
        battleHud?.ClearAbilityPreview();
        RefreshAvailability();
    }

    private void BindTargets()
    {
        foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
        {
            if (controller?.AttackTarget == null)
                continue;
            controller.AttackTarget.OnConfirmRequested -= HandleTargetConfirmRequested;
            controller.AttackTarget.OnConfirmRequested += HandleTargetConfirmRequested;
            if (controller.Runtime != null && !defeatHandlers.ContainsKey(controller.SquadId))
            {
                Action handler = () => HandleSquadDefeated(controller);
                defeatHandlers.Add(controller.SquadId, handler);
                controller.Runtime.OnSquadDefeated += handler;
            }
            if (controller.Runtime != null && !moraleHandlers.ContainsKey(controller.SquadId))
            {
                Action<float> handler = _ => HandleMoraleChanged(controller);
                moraleHandlers.Add(controller.SquadId, handler);
                controller.Runtime.OnMoraleChanged += handler;
            }
        }
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;
        int definitionCount = abilityService != null ? abilityService.Abilities.Count : 0;
        for (int i = 0; i < abilityControls.Count && i < definitionCount; i++)
        {
            BattleActionControlView control = abilityControls[i];
            AbilityDefinition definition = abilityService.Abilities[i];
            if (control?.Button == null)
                continue;
            ButtonKey key = new ButtonKey(control, definition.StableId);
            UnityAction handler = () => TryUseAbility(definition);
            buttonHandlers[key] = handler;
            control.Button.onClick.AddListener(handler);
        }
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
        if (abilityService != null)
        {
            abilityService.OnAbilityResolved += HandleAbilityResolved;
            abilityService.OnCooldownChanged += HandleCooldownChanged;
        }
        listenersBound = true;
    }

    private void UnbindListeners()
    {
        foreach (KeyValuePair<ButtonKey, UnityAction> pair in buttonHandlers)
        {
            if (pair.Key.Control?.Button != null)
                pair.Key.Control.Button.onClick.RemoveListener(pair.Value);
        }
        buttonHandlers.Clear();
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
        if (abilityService != null)
        {
            abilityService.OnAbilityResolved -= HandleAbilityResolved;
            abilityService.OnCooldownChanged -= HandleCooldownChanged;
        }
        if (squadBootstrap != null)
        {
            foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
            {
                if (controller?.AttackTarget != null)
                    controller.AttackTarget.OnConfirmRequested -= HandleTargetConfirmRequested;
                if (controller?.Runtime != null &&
                    defeatHandlers.TryGetValue(controller.SquadId, out Action handler))
                    controller.Runtime.OnSquadDefeated -= handler;
                if (controller?.Runtime != null &&
                    moraleHandlers.TryGetValue(
                        controller.SquadId,
                        out Action<float> moraleHandler))
                    controller.Runtime.OnMoraleChanged -= moraleHandler;
            }
        }
        defeatHandlers.Clear();
        moraleHandlers.Clear();
        listenersBound = false;
    }

    private void HandleTargetConfirmRequested(SquadAttackTarget target) => TryConfirmTarget(target);
    private void HandleSelectionChanged(SquadBattleController selected)
    {
        if (IsAbilityTargeting && selected != turnController.ActiveSquad)
            CancelAbilityTargeting();
        RefreshAvailability();
    }
    private void HandleActiveSquadChanged(SquadBattleController _)
    {
        CancelAbilityTargeting();
        RefreshAvailability();
    }
    private void HandleModeChanged(BattleCommandMode mode)
    {
        if (mode == BattleCommandMode.Ability)
            RefreshTargetHighlights();
        else
        {
            selectedAbility = null;
            ClearTargetingPresentation();
        }
        RefreshAvailability();
    }
    private void HandleMovementStarted(SquadMovementPlan _) => RefreshAvailability();
    private void HandleMovementCompleted(SquadMovementPlan _) => RefreshAvailability();
    private void HandleMovementFailed(string _) => RefreshAvailability();
    private void HandleAbilityResolved(BattleAbilityResult _) => RefreshAvailability();
    private void HandleCooldownChanged(BattleAbilityRuntimeState _) => RefreshAvailability();
    private void HandleSquadDefeated(SquadBattleController controller)
    {
        if (controller == turnController.ActiveSquad || hoveredTarget?.Controller == controller)
            CancelAbilityTargeting();
        RefreshAvailability();
    }
    private void HandleMoraleChanged(SquadBattleController controller)
    {
        if (controller == turnController?.ActiveSquad)
            RefreshAvailability();
    }

    private void OnEnable() => BindListeners();
    private void OnDisable()
    {
        CancelAbilityTargeting();
        UnbindListeners();
    }

    private readonly struct ButtonKey : IEquatable<ButtonKey>
    {
        public ButtonKey(BattleActionControlView control, string abilityId)
        {
            Control = control;
            AbilityId = abilityId;
        }
        public BattleActionControlView Control { get; }
        public string AbilityId { get; }
        public bool Equals(ButtonKey other) => Control == other.Control && AbilityId == other.AbilityId;
        public override bool Equals(object obj) => obj is ButtonKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Control, AbilityId);
    }
}
