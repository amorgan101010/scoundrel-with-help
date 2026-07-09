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
    private const float WeaponGroupMinHeight = 349f;
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

    // A pocketed potion/weapon (PotionJokerSlot/WeaponJokerSlot, index 1 — the
    // joker's own card is always index 0, see JokerPocketSlot.gd) is scaled down
    // to this fraction of full card size so it reads as a small badge instead of
    // covering the joker's card art and the HP label above the slot (playtest
    // bug: the previous full-size stored card, offset by Pile's default ~8px
    // stack_display_gap, poked into the label's bottom edge). ScoundrelGame sets
    // `scale` on the stored Card node to this value when storing.
    public const float PocketedItemScale = 0.42f;

    // Pile.PileDirection.DOWN (addons/card-framework/pile.gd) — a GDScript enum
    // ordinal, not exposed as a type to C#. Enum order there is UP=0, DOWN=1,
    // LEFT=2, RIGHT=3.
    private const int PileDirectionDown = 1;

    private readonly Node _owner;
    private readonly GodotObject _cardFactory;
    private readonly Node _cardManager;
    private readonly SysCollections.Dictionary<string, GodotObject> _godotCards;
    private readonly Control _roomContainer;
    private readonly Control _headerDivider;
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
        Control headerDivider,
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
        _headerDivider = headerDivider;
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
            _weaponGroup.OffsetBottom = _weaponGroup.OffsetTop
                + Mathf.Max(WeaponGroupMinHeight, Mathf.Max(_weaponSlot.OffsetBottom, _inPlayGroup.OffsetBottom));
            return;
        }

        _inPlayGroup.OffsetLeft = 0f;
        _inPlayGroup.OffsetTop = _weaponSlot.OffsetBottom + WeaponAndInPlayGap;
        if (_inPlayGroup.OffsetTop < _weaponSlot.OffsetBottom + InPlayMinGapFromSlot)
            _inPlayGroup.OffsetTop = _weaponSlot.OffsetBottom + InPlayMinGapFromSlot;

        _inPlayGroup.OffsetRight = weaponGroupWidth;
        _inPlayGroup.OffsetBottom = _inPlayGroup.OffsetTop + inPlayHeight;
        _weaponGroup.OffsetBottom = _weaponGroup.OffsetTop
            + Mathf.Max(WeaponGroupMinHeight, Mathf.Max(_inPlayGroup.OffsetBottom, _weaponSlot.OffsetBottom));
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

        PositionJokerSlot(_potionJokerHpLabel, _potionJokerSlot, 0f, labelHeight, cardSize);
        PositionJokerSlot(_weaponJokerHpLabel, _weaponJokerSlot, cardSize.X + JokerSlotGap, labelHeight, cardSize);

        _jokerGroup.OffsetBottom = _jokerGroup.OffsetTop + labelHeight + cardSize.Y;
    }

    /// <summary>
    /// Positions one joker's HP label and card slot at horizontal offset
    /// <paramref name="xOffset"/> within JokerGroup, and configures the slot's
    /// Pile so a pocketed potion/weapon (index 1) lands as a small badge.
    /// Shared by both joker slots in <see cref="UpdateJokerGroupLayout"/> so a
    /// future layout tweak can't land on only one of the two copies.
    /// </summary>
    private void PositionJokerSlot(Label hpLabel, Control slot, float xOffset, float labelHeight, Vector2 cardSize)
    {
        hpLabel.OffsetLeft = xOffset;
        hpLabel.OffsetRight = xOffset + cardSize.X;
        hpLabel.OffsetTop = 0f;
        hpLabel.OffsetBottom = labelHeight;

        slot.OffsetLeft = xOffset;
        slot.OffsetTop = labelHeight;
        slot.OffsetRight = xOffset + cardSize.X;
        slot.OffsetBottom = labelHeight + cardSize.Y;

        // A stored potion/weapon (index 1 in this Pile) must land as a small badge
        // in the bottom of the slot, clear of the joker's own card art and the HP
        // label above — never covering either (playtest bug fixed here). Rather
        // than fighting the framework's own layout/tween machinery with a manual
        // position override (any absolute position we set gets re-tweened back by
        // Pile's deferred reapply, Card.return_card(), and this very resize path),
        // we teach the Pile itself where index 1 belongs: `layout = DOWN` moves it
        // straight down from the joker's own position (index 0, always at offset
        // zero), and `stack_display_gap` is sized so the *scaled-down* card's
        // bottom edge lands exactly flush with the slot's bottom edge.
        //
        // Derivation: a Control scales around `pivot_offset`, which Card.gd always
        // sets to (unscaled) card_size / 2 — the pivot point's global position is
        // therefore fixed under scaling, i.e. it IS the shrunk card's visual
        // center. We want that center at slot_bottom - (cardSize.Y*scale)/2, i.e.
        // offset.y = cardSize.Y/2 - (cardSize.Y*scale)/2 = (cardSize.Y/2)*(1-scale).
        // ScoundrelGame sets `scale` on the stored Card node to PocketedItemScale
        // when storing (see ShrinkCardForPocket).
        var pocketedItemOffset = (int)((cardSize.Y / 2f) * (1f - PocketedItemScale));
        slot.Set("layout", PileDirectionDown);
        slot.Set("stack_display_gap", pocketedItemOffset);

        // Mirrors the room's one-liner in UpdateCardSize — without this, a
        // resize while something is pocketed updates the gap value here but
        // doesn't re-tween the already-placed badge card to match (the same
        // pre-existing gap applies to every non-room Pile; out of scope for this
        // fix, called out for visibility).
        slot.Call("_update_target_positions");
    }

    private void UpdateRoomLayout(Vector2 cardSize)
    {
        var roomWidth = cardSize.X * 2f + RoomSlotGap;
        var roomHeight = cardSize.Y * 2f + RoomSlotGap;
        var halfRoomWidth = roomWidth / 2f;
        var halfRoomHeight = roomHeight / 2f;

        // Center the room between the header divider and the bottom button row,
        // not on the screen's own 50% mark -- those two landmarks sit at different
        // distances from the top/bottom edges (header taller than the button bar),
        // so a screen-centered room reads as hugging the divider with a large gap
        // above the buttons. RoomContainer is top-anchored (anchor_top/bottom = 0,
        // see Game.tscn) specifically so these offsets are plain viewport-space
        // pixels, directly comparable to headerDivider/bottomButtonGroup's own
        // rects (same "UI" parent, same coordinate space) with no anchor-relative
        // math needed.
        float dividerBottom = _headerDivider.GetRect().Position.Y + _headerDivider.GetRect().Size.Y;
        float buttonTop = _bottomButtonGroup.GetRect().Position.Y;
        float centerY = (dividerBottom + buttonTop) / 2f;

        _roomContainer.OffsetLeft = -halfRoomWidth;
        _roomContainer.OffsetRight = halfRoomWidth;
        _roomContainer.OffsetTop = centerY - halfRoomHeight;
        _roomContainer.OffsetBottom = centerY + halfRoomHeight;
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
