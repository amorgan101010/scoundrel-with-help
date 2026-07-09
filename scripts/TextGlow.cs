using Godot;

/// <summary>
/// A wide, dim, soft-edged glow behind a Label, hugging its exact letterforms —
/// per direct feedback across three rounds:
///
/// 1) Label's own per-glyph font_shadow_color is a separate alpha-blended draw per
///    glyph, so where two letters' dilated shadows overlap, the overlap visibly
///    "doubles" in brightness (standard alpha-compositing stacking), and a wider
///    outline only makes the overlap worse. Fixed by rendering the dilated shadow
///    shape ONCE into a SubViewport at full opacity (so overlapping regions all
///    clamp to the same value, nothing left to double), then drawing that flat
///    result as a single texture with the target dim alpha applied once via
///    Modulate, not per-glyph.
///
/// 2) That flat render has a hard dilated edge — uniform intensity with no
///    falloff, unlike the health ring's native StyleBoxFlat shadow (which Godot
///    blurs internally). Fixed by rendering the SubViewport at a fraction of the
///    real pixel size (DownscaleFactor) and letting the TextureRect's own default
///    linear-filtered upscale blur it back up to full size — a standard cheap
///    blur trick, not a shader.
///
/// 3) A wide glow needs room to extend past the label's own tight bounding box or
///    it clips at the render target's edge — Margin expands the SubViewport/
///    TextureRect bounds symmetrically and offsets the internal shadow copy to
///    match, so the glow can reach outward on all sides without being cut off.
/// </summary>
public partial class TextGlow : TextureRect
{
    private const float DownscaleFactor = 0.3f;

    public static TextGlow Apply(Label label, Font font, int fontSize, Color glowColor, float alpha, int outlineSize, float margin)
    {
        var canvasSize = label.Size + new Vector2(margin, margin) * 2f;
        var renderSize = canvasSize * DownscaleFactor;

        var viewport = new SubViewport
        {
            Size = new Vector2I(Mathf.CeilToInt(renderSize.X), Mathf.CeilToInt(renderSize.Y)),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };

        var shadowLabel = new Label
        {
            Text     = label.Text,
            Position = new Vector2(margin, margin) * DownscaleFactor,
            Size     = label.Size * DownscaleFactor,
        };
        shadowLabel.AddThemeFontOverride("font", font);
        shadowLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(fontSize * DownscaleFactor));
        shadowLabel.AddThemeColorOverride("font_color", Colors.Transparent);
        shadowLabel.AddThemeColorOverride("font_shadow_color", Colors.White);
        shadowLabel.AddThemeConstantOverride("shadow_offset_x", 0);
        shadowLabel.AddThemeConstantOverride("shadow_offset_y", 0);
        shadowLabel.AddThemeConstantOverride("shadow_outline_size", Mathf.RoundToInt(outlineSize * DownscaleFactor));
        viewport.AddChild(shadowLabel);

        var glow = new TextGlow
        {
            Position    = label.Position - new Vector2(margin, margin),
            Size        = canvasSize,
            Modulate    = glowColor with { A = alpha },
            MouseFilter = MouseFilterEnum.Ignore,
        };
        glow.AddChild(viewport);
        glow.Texture = viewport.GetTexture();

        var parent = label.GetParent();
        parent.AddChild(glow);
        parent.MoveChild(glow, label.GetIndex());
        return glow;
    }
}
