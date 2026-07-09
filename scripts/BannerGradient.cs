using Godot;

/// <summary>
/// Shared "gradient banner with rounded top corners" technique, used by both
/// RoomCardOverlay and CompanionPanelOverlay's header bars (per direct feedback:
/// "card banners ... have gradients instead of solid colors"). StyleBoxFlat's
/// bg_color is flat-only, so the fill comes from StyleBoxTexture + GradientTexture2D
/// (both generated procedurally at runtime from Color stops -- no image asset).
/// StyleBoxTexture has no corner-radius equivalent, though, so its own corners come
/// out square; CornerMask patches redraw just the rounded border STROKE (with a
/// transparent fill, so none of the gradient is hidden) on top of the two corners
/// that sit at the card's own rounded top corners, restoring the illusion of
/// roundness. Confirmed via local screenshot before this was extracted from
/// RoomCardOverlay (which had it inline first).
/// </summary>
public static class BannerGradient
{
    public static StyleBoxTexture Style(Color baseColor)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, baseColor.Lightened(0.18f));
        gradient.SetColor(1, baseColor.Darkened(0.12f));
        var texture = new GradientTexture2D
        {
            Gradient = gradient,
            Width    = 8,
            Height   = 64,
            Fill     = GradientTexture2D.FillEnum.Linear,
            FillTo   = new Vector2(0f, 1f),
        };
        return new StyleBoxTexture { Texture = texture };
    }

    public static Panel CornerMask(float w, int radius, bool isLeft)
    {
        float patchSize = radius * 2f;
        var style = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = ScoundrelPalette.DividerGoldBrown };
        style.BorderWidthTop    = 2;
        style.BorderWidthLeft   = isLeft ? 2 : 0;
        style.BorderWidthRight  = isLeft ? 0 : 2;
        style.BorderWidthBottom = 0;
        if (isLeft) style.CornerRadiusTopLeft = radius;
        else style.CornerRadiusTopRight = radius;

        var patch = new Panel
        {
            OffsetTop    = 0f,
            OffsetBottom = patchSize,
            OffsetLeft   = isLeft ? 0f : w - patchSize,
            OffsetRight  = isLeft ? patchSize : w,
            MouseFilter  = Control.MouseFilterEnum.Ignore,
        };
        patch.AddThemeStyleboxOverride("panel", style);
        return patch;
    }
}
