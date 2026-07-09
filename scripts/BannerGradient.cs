using System.Collections.Generic;
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
///
/// Both Style() and CornerMask() are called once per room/companion card overlay
/// build, which happens often (every room refill, every viewport resize) -- caching
/// by the (small, finite) parameter set that actually varies is not an optimization,
/// it's required: a gdUnit4 scene-test run creating a fresh Gradient/
/// GradientTexture2D/StyleBoxTexture/StyleBoxFlat on every single call leaked
/// hundreds of native RefCounted references faster than the GC could reclaim them
/// and crashed the runner outright ("Leaked unsafe reference ... Aborted (core
/// dumped)") -- confirmed by comparing against the immediately prior commit's clean
/// CI run, which had zero such leaks before this file existed.
/// </summary>
public static class BannerGradient
{
    private static readonly Dictionary<Color, StyleBoxTexture> StyleCache = new();
    private static readonly Dictionary<(int radius, bool isLeft), StyleBoxFlat> MaskStyleCache = new();

    public static StyleBoxTexture Style(Color baseColor)
    {
        if (StyleCache.TryGetValue(baseColor, out var cached)) return cached;

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
        var style = new StyleBoxTexture { Texture = texture };
        StyleCache[baseColor] = style;
        return style;
    }

    public static Panel CornerMask(float w, int radius, bool isLeft)
    {
        float patchSize = radius * 2f;

        if (!MaskStyleCache.TryGetValue((radius, isLeft), out var style))
        {
            style = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = ScoundrelPalette.DividerGoldBrown };
            // Border width is intentionally a bit wider than the background's own
            // 2px border (see RoomCardOverlay/CompanionPanelOverlay) -- 2px only
            // just covers the theoretical max corner nub (~0.293*radius, 2.93px at
            // radius 10) with almost no margin, and a live in-editor screenshot
            // showed a thin sliver of gradient still escaping the rounded corner
            // that this synthetic headless capture didn't reproduce. 3px trades a
            // barely-perceptible corner-only border thickness bump for reliably
            // covering the nub regardless of small rendering/AA differences.
            style.BorderWidthTop    = 3;
            style.BorderWidthLeft   = isLeft ? 3 : 0;
            style.BorderWidthRight  = isLeft ? 0 : 3;
            style.BorderWidthBottom = 0;
            if (isLeft) style.CornerRadiusTopLeft = radius;
            else style.CornerRadiusTopRight = radius;
            MaskStyleCache[(radius, isLeft)] = style;
        }

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
