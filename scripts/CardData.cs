using Godot;
using Godot.Collections;

// Suit enum lives in ScoundrelRules.cs (Godot-free, testable).

public class CardData
{
    public Suit Suit { get; init; }
    public int Rank { get; init; }   // 1=A, 2-10=face, 11=J, 12=Q, 13=K
    public string CardName { get; init; } = "";

    // Clubs/Spades: A counts as 14 (strongest monster)
    public int MonsterValue => Suit is Suit.Clubs or Suit.Spades
        ? ScoundrelRules.MonsterValue(Rank)
        : 0;

    // Diamonds: weapon value equals rank (A=1, 2-10=face)
    public int WeaponValue => Suit == Suit.Diamonds ? Rank : 0;

    // Hearts: potion value equals rank (2-10 only in Scoundrel deck)
    public int PotionValue => Suit == Suit.Hearts ? Rank : 0;

    public static CardData FromGodotCard(GodotObject card)
    {
        var info = card.Get("card_info").AsGodotDictionary();
        var suitStr = info["suit"].AsString();
        var rank = info["rank"].AsInt32();
        var name = info["name"].AsString();

        var suit = suitStr switch
        {
            "clubs"    => Suit.Clubs,
            "spades"   => Suit.Spades,
            "hearts"   => Suit.Hearts,
            "diamonds" => Suit.Diamonds,
            _          => Suit.Clubs,
        };

        return new CardData { Suit = suit, Rank = rank, CardName = name };
    }
}
