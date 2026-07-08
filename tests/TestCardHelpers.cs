using System;
using System.Collections.Generic;

namespace ScoundrelTests;

// ── Helpers ───────────────────────────────────────────────────────────────────

internal static class Cards
{
    public static CardModel Monster(int rank) => new(Suit.Clubs,    rank);
    public static CardModel Spade(int rank)   => new(Suit.Spades,   rank);
    public static CardModel Weapon(int rank)  => new(Suit.Diamonds, rank);
    public static CardModel Potion(int rank)  => new(Suit.Hearts,   rank);
    public static CardModel RedJoker()        => new(Suit.RedJoker, 0, "joker_red");
    public static CardModel BlackJoker()      => new(Suit.BlackJoker, 0, "joker_black");

    public static CardModel Blacksmith(int rank) => new(Suit.Diamonds, rank, BlacksmithName(rank));
    public static CardModel Merchant(int rank)   => new(Suit.Hearts,   rank, MerchantName(rank));

    private static string BlacksmithName(int rank) => rank switch
    {
        11 => "jack_diamonds",
        12 => "queen_diamonds",
        13 => "king_diamonds",
        ScoundrelRules.AceRank => "ace_diamonds",
        _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Not a Blacksmith rank"),
    };

    private static string MerchantName(int rank) => rank switch
    {
        11 => "jack_hearts",
        12 => "queen_hearts",
        13 => "king_hearts",
        ScoundrelRules.AceRank => "ace_hearts",
        _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Not a Merchant rank"),
    };

    // Pad a short card list to 4 so DealRoom fills the room immediately.
    // Extra padding cards are weak monsters that sit at the bottom of the deck.
    public static CardModel[] PadToFour(params CardModel[] cards)
    {
        var padded = new List<CardModel>(cards);
        while (padded.Count < ScoundrelRules.RoomSize) padded.Insert(0, Monster(2));
        return padded.ToArray();
    }

    // Build a deck whose top RoomSize cards (last in array) are the supplied room cards.
    public static GameEngine RoomOf(params CardModel[] roomCards)
    {
        var deck = PadToFour(roomCards);
        return new GameEngine(deck);
    }
}
