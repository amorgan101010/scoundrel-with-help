using Godot;
using SysCollections = System.Collections.Generic;

public sealed class ScoundrelLayoutController
{
    private const float RoomSlotGap = 20f;
    private const float ResizeDebounceSeconds = 0.12f;
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
        float scale = vpSize.Y / 1080f;
        if (scale <= 0f) scale = 1f;

        var cardSize = new Vector2(_baseCardWidth * scale, _baseCardHeight * scale);
        _setCardSize(cardSize);

        _cardFactory.Set("card_size", cardSize);
        _cardManager.Set("card_size", cardSize);

        foreach (var godotCard in _godotCards.Values)
            godotCard.Set("card_size", cardSize);

        UpdateRoomLayout(cardSize);
        UpdateWeaponGroupLayout(cardSize);

        _roomContainer.Call("_update_target_positions");
        UpdateButtonGroupWidths();
    }

    private void UpdateWeaponGroupLayout(Vector2 cardSize)
    {
        var leftPanelWidth = _leftPanel.Size.X;
        if (leftPanelWidth <= 0f)
            leftPanelWidth = 430f;

        var weaponGroupWidth = Mathf.Max(0f, leftPanelWidth - (WeaponGroupHorizontalPadding * 2f));

        _weaponGroup.OffsetLeft = WeaponGroupHorizontalPadding;
        _weaponGroup.OffsetRight = WeaponGroupHorizontalPadding + weaponGroupWidth;

        var weaponFontSize = (int)Mathf.Round(Mathf.Clamp(cardSize.Y * 0.06f, WeaponLabelMinFontSize, WeaponLabelMaxFontSize));
        var inPlayHeaderFontSize = (int)Mathf.Round(Mathf.Clamp(cardSize.Y * 0.042f, InPlayHeaderMinFontSize, InPlayHeaderMaxFontSize));
        var inPlaySuitFontSize = (int)Mathf.Round(Mathf.Clamp(cardSize.Y * 0.05f, InPlaySuitMinFontSize, InPlaySuitMaxFontSize));

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
            inPlayHeight = 130f;

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
        float cardScale = vpSize.Y / 1080f;
        if (cardScale <= 0f) cardScale = 1f;

        const float designCardWidth = 225f;
        const float designRightMargin = 30f;

        float scaledCardWidth = designCardWidth * cardScale;
        float scaledGroupWidth = scaledCardWidth + designRightMargin;

        float offsetLeft = -scaledGroupWidth;
        float offsetRight = -designRightMargin;

        _deckGroup.OffsetLeft = (int)offsetLeft;
        _deckGroup.OffsetRight = (int)offsetRight;

        _discardGroup.OffsetLeft = (int)offsetLeft;
        _discardGroup.OffsetRight = (int)offsetRight;
    }

    private void ResizeHelpDialog()
    {
        var viewportSize = _owner.GetViewport().GetVisibleRect().Size;
        var width = Mathf.Min(760f, viewportSize.X * 0.85f);
        var height = Mathf.Min(800f, viewportSize.Y * 0.85f);
        _helpDialog.Size = new Vector2I((int)width, (int)height);
    }
}
