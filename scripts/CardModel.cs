/// <summary>
/// Godot-free card data record. Used by GameEngine and unit tests.
/// </summary>
public record CardModel(Suit Suit, int Rank, string Name = "")
{
    public bool IsMonster => Suit is Suit.Clubs or Suit.Spades;
    public bool IsWeapon  => Suit == Suit.Diamonds;
    public bool IsPotion  => Suit == Suit.Hearts;

    public int MonsterValue => ScoundrelRules.MonsterValue(Rank);
    public int WeaponValue  => Rank;
    public int PotionValue  => Rank;
}
