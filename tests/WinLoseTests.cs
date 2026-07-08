using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Win / lose ────────────────────────────────────────────────────────────────

[TestFixture]
public class WinLoseTests
{
    [Test]
    public void TakeAllCards_Won()
    {
        // RoomSize-card deck → one room, take all RoomSize → win
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5)
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);

        Assert.That(engine.Won, Is.True, "Should win after taking all deck cards");
        Assert.That(engine.GameOver, Is.False, "GameOver should be false when winning");
    }

    [Test]
    public void TakeLastCard_InPartialFinalRoom_Wins()
    {
        // (RoomSize+1)-card deck: room 1 gets RoomSize, the extra card is alone in room 2.
        var finalCard = Cards.Potion(2);
        var deck = new[]
        {
            finalCard,
            Cards.Potion(3), Cards.Potion(4), Cards.Potion(5), Cards.Potion(6) // room 1
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]); // auto-deals room 2 (just finalCard)

        Assert.That(engine.Room.Count, Is.EqualTo(1), "Final room should have 1 card");
        Assert.That(engine.Room[0], Is.EqualTo(finalCard), "Final card should be the remaining card");

        engine.TakeCard(finalCard);

        Assert.That(engine.Won, Is.True);
    }

    [Test]
    public void HealthHitsZero_GameOver_NotWon()
    {
        var engine = Cards.RoomOf(
            Cards.Monster(9), Cards.Monster(9), Cards.Monster(9), Cards.Potion(2));

        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster)); // dead

        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.Won, Is.False);
    }
}
