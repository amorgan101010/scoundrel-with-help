using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Room advancement ──────────────────────────────────────────────────────────

[TestFixture]
public class RoomProgressTests
{
    [Test]
    public void TakingAllFourCards_AutoAdvances()
    {
        var cards = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Weapon(4), Cards.Monster(5) };
        // 8 cards total: first room = last 4, second room = first 4
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), Cards.Potion(2),
            cards[0], cards[1], cards[2], cards[3]
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(cards[3]);
        engine.TakeCard(cards[2]);
        engine.TakeCard(cards[1]);
        engine.TakeCard(cards[0]); // 4th card — should auto-deal next room

        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize));
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(0)); // reset
    }

    [Test]
    public void TakingThreeCards_EnablesNextRoom()
    {
        var c1 = Cards.Potion(2);
        var c2 = Cards.Potion(3);
        var c3 = Cards.Weapon(4);
        var c4 = Cards.Monster(5);
        var engine = Cards.RoomOf(c1, c2, c3, c4);

        engine.TakeCard(c4);
        engine.TakeCard(c3);
        engine.TakeCard(c2);

        Assert.That(engine.CanNextRoom, Is.True);
        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize - ScoundrelRules.MinCardsTaken));
    }

    [Test]
    public void NextRoom_CarriesOverRemainingCard()
    {
        var leftover = Cards.Monster(5);
        var c1 = Cards.Potion(2);
        var c2 = Cards.Potion(3);
        var c3 = Cards.Weapon(4);
        // Deck: pad(4) for room 2 filler at bottom, then room 1
        var deck = new[]
        {
            Cards.Monster(2), Cards.Monster(2), Cards.Monster(2), Cards.Monster(2),
            c1, c2, c3, leftover
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(c1);
        engine.TakeCard(c2);
        engine.TakeCard(c3);
        engine.NextRoom();

        Assert.That(engine.Room, Contains.Item(leftover));
        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize)); // leftover + 3 new
    }

    [Test]
    public void NextRoom_ResetsCardsTakenThisRoom()
    {
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), // room 2
            Cards.Potion(3), Cards.Potion(4), Cards.Potion(5), Cards.Potion(6)  // room 1
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(ScoundrelRules.MinCardsTaken));

        engine.NextRoom();

        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(0));
    }

    [Test]
    public void NextRoom_BeforeMinCardsTaken_Throws()
    {
        var engine = Cards.RoomOf(
            Cards.Potion(2), Cards.Potion(3), Cards.Weapon(4), Cards.Monster(5));

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);

        Assert.Throws<InvalidOperationException>(() => engine.NextRoom(), $"Must take at least {ScoundrelRules.MinCardsTaken} cards");
    }
}
