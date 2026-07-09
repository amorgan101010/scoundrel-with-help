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
        // Only present for monster/weapon/potion cards -- tools/gen_cards.py writes it
        // from its own NAMES dict (the per-card flavor name it used to bake into the
        // old full card art's bottom text banner). Blacksmith/Merchant/Joker cards have
        // no real art or flavor name yet, so this is null for them (see
        // RoomCardContent.DisplayName's fallback).
        string? flavorName = info.ContainsKey("flavor_name") ? info["flavor_name"].AsString() : null;

        var suit = info["suit"].AsString() switch
        {
            "clubs"       => Suit.Clubs,
            "spades"      => Suit.Spades,
            "hearts"      => Suit.Hearts,
            "diamonds"    => Suit.Diamonds,
            "red_joker"   => Suit.RedJoker,
            "black_joker" => Suit.BlackJoker,
            _             => Suit.Clubs,
        };

        return new CardModel(suit, rank, name, flavorName);
    }
}
