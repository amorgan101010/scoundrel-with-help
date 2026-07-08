using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── SlainMonsterCount (Blacksmith infrastructure) ──────────────────────────────

[TestFixture]
public class SlainMonsterCountTests
{
    [Test]
    public void NoWeaponEquipped_StaysZero()
    {
        var engine = Cards.RoomOf(Cards.Monster(5), Cards.Potion(2), Cards.Potion(3), Cards.Potion(4));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
    }

    [Test]
    public void FightingBareHanded_DoesNotIncrementCount()
    {
        var weapon  = Cards.Weapon(7);
        var monster = Cards.Monster(5);
        var engine  = Cards.RoomOf(weapon, monster, Cards.Potion(2), Cards.Potion(3));

        engine.TakeCard(weapon);
        engine.TakeCard(monster, useWeapon: false);

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
    }

    [Test]
    public void WeaponBlocksMonster_IncrementsCount()
    {
        var weapon  = Cards.Weapon(7);
        var monster = Cards.Monster(5);
        var engine  = Cards.RoomOf(weapon, monster, Cards.Potion(2), Cards.Potion(3));

        engine.TakeCard(weapon);
        engine.TakeCard(monster);

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));
    }

    [Test]
    public void WeaponCannotBlock_MonsterAtFloor_DoesNotIncrementCount()
    {
        var weapon    = Cards.Weapon(7);
        var monster9  = Cards.Monster(9);
        var monster9b = Cards.Monster(9);
        var deck = new[]
        {
            Cards.Monster(2), Cards.Monster(2), Cards.Monster(2), Cards.Monster(2),
            Cards.Monster(2),
            monster9b,
            Cards.Potion(2), Cards.Potion(3), monster9, weapon
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // blocked → count 1, floor → 9
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));

        engine.TakeCard(engine.Room[0]);
        engine.NextRoom();

        engine.TakeCard(monster9b); // 9 not < floor(9) → bare-handed, no increment
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));
    }

    [Test]
    public void EquippingNewWeapon_ResetsCountToZero()
    {
        var weapon1 = Cards.Weapon(7);
        var monster = Cards.Monster(5);
        var weapon2 = Cards.Weapon(3);
        var engine  = Cards.RoomOf(weapon1, monster, weapon2, Cards.Potion(2));

        engine.TakeCard(weapon1);
        engine.TakeCard(monster);
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));

        engine.TakeCard(weapon2);
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
    }

    [Test]
    public void EquippingFirstWeapon_CountStartsAtZero()
    {
        var weapon = Cards.Weapon(5);
        var engine = Cards.RoomOf(weapon, Cards.Potion(2), Cards.Potion(3), Cards.Potion(4));

        engine.TakeCard(weapon);

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
    }
}
