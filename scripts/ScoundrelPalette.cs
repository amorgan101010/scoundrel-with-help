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

    // ── Room-card overlay banner colors (chunk 2, revised chunk 6 — see RoomCardOverlay.cs) ──
    // Chunk 2 originally gave every card of a given KIND (Monster/Weapon/Potion) one
    // shared banner color. Chunk 6 replaced that with per-SUIT colors sampled directly
    // from that suit's existing card_assets/*.svg border-stroke color, since chunk 6
    // also started showing the real per-suit-colored card illustration in the icon
    // slot — a fixed "all monsters are red" banner looked wrong sitting above
    // differently-colored (green clubs / purple spades) real art. Blacksmith/Merchant
    // (friendly NPCs, not weapons/potions despite sharing their suits) keep one shared
    // banner regardless of suit, matching the mockup's own Blacksmith example
    // directly — Jokers do NOT share this anymore (see RoomCardOverlay.BannerColors),
    // they keep their own companion identity color even as a room card.
    // Friendly-NPC banner fill (Blacksmith/Merchant). Originally a yellow-gold
    // (#C99D2D) too close in hue to BannerDiamondsGold's olive-gold below for the two
    // to read as different colors at a glance — shifted toward orange (hue ~24° vs
    // diamonds' ~40°) to actually separate them, per direct feedback.
    public static readonly Color BannerGoldFriendly  = new Color(0.788f, 0.416f, 0.176f); // #C96A2D
    // Clubs banner (monster) — sampled from 10_clubs.svg's border stroke.
    public static readonly Color BannerClubsGreen    = new Color(0.157f, 0.412f, 0.243f); // #286B3E
    // Spades banner (monster) — sampled from 7_spades.svg's border stroke.
    public static readonly Color BannerSpadesPurple  = new Color(0.227f, 0.125f, 0.463f); // #3A2076
    // Diamonds banner (weapon) — sampled from 7_diamonds.svg's border stroke.
    public static readonly Color BannerDiamondsGold  = new Color(0.486f, 0.365f, 0.078f); // #7C5D14
    // Hearts/potion banner now uses PotionRoseBright (below), not a suit-sampled red,
    // to match the "Potions" REMAINING-tally text color — see RoomCardOverlay.
    // BannerColors.
    // Brighter red used for the monster footer's large damage numeral.
    public static readonly Color MonsterRedValue     = new Color(0.761f, 0.231f, 0.290f); // #C23B4A
    // Neutral warm gray used for room-card overlay body/description text.
    public static readonly Color DescriptionGray     = new Color(0.741f, 0.702f, 0.663f); // #BDB3A9
    // Brighter blue/rose accents — NOT icon tints anymore (chunk 6 removed the
    // hand-drawn room-card icon in favor of real card art), but still used by the
    // chunk-3 "REMAINING" gameplay-tally Weapons/Potions label colors and by
    // WeaponPanelOverlay's icon before chunk 6 gets around to porting that panel
    // to real art too — kept as shared named colors rather than removed.
    public static readonly Color WeaponBlueBright    = new Color(0.475f, 0.647f, 0.847f); // #79A5D8
    public static readonly Color PotionRoseBright    = new Color(0.796f, 0.412f, 0.616f); // #CB699D

    // ── Weapon panel colors (chunk 3 — see WeaponPanelOverlay.cs) ───────────
    // Muted dusty rose used for the weapon panel's "next: < N" constraint hint —
    // no pixel-probe tool was available this session, so this is a close visual
    // match to the mockup's soft rose-red rather than an exact sampled value
    // (unlike the chunk-1/chunk-2 colors above, which were sampled directly).
    public static readonly Color ConstraintRose      = new Color(0.706f, 0.408f, 0.455f); // #B46874

    // ── Companion panel colors (chunk 4 — see CompanionPanelOverlay.cs) ─────
    // Sampled directly from the mockup's RED JOKER/BLACK JOKER banner pixels
    // (pixel-probed with PIL this session, averaging each banner's left/right
    // edges to avoid the centered banner-title text). Deliberately distinct from
    // BannerMaroonMonster/BannerGoldFriendly above: once a Joker becomes a
    // companion (this panel) it gets its own identity color rather than the
    // generic room-card monster/friendly banner families — that's intentional
    // per the mockup, not an inconsistency to reconcile.
    // Red Joker (Potion Joker) banner fill; also reused for the HP bar's red
    // fill, since both companions' HP bars sample to this same red in the
    // mockup and a second near-duplicate color would be pure token bloat.
    public static readonly Color CompanionRedBanner   = new Color(0.518f, 0.188f, 0.192f); // #843031
    // Black Joker (Weapon Joker) banner fill — a warm near-black, sampled
    // distinctly lighter/warmer than BackgroundNearBlack/CardBackMaroon above.
    public static readonly Color CompanionBlackBanner = new Color(0.173f, 0.157f, 0.129f); // #2C2821

    // ── Bottom action button colors (chunk 5 — RunButton/NextRoomButton) ────
    // Pixel-sampled (PIL, median over ~3000 border+text pixels) from the mockup's
    // "RUN — SKIP ROOM" pill button. Closest existing color is CompanionRedBanner,
    // but at a normalized RGB distance of ~0.115 (~15/255 combined) it reads as a
    // visibly different red, so this gets its own constant rather than drifting
    // the button color toward an unrelated companion-identity red.
    public static readonly Color ButtonOutlineRose = new Color(0.616f, 0.231f, 0.235f); // #9D3B3C
    // Pixel-sampled fill from the mockup's solid-gold "NEXT ROOM →" pill button.
    // Closest existing gold (BannerGoldFriendly) differs mainly in the blue
    // channel (0.353 vs 0.176 — a visibly warmer/less-saturated gold here), so
    // this is its own sampled value rather than reusing the banner gold.
    public static readonly Color ButtonSolidGold  = new Color(0.769f, 0.624f, 0.353f); // #C49F5A

    // ── Fonts ───────────────────────────────────────────────────────────────
    public const string DisplayBoldFontPath       = "res://assets/fonts/NotoSerifDisplay-Bold.ttf";
    public const string SerifRegularFontPath      = "res://assets/fonts/NotoSerif-Regular.ttf";
    // SIL Open Font License, same terms as the two fonts above -- verified via the
    // font file's own embedded name-table license string before bundling.
    public const string DisplayDecorativeFontPath = "res://assets/fonts/CinzelDecorative-Bold.ttf";

    private static Font? _displayBold;
    private static Font? _serifRegular;
    private static Font? _displayDecorative;

    /// <summary>Noto Serif Display Bold — badge/HP/weapon numbers, weapon name strip.</summary>
    public static Font DisplayBold  => _displayBold  ??= GD.Load<Font>(DisplayBoldFontPath);
    /// <summary>Noto Serif Regular — subtitles, section labels, body text.</summary>
    public static Font SerifRegular => _serifRegular ??= GD.Load<Font>(SerifRegularFontPath);
    /// <summary>Cinzel Decorative Bold — the game title and card/companion banner
    /// headers only (per direct feedback, matching ui_overhaul.png), not every
    /// DisplayBold use -- its ornate strokes read poorly at small numeral sizes.</summary>
    public static Font DisplayDecorative => _displayDecorative ??= GD.Load<Font>(DisplayDecorativeFontPath);
}
