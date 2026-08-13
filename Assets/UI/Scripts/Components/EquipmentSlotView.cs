using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EquipmentSlotView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Image frame;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text emptyLabel;
    [SerializeField] private Button button;

    private EquipmentSlotKind slot;
    private Action<EquipmentSlotKind> selected;

    public EquipmentSlotKind Slot => slot;
    public Button Button => button;

    public void Configure(
        PurgatoryUITheme configuredTheme,
        Image frameImage,
        Image iconImage,
        TMP_Text label,
        Button configuredButton)
    {
        theme = configuredTheme;
        frame = frameImage;
        icon = iconImage;
        emptyLabel = label;
        button = configuredButton;
        ApplyTheme();
    }

    public void Render(EquipmentSlotPresentationModel model)
    {
        bool occupied = model != null && model.occupied && model.icon != null;
        if (icon != null)
        {
            icon.sprite = occupied ? model.icon : null;
            icon.enabled = occupied;
        }
        if (emptyLabel != null)
        {
            emptyLabel.text = model?.label ?? string.Empty;
            emptyLabel.gameObject.SetActive(!occupied);
        }
        if (button != null)
            button.interactable = model != null && model.interactable;
    }

    public void Bind(EquipmentSlotKind configuredSlot,
        EquipmentSlotPresentationModel model, Action<EquipmentSlotKind> onSelected)
    {
        button?.onClick.RemoveListener(HandleSelected);
        slot = configuredSlot;
        selected = onSelected;
        Render(model);
        button?.onClick.AddListener(HandleSelected);
    }

    private void HandleSelected() => selected?.Invoke(slot);

    private void OnDestroy()
    {
        button?.onClick.RemoveListener(HandleSelected);
        selected = null;
    }

    public void ApplyTheme()
    {
        if (theme == null)
            return;
        if (frame != null)
        {
            frame.sprite = theme.EquipmentSlotSprite;
            frame.type = frame.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            frame.color = Color.white;
        }
        if (emptyLabel != null)
        {
            emptyLabel.font = theme.PrimaryFont;
            emptyLabel.fontSize = theme.CaptionSize;
            emptyLabel.color = theme.Disabled;
        }
    }
}
