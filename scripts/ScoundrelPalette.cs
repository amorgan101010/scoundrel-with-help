using Godot;

/// <summary>
/// Shared visual foundation for the UI overhaul (see ui_overhaul.png at the repo root
/// for the source mockup). Colors below were sampled directly from that mockup's PNG
/// pixels (not eyeballed) so every later chunk of the redesign (room cards, weapon
/// panel, companion panel, ...) can reuse the exact same values instead of drifting.
///
/// Fonts are two OFL-licensed system fonts bundled into assets/fonts/: Noto Serif
/// Display Bold for large serif headings, Noto Serif Regular for body/label text.
/// There's no existing convention in this codebase for a centralized Theme .tres
/// resource — every StyleBoxFlat/Color today is either a scene sub_resource or an
/// inline `new Color(...)` in C# — so this plain static class follows that existing
/// pattern rather than introducing a new one. `.tscn` sub_resources can't reference
/// C# constants, so StyleBoxFlat colors defined in Game.tscn are hand-kept in sync
/// with the values here; if you touch one, touch the other.
/// </summary>
public static class ScoundrelPalette
{
    // ── Colors (sampled from ui_overhaul.png) ──────────────────────────────
    // Near-black warm brown page background.
    public static readonly Color BackgroundNearBlack = new Color(0.098f, 0.078f, 0.063f); // #191410
    // Muted gold-brown used for the full-width header divider line.
    public static readonly Color DividerGoldBrown    = new Color(0.192f, 0.149f, 0.098f); // #312619
    // Saturated gold used for ring/border outlines (HP badge ring, deck-back border).
    public static readonly Color AccentGold          = new Color(0.651f, 0.502f, 0.235f); // #A6803C
    // Cream/off-white used for large serif display headings ("SCOUNDREL").
    public static readonly Color TitleCream          = new Color(0.886f, 0.831f, 0.702f); // #E2D4B3
    // Muted gray-gold used for subtitles, small-caps section labels, and outline-button text.
    public static readonly Color MutedGoldGray       = new Color(0.608f, 0.549f, 0.439f); // #9B8C70
    // Brighter gold used for the deck-count "N LEFT" readout text.
    public static readonly Color BrightGold          = new Color(0.886f, 0.749f, 0.486f); // #E2BF7C
    // Dark maroon-brown card-back fill for the deck-count badge.
    public static readonly Color CardBackMaroon      = new Color(0.133f, 0.094f, 0.059f); // #221810

    // ── Fonts ───────────────────────────────────────────────────────────────
    public const string DisplayBoldFontPath  = "res://assets/fonts/NotoSerifDisplay-Bold.ttf";
    public const string SerifRegularFontPath = "res://assets/fonts/NotoSerif-Regular.ttf";

    private static Font? _displayBold;
    private static Font? _serifRegular;

    /// <summary>Noto Serif Display Bold — big headings ("SCOUNDREL"), badge numbers.</summary>
    public static Font DisplayBold  => _displayBold  ??= GD.Load<Font>(DisplayBoldFontPath);
    /// <summary>Noto Serif Regular — subtitles, section labels, body text.</summary>
    public static Font SerifRegular => _serifRegular ??= GD.Load<Font>(SerifRegularFontPath);
}
