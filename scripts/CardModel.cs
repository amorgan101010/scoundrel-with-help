/// <summary>
/// Godot-free card data record. Used by GameEngine and unit tests.
/// </summary>
public record CardModel(Suit Suit, int Rank, string Name = "")
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
}
