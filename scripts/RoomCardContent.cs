/// <summary>
/// Which of the four visual banner treatments a card kind gets on the room-card
/// overlay (see RoomCardOverlay.cs). Monster/Weapon/Potion each get their own banner
/// color; Blacksmith/Merchant/PotionJoker/WeaponJoker all read as "friendly" and share
/// the gold/tan banner per the ui_overhaul.png mockup (which only shows one friendly
/// example — Blacksmith — so the other three friendly kinds are extrapolated from it).
/// </summary>
public enum RoomCardBannerFamily { Monster, Weapon, Potion, Friendly }

/// <summary>
/// Pure (Godot-free) content derivation for a room card's visual overlay: display
/// name, banner family, and footer text/value. Split out from RoomCardOverlay (the
/// Godot Control that actually draws these) the same way ScoundrelRules is split from
/// ScoundrelGame — so this is unit-testable without spinning up a scene tree.
///
/// Monster/Weapon/Potion cards DO have a per-card flavor name (e.g. "CYCLOPS",
/// "ELVEN BOW") — tools/gen_cards.py's NAMES dict, now surfaced onto CardModel via
/// CardData.FromGodotCard's flavor_name read (see CardModel.cs/CardData.cs) instead of
/// only being baked into the old full card art's own bottom text banner. DisplayName
/// falls back to "{RANK} OF {SUIT}" only if FlavorName is somehow absent (defensive;
/// every card gen_cards.py generates has one).
/// </summary>
public static class RoomCardContent
{
    public static RoomCardBannerFamily BannerFamily(CardKind kind) => kind switch
    {
        CardKind.Monster => RoomCardBannerFamily.Monster,
        CardKind.Weapon  => RoomCardBannerFamily.Weapon,
        CardKind.Potion  => RoomCardBannerFamily.Potion,
        CardKind.Blacksmith or CardKind.Merchant or CardKind.PotionJoker or CardKind.WeaponJoker
            => RoomCardBannerFamily.Friendly,
        _ => throw new System.InvalidOperationException($"Unhandled card kind: {kind}"),
    };

    // Title Case, not ALL CAPS -- Cinzel Decorative (the font these render in, see
    // ScoundrelPalette.DisplayDecorative) substitutes a distinct, more ornate glyph
    // for at least one letter (a curled "U") when it sees genuine uppercase input,
    // which read as visibly fussier than the mockup's plainer block-letter look;
    // confirmed via a local render comparing "SCOUNDREL" against "Scoundrel" in the
    // same font before making this change.
    public static string DisplayName(CardModel card) => card.Kind switch
    {
        CardKind.Blacksmith  => "Blacksmith",
        CardKind.Merchant    => "Merchant",
        CardKind.PotionJoker => "Red Joker",
        CardKind.WeaponJoker => "Black Joker",
        CardKind.Monster or CardKind.Weapon or CardKind.Potion
            => TitleCase(card.FlavorName) ?? $"{RankName(card.Rank)} of {SuitName(card.Suit)}",
        _ => throw new System.InvalidOperationException($"Unhandled card kind: {card.Kind}"),
    };

    // tools/gen_cards.py's NAMES dict (surfaced as CardModel.FlavorName) is ALL CAPS
    // -- kept that way there since it's also historically baked into the old full
    // card art's own bottom banner, out of scope to touch here -- so the display
    // path lower-cases and re-title-cases it rather than assuming the source data's
    // casing.
    private static string? TitleCase(string? s) => s == null
        ? null
        : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    /// <summary>Left-hand footer text: suit glyph + singular suit name — "BLESSING"
    /// was a meaningless static label for Blacksmith/Merchant (direct feedback: "it
    /// should display the suit and rank like any other card"), so they now use the
    /// same suit+name pattern as Monster/Weapon/Potion. PotionJoker/WeaponJoker keep
    /// "COMPANION" since they have no suit of their own worth showing.</summary>
    public static string FooterLeftText(CardModel card) => card.Kind switch
    {
        CardKind.Monster or CardKind.Weapon or CardKind.Potion or CardKind.Blacksmith or CardKind.Merchant
            => $"{SuitGlyph(card.Suit)} {SuitNameSingular(card.Suit)}",
        CardKind.PotionJoker or CardKind.WeaponJoker => "COMPANION",
        _ => throw new System.InvalidOperationException($"Unhandled card kind: {card.Kind}"),
    };

    /// <summary>Right-hand footer numeral: the value shown large in the mockup's
    /// "♠ SPADE   4" row. Blacksmith/Merchant have no game-mechanical "value" the way
    /// a monster/weapon/potion does, so this shows their plain rank instead (same
    /// "display suit and rank like any other card" feedback as FooterLeftText)
    /// rather than the sparkle placeholder it used to fall through to. PotionJoker/
    /// WeaponJoker still get null/sparkle -- they're suitless.</summary>
    public static int? FooterValue(CardModel card) => card.Kind switch
    {
        CardKind.Monster    => card.MonsterValue,
        CardKind.Weapon     => card.WeaponValue,
        CardKind.Potion     => card.PotionValue,
        CardKind.Blacksmith or CardKind.Merchant => card.Rank,
        _ => null,
    };

    public static string RankName(int rank) => rank switch
    {
        1  => "Ace",
        11 => "Jack",
        12 => "Queen",
        13 => "King",
        _  => rank.ToString(),
    };

    public static string SuitName(Suit suit) => suit switch
    {
        Suit.Clubs    => "Clubs",
        Suit.Spades   => "Spades",
        Suit.Hearts   => "Hearts",
        Suit.Diamonds => "Diamonds",
        _ => throw new System.InvalidOperationException($"Suit {suit} has no room-card name."),
    };

    public static string SuitNameSingular(Suit suit) => suit switch
    {
        Suit.Clubs    => "CLUB",
        Suit.Spades   => "SPADE",
        Suit.Hearts   => "HEART",
        Suit.Diamonds => "DIAMOND",
        _ => throw new System.InvalidOperationException($"Suit {suit} has no room-card name."),
    };

    public static string SuitGlyph(Suit suit) => suit switch
    {
        Suit.Clubs    => "♣",
        Suit.Spades   => "♠",
        Suit.Hearts   => "♥",
        Suit.Diamonds => "♦",
        _ => throw new System.InvalidOperationException($"Suit {suit} has no glyph."),
    };

    /// <summary>
    /// Overlay description body text: ScoundrelRules.TooltipFor with its leading
    /// "Kind — " prefix stripped, since the overlay's banner already shows the name/kind
    /// and repeating it in the body would be redundant. Falls back to the untouched
    /// string if it doesn't have the expected "Kind — " shape (defensive; every
    /// TooltipFor branch emits one today).
    /// </summary>
    public static string Description(string tooltip)
    {
        int idx = tooltip.IndexOf(" — ", System.StringComparison.Ordinal);
        return idx >= 0 ? tooltip[(idx + 3)..] : tooltip;
    }
}
