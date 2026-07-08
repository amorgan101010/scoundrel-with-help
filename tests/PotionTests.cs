using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Potions ───────────────────────────────────────────────────────────────────

[TestFixture]
public class PotionTests
{
    [Test]
    public void Potion_HealsCorrectly()
    {
        var monster = Cards.Monster(8);
        var potion  = Cards.Potion(5);
        var engine  = Cards.RoomOf(monster, potion, Cards.Weapon(3), Cards.Potion(2));

        engine.TakeCard(monster); // -8 → 12 HP
        engine.TakeCard(potion);  // +5 → 17 HP

        Assert.That(engine.Health, Is.EqualTo(17));
    }

    [Test]
    public void Potion_CapsAtMaxHealth()
    {
        var potion = Cards.Potion(9);
        var engine = Cards.RoomOf(potion, Cards.Weapon(3), Cards.Potion(2), Cards.Potion(4));

        engine.TakeCard(potion); // already at 20, stays at 20

        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.MaxHealth));
    }

    [Test]
    public void SecondPotion_InSameRoom_IsWasted()
    {
        var p5 = Cards.Potion(5);
        var p6 = Cards.Potion(6);
        var engine = Cards.RoomOf(Cards.Monster(8), p5, p6, Cards.Weapon(3));

        engine.TakeCard(Cards.Monster(8)); // lose some HP
        int healthAfterFirst = engine.Health;
        engine.TakeCard(p5);               // first potion heals
        engine.TakeCard(p6);               // second potion wasted

        Assert.That(engine.PotionWastedThisRoom, Is.True);
        Assert.That(engine.Health, Is.EqualTo(Math.Min(ScoundrelRules.MaxHealth, healthAfterFirst + 5)));
    }

    [Test]
    public void PotionTracker_ResetsEachRoom()
    {
        // Room 1: use a potion. Room 2: use another potion — should not be wasted.
        var p5  = Cards.Potion(5);
        var m8  = Cards.Monster(8);
        var p4  = Cards.Potion(4);

        var deck = new[]
        {
            Cards.Monster(2), Cards.Monster(2), p4,   Cards.Monster(2),  // room 2
            Cards.Weapon(3),  Cards.Potion(3),  m8,   p5                  // room 1
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(p5);
        engine.TakeCard(m8);
        engine.TakeCard(engine.Room[0]);
        engine.NextRoom();

        int healthBeforeRoom2 = engine.Health;
        engine.TakeCard(p4); // should heal, not waste

        Assert.That(engine.PotionWastedThisRoom, Is.False);
        Assert.That(engine.Health, Is.EqualTo(Math.Min(ScoundrelRules.MaxHealth, healthBeforeRoom2 + 4)));
    }
}
