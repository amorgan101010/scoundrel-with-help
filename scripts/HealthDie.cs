using Godot;

/// <summary>
/// Draws a stylized D20 polygon whose fill color shifts green→yellow→red as HP drops.
/// A child Label shows "cur/max" centered inside the die.
/// </summary>
public partial class HealthDie : Control
{
    private Label _label = null!;
    private int _cur = 20;
    private int _max = 20;

    public override void _Ready()
    {
        _label = new Label();
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment   = VerticalAlignment.Center;
        _label.AddThemeFontSizeOverride("font_size", 15);
        _label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.95f));
        _label.MouseFilter         = MouseFilterEnum.Ignore;
        _label.Text                = "20";
        AddChild(_label);
    }

    public void SetHealth(int cur, int max)
    {
        _cur = cur;
        _max = max;
        if (_label != null)
            _label.Text = cur.ToString();
        TooltipText = $"HP: {cur} / {max}";
        QueueRedraw();
    }

    public override void _Draw()
    {
        float cx = Size.X / 2f;
        float cy = Size.Y / 2f;
        float r  = Mathf.Min(cx, cy) - 3f;

        float frac = _max > 0 ? (float)_cur / _max : 0f;
        Color fill = GetFillColor(frac);

        // Outer decagon — 10 vertices, top-pointing
        var outer = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = -Mathf.Pi / 2f + i * Mathf.Tau / 10f;
            outer[i] = new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a));
        }

        // Inner pentagon — 5 vertices, each between two adjacent outer vertices
        // inner[k] sits between outer[2k] and outer[2k+1]
        var inner = new Vector2[5];
        float innerR = r * 0.42f;
        for (int i = 0; i < 5; i++)
        {
            float a = -Mathf.Pi / 2f + Mathf.Pi / 10f + i * Mathf.Tau / 5f;
            inner[i] = new Vector2(cx + innerR * Mathf.Cos(a), cy + innerR * Mathf.Sin(a));
        }

        // Fill
        DrawColoredPolygon(outer, fill);
        DrawColoredPolygon(inner, fill.Lightened(0.15f));

        // Edges
        var edgeColor  = new Color(1f, 1f, 1f, 0.8f);
        var facetColor = new Color(1f, 1f, 1f, 0.28f);

        var outerLoop = new Vector2[11];
        outer.CopyTo(outerLoop, 0);
        outerLoop[10] = outer[0];
        DrawPolyline(outerLoop, edgeColor, 2f);

        var innerLoop = new Vector2[6];
        inner.CopyTo(innerLoop, 0);
        innerLoop[5] = inner[0];
        DrawPolyline(innerLoop, facetColor, 1f);

        // Spokes from each inner vertex to its two flanking outer vertices
        for (int k = 0; k < 5; k++)
        {
            DrawLine(inner[k], outer[2 * k],           facetColor, 1f);
            DrawLine(inner[k], outer[(2 * k + 1) % 10], facetColor, 1f);
        }
    }

    private static Color GetFillColor(float frac)
    {
        var green  = new Color(0.15f, 0.65f, 0.18f);
        var yellow = new Color(0.88f, 0.74f, 0.08f);
        var red    = new Color(0.80f, 0.10f, 0.10f);
        if (frac >= 0.5f)
            return yellow.Lerp(green, (frac - 0.5f) * 2f);
        return red.Lerp(yellow, frac * 2f);
    }
}
