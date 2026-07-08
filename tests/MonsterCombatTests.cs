using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Monster combat ────────────────────────────────────────────────────────────

[TestFixture]
public class MonsterCombatTests
{
    [Test]
    public void FightUnarmed_TakesFullMonsterValue()
    {
        var monster = Cards.Monster(8);
        var engine  = Cards.RoomOf(monster, Cards.Potion(2), Cards.Weapon(5), Cards.Potion(3));

        engine.TakeCard(monster);

        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.StartHealth - 8));
    }

    [Test]
    public void FightingMonster_AddsMonsterToDiscard()
    {
        var monster = Cards.Monster(8);
        var engine  = Cards.RoomOf(monster, Cards.Potion(2), Cards.Weapon(5), Cards.Potion(3));

        engine.TakeCard(monster);

        Assert.That(engine.Discard, Contains.Item(monster));
    }

    [Test]
    public void FightAce_MonsterValueIs14()
    {
        var ace    = Cards.Monster(ScoundrelRules.AceRank);
        var engine = Cards.RoomOf(ace, Cards.Potion(2), Cards.Weapon(3), Cards.Potion(4));

        engine.TakeCard(ace);

        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.StartHealth - ScoundrelRules.AceMonsterValue));
    }

    [Test]
    public void FightWithWeapon_DamageIsReduced()
    {
        var weapon  = Cards.Weapon(7);
        var monster = Cards.Monster(10);
        var engine  = Cards.RoomOf(weapon, monster, Cards.Potion(2), Cards.Potion(3));

        engine.TakeCard(weapon);
        engine.TakeCard(monster);

        // Damage = 10 - 7 = 3
        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.StartHealth - 3));
    }

    [Test]
    public void FightWithWeapon_StrongerThanMonster_ZeroDamage()
    {
        var weapon  = Cards.Weapon(8);
        var monster = Cards.Monster(3);
        var engine  = Cards.RoomOf(weapon, monster, Cards.Potion(2), Cards.Potion(3));

        engine.TakeCard(weapon);
        int healthBefore = engine.Health;
        engine.TakeCard(monster);

        Assert.That(engine.Health, Is.EqualTo(healthBefore));
    }

    [Test]
    public void FightWithWeapon_SetsDegradedFloor()
    {
        var weapon  = Cards.Weapon(7);
        var monster = Cards.Monster(9);
        var engine  = Cards.RoomOf(weapon, monster, Cards.Potion(2), Cards.Potion(3));

        engine.TakeCard(weapon);
        engine.TakeCard(monster);

        Assert.That(engine.WeaponFloor, Is.EqualTo(9));
    }

    [Test]
    public void WeaponCannotBlock_MonsterAtFloor_TakesFullDamage()
    {
        var weapon   = Cards.Weapon(7);
        var monster9 = Cards.Monster(9);
        var monster9b = Cards.Monster(9);
        // Need 6 cards across two rooms: [w, m9, pad, pad] then [m9b, pad, pad, pad]
        var deck = new[]
        {
            Cards.Monster(2), Cards.Monster(2), Cards.Monster(2), Cards.Monster(2),  // room 2 filler
            Cards.Monster(2),                                                          // extra bottom padding
            monster9b,                                                                  // room 2 top
            Cards.Potion(2), Cards.Potion(3), monster9, weapon                        // room 1
        };
        var engine = new GameEngine(deck);

        // Room 1: equip weapon, fight 9 (floor → 9), take 2 more to fill MinCardsTaken
        engine.TakeCard(weapon);
        engine.TakeCard(monster9);
        engine.TakeCard(engine.Room[0]);
        engine.NextRoom();

        // Room 2: fight another 9 — floor is 9, can't use weapon (not strictly less)
        int healthBefore = engine.Health;
        engine.TakeCard(monster9b);

        Assert.That(engine.Health, Is.EqualTo(healthBefore - 9)); // full damage
    }

    [Test]
    public void WeaponCanBlock_MonsterBelowFloor_ReducesDamage()
    {
        var weapon  = Cards.Weapon(7);
        var m10     = Cards.Monster(10);
        var m8      = Cards.Monster(8);
        // Room 1: weapon + m10 + 2 filler; Room 2: m8 + 3 filler
        var deck = new[]
        {
            Cards.Monster(2), Cards.Monster(2), Cards.Monster(2), m8,
            Cards.Potion(2), Cards.Potion(3), m10, weapon
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(weapon);
        engine.TakeCard(m10); // floor → 10; damage = 10-7 = 3
        engine.TakeCard(engine.Room[0]);
        engine.NextRoom();

        int healthBefore = engine.Health;
        engine.TakeCard(m8); // 8 < floor(10), weapon blocks: damage = 8-7 = 1

        Assert.That(engine.Health, Is.EqualTo(healthBefore - 1));
        Assert.That(engine.WeaponFloor, Is.EqualTo(8));
    }

    [Test]
    public void UseWeaponFalse_TakesFullDamageEvenWithWeaponEquipped()
    {
        var weapon  = Cards.Weapon(7);
        var monster = Cards.Monster(10);
        var engine  = Cards.RoomOf(weapon, monster, Cards.Potion(2), Cards.Potion(3));

        engine.TakeCard(weapon);
        int healthBefore = engine.Health;
        engine.TakeCard(monster, useWeapon: false);

        Assert.That(engine.Health, Is.EqualTo(healthBefore - 10)); // 10, not 10-7=3
    }

    [Test]
    public void UseWeaponFalse_DoesNotUpdateWeaponFloor()
    {
        var weapon  = Cards.Weapon(7);
        var monster = Cards.Monster(5);
        var engine  = Cards.RoomOf(weapon, monster, Cards.Potion(2), Cards.Potion(3));

        engine.TakeCard(weapon);
        int floorBefore = engine.WeaponFloor;
        engine.TakeCard(monster, useWeapon: false);

        Assert.That(engine.WeaponFloor, Is.EqualTo(floorBefore)); // floor unchanged
    }

    [Test]
    public void NewWeapon_ResetsFloor()
    {
        var weapon1 = Cards.Weapon(5);
        var weapon2 = Cards.Weapon(9);
        var monster = Cards.Monster(10);
        var engine  = Cards.RoomOf(weapon1, weapon2, monster, Cards.Potion(2));

        engine.TakeCard(weapon1);
        engine.TakeCard(monster); // floor → 10
        engine.TakeCard(weapon2); // new weapon: floor resets to MaxValue

        Assert.That(engine.WeaponFloor, Is.EqualTo(int.MaxValue));
        Assert.That(engine.EquippedWeapon, Is.EqualTo(weapon2));
    }

    [Test]
    public void OldWeapon_MovedToDiscard_WhenReplaced()
    {
        var weapon1 = Cards.Weapon(5);
        var weapon2 = Cards.Weapon(9);
        var engine  = Cards.RoomOf(weapon1, weapon2, Cards.Potion(2), Cards.Potion(3));

        engine.TakeCard(weapon1);
        engine.TakeCard(weapon2);

        Assert.That(engine.Discard, Contains.Item(weapon1));
    }

    [Test]
    public void FightingKillsPlayer_GameOverSet()
    {
        // Stack enough monsters to kill a 20 HP player
        var m9a = Cards.Monster(9);
        var m9b = Cards.Monster(9);
        var m9c = Cards.Monster(9);
        var engine = Cards.RoomOf(m9a, m9b, m9c, Cards.Monster(3));

        engine.TakeCard(m9a); // -9 → 11 HP
        engine.TakeCard(m9b); // -9 → 2 HP
        engine.TakeCard(m9c); // -9 → 0 HP → game over

        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.Health, Is.EqualTo(0));
    }

    [Test]
    public void DeadEngine_ThrowsOnFurtherAction()
    {
        var m10 = Cards.Monster(10);
        var m10b = Cards.Monster(10);
        var engine = Cards.RoomOf(m10, m10b, Cards.Monster(5), Cards.Monster(5));

        engine.TakeCard(m10);
        engine.TakeCard(m10b); // dead (0 HP)

        Assert.Throws<InvalidOperationException>(() => engine.TakeCard(engine.Room[0]));
    }
}
