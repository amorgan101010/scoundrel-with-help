using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Give equipped weapon to Weapon Joker (PRD §6 follow-up, chunk 13) ──────────────────
// The reverse direction of RetrieveWeaponTests above: instead of pulling a pocketed
// weapon out to equip, the player can push the already-equipped weapon into the pocket.

[TestFixture]
public class GiveEquippedWeaponToJokerTests
{
    [Test]
    public void CanGiveEquippedWeaponToJoker_FalseWhenNoWeaponJoker()
    {
        var weapon = Cards.Weapon(5);
        var engine = Cards.RoomOf(weapon, Cards.Potion(2), Cards.Potion(3), Cards.Potion(4));
        engine.TakeCard(weapon); // equips, but the Weapon Joker was never taken

        Assert.That(engine.EquippedWeapon, Is.EqualTo(weapon));
        Assert.That(engine.CanGiveEquippedWeaponToJoker, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.GiveEquippedWeaponToJoker());
    }

    [Test]
    public void CanGiveEquippedWeaponToJoker_FalseIfPocketAlreadyOccupied()
    {
        var joker   = Cards.BlackJoker();
        var equipped = Cards.Weapon(5);
        var pocketed = Cards.Weapon(6);
        var engine = Cards.RoomOf(joker, equipped, pocketed, Cards.Potion(2));

        engine.TakeCard(joker);
        engine.TakeCard(equipped);   // equips
        engine.StoreWeapon(pocketed); // pocket already occupied

        Assert.That(engine.CanGiveEquippedWeaponToJoker, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.GiveEquippedWeaponToJoker());
        Assert.That(engine.EquippedWeapon, Is.EqualTo(equipped), "Blocked call must not disturb the equipped weapon");
        Assert.That(engine.PocketedWeapon, Is.EqualTo(pocketed), "Blocked call must not disturb the pocket");
    }

    [Test]
    public void CanGiveEquippedWeaponToJoker_FalseWhenNoWeaponEquipped()
    {
        var joker  = Cards.BlackJoker();
        var engine = Cards.RoomOf(joker, Cards.Potion(2), Cards.Potion(3), Cards.Potion(4));
        engine.TakeCard(joker); // joker taken, but nothing equipped

        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.CanGiveEquippedWeaponToJoker, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.GiveEquippedWeaponToJoker());
    }

    [Test]
    public void CanGiveEquippedWeaponToJoker_FalseWhenGameOver()
    {
        var joker  = Cards.BlackJoker();
        var weapon = Cards.Weapon(5);
        var m9a = Cards.Monster(9);
        var m9b = Cards.Monster(9);
        var m9c = Cards.Monster(9);
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), m9c,
            m9b, m9a, weapon, joker
        };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.TakeCard(weapon);
        engine.TakeCard(m9a); // 20 -> 11
        engine.TakeCard(m9b); // 11 -> 2; room empties -> deals room 2
        engine.TakeCard(m9c); // 2 -> 0 -> GameOver

        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.CanGiveEquippedWeaponToJoker, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.GiveEquippedWeaponToJoker());
    }

    [Test]
    public void GiveEquippedWeaponToJoker_HappyPath_MovesWeaponToPocket_ResetsSlainMonsterCountAndFloor()
    {
        var joker   = Cards.BlackJoker();
        var weapon  = Cards.Weapon(7);
        var monster = Cards.Monster(3); // weak enough that the fresh weapon blocks it fully
        var engine  = Cards.RoomOf(joker, weapon, monster, Cards.Potion(2));

        engine.TakeCard(joker);
        engine.TakeCard(weapon);  // equips
        engine.TakeCard(monster); // weapon blocks: SlainMonsterCount -> 1, WeaponFloor set
        int takenBefore = engine.CardsTakenThisRoom;

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));
        Assert.That(engine.WeaponFloor, Is.Not.EqualTo(int.MaxValue));
        Assert.That(engine.CanGiveEquippedWeaponToJoker, Is.True);

        engine.GiveEquippedWeaponToJoker();

        Assert.That(engine.PocketedWeapon, Is.EqualTo(weapon));
        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.WeaponFloor, Is.EqualTo(int.MaxValue));
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(0));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(0));
        Assert.That(engine.Discard, Does.Not.Contain(weapon), "The weapon moves to the pocket, not the discard");
        Assert.That(engine.Room, Does.Not.Contain(weapon));
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(takenBefore), "Side action — not a room pick");
    }

    [Test]
    public void GiveEquippedWeaponToJoker_HappyPath_ResetsAttackBonuses()
    {
        var joker  = Cards.BlackJoker();
        var weapon = Cards.Weapon(6);
        var jack   = Cards.Blacksmith(11);
        var ace    = Cards.Blacksmith(ScoundrelRules.AceRank);
        // Bottom -> top. Room 1 = {joker, weapon, jack, ace}; room 2 refill (dealt once
        // room 1 empties) keeps the game alive with 4 spare potions so GiveEquippedWeaponToJoker
        // below isn't blocked by IsOver.
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5),
            ace, jack, weapon, joker
        };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.TakeCard(weapon);        // equips
        engine.UseBlacksmith(jack);     // slain 0 -> WeaponAttackBonus = 1
        engine.UseBlacksmith(ace);      // slain still 0 -> SingleUseWeaponBonus = 4; room empties -> deals room 2
        int takenBefore = engine.CardsTakenThisRoom;

        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(1));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(4));
        Assert.That(engine.CanGiveEquippedWeaponToJoker, Is.True);

        engine.GiveEquippedWeaponToJoker();

        Assert.That(engine.PocketedWeapon, Is.EqualTo(weapon));
        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.WeaponFloor, Is.EqualTo(int.MaxValue));
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(0));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(0));
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(takenBefore), "Side action — not a room pick");
    }
}
