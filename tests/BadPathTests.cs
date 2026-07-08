using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Bad-path / guard tests ────────────────────────────────────────────────────

[TestFixture]
public class BadPathTests
{
    [Test]
    public void TakeCard_NotInRoom_Throws()
    {
        var engine  = Cards.RoomOf(Cards.Potion(2), Cards.Potion(3), Cards.Weapon(4), Cards.Monster(5));
        var phantom = Cards.Monster(7); // was never in any room
        Assert.Throws<ArgumentException>(() => engine.TakeCard(phantom));
    }

    [Test]
    public void TakeCard_AfterWon_Throws()
    {
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5) };
        var engine = new GameEngine(deck);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        Assert.Throws<InvalidOperationException>(() => engine.TakeCard(Cards.Potion(2)));
    }

    [Test]
    public void NextRoom_AfterGameOver_Throws()
    {
        var engine = Cards.RoomOf(Cards.Monster(9), Cards.Monster(9), Cards.Monster(9), Cards.Potion(2));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        Assert.That(engine.GameOver, Is.True);

        Assert.Throws<InvalidOperationException>(() => engine.NextRoom());
    }

    [Test]
    public void NextRoom_AfterWon_Throws()
    {
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5) };
        var engine = new GameEngine(deck);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        Assert.Throws<InvalidOperationException>(() => engine.NextRoom());
    }

    [Test]
    public void Run_AfterGameOver_Throws()
    {
        var engine = Cards.RoomOf(Cards.Monster(9), Cards.Monster(9), Cards.Monster(9), Cards.Potion(2));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        Assert.That(engine.GameOver, Is.True);

        Assert.Throws<InvalidOperationException>(() => engine.Run());
    }

    [Test]
    public void Run_AfterWon_Throws()
    {
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5) };
        var engine = new GameEngine(deck);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        Assert.Throws<InvalidOperationException>(() => engine.Run());
    }
}
