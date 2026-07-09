using System.Collections.Generic;
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
/// Each of the 4 corners gets a CornerNubMask (see that file's class doc) instead
/// of a border-stroke mask -- confirmed via a real in-editor screenshot that a
/// border-stroke mask leaves the actual corner nub completely uncovered (no
/// geometry there at all, regardless of border width), so the square gradient
/// texture still showed through underneath.
///
/// Resized can fire repeatedly (layout settling, viewport resize) and each firing
/// used to build a fresh StyleBoxFlat per corner mask + glow -- same "leaked unsafe
/// reference" crash BannerGradient hit, see its class doc. Style resources are
/// cached by their (small, finite) actual parameters; only the Controls themselves
/// (which must reflect the button's current pixel size) are rebuilt per call.
/// </summary>
public static class ButtonGradient
{
    private const string ChromeName = "GradientChrome";

    private static readonly Dictionary<(int radius, Color glowColor), StyleBoxFlat> GlowStyleCache = new();

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
            {
                var position = new Vector2(isLeft ? 0f : w - radius, isTop ? 0f : h - radius);
                chrome.AddChild(CornerNubMask.Create(position, radius, isTop, isLeft, ScoundrelPalette.BackgroundNearBlack));
            }

        var glow = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        glow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        glow.AddThemeStyleboxOverride("panel", GlowStyle(radius, glowColor));
        chrome.AddChild(glow);
    }

    private static StyleBoxFlat GlowStyle(int radius, Color glowColor)
    {
        if (GlowStyleCache.TryGetValue((radius, glowColor), out var cached)) return cached;

        var style = new StyleBoxFlat { BgColor = Colors.Transparent, ShadowColor = glowColor };
        style.ShadowSize = 12;
        style.SetCornerRadiusAll(radius);
        GlowStyleCache[(radius, glowColor)] = style;
        return style;
    }
}
