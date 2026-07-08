using Godot;

/// <summary>
/// HP badge — circular gold-ring outline (ui_overhaul.png header treatment) with the
/// current HP centered and an "HP" caption below the ring. Replaces the old d20 SVG
/// die art (and its green→yellow→red full-body tint) with a static StyleBoxFlat ring
/// so it matches the mockup's header badge exactly — ring stays gold, number stays
/// cream, regardless of HP fraction. The old danger-tint behavior is dropped rather
/// than moved elsewhere; flagged in the chunk report in case a later pass wants a
/// HP-based accent back (e.g. on the number or ring at low health).
/// </summary>
public partial class HealthDie : Control
{
    // Ring diameter in pixels — matches this control's offset_right - offset_left in
    // Game.tscn (40 → 150 = 110). Hardcoded rather than read from Size at _Ready():
    // ScoundrelLayoutController.UpdateButtonGroupWidths has a known gotcha where a
    // Control's Size can still be 0 at that point in the frame and has to defer-retry;
    // HealthDie isn't touched by that controller at all, so sidestep the whole
    // question by not depending on runtime Size here.
    private const float RingSide       = 110f;
    private const int RingBorderWidth  = 3;
    private const int NumberFontSize   = 40;
    private const int CaptionFontSize  = 13;
    private const float CaptionGap     = 6f;
    private const float CaptionHeight  = 22f;

    private Label _label   = null!;
    private int _cur = 20;
    private int _max = 20;

    public override void _Ready()
    {
        var ringStyle = new StyleBoxFlat
        {
            BgColor     = ScoundrelPalette.BackgroundNearBlack,
            BorderColor = ScoundrelPalette.AccentGold,
        };
        ringStyle.SetBorderWidthAll(RingBorderWidth);
        ringStyle.SetCornerRadiusAll((int)(RingSide / 2f)); // full circle when width == height

        var ring = new Panel();
        ring.OffsetLeft   = 0f;
        ring.OffsetTop    = 0f;
        ring.OffsetRight  = RingSide;
        ring.OffsetBottom = RingSide;
        ring.MouseFilter  = MouseFilterEnum.Ignore;
        ring.AddThemeStyleboxOverride("panel", ringStyle);
        AddChild(ring);

        // HP number — centered on the ring, static cream to match the mockup.
        _label = new Label();
        _label.OffsetLeft   = 0f;
        _label.OffsetTop    = 0f;
        _label.OffsetRight  = RingSide;
        _label.OffsetBottom = RingSide;
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment   = VerticalAlignment.Center;
        _label.AddThemeFontOverride("font", ScoundrelPalette.DisplayBold);
        _label.AddThemeFontSizeOverride("font_size", NumberFontSize);
        _label.AddThemeColorOverride("font_color", ScoundrelPalette.TitleCream);
        _label.MouseFilter = MouseFilterEnum.Ignore;
        _label.Text        = "20";
        AddChild(_label);

        // "HP" caption below the ring, muted gold-brown small-caps styling.
        var caption = new Label();
        caption.OffsetLeft   = 0f;
        caption.OffsetTop    = RingSide + CaptionGap;
        caption.OffsetRight  = RingSide;
        caption.OffsetBottom = RingSide + CaptionGap + CaptionHeight;
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        caption.AddThemeFontOverride("font", ScoundrelPalette.SerifRegular);
        caption.AddThemeFontSizeOverride("font_size", CaptionFontSize);
        caption.AddThemeColorOverride("font_color", ScoundrelPalette.MutedGoldGray);
        caption.MouseFilter = MouseFilterEnum.Ignore;
        caption.Text        = "HP";
        AddChild(caption);

        Refresh();
    }

    public void SetHealth(int cur, int max)
    {
        _cur        = cur;
        _max        = max;
        TooltipText = $"HP: {cur} / {max}";
        Refresh();
    }

    private void Refresh()
    {
        if (_label == null) return;
        _label.Text = _cur.ToString();
    }
}
