using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Discard without activating ────────────────────────────────────────────────

[TestFixture]
public class DiscardTests
{
    [Test]
    public void DiscardWeapon_NoActivate_GoesToDiscardNotEquipped()
    {
        var weapon = Cards.Weapon(6);
        var engine = Cards.RoomOf(weapon, Cards.Monster(4), Cards.Potion(5), Cards.Monster(8));

        engine.TakeCard(weapon, activateCard: false);

        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.Discard, Contains.Item(weapon));
    }

    [Test]
    public void DiscardPotion_NoActivate_DoesNotHeal()
    {
        var monster = Cards.Monster(8);
        var potion  = Cards.Potion(5);
        var engine  = Cards.RoomOf(monster, potion, Cards.Weapon(3), Cards.Potion(2));

        engine.TakeCard(monster);
        int hpAfterDamage = engine.Health;
        engine.TakeCard(potion, activateCard: false);

        Assert.That(engine.Health, Is.EqualTo(hpAfterDamage));
        Assert.That(engine.PotionUsedThisRoom, Is.False);
        Assert.That(engine.Discard, Contains.Item(potion));
    }

    [Test]
    public void DiscardPotion_NoActivate_CountsTowardMinCardsTaken()
    {
        var p1 = Cards.Potion(2);
        var p2 = Cards.Potion(3);
        var p3 = Cards.Potion(4);
        var engine = Cards.RoomOf(p1, p2, p3, Cards.Weapon(5));

        engine.TakeCard(p1, activateCard: false);
        engine.TakeCard(p2, activateCard: false);
        engine.TakeCard(p3, activateCard: false);

        Assert.That(engine.CanNextRoom, Is.True);
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(3));
    }
}
