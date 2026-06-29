using Godot;
using System;

/// <summary>
/// Partial class: Viewport responsiveness and reactive UI layout.
/// Handles viewport resize events, card size scaling, button width adjustments,
/// and help dialog resizing to keep the game playable at all viewport sizes.
/// </summary>
public partial class ScoundrelGame : Node
{
    // ── Viewport resize debouncing ────────────────────────────────────────
    // Timer-based debounce to delay expensive layout work (card size updates,
    // button repositioning) until the user has finished resizing the window.
    private Control _deckGroup = null!;
    private Control _discardGroup = null!;

    private void OnViewportResized()
    {
        // Restart debounce timer; only when the timer finally times out do we
        // recompute card sizes and update the help dialog.
        if (_resizeTimer == null) return;
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    // ── Resize debounce timeout (executes expensive layout operations) ─────
    private void OnResizeDebounceTimeout()
    {
        var vpSize = GetViewport().GetVisibleRect().Size;
        UpdateCardSize(vpSize);
        UpdateDeckDiscardLayout(vpSize);
        if (!_helpDialog.Visible) return;
        ResizeHelpDialog();
        _helpDialog.PopupCentered();
    }

    // ── Deck/Discard responsive layout ────────────────────────────────────
    /// <summary>
    /// Adjusts the deck and discard pile positions to keep them within the visible
    /// viewport at sub-1080p resolutions. The piles are anchored to the right edge
    /// with negative offsets; this method clamps them to stay visible.
    /// </summary>
    private void UpdateDeckDiscardLayout(Vector2 vpSize)
    {
        // Deck group: offset_left = -255, offset_right = -30 (hardcoded for 1920px)
        // These piles must stay within the viewport bounds.
        // RightPanel spans anchor_left=0.6666667 to anchor_right=1.0
        var rightPanelLeft = vpSize.X * (2f / 3f);
        var rightPanelWidth = vpSize.X - rightPanelLeft;

        // Min safe offset so the pile doesn't slide off the left edge of the screen
        var minOffsetLeft = -(rightPanelWidth - 30f);  // Leave 30px margin on the right

        if (_deckGroup != null && _deckGroup.OffsetLeft < minOffsetLeft)
            _deckGroup.OffsetLeft = (int)minOffsetLeft;

        if (_discardGroup != null && _discardGroup.OffsetLeft < minOffsetLeft)
            _discardGroup.OffsetLeft = (int)minOffsetLeft;
    }

    // ── Help dialog resizing ──────────────────────────────────────────────
    /// <summary>
    /// Resizes the help dialog to fit the current viewport while maintaining
    /// readable proportions. Clamps to min 760×800 or 85% of viewport,
    /// whichever is smaller.
    /// </summary>
    private void ResizeHelpDialog()
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Min(760f, viewportSize.X * 0.85f);
        var height = Mathf.Min(800f, viewportSize.Y * 0.85f);
        _helpDialog.Size = new Vector2I((int)width, (int)height);
    }

    // ── Card size scaling ─────────────────────────────────────────────────
    /// <summary>
    /// Scales card dimensions based on viewport height. Reference design is 1080p.
    /// Updates the card factory, card manager, and all instantiated cards.
    /// Also triggers button group width recalculation and room layout updates.
    /// </summary>
    private void UpdateCardSize(Vector2 vpSize = default)
    {
        if (vpSize == default) vpSize = GetViewport().GetVisibleRect().Size;

        // Reference was designed for a 1080p height viewport.
        float scale = vpSize.Y / 1080f;
        if (scale <= 0f) scale = 1f;
        _cardSize = new Vector2(BaseCardWidth * scale, BaseCardHeight * scale);

        // Apply to factory so newly created cards use the size
        _cardFactory.Set("card_size", _cardSize);

        // Also set the CardManager's card_size so containers using
        // `card_manager.card_size` (RoomContainer, Hand, etc.) get the
        // updated value for layout calculations.
        _cardManager.Set("card_size", _cardSize);

        // Also update any already-created cards so visuals stay in sync
        foreach (var godotCard in _godotCards.Values)
            godotCard.Set("card_size", _cardSize);

        // Tell the room container to recompute card target positions to match
        // the new card size so cards don't overlap when viewport shrinks.
        _roomContainer.Call("_update_target_positions");
        UpdateButtonGroupWidths();
    }

    // ── Button group width alignment ──────────────────────────────────────
    /// <summary>
    /// Adjusts button group widths to match the room container width,
    /// keeping them centered. This ensures buttons stay aligned with the room
    /// layout regardless of viewport size changes.
    /// </summary>
    private void UpdateButtonGroupWidths()
    {
        var roomWidth = _roomContainer.GetRect().Size.X;
        if (roomWidth <= 0)
        {
            // Room dimensions not yet available; defer and retry next frame.
            CallDeferred(nameof(UpdateButtonGroupWidths));
            return;
        }

        // Match the top/bottom button groups to the current room width, keeping
        // them centered above and below the room container.
        var halfWidth = (int)(roomWidth / 2f);
        _topButtonGroup.OffsetLeft    = -halfWidth;
        _topButtonGroup.OffsetRight   = halfWidth;
        _bottomButtonGroup.OffsetLeft = -halfWidth;
        _bottomButtonGroup.OffsetRight= halfWidth;
    }
}
