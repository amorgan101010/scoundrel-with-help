using Godot;

/// <summary>
/// A wide, uniformly-dim glow behind a Label, hugging its exact letterforms —
/// per direct feedback that Label's own per-glyph font_shadow_color technique
/// (used for one round of this) has a real defect: each glyph's dilated shadow is
/// a separate alpha-blended draw, so where two letters' dilated shadows overlap,
/// the overlap visibly "doubles" in brightness (standard alpha-compositing
/// stacking) — and widening shadow_outline_size only makes the overlap worse,
/// the opposite of what "wider but still subtle" needs.
///
/// Fixed by rendering the dilated shadow shape ONCE into a SubViewport at full
/// opacity (so every pixel the shape covers clamps to the same value — nothing
/// left to double, regardless of how many letters overlap there), then drawing
/// that flat result as a single texture with the target dim alpha applied as one
/// uniform Modulate multiply, not per-glyph.
/// </summary>
public partial class TextGlow : TextureRect
{
    public static TextGlow Apply(Label label, Font font, int fontSize, Color glowColor, float alpha, int outlineSize)
    {
        var viewport = new SubViewport
        {
            Size = new Vector2I(Mathf.CeilToInt(label.Size.X), Mathf.CeilToInt(label.Size.Y)),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };

        var shadowLabel = new Label
        {
            Text = label.Text,
            Size = label.Size,
        };
        shadowLabel.AddThemeFontOverride("font", font);
        shadowLabel.AddThemeFontSizeOverride("font_size", fontSize);
        shadowLabel.AddThemeColorOverride("font_color", Colors.Transparent);
        shadowLabel.AddThemeColorOverride("font_shadow_color", Colors.White);
        shadowLabel.AddThemeConstantOverride("shadow_offset_x", 0);
        shadowLabel.AddThemeConstantOverride("shadow_offset_y", 0);
        shadowLabel.AddThemeConstantOverride("shadow_outline_size", outlineSize);
        viewport.AddChild(shadowLabel);

        var glow = new TextGlow
        {
            Position    = label.Position,
            Size        = label.Size,
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
