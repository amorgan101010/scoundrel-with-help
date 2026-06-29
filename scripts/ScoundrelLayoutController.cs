using Godot;
using SysCollections = System.Collections.Generic;

public sealed class ScoundrelLayoutController
{
    private const float RoomSlotGap = 20f;
    private const float ResizeDebounceSeconds = 0.12f;

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

        _roomContainer.Call("_update_target_positions");
        UpdateButtonGroupWidths();
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
