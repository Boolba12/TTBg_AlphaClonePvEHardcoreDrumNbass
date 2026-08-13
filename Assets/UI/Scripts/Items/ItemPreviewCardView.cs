using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct ItemPreviewCardState
{
    public ItemPreviewCardState(bool selected, bool equipped, bool disabled)
    {
        Selected = selected;
        Equipped = equipped;
        Disabled = disabled;
    }

    public bool Selected { get; }
    public bool Equipped { get; }
    public bool Disabled { get; }
}

public sealed class ItemPreviewCardView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Image frame;
    [SerializeField] private Image preview;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text categoryLabel;
    [SerializeField] private TMP_Text emptyLabel;
    [SerializeField] private Image selectionFrame;
    [SerializeField] private Image disabledOverlay;
    [SerializeField] private Button button;

    private string instanceId;
    private Action<string> selected;

    public string DisplayedId { get; private set; }
    public Sprite DisplayedPreview => preview != null ? preview.sprite : null;
    public bool IsEmpty { get; private set; }
    public bool IsSelected { get; private set; }
    public bool IsEquipped { get; private set; }
    public bool IsDisabled { get; private set; }
    public string InstanceId => instanceId;
    public Button Button => button;

    public void Configure(
        PurgatoryUITheme configuredTheme,
        Image configuredFrame,
        Image previewImage,
        TMP_Text configuredTitle,
        TMP_Text configuredCategory,
        TMP_Text configuredEmptyLabel,
        Image configuredSelectionFrame,
        Image configuredDisabledOverlay,
        Button configuredButton = null)
    {
        theme = configuredTheme;
        frame = configuredFrame;
        preview = previewImage;
        titleLabel = configuredTitle;
        categoryLabel = configuredCategory;
        emptyLabel = configuredEmptyLabel;
        selectionFrame = configuredSelectionFrame;
        disabledOverlay = configuredDisabledOverlay;
        button = configuredButton != null ? configuredButton : GetComponent<Button>();
        ApplyTheme();
        Render(null, false, true);
    }

    public void BindWeapon(EquipmentItemInstance instance, Weapon definition,
        ItemPreviewCardState state, Action<string> onSelected)
    {
        Unbind();
        instanceId = instance?.InstanceId ?? string.Empty;
        selected = onSelected;
        DisplayedId = definition?.StableId ?? string.Empty;
        IsEmpty = definition == null || definition.PreviewSprite == null;
        IsSelected = state.Selected;
        IsEquipped = state.Equipped;
        IsDisabled = state.Disabled;
        if (preview != null)
        {
            preview.sprite = !IsEmpty ? definition.PreviewSprite : theme?.IconPlaceholderSprite;
            preview.enabled = preview.sprite != null;
            preview.preserveAspect = true;
        }
        if (titleLabel != null) titleLabel.text = definition?.DisplayName ?? "Missing definition";
        if (categoryLabel != null)
            categoryLabel.text = state.Equipped
                ? $"{definition?.Class} • Equipped"
                : definition?.Class.ToString() ?? "Unavailable";
        if (emptyLabel != null)
        {
            emptyLabel.text = definition == null ? "Definition unavailable" : "Preview unavailable";
            emptyLabel.gameObject.SetActive(IsEmpty);
        }
        if (selectionFrame != null) selectionFrame.enabled = state.Selected;
        if (disabledOverlay != null) disabledOverlay.enabled = state.Disabled;
        if (button != null)
        {
            button.interactable = !state.Disabled;
            button.onClick.AddListener(HandleSelected);
        }
    }

    public void BindEquipment(EquipmentItemInstance instance,
        EquipmentItemDefinition definition, ItemPreviewCardState state,
        Action<string> onSelected)
    {
        if (definition is Weapon weapon)
        {
            BindWeapon(instance, weapon, state, onSelected);
            return;
        }

        Unbind();
        instanceId = instance?.InstanceId ?? string.Empty;
        selected = onSelected;
        DisplayedId = definition?.StableId ?? string.Empty;
        IsEmpty = definition == null || definition.PreviewSprite == null;
        IsSelected = state.Selected;
        IsEquipped = state.Equipped;
        IsDisabled = state.Disabled;
        if (preview != null)
        {
            preview.sprite = !IsEmpty ? definition.PreviewSprite : theme?.IconPlaceholderSprite;
            preview.enabled = preview.sprite != null;
            preview.preserveAspect = true;
        }
        if (titleLabel != null)
            titleLabel.text = definition?.DisplayName ?? "Missing definition";
        if (categoryLabel != null)
            categoryLabel.text = definition == null
                ? "Unavailable"
                : state.Equipped ? $"{definition.Category} - Equipped" : definition.Category.ToString();
        if (emptyLabel != null)
        {
            emptyLabel.text = definition == null
                ? "Definition unavailable" : "Preview unavailable";
            emptyLabel.gameObject.SetActive(IsEmpty);
        }
        if (selectionFrame != null) selectionFrame.enabled = state.Selected;
        if (disabledOverlay != null) disabledOverlay.enabled = state.Disabled;
        if (button != null)
        {
            button.interactable = !state.Disabled;
            button.onClick.AddListener(HandleSelected);
        }
    }

    private void HandleSelected() => selected?.Invoke(instanceId);

    private void Unbind()
    {
        button?.onClick.RemoveListener(HandleSelected);
        selected = null;
    }

    public void Render(ItemPresentationRecord record, bool selected, bool disabled)
    {
        Render(record, new ItemPreviewCardState(selected, false, disabled));
    }

    public void Render(ItemPresentationRecord record, ItemPreviewCardState state)
    {
        DisplayedId = record?.StableId ?? string.Empty;
        IsEmpty = record == null || record.PreviewSprite == null;
        IsSelected = state.Selected;
        IsEquipped = state.Equipped;
        IsDisabled = state.Disabled;
        if (preview != null)
        {
            preview.sprite = !IsEmpty ? record.PreviewSprite : theme?.IconPlaceholderSprite;
            preview.enabled = preview.sprite != null;
            preview.preserveAspect = true;
        }
        if (titleLabel != null)
            titleLabel.text = record?.DisplayName ?? "No item selected";
        if (categoryLabel != null)
        {
            string category = record != null ? record.Category.ToString() : "Empty";
            categoryLabel.text = state.Equipped ? $"{category} • Equipped" : category;
        }
        if (emptyLabel != null)
        {
            emptyLabel.text = record?.IsPlaceholder == true
                ? "Development placeholder"
                : "Preview unavailable";
            emptyLabel.gameObject.SetActive(IsEmpty);
        }
        if (selectionFrame != null)
            selectionFrame.enabled = state.Selected;
        if (disabledOverlay != null)
            disabledOverlay.enabled = state.Disabled;
    }

    private void ApplyTheme()
    {
        if (theme == null)
            return;
        if (frame != null)
        {
            frame.sprite = theme.EquipmentSlotSprite;
            frame.type = frame.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            frame.color = Color.white;
        }
        if (selectionFrame != null)
        {
            selectionFrame.sprite = theme.SelectedFrameSprite;
            selectionFrame.type = selectionFrame.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            selectionFrame.color = Color.white;
        }
        if (disabledOverlay != null)
            disabledOverlay.color = theme.Overlay;
        ApplyText(titleLabel, theme.TextPrimary, theme.BodySize);
        ApplyText(categoryLabel, theme.Gold, theme.CaptionSize);
        ApplyText(emptyLabel, theme.TextSecondary, theme.CaptionSize);
    }

    private void ApplyText(TMP_Text target, Color color, float size)
    {
        if (target == null)
            return;
        target.font = theme.PrimaryFont;
        target.fontSize = size;
        target.color = color;
    }

    private void OnDestroy() => Unbind();
}
