using Godot;

/// <summary>
/// D20 health display using the "Pixel Spritesheet Dice 20d" asset by srspooky
/// (https://srspooky.itch.io/pixel-spritesheet-dice20d).
///
/// Shows the Dice{hp}.png frame so the face value matches current HP.
/// Applies a modulate tint over the grey+dark-red-line art:
///   green (full) → yellow (half) → red (critical).
/// Hovering shows "HP: cur / max" as a tooltip.
/// </summary>
public partial class HealthDie : Control
{
    private TextureRect _rect = null!;
    private int _cur = 20;
    private int _max = 20;

    public override void _Ready()
    {
        _rect            = new TextureRect();
        _rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _rect.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        _rect.StretchMode = TextureRect.StretchModeEnum.Scale;
        _rect.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_rect);

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

        int frame = Mathf.Clamp(_cur, 1, 20);
        _rect.Texture  = GD.Load<Texture2D>($"res://Dice 96x96/pixel img 96x96/Dice{frame}.png");
        _rect.Modulate = TintForFraction(_max > 0 ? (float)_cur / _max : 0f);
    }

    // The sprite base is grey with dark-red lines.
    // Modulate multiplies each pixel channel, so values < 1 suppress a channel
    // (pushing grey toward the remaining channels) while 1.0 leaves it unchanged.
    private static Color TintForFraction(float frac)
    {
        var green  = new Color(0.40f, 1.00f, 0.40f); // grey → medium green
        var yellow = new Color(1.00f, 0.88f, 0.12f); // grey → warm gold
        var red    = new Color(1.00f, 0.20f, 0.20f); // grey → vivid red
        if (frac >= 0.5f)
            return yellow.Lerp(green, (frac - 0.5f) * 2f);
        return red.Lerp(yellow, frac * 2f);
    }
}
