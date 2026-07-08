using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Merchant — Heart Face Cards (PRD §6, item 4) ────────────────────────────────

[TestFixture]
public class MerchantTests
{
    [Test]
    public void CanUseMerchant_FalseForCardNotInRoom()
    {
        var engine = Cards.RoomOf(Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Weapon(5));
        var phantom = Cards.Merchant(11); // never dealt into any room

        Assert.That(engine.CanUseMerchant(phantom), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.UseMerchant(phantom));
    }

    [Test]
    public void CanUseMerchant_TrueEvenWithoutEquippedWeapon()
    {
        var jack = Cards.Merchant(11);
        var engine = Cards.RoomOf(jack, Cards.Potion(2), Cards.Potion(3), Cards.Potion(4));

        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.CanUseMerchant(jack), Is.True,
            "'Cannot be used' (no weapon) is handled by recycling inside UseMerchant, not by blocking the call");
    }

    // ── HP formula for Jack/Queen/King ───────────────────────────────────────────

    // Weapon(5) leaks partial damage against monsters 9 and 8 (CalcDamage 4 then 3), so
    // health drops to 13 before the sale — well below MaxHealth (20) plus the largest
    // possible gain (King's 6), so Heal never saturates and each rank's distinct expected
    // HP is actually observable (a saturated result couldn't distinguish Jack/Queen/King).
    [TestCase(11, 3)] // Jack:  max(1, 5 - 2) + 0 = 3
    [TestCase(12, 4)] // Queen: max(1, 5 - 2) + 1 = 4
    [TestCase(13, 6)] // King:  max(1, 5 - 2) + 3 = 6
    public void UseMerchant_FaceCard_HealsMaxOfOneOrWeaponMinusSlainCount_PlusBonus(int rank, int expectedGain)
    {
        var weapon   = Cards.Weapon(5);
        var monster9 = Cards.Monster(9);
        var monster8 = Cards.Spade(8);
        var merchant = Cards.Merchant(rank);
        var engine   = Cards.RoomOf(weapon, monster9, monster8, merchant);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // blocked -> slain 1, floor 9, damage 4 -> health 16
        engine.TakeCard(monster8); // blocked -> slain 2, floor 8, damage 3 -> health 13
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(2));

        int healthBefore = engine.Health;
        Assert.That(healthBefore, Is.EqualTo(13), "sanity check on the damage arithmetic");

        engine.UseMerchant(merchant);

        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.Heal(healthBefore, expectedGain)));
        Assert.That(engine.Discard, Contains.Item(merchant));
        Assert.That(engine.EquippedWeapon, Is.Null);
    }

    [Test]
    public void UseMerchant_HeavilyUsedWeapon_FloorsHpGainAtOneBeforeBonus()
    {
        var weapon   = Cards.Weapon(2);
        var monster9 = Cards.Monster(9);
        var monster8 = Cards.Spade(8);
        var monster7 = Cards.Monster(7);
        var king     = Cards.Merchant(13); // King, +3
        var filler1  = Cards.Potion(2);
        var filler2  = Cards.Potion(3);

        // Deck bottom -> top. Room 1 = {weapon, monster9, monster8, monster7}; room 2 refill
        // (dealt once room 1 empties) = {filler0, king, filler1, filler2}.
        var filler0 = Cards.Potion(4);
        var deck = new[] { filler0, king, filler1, filler2, weapon, monster9, monster8, monster7 };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // blocked -> slain 1, floor 9
        engine.TakeCard(monster8); // blocked -> slain 2, floor 8
        engine.TakeCard(monster7); // empties room 1 -> deals room 2; blocked -> slain 3, floor 7
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(3), "SlainMonsterCount (3) now exceeds WeaponValue (2)");

        int healthBefore = engine.Health;
        engine.UseMerchant(king); // max(1, 2 - 3) + 3 = 1 + 3 = 4, floored at 1 before the bonus

        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.Heal(healthBefore, 4)));
    }

    [Test]
    public void UseMerchant_ZeroAttachedMonsters_YieldsUnreducedFormulaValue()
    {
        var weapon   = Cards.Weapon(7);
        var monster9 = Cards.Monster(9);
        var jack     = Cards.Merchant(11);
        var filler   = Cards.Potion(2);
        var engine   = Cards.RoomOf(monster9, weapon, jack, filler);

        // Take an unblocked hit first (no weapon yet) to bring health below MaxHealth by
        // more than the sale's gain, so the assertion below can't saturate at the cap.
        engine.TakeCard(monster9, useWeapon: false); // 20 -> 11
        engine.TakeCard(weapon);                     // equip afterwards; SlainMonsterCount starts at 0
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));

        int healthBefore = engine.Health;
        Assert.That(healthBefore, Is.EqualTo(11), "sanity check on the damage arithmetic");

        engine.UseMerchant(jack); // Jack: bonus 0, formula = max(1, 7 - 0) + 0 = 7

        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.Heal(healthBefore, 7)));
    }

    // ── Ace of Hearts special case ───────────────────────────────────────────────

    [Test]
    public void UseMerchant_AceOfHearts_FullWeaponValuePlusFive_IgnoresSlainMonsterCount()
    {
        // Weapon(3) leaks partial damage against monsters 9 and 8 (CalcDamage 6 then 5),
        // dropping health to 9 before the sale — low enough that the Ace's gain (8) can't
        // saturate at MaxHealth, and different enough from the J/Q/K reading (which would
        // give max(1, 3 - 2) + 0 = 1) that the two formulas are clearly distinguishable.
        var weapon   = Cards.Weapon(3);
        var monster9 = Cards.Monster(9);
        var monster8 = Cards.Spade(8);
        var ace      = Cards.Merchant(ScoundrelRules.AceRank);
        var engine   = Cards.RoomOf(weapon, monster9, monster8, ace);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // blocked -> slain 1, floor 9, damage 6 -> health 14
        engine.TakeCard(monster8); // blocked -> slain 2, floor 8, damage 5 -> health 9
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(2));

        int healthBefore = engine.Health;
        Assert.That(healthBefore, Is.EqualTo(9), "sanity check on the damage arithmetic");

        engine.UseMerchant(ace); // Ace ignores SlainMonsterCount entirely: 3 + 5 = 8

        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.Heal(healthBefore, 8)));
    }

    // ── Healing cap ───────────────────────────────────────────────────────────────

    [Test]
    public void UseMerchant_HealCappedAtMaxHealth()
    {
        var weapon   = Cards.Weapon(10);
        var monster5 = Cards.Monster(5);
        var ace      = Cards.Merchant(ScoundrelRules.AceRank);
        var filler   = Cards.Potion(2);
        var engine   = Cards.RoomOf(weapon, monster5, ace, filler);

        engine.TakeCard(monster5, useWeapon: false); // 20 -> 15; no weapon equipped yet
        engine.TakeCard(weapon);                     // equip afterwards; SlainMonsterCount stays 0
        Assert.That(engine.Health, Is.EqualTo(15));
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));

        engine.UseMerchant(ace); // 10 + 5 = 15 HP gained; 15 + 15 = 30, capped at MaxHealth

        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.MaxHealth));
    }

    // ── Selling clears the weapon system ─────────────────────────────────────────

    [Test]
    public void UseMerchant_ClearsWeaponSystemToNoWeaponDefaults_AndDiscardsOldWeapon()
    {
        var weapon   = Cards.Weapon(5);
        var jack     = Cards.Blacksmith(11);
        var monster9 = Cards.Monster(9);
        var queen    = Cards.Merchant(12);
        var engine   = Cards.RoomOf(weapon, jack, monster9, queen);

        engine.TakeCard(weapon);
        engine.UseBlacksmith(jack); // slain 0 -> WeaponAttackBonus = 1
        engine.TakeCard(monster9);  // blocked -> slain 1, floor 9
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(1));
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));
        Assert.That(engine.WeaponFloor, Is.EqualTo(9));

        engine.UseMerchant(queen);

        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.Discard, Contains.Item(weapon));
        Assert.That(engine.Discard, Contains.Item(queen));
        Assert.That(engine.WeaponFloor, Is.EqualTo(int.MaxValue));
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(0));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(0));
    }

    // ── Recycle instead of sell/discard ──────────────────────────────────────────

    [TestCase(true)]
    [TestCase(false)]
    public void UseMerchant_NoWeaponEquipped_RecyclesToDeck_RegardlessOfActivateFlag(bool activate)
    {
        var jack    = Cards.Merchant(11);
        var filler1 = Cards.Potion(2);
        var filler2 = Cards.Potion(3);
        var filler3 = Cards.Potion(4);
        var engine  = Cards.RoomOf(jack, filler1, filler2, filler3);

        Assert.That(engine.EquippedWeapon, Is.Null);

        engine.UseMerchant(jack, activate);

        Assert.That(engine.Deck, Contains.Item(jack));
        Assert.That(engine.Discard, Does.Not.Contain(jack));
        Assert.That(engine.Room, Does.Not.Contain(jack));
    }

    [Test]
    public void UseMerchant_ActivateFalse_WithWeaponEquipped_Recycles_DoesNotSell()
    {
        var weapon  = Cards.Weapon(5);
        var jack    = Cards.Merchant(11);
        var filler1 = Cards.Potion(2);
        var filler2 = Cards.Potion(3);
        var engine  = Cards.RoomOf(weapon, jack, filler1, filler2);

        engine.TakeCard(weapon);
        int healthBefore = engine.Health;

        engine.UseMerchant(jack, activate: false);

        Assert.That(engine.Deck, Contains.Item(jack));
        Assert.That(engine.Discard, Does.Not.Contain(jack));
        Assert.That(engine.EquippedWeapon, Is.EqualTo(weapon), "Declined card must not sell the weapon");
        Assert.That(engine.Health, Is.EqualTo(healthBefore));
    }

    // ── Independence from the Black Joker's pocket weapon ───────────────────────

    [Test]
    public void UseMerchant_DoesNotAffect_BlackJokerPocketWeapon()
    {
        var blackJoker   = Cards.BlackJoker();
        var pocketWeapon = Cards.Weapon(6);
        var mainWeapon   = Cards.Weapon(5);
        var merchant     = Cards.Merchant(12);
        var engine       = Cards.RoomOf(blackJoker, pocketWeapon, mainWeapon, merchant);

        engine.TakeCard(blackJoker);      // HasWeaponJoker = true
        engine.StoreWeapon(pocketWeapon); // pocket weapon set
        engine.TakeCard(mainWeapon);      // equip main weapon

        Assert.That(engine.PocketedWeapon, Is.EqualTo(pocketWeapon));

        engine.UseMerchant(merchant); // sells the main weapon

        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.PocketedWeapon, Is.EqualTo(pocketWeapon), "Merchant must not touch the pocket weapon");
    }

    // ── TakeCard integration (routes to the same ApplyMerchantEffect as UseMerchant) ────

    [Test]
    public void TakingMerchantCard_ThroughTakeCard_WithWeaponEquipped_SellsWeapon()
    {
        var weapon   = Cards.Weapon(10);
        var merchant = Cards.Merchant(13); // King, +3
        var filler1  = Cards.Potion(2);
        var filler2  = Cards.Potion(3);
        var engine   = Cards.RoomOf(weapon, merchant, filler1, filler2);

        engine.TakeCard(weapon);
        int healthBefore = engine.Health;

        engine.TakeCard(merchant); // routed through TakeCard's IsMerchant branch, not UseMerchant directly

        int expectedGain = Math.Max(1, weapon.WeaponValue - 0) + 3;
        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.Heal(healthBefore, expectedGain)));
        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.Discard, Contains.Item(merchant));
        Assert.That(engine.Discard, Contains.Item(weapon));
    }

    [Test]
    public void TakingMerchantCard_ThroughTakeCard_ActivateFalse_RecyclesEvenWithWeaponEquipped()
    {
        var weapon   = Cards.Weapon(10);
        var merchant = Cards.Merchant(11);
        var filler1  = Cards.Potion(2);
        var filler2  = Cards.Potion(3);
        var engine   = Cards.RoomOf(weapon, merchant, filler1, filler2);

        engine.TakeCard(weapon);
        engine.TakeCard(merchant, activateCard: false); // declined -> recycle, not TakeCard's plain discard

        Assert.That(engine.Deck, Contains.Item(merchant));
        Assert.That(engine.Discard, Does.Not.Contain(merchant));
        Assert.That(engine.EquippedWeapon, Is.EqualTo(weapon));
    }

    // ── Game-over / won guards ───────────────────────────────────────────────────

    [Test]
    public void UseMerchant_AfterWon_Throws()
    {
        var filler1 = Cards.Potion(2);
        var filler2 = Cards.Potion(3);
        var filler3 = Cards.Potion(4);
        var filler4 = Cards.Potion(5);
        // 4-card deck: taking all 4 room cards empties both room and deck -> Won.
        var deck = new[] { filler1, filler2, filler3, filler4 };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        var merchant = Cards.Merchant(11); // never dealt into any room
        Assert.That(engine.CanUseMerchant(merchant), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.UseMerchant(merchant));
    }

    [Test]
    public void UseMerchant_AfterGameOver_Throws_CardStaysInRoom()
    {
        var merchant = Cards.Merchant(11);
        var m9a      = Cards.Monster(9);
        var m9b      = Cards.Monster(9);
        var m9c      = Cards.Monster(9);
        var engine   = Cards.RoomOf(merchant, m9a, m9b, m9c);

        engine.TakeCard(m9a); // 20 -> 11
        engine.TakeCard(m9b); // 11 -> 2
        engine.TakeCard(m9c); // 2 -> 0 -> GameOver; merchant was never taken

        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.Room, Contains.Item(merchant), "Merchant remains in the room, untouched");
        Assert.That(engine.CanUseMerchant(merchant), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.UseMerchant(merchant));
    }
}
