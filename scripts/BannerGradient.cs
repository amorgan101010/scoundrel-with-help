using System.Collections.Generic;
using Godot;

/// <summary>
/// Shared "gradient banner with rounded top corners" technique, used by both
/// RoomCardOverlay and CompanionPanelOverlay's header bars (per direct feedback:
/// "card banners ... have gradients instead of solid colors"). StyleBoxFlat's
/// bg_color is flat-only, so the fill comes from StyleBoxTexture + GradientTexture2D
/// (both generated procedurally at runtime from Color stops -- no image asset).
/// StyleBoxTexture has no corner-radius equivalent, though, so its own corners come
/// out square; CornerMask covers the two corners that sit at the card's own rounded
/// top corners with a CornerNubMask (see that file's class doc for why a
/// border-stroke mask -- this file's previous technique -- doesn't actually work:
/// confirmed via a real in-editor screenshot that the nub itself has no covering
/// geometry at all under that approach, regardless of border width).
///
/// Style() is called once per room/companion card overlay build, which happens
/// often (every room refill, every viewport resize) -- caching by the (small,
/// finite) parameter set that actually varies is not an optimization, it's
/// required: a gdUnit4 scene-test run creating a fresh Gradient/GradientTexture2D/
/// StyleBoxTexture on every single call leaked hundreds of native RefCounted
/// references faster than the GC could reclaim them and crashed the runner
/// outright ("Leaked unsafe reference ... Aborted (core dumped)").
/// </summary>
public static class BannerGradient
{
    private static readonly Dictionary<Color, StyleBoxTexture> StyleCache = new();

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

    public static CornerNubMask CornerMask(float w, int radius, bool isLeft)
    {
        var position = new Vector2(isLeft ? 0f : w - radius, 0f);
        return CornerNubMask.Create(position, radius, isTop: true, isLeft, ScoundrelPalette.BackgroundNearBlack);
    }
}
