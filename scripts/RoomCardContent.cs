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
/// Monster/Weapon/Potion cards have no per-card flavor name in this codebase today
/// (CardModel only carries Suit+Rank), so their display name is derived as
/// "{RANK} OF {SUIT}" (e.g. "10 OF SPADES") rather than invented flavor text —
/// Blacksmith/Merchant/PotionJoker/WeaponJoker do have real names and use those.
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

    public static string DisplayName(CardModel card) => card.Kind switch
    {
        CardKind.Blacksmith  => "BLACKSMITH",
        CardKind.Merchant    => "MERCHANT",
        CardKind.PotionJoker => "RED JOKER",
        CardKind.WeaponJoker => "BLACK JOKER",
        CardKind.Monster or CardKind.Weapon or CardKind.Potion
            => $"{RankName(card.Rank)} OF {SuitName(card.Suit)}",
        _ => throw new System.InvalidOperationException($"Unhandled card kind: {card.Kind}"),
    };

    /// <summary>Left-hand footer text: suit glyph + singular suit name for a plain
    /// monster/weapon/potion card ("♠ SPADE"), or a small role tag for the rest.</summary>
    public static string FooterLeftText(CardModel card) => card.Kind switch
    {
        CardKind.Monster or CardKind.Weapon or CardKind.Potion
            => $"{SuitGlyph(card.Suit)} {SuitNameSingular(card.Suit)}",
        CardKind.Blacksmith or CardKind.Merchant     => "BLESSING",
        CardKind.PotionJoker or CardKind.WeaponJoker => "COMPANION",
        _ => throw new System.InvalidOperationException($"Unhandled card kind: {card.Kind}"),
    };

    /// <summary>Right-hand footer numeral for monster/weapon/potion cards (the value
    /// shown large in the mockup's "♠ SPADE   4" row); null for the friendly kinds,
    /// which show a sparkle glyph there instead (see RoomCardOverlay).</summary>
    public static int? FooterValue(CardModel card) => card.Kind switch
    {
        CardKind.Monster => card.MonsterValue,
        CardKind.Weapon  => card.WeaponValue,
        CardKind.Potion  => card.PotionValue,
        _ => null,
    };

    public static string RankName(int rank) => rank switch
    {
        1  => "ACE",
        11 => "JACK",
        12 => "QUEEN",
        13 => "KING",
        _  => rank.ToString(),
    };

    public static string SuitName(Suit suit) => suit switch
    {
        Suit.Clubs    => "CLUBS",
        Suit.Spades   => "SPADES",
        Suit.Hearts   => "HEARTS",
        Suit.Diamonds => "DIAMONDS",
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
