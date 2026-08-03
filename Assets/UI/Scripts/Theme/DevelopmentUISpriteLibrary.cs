using UnityEngine;

public sealed class DevelopmentUISpriteLibrary : ScriptableObject
{
    [SerializeField] private Sprite monolithOuterFrame;
    [SerializeField] private Sprite insetPanel;
    [SerializeField] private Sprite sectionHeader;
    [SerializeField] private Sprite bronzeSeparator;
    [SerializeField] private Sprite selectedFrame;
    [SerializeField] private Sprite buttonNormal;
    [SerializeField] private Sprite buttonHover;
    [SerializeField] private Sprite buttonPressed;
    [SerializeField] private Sprite buttonDisabled;
    [SerializeField] private Sprite portraitFrame;
    [SerializeField] private Sprite initiativeCard;
    [SerializeField] private Sprite equipmentSlot;
    [SerializeField] private Sprite emptySlot;
    [SerializeField] private Sprite iconPlaceholder;
    [SerializeField] private Sprite portraitFallback;

    public Sprite Panel => monolithOuterFrame;
    public Sprite Button => buttonNormal;
    public Sprite Frame => portraitFrame;
    public Sprite Separator => bronzeSeparator;
    public Sprite MonolithOuterFrame => monolithOuterFrame;
    public Sprite InsetPanel => insetPanel;
    public Sprite SectionHeader => sectionHeader;
    public Sprite BronzeSeparator => bronzeSeparator;
    public Sprite SelectedFrame => selectedFrame;
    public Sprite ButtonNormal => buttonNormal;
    public Sprite ButtonHover => buttonHover;
    public Sprite ButtonPressed => buttonPressed;
    public Sprite ButtonDisabled => buttonDisabled;
    public Sprite PortraitFrame => portraitFrame;
    public Sprite InitiativeCard => initiativeCard;
    public Sprite EquipmentSlot => equipmentSlot;
    public Sprite EmptySlot => emptySlot;
    public Sprite IconPlaceholder => iconPlaceholder;
    public Sprite PortraitFallback => portraitFallback;

    public void Configure(
        Sprite configuredOuterFrame,
        Sprite configuredInsetPanel,
        Sprite configuredSectionHeader,
        Sprite configuredSeparator,
        Sprite configuredSelectedFrame,
        Sprite configuredButtonNormal,
        Sprite configuredButtonHover,
        Sprite configuredButtonPressed,
        Sprite configuredButtonDisabled,
        Sprite configuredPortraitFrame,
        Sprite configuredInitiativeCard,
        Sprite configuredEquipmentSlot,
        Sprite configuredEmptySlot,
        Sprite configuredIconPlaceholder,
        Sprite fallbackSprite)
    {
        monolithOuterFrame = configuredOuterFrame;
        insetPanel = configuredInsetPanel;
        sectionHeader = configuredSectionHeader;
        bronzeSeparator = configuredSeparator;
        selectedFrame = configuredSelectedFrame;
        buttonNormal = configuredButtonNormal;
        buttonHover = configuredButtonHover;
        buttonPressed = configuredButtonPressed;
        buttonDisabled = configuredButtonDisabled;
        portraitFrame = configuredPortraitFrame;
        initiativeCard = configuredInitiativeCard;
        equipmentSlot = configuredEquipmentSlot;
        emptySlot = configuredEmptySlot;
        iconPlaceholder = configuredIconPlaceholder;
        portraitFallback = fallbackSprite;
    }
}
