using Godot;
using SysCollections = System.Collections.Generic;

public sealed class ScoundrelLayoutController
{
    private const float DesignViewportHeight = 1080f;
    private const float RoomSlotGap = 20f;
    private const float ResizeDebounceSeconds = 0.12f;
    private const float WeaponLabelScale = 0.06f;
    private const float InPlayHeaderScale = 0.042f;
    private const float InPlaySuitScale = 0.05f;
    private const int WeaponLabelMinFontSize = 14;
    private const int WeaponLabelMaxFontSize = 22;
    private const int InPlayHeaderMinFontSize = 12;
    private const int InPlayHeaderMaxFontSize = 16;
    private const int InPlaySuitMinFontSize = 13;
    private const int InPlaySuitMaxFontSize = 18;
    private const float WeaponGroupHorizontalPadding = 30f;
    private const float WeaponSlotTop = 34f;
    private const float WeaponLabelBottomPadding = 8f;
    private const float WeaponAndInPlayGap = 12f;
    private const float InPlayMinWidth = 130f;
    private const float InPlayMinGapFromSlot = 4f;
    private const float WeaponGroupMinBottom = 349f;
    private const float DeckDesignCardWidth = 225f;
    private const float DeckDesignRightMargin = 30f;
    private const float HelpDialogMaxWidth = 760f;
    private const float HelpDialogMaxHeight = 800f;
    private const float HelpDialogViewportScale = 0.85f;
    private const float JokerGroupHorizontalPadding = 30f;
    private const float JokerGroupTopGap = 20f;
    private const float JokerSlotGap = 20f;
    private const float JokerLabelBottomPadding = 4f;
    private const float JokerLabelScale = 0.045f;
    private const int JokerLabelMinFontSize = 12;
    private const int JokerLabelMaxFontSize = 16;

    private readonly Node _owner;
    private readonly GodotObject _cardFactory;
    private readonly Node _cardManager;
    private readonly SysCollections.Dictionary<string, GodotObject> _godotCards;
    private readonly Control _roomContainer;
    private readonly HBoxContainer _topButtonGroup;
    private readonly HBoxContainer _bottomButtonGroup;
    private readonly AcceptDialog _helpDialog;
    private readonly Control _deckGroup;
    private readonly Control _discardGroup;
    private readonly Control _leftPanel;
    private readonly Control _weaponGroup;
    private readonly Control _weaponSlot;
    private readonly Control _inPlayGroup;
    private readonly Label _weaponLabel;
    private readonly Label _inPlayHeader;
    private readonly Label _clubsLabel;
    private readonly Label _spadesLabel;
    private readonly Label _heartsLabel;
    private readonly Label _diamondsLabel;
    private readonly Control _jokerGroup;
    private readonly Control _potionJokerSlot;
    private readonly Control _weaponJokerSlot;
    private readonly Label _potionJokerHpLabel;
    private readonly Label _weaponJokerHpLabel;
    private readonly float _baseInPlayHeight;
    private readonly float _baseCardWidth;
    private readonly float _baseCardHeight;
    private readonly System.Action<Vector2> _setCardSize;
    private readonly Timer _resizeTimer;

    public ScoundrelLayoutController(
        Node owner,
        GodotObject cardFactory,
        Node cardManager,
        SysCollections.Dictionary<string, GodotObject> godotCards,
        Control roomContainer,
        HBoxContainer topButtonGroup,
        HBoxContainer bottomButtonGroup,
        AcceptDialog helpDialog,
        Control deckGroup,
        Control discardGroup,
        Control leftPanel,
        Control weaponGroup,
        Control weaponSlot,
        Control inPlayGroup,
        Label weaponLabel,
        Label inPlayHeader,
        Label clubsLabel,
        Label spadesLabel,
        Label heartsLabel,
        Label diamondsLabel,
        Control jokerGroup,
        Control potionJokerSlot,
        Control weaponJokerSlot,
        Label potionJokerHpLabel,
        Label weaponJokerHpLabel,
        float baseCardWidth,
        float baseCardHeight,
        System.Action<Vector2> setCardSize)
    {
        _owner = owner;
        _cardFactory = cardFactory;
        _cardManager = cardManager;
        _godotCards = godotCards;
        _roomContainer = roomContainer;
        _topButtonGroup = topButtonGroup;
        _bottomButtonGroup = bottomButtonGroup;
        _helpDialog = helpDialog;
        _deckGroup = deckGroup;
        _discardGroup = discardGroup;
        _leftPanel = leftPanel;
        _weaponGroup = weaponGroup;
        _weaponSlot = weaponSlot;
        _inPlayGroup = inPlayGroup;
        _weaponLabel = weaponLabel;
        _inPlayHeader = inPlayHeader;
        _clubsLabel = clubsLabel;
        _spadesLabel = spadesLabel;
        _heartsLabel = heartsLabel;
        _diamondsLabel = diamondsLabel;
        _jokerGroup = jokerGroup;
        _potionJokerSlot = potionJokerSlot;
        _weaponJokerSlot = weaponJokerSlot;
        _potionJokerHpLabel = potionJokerHpLabel;
        _weaponJokerHpLabel = weaponJokerHpLabel;
        _baseInPlayHeight = _inPlayGroup.OffsetBottom - _inPlayGroup.OffsetTop;
        _baseCardWidth = baseCardWidth;
        _baseCardHeight = baseCardHeight;
        _setCardSize = setCardSize;

        _resizeTimer = new Timer
        {
            OneShot = true,
            WaitTime = ResizeDebounceSeconds
        };
        _owner.AddChild(_resizeTimer);
        _resizeTimer.Connect("timeout", Callable.From(OnResizeDebounceTimeout));
    }

    public void ApplyNow()
    {
        UpdateCardSize(_owner.GetViewport().GetVisibleRect().Size);
    }

    public void OnViewportResized()
    {
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    public void ResizeHelpDialogNow()
    {
        ResizeHelpDialog();
        _helpDialog.PopupCentered();
    }

    private void OnResizeDebounceTimeout()
    {
        var vpSize = _owner.GetViewport().GetVisibleRect().Size;
        UpdateCardSize(vpSize);
        UpdateDeckDiscardLayout(vpSize);
        if (!_helpDialog.Visible) return;
        ResizeHelpDialog();
        _helpDialog.PopupCentered();
    }

    private void UpdateCardSize(Vector2 vpSize)
    {
        float scale = vpSize.Y / DesignViewportHeight;
        if (scale <= 0f) scale = 1f;

        var cardSize = new Vector2(_baseCardWidth * scale, _baseCardHeight * scale);
        _setCardSize(cardSize);

        _cardFactory.Set("card_size", cardSize);
        _cardManager.Set("card_size", cardSize);

        foreach (var godotCard in _godotCards.Values)
            godotCard.Set("card_size", cardSize);

        UpdateRoomLayout(cardSize);
        UpdateWeaponGroupLayout(cardSize);
        UpdateJokerGroupLayout(cardSize);

        _roomContainer.Call("_update_target_positions");
        UpdateButtonGroupWidths();
    }

    private void UpdateWeaponGroupLayout(Vector2 cardSize)
    {
        var leftPanelWidth = _leftPanel.Size.X;
        if (leftPanelWidth <= 0f)
            leftPanelWidth = _weaponGroup.Size.X + (WeaponGroupHorizontalPadding * 2f);

        var weaponGroupWidth = Mathf.Max(0f, leftPanelWidth - (WeaponGroupHorizontalPadding * 2f));

        _weaponGroup.OffsetLeft = WeaponGroupHorizontalPadding;
        _weaponGroup.OffsetRight = WeaponGroupHorizontalPadding + weaponGroupWidth;

        var weaponFontSize = (int)Mathf.Round(Mathf.Clamp(cardSize.Y * WeaponLabelScale, WeaponLabelMinFontSize, WeaponLabelMaxFontSize));
        var inPlayHeaderFontSize = (int)Mathf.Round(Mathf.Clamp(cardSize.Y * InPlayHeaderScale, InPlayHeaderMinFontSize, InPlayHeaderMaxFontSize));
        var inPlaySuitFontSize = (int)Mathf.Round(Mathf.Clamp(cardSize.Y * InPlaySuitScale, InPlaySuitMinFontSize, InPlaySuitMaxFontSize));

        _weaponLabel.AddThemeFontSizeOverride("font_size", weaponFontSize);
        _inPlayHeader.AddThemeFontSizeOverride("font_size", inPlayHeaderFontSize);
        _clubsLabel.AddThemeFontSizeOverride("font_size", inPlaySuitFontSize);
        _spadesLabel.AddThemeFontSizeOverride("font_size", inPlaySuitFontSize);
        _heartsLabel.AddThemeFontSizeOverride("font_size", inPlaySuitFontSize);
        _diamondsLabel.AddThemeFontSizeOverride("font_size", inPlaySuitFontSize);

        var weaponLabelHeight = weaponFontSize + WeaponLabelBottomPadding;
        _weaponLabel.OffsetRight = weaponGroupWidth;
        _weaponLabel.OffsetBottom = weaponLabelHeight;

        _weaponSlot.OffsetTop = WeaponSlotTop;
        _weaponSlot.OffsetRight = cardSize.X;
        _weaponSlot.OffsetBottom = WeaponSlotTop + cardSize.Y;

        var inPlayX = _weaponSlot.OffsetRight + WeaponAndInPlayGap;
        var inPlayY = _weaponSlot.OffsetTop;
        var inPlayHeight = _inPlayGroup.OffsetBottom - _inPlayGroup.OffsetTop;

        if (inPlayHeight <= 0f)
            inPlayHeight = Mathf.Max(_baseInPlayHeight, _inPlayGroup.GetCombinedMinimumSize().Y);

        var availableRightWidth = weaponGroupWidth - inPlayX;
        if (availableRightWidth >= InPlayMinWidth)
        {
            _inPlayGroup.OffsetLeft = inPlayX;
            _inPlayGroup.OffsetTop = inPlayY;
            _inPlayGroup.OffsetRight = weaponGroupWidth;
            _inPlayGroup.OffsetBottom = inPlayY + inPlayHeight;
            _weaponGroup.OffsetBottom = Mathf.Max(WeaponGroupMinBottom, Mathf.Max(_weaponSlot.OffsetBottom, _inPlayGroup.OffsetBottom));
            return;
        }

        _inPlayGroup.OffsetLeft = 0f;
        _inPlayGroup.OffsetTop = _weaponSlot.OffsetBottom + WeaponAndInPlayGap;
        if (_inPlayGroup.OffsetTop < _weaponSlot.OffsetBottom + InPlayMinGapFromSlot)
            _inPlayGroup.OffsetTop = _weaponSlot.OffsetBottom + InPlayMinGapFromSlot;

        _inPlayGroup.OffsetRight = weaponGroupWidth;
        _inPlayGroup.OffsetBottom = _inPlayGroup.OffsetTop + inPlayHeight;
        _weaponGroup.OffsetBottom = Mathf.Max(WeaponGroupMinBottom, Mathf.Max(_inPlayGroup.OffsetBottom, _weaponSlot.OffsetBottom));
    }

    /// <summary>
    /// Positions JokerGroup in the bottom third of LeftPanel, directly below the
    /// (dynamically-sized) WeaponGroup, with the Potion Joker slot on the left
    /// and the Weapon Joker slot on the right. Follows the same offset/scale
    /// conventions as UpdateWeaponGroupLayout: joker slots render at the full
    /// card_size (cards can't be individually scaled smaller within a Pile),
    /// with a scaled HP label above each.
    /// </summary>
    private void UpdateJokerGroupLayout(Vector2 cardSize)
    {
        var leftPanelWidth = _leftPanel.Size.X;
        if (leftPanelWidth <= 0f)
            leftPanelWidth = _jokerGroup.Size.X + (JokerGroupHorizontalPadding * 2f);

        var jokerGroupWidth = Mathf.Max(0f, leftPanelWidth - (JokerGroupHorizontalPadding * 2f));

        _jokerGroup.OffsetLeft = JokerGroupHorizontalPadding;
        _jokerGroup.OffsetRight = JokerGroupHorizontalPadding + jokerGroupWidth;
        _jokerGroup.OffsetTop = _weaponGroup.OffsetBottom + JokerGroupTopGap;

        var labelFontSize = (int)Mathf.Round(Mathf.Clamp(cardSize.Y * JokerLabelScale, JokerLabelMinFontSize, JokerLabelMaxFontSize));
        _potionJokerHpLabel.AddThemeFontSizeOverride("font_size", labelFontSize);
        _weaponJokerHpLabel.AddThemeFontSizeOverride("font_size", labelFontSize);
        var labelHeight = labelFontSize + JokerLabelBottomPadding;

        _potionJokerHpLabel.OffsetLeft = 0f;
        _potionJokerHpLabel.OffsetRight = cardSize.X;
        _potionJokerHpLabel.OffsetTop = 0f;
        _potionJokerHpLabel.OffsetBottom = labelHeight;

        _potionJokerSlot.OffsetLeft = 0f;
        _potionJokerSlot.OffsetTop = labelHeight;
        _potionJokerSlot.OffsetRight = cardSize.X;
        _potionJokerSlot.OffsetBottom = labelHeight + cardSize.Y;

        var weaponJokerX = cardSize.X + JokerSlotGap;
        _weaponJokerHpLabel.OffsetLeft = weaponJokerX;
        _weaponJokerHpLabel.OffsetRight = weaponJokerX + cardSize.X;
        _weaponJokerHpLabel.OffsetTop = 0f;
        _weaponJokerHpLabel.OffsetBottom = labelHeight;

        _weaponJokerSlot.OffsetLeft = weaponJokerX;
        _weaponJokerSlot.OffsetTop = labelHeight;
        _weaponJokerSlot.OffsetRight = weaponJokerX + cardSize.X;
        _weaponJokerSlot.OffsetBottom = labelHeight + cardSize.Y;

        _jokerGroup.OffsetBottom = _jokerGroup.OffsetTop + labelHeight + cardSize.Y;
    }

    private void UpdateRoomLayout(Vector2 cardSize)
    {
        var roomWidth = cardSize.X * 2f + RoomSlotGap;
        var roomHeight = cardSize.Y * 2f + RoomSlotGap;
        var halfRoomWidth = roomWidth / 2f;
        var halfRoomHeight = roomHeight / 2f;

        _roomContainer.OffsetLeft = -halfRoomWidth;
        _roomContainer.OffsetRight = halfRoomWidth;
        _roomContainer.OffsetTop = -halfRoomHeight;
        _roomContainer.OffsetBottom = halfRoomHeight;
    }

    private void UpdateButtonGroupWidths()
    {
        var roomWidth = _roomContainer.GetRect().Size.X;
        if (roomWidth <= 0)
        {
            _owner.CallDeferred(nameof(UpdateButtonGroupWidths));
            return;
        }

        var halfWidth = (int)(roomWidth / 2f);
        _topButtonGroup.OffsetLeft = -halfWidth;
        _topButtonGroup.OffsetRight = halfWidth;
        _bottomButtonGroup.OffsetLeft = -halfWidth;
        _bottomButtonGroup.OffsetRight = halfWidth;
    }

    private void UpdateDeckDiscardLayout(Vector2 vpSize)
    {
        float cardScale = vpSize.Y / DesignViewportHeight;
        if (cardScale <= 0f) cardScale = 1f;

        float scaledCardWidth = DeckDesignCardWidth * cardScale;
        float scaledGroupWidth = scaledCardWidth + DeckDesignRightMargin;

        float offsetLeft = -scaledGroupWidth;
        float offsetRight = -DeckDesignRightMargin;

        _deckGroup.OffsetLeft = (int)offsetLeft;
        _deckGroup.OffsetRight = (int)offsetRight;

        _discardGroup.OffsetLeft = (int)offsetLeft;
        _discardGroup.OffsetRight = (int)offsetRight;
    }

    private void ResizeHelpDialog()
    {
        var viewportSize = _owner.GetViewport().GetVisibleRect().Size;
        var width = Mathf.Min(HelpDialogMaxWidth, viewportSize.X * HelpDialogViewportScale);
        var height = Mathf.Min(HelpDialogMaxHeight, viewportSize.Y * HelpDialogViewportScale);
        _helpDialog.Size = new Vector2I((int)width, (int)height);
    }
}
