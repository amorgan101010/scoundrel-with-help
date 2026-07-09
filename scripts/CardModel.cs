/// <summary>
/// The seven card classifications a <see cref="CardModel"/> can fall into. Backs
/// <see cref="CardModel.Kind"/> — a single value to switch on instead of independently
/// re-deriving the same classification from the Is* booleans at every dispatch site
/// (GameEngine.TakeCard, ScoundrelGame.OnCardSelected/OnCardDragStarted,
/// ScoundrelRules.TooltipFor). Switching on this enum with a throwing `default` gives
/// those call sites a loud failure if an 8th kind is ever added and a site is missed,
/// instead of a silent no-op/empty-string fallback.
/// </summary>
public enum CardKind { Monster, Weapon, Potion, Blacksmith, Merchant, PotionJoker, WeaponJoker }

/// <summary>
/// Godot-free card data record. Used by GameEngine and unit tests.
/// </summary>
public record CardModel(Suit Suit, int Rank, string Name = "", string? FlavorName = null)
{
    public bool IsMonster => Suit is Suit.Clubs or Suit.Spades;
    public bool IsWeapon  => Suit == Suit.Diamonds && Rank is >= 2 and <= 10;
    public bool IsPotion  => Suit == Suit.Hearts   && Rank is >= 2 and <= 10;

    // Diamond/Heart face cards + Ace (Rank 1, or 11-13) are Blacksmith/Merchant cards
    // in Extended mode, not weapons/potions.
    public bool IsBlacksmith => Suit == Suit.Diamonds && (Rank == 1 || Rank >= 11);
    public bool IsMerchant   => Suit == Suit.Hearts   && (Rank == 1 || Rank >= 11);

    public bool IsPotionJoker => Suit == Suit.RedJoker;
    public bool IsWeaponJoker => Suit == Suit.BlackJoker;

    public int MonsterValue => ScoundrelRules.MonsterValue(Rank);
    public int WeaponValue  => Rank;
    public int PotionValue  => Rank;

    /// <summary>
    /// Thin wrapper around the Is* classification booleans above, picking whichever one
    /// is true. Kept as a derived property (not a stored field) so it can never disagree
    /// with the booleans it's built from. The throwing fallback is genuinely unreachable
    /// today (every CardModel matches exactly one of the seven), but a plain
    /// InvalidOperationException here is far cheaper than a silently wrong dispatch
    /// decision if that ever stops being true.
    /// </summary>
    public CardKind Kind =>
        IsMonster     ? CardKind.Monster     :
        IsWeapon      ? CardKind.Weapon      :
        IsPotion      ? CardKind.Potion      :
        IsBlacksmith  ? CardKind.Blacksmith  :
        IsMerchant    ? CardKind.Merchant    :
        IsPotionJoker ? CardKind.PotionJoker :
        IsWeaponJoker ? CardKind.WeaponJoker :
        throw new System.InvalidOperationException(
            $"Card '{Name}' (Suit={Suit}, Rank={Rank}) does not match any known CardKind.");
}
