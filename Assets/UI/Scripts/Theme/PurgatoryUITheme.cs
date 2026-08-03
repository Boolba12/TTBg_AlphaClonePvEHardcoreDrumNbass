using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "PurgatoryUITheme", menuName = "Game/UI/Purgatory UI Theme")]
public sealed class PurgatoryUITheme : ScriptableObject
{
    [Header("Typography")]
    [SerializeField] private TMP_FontAsset primaryFont;
    [SerializeField] private TMP_FontAsset accentFont;
    [SerializeField, Min(8)] private float captionSize = 18f;
    [SerializeField, Min(8)] private float bodySize = 22f;
    [SerializeField, Min(8)] private float headingSize = 30f;

    [Header("Replaceable sprites")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Sprite frameSprite;
    [SerializeField] private Sprite separatorSprite;
    [SerializeField] private Sprite developmentPortraitFallback;

    [Header("Battle HUD visual-pass sprites")]
    [SerializeField] private Sprite insetPanelSprite;
    [SerializeField] private Sprite sectionHeaderSprite;
    [SerializeField] private Sprite selectedFrameSprite;
    [SerializeField] private Sprite buttonHoverSprite;
    [SerializeField] private Sprite buttonPressedSprite;
    [SerializeField] private Sprite buttonDisabledSprite;
    [SerializeField] private Sprite portraitFrameSprite;
    [SerializeField] private Sprite initiativeCardSprite;
    [SerializeField] private Sprite equipmentSlotSprite;
    [SerializeField] private Sprite emptySlotSprite;
    [SerializeField] private Sprite iconPlaceholderSprite;

    [Header("Purgatory palette")]
    [SerializeField] private Color blackStone = new Color32(15, 17, 18, 245);
    [SerializeField] private Color darkSteel = new Color32(35, 42, 45, 245);
    [SerializeField] private Color granite = new Color32(67, 72, 70, 255);
    [SerializeField] private Color marble = new Color32(215, 214, 199, 255);
    [SerializeField] private Color bronze = new Color32(133, 88, 42, 255);
    [SerializeField] private Color gold = new Color32(207, 164, 70, 255);
    [SerializeField] private Color emerald = new Color32(31, 135, 89, 255);
    [SerializeField] private Color danger = new Color32(155, 51, 47, 255);
    [SerializeField] private Color disabled = new Color32(91, 94, 91, 190);

    [Header("Semantic colors")]
    [SerializeField] private Color surfaceInset = new Color32(21, 25, 26, 248);
    [SerializeField] private Color surfaceRaised = new Color32(42, 48, 49, 248);
    [SerializeField] private Color playerSide = new Color32(52, 116, 91, 255);
    [SerializeField] private Color enemySide = new Color32(126, 64, 54, 255);
    [SerializeField] private Color textPrimary = new Color32(223, 220, 202, 255);
    [SerializeField] private Color textSecondary = new Color32(154, 157, 147, 255);
    [SerializeField] private Color overlay = new Color32(8, 10, 10, 210);
    [SerializeField] private Color separator = new Color32(145, 98, 49, 255);

    [Header("Layout tokens")]
    [SerializeField, Min(0)] private float spaceSmall = 8f;
    [SerializeField, Min(0)] private float spaceMedium = 16f;
    [SerializeField, Min(0)] private float spaceLarge = 24f;
    [SerializeField, Min(0)] private float safeMargin = 28f;
    [SerializeField, Min(0)] private float cornerRadius = 8f;
    [SerializeField, Min(0)] private float borderWidth = 2f;
    [SerializeField, Min(1)] private float minimumButtonHeight = 48f;
    [SerializeField, Min(0)] private float compactPadding = 6f;
    [SerializeField, Min(0)] private float panelPadding = 10f;
    [SerializeField, Min(0)] private float compactSpacing = 6f;
    [SerializeField, Min(1)] private float portraitSize = 116f;
    [SerializeField, Min(1)] private float initiativePortraitSize = 56f;
    [SerializeField, Min(1)] private float initiativeCardWidth = 156f;
    [SerializeField, Min(1)] private float initiativeCardHeight = 66f;
    [SerializeField, Min(1)] private float actionControlHeight = 60f;

    [Header("Localization-ready defaults")]
    [SerializeField] private string squadLabel = "Squad";
    [SerializeField] private string healthLabel = "Health";
    [SerializeField] private string actionPointsLabel = "Action Points";
    [SerializeField] private string moraleLabel = "Morale";
    [SerializeField] private string warriorsLabel = "Warriors";
    [SerializeField] private string initiativeLabel = "Initiative";
    [SerializeField] private string unavailableLabel = "Unavailable in this build";
    [SerializeField] private string emptySquadLabel = "Player squad is unavailable";

    public TMP_FontAsset PrimaryFont => primaryFont;
    public TMP_FontAsset AccentFont => accentFont != null ? accentFont : primaryFont;
    public float CaptionSize => captionSize;
    public float BodySize => bodySize;
    public float HeadingSize => headingSize;
    public Sprite PanelSprite => panelSprite;
    public Sprite ButtonSprite => buttonSprite;
    public Sprite FrameSprite => frameSprite;
    public Sprite SeparatorSprite => separatorSprite;
    public Sprite DevelopmentPortraitFallback => developmentPortraitFallback;
    public Sprite OuterFrameSprite => panelSprite;
    public Sprite InsetPanelSprite => insetPanelSprite != null ? insetPanelSprite : panelSprite;
    public Sprite SectionHeaderSprite => sectionHeaderSprite != null
        ? sectionHeaderSprite
        : InsetPanelSprite;
    public Sprite SelectedFrameSprite => selectedFrameSprite != null ? selectedFrameSprite : frameSprite;
    public Sprite ButtonHoverSprite => buttonHoverSprite != null ? buttonHoverSprite : buttonSprite;
    public Sprite ButtonPressedSprite => buttonPressedSprite != null ? buttonPressedSprite : buttonSprite;
    public Sprite ButtonDisabledSprite => buttonDisabledSprite != null ? buttonDisabledSprite : buttonSprite;
    public Sprite PortraitFrameSprite => portraitFrameSprite != null ? portraitFrameSprite : frameSprite;
    public Sprite InitiativeCardSprite => initiativeCardSprite != null ? initiativeCardSprite : frameSprite;
    public Sprite EquipmentSlotSprite => equipmentSlotSprite != null ? equipmentSlotSprite : frameSprite;
    public Sprite EmptySlotSprite => emptySlotSprite != null ? emptySlotSprite : insetPanelSprite;
    public Sprite IconPlaceholderSprite => iconPlaceholderSprite != null
        ? iconPlaceholderSprite
        : developmentPortraitFallback;
    public Color BlackStone => blackStone;
    public Color DarkSteel => darkSteel;
    public Color Granite => granite;
    public Color Marble => marble;
    public Color Bronze => bronze;
    public Color Gold => gold;
    public Color Emerald => emerald;
    public Color Danger => danger;
    public Color Disabled => disabled;
    public Color SurfaceInset => surfaceInset;
    public Color SurfaceRaised => surfaceRaised;
    public Color PlayerSide => playerSide;
    public Color EnemySide => enemySide;
    public Color TextPrimary => textPrimary;
    public Color TextSecondary => textSecondary;
    public Color Overlay => overlay;
    public Color Separator => separator;
    public float SpaceSmall => spaceSmall;
    public float SpaceMedium => spaceMedium;
    public float SpaceLarge => spaceLarge;
    public float SafeMargin => safeMargin;
    public float CornerRadius => cornerRadius;
    public float BorderWidth => borderWidth;
    public float MinimumButtonHeight => minimumButtonHeight;
    public float CompactPadding => compactPadding;
    public float PanelPadding => panelPadding;
    public float CompactSpacing => compactSpacing;
    public float PortraitSize => portraitSize;
    public float InitiativePortraitSize => initiativePortraitSize;
    public float InitiativeCardWidth => initiativeCardWidth;
    public float InitiativeCardHeight => initiativeCardHeight;
    public float ActionControlHeight => actionControlHeight;
    public string SquadLabel => squadLabel;
    public string HealthLabel => healthLabel;
    public string ActionPointsLabel => actionPointsLabel;
    public string MoraleLabel => moraleLabel;
    public string WarriorsLabel => warriorsLabel;
    public string InitiativeLabel => initiativeLabel;
    public string UnavailableLabel => unavailableLabel;
    public string EmptySquadLabel => emptySquadLabel;

#if UNITY_EDITOR
    public void ConfigureDevelopmentDefaults(
        TMP_FontAsset font,
        Sprite panel,
        Sprite button,
        Sprite frame,
        Sprite separator,
        Sprite portraitFallback)
    {
        primaryFont = font;
        accentFont = font;
        panelSprite = panel;
        buttonSprite = button;
        frameSprite = frame;
        separatorSprite = separator;
        developmentPortraitFallback = portraitFallback;
    }

    public void ConfigureVisualPassDefaults(
        TMP_FontAsset font,
        DevelopmentUISpriteLibrary sprites)
    {
        primaryFont = font;
        accentFont = font;
        panelSprite = sprites.MonolithOuterFrame;
        buttonSprite = sprites.ButtonNormal;
        frameSprite = sprites.PortraitFrame;
        separatorSprite = sprites.BronzeSeparator;
        developmentPortraitFallback = sprites.PortraitFallback;
        insetPanelSprite = sprites.InsetPanel;
        sectionHeaderSprite = sprites.SectionHeader;
        selectedFrameSprite = sprites.SelectedFrame;
        buttonHoverSprite = sprites.ButtonHover;
        buttonPressedSprite = sprites.ButtonPressed;
        buttonDisabledSprite = sprites.ButtonDisabled;
        portraitFrameSprite = sprites.PortraitFrame;
        initiativeCardSprite = sprites.InitiativeCard;
        equipmentSlotSprite = sprites.EquipmentSlot;
        emptySlotSprite = sprites.EmptySlot;
        iconPlaceholderSprite = sprites.IconPlaceholder;

        safeMargin = 20f;
        spaceSmall = 6f;
        spaceMedium = 10f;
        spaceLarge = 16f;
        compactPadding = 6f;
        panelPadding = 10f;
        compactSpacing = 6f;
        borderWidth = 2f;
        minimumButtonHeight = 48f;
        portraitSize = 116f;
        initiativePortraitSize = 56f;
        initiativeCardWidth = 156f;
        initiativeCardHeight = 66f;
        actionControlHeight = 60f;
    }
#endif
}
