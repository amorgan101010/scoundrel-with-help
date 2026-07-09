using Godot;

/// <summary>
/// Gradient fill + rounded-corner restore + glow for a solid-fill pill button (e.g.
/// NextRoomButton) — per direct feedback ("card banners and buttons have gradients
/// instead of solid colors", plus the earlier "next room button ... have a subtle
/// glow"). Built at runtime in C# rather than as .tscn StyleBoxTexture/Gradient
/// sub_resources because the button's actual pixel width isn't known until
/// HBoxContainer layout settles (and changes again on viewport resize) — this hooks
/// Control.Resized to rebuild whenever that happens, the same "Size can be 0 at
/// first pass" gotcha ScoundrelLayoutController.UpdateButtonGroupWidths already
/// works around for this exact button group.
///
/// Each corner mask is a 2×radius patch (NOT radius×radius — verified via local
/// screenshot that a radius-sized patch makes Godot's corner_radius describe a
/// degenerate arc centered at the wrong point, producing an inverted "vesica" bite
/// instead of a rounded cap) with a transparent fill and a border painted in the
/// page background color (this button sits directly over UI/Background, a uniform
/// ColorRect, so that's accurate), traced along the TRUE corner's own arc and wide
/// enough (radius * 0.42, comfortably more than the ~0.293*radius max gap between a
/// square corner and its rounded arc) to fully cover the square texture's corner
/// nub. Same border-stroke-over-transparent-fill idea as BannerGradient's mask, but
/// this button has no real border to blend into, so the border width does the
/// masking work directly rather than just tracing an existing line.
/// </summary>
public static class ButtonGradient
{
    private const string ChromeName = "GradientChrome";

    public static void Apply(Button button, Color baseColor, Color glowColor, int radius)
    {
        button.AddThemeStyleboxOverride("normal", BannerGradient.Style(baseColor));
        button.Resized += () => Rebuild(button, radius, glowColor);
        Rebuild(button, radius, glowColor);
    }

    private static void Rebuild(Button button, int radius, Color glowColor)
    {
        button.GetNodeOrNull<Control>(ChromeName)?.QueueFree();

        float w = button.Size.X, h = button.Size.Y;
        if (w <= 0f || h <= 0f) return; // not laid out yet -- Resized fires again once it is

        var chrome = new Control { Name = ChromeName, MouseFilter = Control.MouseFilterEnum.Ignore };
        chrome.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(chrome);

        foreach (var isTop in new[] { true, false })
            foreach (var isLeft in new[] { true, false })
                chrome.AddChild(CornerMask(w, h, radius, isTop, isLeft));

        var glow = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        glow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var glowStyle = new StyleBoxFlat { BgColor = Colors.Transparent, ShadowColor = glowColor };
        glowStyle.ShadowSize = 12;
        glowStyle.SetCornerRadiusAll(radius);
        glow.AddThemeStyleboxOverride("panel", glowStyle);
        chrome.AddChild(glow);
    }

    private static Panel CornerMask(float w, float h, int radius, bool isTop, bool isLeft)
    {
        float size = radius * 2f;
        int borderWidth = (int)(radius * 0.42f);

        var style = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = ScoundrelPalette.BackgroundNearBlack };
        style.BorderWidthTop    = isTop ? borderWidth : 0;
        style.BorderWidthBottom = isTop ? 0 : borderWidth;
        style.BorderWidthLeft   = isLeft ? borderWidth : 0;
        style.BorderWidthRight  = isLeft ? 0 : borderWidth;
        if (isTop && isLeft) style.CornerRadiusTopLeft = radius;
        else if (isTop && !isLeft) style.CornerRadiusTopRight = radius;
        else if (!isTop && isLeft) style.CornerRadiusBottomLeft = radius;
        else style.CornerRadiusBottomRight = radius;

        var patch = new Panel
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position    = new Vector2(isLeft ? 0f : w - size, isTop ? 0f : h - size),
            Size        = new Vector2(size, size),
        };
        patch.AddThemeStyleboxOverride("panel", style);
        return patch;
    }
}
