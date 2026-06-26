using Godot;

/// <summary>
/// D20 health display. Uses a vector SVG die body (dice_assets/d20.svg) with a
/// modulate tint that shifts green→yellow→red as HP drops. The current HP
/// is rendered as a centered Label on top of the die so it always stays legible.
///
/// </summary>
public partial class HealthDie : Control
{
    private TextureRect _rect  = null!;
    private Label       _label = null!;
    private int _cur = 20;
    private int _max = 20;

    public override void _Ready()
    {
        // Die body — fills the whole control
        _rect             = new TextureRect();
        _rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _rect.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        _rect.StretchMode = TextureRect.StretchModeEnum.Scale;
        _rect.MouseFilter = MouseFilterEnum.Ignore;
        _rect.Texture     = GD.Load<Texture2D>("res://dice_assets/d20.svg");
        AddChild(_rect);

        // HP number — white for visibility against all health colors
        _label = new Label();
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment   = VerticalAlignment.Center;
        _label.AddThemeFontSizeOverride("font_size", 46);
        var fontColor = new Color(0.102f, 0.102f, 0.102f);
        _label.AddThemeColorOverride("font_color", fontColor);
        _label.MouseFilter = MouseFilterEnum.Ignore;
        _label.Text        = "20";
        AddChild(_label);

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
        if (_rect == null) return;
        if (_label != null)
            _label.Text = _cur.ToString();
        _rect.Modulate = TintForFraction(_max > 0 ? (float)_cur / _max : 0f);
    }

    // Grey base art: modulate suppresses unwanted channels to push toward target hue.
    private static Color TintForFraction(float frac)
    {
        var green  = new Color(0.40f, 1.00f, 0.40f);
        var yellow = new Color(1.00f, 0.88f, 0.12f);
        var red    = new Color(1.00f, 0.20f, 0.20f);
        if (frac >= 0.5f)
            return yellow.Lerp(green, (frac - 0.5f) * 2f);
        return red.Lerp(yellow, frac * 2f);
    }
}
