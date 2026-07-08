using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Full game scenario ────────────────────────────────────────────────────────

[TestFixture]
public class FullScenarioTests
{
    /// <summary>
    /// A scripted 8-card mini-game:
    ///   Room 1: weapon(7), monster(10), potion(5), monster(6)
    ///   Room 2: monster(4), monster(3), potion(2), monster(2)
    /// Expected final state: alive, Won.
    /// </summary>
    [Test]
    public void EightCardGame_SurvivesToVictory()
    {
        var w7  = Cards.Weapon(7);
        var m10 = Cards.Monster(10);
        var p5  = Cards.Potion(5);
        var m6  = Cards.Monster(6);

        var m4  = Cards.Monster(4);
        var m3  = Cards.Monster(3);
        var p2  = Cards.Potion(2);
        var m2  = Cards.Monster(2);

        // Deck bottom→top: room 2 at bottom, room 1 at top
        var deck = new[] { m2, p2, m3, m4, m6, p5, m10, w7 };
        var engine = new GameEngine(deck);

        // Room 1: equip weapon, fight m10 (damage=3), drink potion (+5), fight m6 (damage=0 capped)
        engine.TakeCard(w7);             // equip weapon(7)
        engine.TakeCard(m10);            // damage = 10-7 = 3 → HP 17; floor → 10
        engine.TakeCard(p5);             // heal +5 → HP 20 (capped)
        engine.TakeCard(m6);             // 6 < floor(10), weapon: damage = 6-7 = 0 → HP 20; floor → 6

        // Room 2 was auto-dealt
        Assert.That(engine.Room.Count, Is.EqualTo(4));
        Assert.That(engine.Health, Is.EqualTo(20));

        // Room 2: fight all monsters and drink potion
        engine.TakeCard(m4);   // 4 < floor(6), weapon: damage = 4-7 = 0 → HP 20; floor → 4
        engine.TakeCard(p2);   // heal +2 → HP 20 (capped, already full)
        engine.TakeCard(m3);   // 3 < floor(4), weapon: damage = 3-7 = 0 → HP 20; floor → 3
        engine.TakeCard(m2);   // 2 < floor(3), weapon: damage = 2-7 = 0 → HP 20

        Assert.That(engine.Won,      Is.True);
        Assert.That(engine.GameOver, Is.False);
        Assert.That(engine.Health,   Is.EqualTo(20));
    }

    /// <summary>
    /// Run room 1, fight rooms 2 and 3 normally. Verifies run → can't run → can run again.
    /// </summary>
    [Test]
    public void RunThenFight_FlowIsCorrect()
    {
        // 12-card deck: 3 rooms of 4
        var deck = Enumerable.Range(0, 12).Select(i => Cards.Potion(2)).ToList();
        var engine = new GameEngine(deck);

        // Room 1: run
        engine.Run();
        Assert.That(engine.RanLastRoom, Is.True);

        // Room 2: take 3 cards, advance (can't run — RanLastRoom)
        Assert.That(engine.CanRun, Is.False);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.NextRoom(); // clears RanLastRoom

        // Room 3: can run again
        Assert.That(engine.CanRun, Is.True);
    }
}
