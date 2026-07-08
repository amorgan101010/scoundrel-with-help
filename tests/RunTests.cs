using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Run ───────────────────────────────────────────────────────────────────────

[TestFixture]
public class RunTests
{
    [Test]
    public void Run_ClearsRoom_AndDealsNew()
    {
        var deck = Enumerable.Range(0, 8).Select(_ => Cards.Potion(2)).ToArray();
        var engine = new GameEngine(deck);

        int deckBefore = engine.Deck.Count; // RoomSize (8 total, RoomSize dealt to room)
        engine.Run();

        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize));      // new room dealt
        Assert.That(engine.Deck.Count, Is.EqualTo(deckBefore)); // same deck size (RoomSize back in, RoomSize dealt out)
    }

    [Test]
    public void Run_SetsRanLastRoom()
    {
        var deck = Enumerable.Range(0, 8).Select(_ => Cards.Monster(2)).ToArray();
        var engine = new GameEngine(deck);

        engine.Run();

        Assert.That(engine.RanLastRoom, Is.True);
        Assert.That(engine.CanRun, Is.False);
    }

    [Test]
    public void CannotRunTwiceInARow_Throws()
    {
        var deck = Enumerable.Range(0, 16).Select(_ => Cards.Monster(2)).ToArray();
        var engine = new GameEngine(deck);

        engine.Run();

        Assert.Throws<InvalidOperationException>(() => engine.Run());
    }

    [Test]
    public void RunThenNextRoom_AllowsRunAgain()
    {
        var deck = Enumerable.Range(0, 16).Select(_ => Cards.Monster(2)).ToArray();
        var engine = new GameEngine(deck);

        engine.Run(); // run room 1

        // take 3 cards in room 2 to unlock NextRoom
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.NextRoom(); // advances, resets RanLastRoom

        Assert.That(engine.CanRun, Is.True, "Should be able to run after NextRoom");
    }

    [Test]
    public void RunReturnsAllFourRoomCards_ToDeck()
    {
        var roomCards = new[]
        {
            Cards.Monster(3), Cards.Monster(4), Cards.Weapon(5), Cards.Potion(6)
        };
        // 8-card deck: RoomSize filler at bottom, RoomSize room cards at top
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), Cards.Potion(2),
            roomCards[0], roomCards[1], roomCards[2], roomCards[3]
        };
        var engine = new GameEngine(deck);
        // Room is dealt; deck has RoomSize filler cards
        Assert.That(engine.Deck.Count, Is.EqualTo(ScoundrelRules.RoomSize));

        engine.Run();

        // The RoomSize room cards went back to deck, then RoomSize were dealt to new room
        Assert.That(engine.Deck.Count, Is.EqualTo(ScoundrelRules.RoomSize)); // RoomSize (back) + RoomSize filler - RoomSize (dealt) = RoomSize
        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize));
    }

    [Test]
    public void Run_PartialRoom_OnlyRemainingCardsSinkToBottom()
    {
        var m3 = Cards.Monster(3);
        var m4 = Cards.Monster(4);
        var w5 = Cards.Weapon(5);
        var p6 = Cards.Potion(6);
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), // filler
            m3, m4, w5, p6  // room 1 (top, dealt first)
        };
        var engine = new GameEngine(deck);

        // Take one card — 3 remain in the room.
        engine.TakeCard(p6);
        Assert.That(engine.Room.Count, Is.EqualTo(3));

        engine.Run();

        // New room was dealt (RoomSize cards).
        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize));
        // Only the 3 remaining cards sank; the taken potion is NOT in the deck.
        Assert.That(engine.Deck.Count, Is.EqualTo(3)); // 3 sank + RoomSize filler - RoomSize dealt
        Assert.That(engine.Deck.Take(3), Is.EquivalentTo(new[] { m3, m4, w5 }));
        Assert.That(engine.Deck, Does.Not.Contain(p6));
    }

    [Test]
    public void Run_ResetsPotionUsedThisRoom()
    {
        var p5 = Cards.Potion(5);
        var deck = new[]
        {
            Cards.Monster(2), Cards.Monster(2), Cards.Monster(2), Cards.Monster(2), // room 2
            Cards.Monster(3), Cards.Weapon(4), Cards.Monster(5), p5                 // room 1
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(p5);
        Assert.That(engine.PotionUsedThisRoom, Is.True, "Potion should be marked as used");

        engine.Run();

        Assert.That(engine.PotionUsedThisRoom, Is.False, "Potion used flag should reset after run");
    }

    [Test]
    public void Run_PutsRoomCardsAtDeckBottom()
    {
        // 12-card deck: room3 (bottom), room2 (middle), room1 (top → dealt to room first)
        var room1 = new[] { Cards.Monster(3), Cards.Monster(4), Cards.Weapon(5), Cards.Potion(6) };
        var room2 = new[] { Cards.Potion(2),  Cards.Potion(2),  Cards.Potion(2),  Cards.Potion(2)  };
        var room3 = new[] { Cards.Monster(7), Cards.Monster(8), Cards.Monster(9), Cards.Monster(10) };
        var deck = room3.Concat(room2).Concat(room1).ToArray();
        var engine = new GameEngine(deck);

        engine.Run();

        // room2 is now the new room (previously the top of the remaining deck).
        // room1 cards returned to index 0 (bottom); room3 cards sit above them.
        Assert.That(engine.Deck.Take(ScoundrelRules.RoomSize),  Is.EquivalentTo(room1));
        Assert.That(engine.Deck.Skip(ScoundrelRules.RoomSize),  Is.EquivalentTo(room3));
    }
}
