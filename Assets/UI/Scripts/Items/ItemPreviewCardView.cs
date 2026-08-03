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

    public string DisplayedId { get; private set; }
    public Sprite DisplayedPreview => preview != null ? preview.sprite : null;
    public bool IsEmpty { get; private set; }
    public bool IsSelected { get; private set; }
    public bool IsEquipped { get; private set; }
    public bool IsDisabled { get; private set; }

    public void Configure(
        PurgatoryUITheme configuredTheme,
        Image configuredFrame,
        Image previewImage,
        TMP_Text configuredTitle,
        TMP_Text configuredCategory,
        TMP_Text configuredEmptyLabel,
        Image configuredSelectionFrame,
        Image configuredDisabledOverlay)
    {
        theme = configuredTheme;
        frame = configuredFrame;
        preview = previewImage;
        titleLabel = configuredTitle;
        categoryLabel = configuredCategory;
        emptyLabel = configuredEmptyLabel;
        selectionFrame = configuredSelectionFrame;
        disabledOverlay = configuredDisabledOverlay;
        ApplyTheme();
        Render(null, false, true);
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
}
