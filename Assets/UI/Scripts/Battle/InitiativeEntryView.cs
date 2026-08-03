using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InitiativeEntryView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private Image background;
    [SerializeField] private Image sideAccent;
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text squadIdLabel;
    [SerializeField] private TMP_Text initiativeLabel;
    [SerializeField] private SelectionHighlightView selectionHighlight;

    public string DisplayedSquadId { get; private set; }
    public Sprite DisplayedPortrait => portrait != null ? portrait.sprite : null;
    public bool DisplaysDefeatedState { get; private set; }

    public void Configure(
        PurgatoryUITheme configuredTheme,
        Image backgroundImage,
        Image sideAccentImage,
        Image portraitImage,
        TMP_Text squadLabel,
        TMP_Text valueLabel,
        SelectionHighlightView highlight)
    {
        theme = configuredTheme;
        background = backgroundImage;
        sideAccent = sideAccentImage;
        portrait = portraitImage;
        squadIdLabel = squadLabel;
        initiativeLabel = valueLabel;
        selectionHighlight = highlight;
        ApplyTheme();
    }

    public void Render(InitiativeEntryModel model)
    {
        DisplayedSquadId = model.SquadId;
        DisplaysDefeatedState = model.IsDefeated;
        if (squadIdLabel != null)
            squadIdLabel.text = model.SquadId;
        if (initiativeLabel != null)
            initiativeLabel.text = UIStatFormatter.FormatInteger(model.Initiative);
        if (portrait != null)
        {
            portrait.sprite = model.Portrait != null
                ? model.Portrait
                : theme?.DevelopmentPortraitFallback;
            portrait.enabled = portrait.sprite != null;
            portrait.color = model.IsDefeated
                ? new Color(0.42f, 0.42f, 0.42f, 0.82f)
                : Color.white;
        }
        if (background != null && theme != null)
            background.color = model.IsDefeated ? theme.Disabled : Color.white;
        if (sideAccent != null && theme != null)
            sideAccent.color = model.Side == BattleSide.Player
                ? theme.PlayerSide
                : theme.EnemySide;
        if (squadIdLabel != null && theme != null)
            squadIdLabel.color = model.IsDefeated ? theme.Disabled : theme.TextPrimary;
        selectionHighlight?.SetHighlighted(model.IsSelected);
    }

    private void ApplyTheme()
    {
        if (theme == null)
            return;
        if (background != null)
        {
            background.sprite = theme.InitiativeCardSprite;
            background.type = background.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            background.color = Color.white;
        }
        if (squadIdLabel != null)
        {
            squadIdLabel.font = theme.PrimaryFont;
            squadIdLabel.fontSize = theme.CaptionSize;
            squadIdLabel.color = theme.TextPrimary;
        }
        if (initiativeLabel != null)
        {
            initiativeLabel.font = theme.AccentFont;
            initiativeLabel.fontSize = theme.BodySize;
            initiativeLabel.color = theme.Gold;
        }
    }
}
