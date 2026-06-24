using Godot;
using Godot.Collections;

// Suit enum lives in ScoundrelRules.cs (Godot-free, testable).
// CardModel lives in CardModel.cs (Godot-free, testable).
// This file is the Godot-facing adapter only.

public static class CardData
{
    public static CardModel FromGodotCard(GodotObject card)
    {
        var info = card.Get("card_info").AsGodotDictionary();
        var rank = info["rank"].AsInt32();
        var name = info["name"].AsString();

        var suit = info["suit"].AsString() switch
        {
            "clubs"    => Suit.Clubs,
            "spades"   => Suit.Spades,
            "hearts"   => Suit.Hearts,
            "diamonds" => Suit.Diamonds,
            _          => Suit.Clubs,
        };

        return new CardModel(suit, rank, name);
    }
}
