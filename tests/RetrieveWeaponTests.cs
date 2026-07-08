using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Weapon Pocket retrieval (PRD §6 follow-up, chunk 6) ────────────────────────
// The pocketed weapon no longer has any combat role of its own (CanFightWithPocketWeapon/
// FightWithPocketWeapon/PocketWeaponFloor removed) — RetrieveWeapon is now what gives the
// pocket its purpose: pulling the stashed weapon out to equip as the player's actual weapon.

[TestFixture]
public class RetrieveWeaponTests
{
    [Test]
    public void CanRetrieveWeapon_FalseWhenJokerNeverTaken()
    {
        var engine = Cards.RoomOf(Cards.Weapon(5), Cards.Potion(2), Cards.Potion(3), Cards.Potion(4));

        Assert.That(engine.CanRetrieveWeapon, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.RetrieveWeapon());
    }

    [Test]
    public void CanRetrieveWeapon_FalseWhenPocketEmpty()
    {
        var joker = Cards.BlackJoker();
        var engine = Cards.RoomOf(joker, Cards.Weapon(5), Cards.Potion(2), Cards.Potion(3));
        engine.TakeCard(joker);

        Assert.That(engine.CanRetrieveWeapon, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.RetrieveWeapon());
    }

    [Test]
    public void RetrieveWeapon_AvailableAnytimeRegardlessOfRoomState()
    {
        // Only 2 cards taken this room (joker + stored weapon) — below MinCardsTaken, so
        // NextRoom would be blocked. Retrieval is a side action, not a room pick, so it's
        // available regardless.
        var joker  = Cards.BlackJoker();
        var weapon = Cards.Weapon(5);
        var engine = Cards.RoomOf(joker, weapon, Cards.Potion(2), Cards.Potion(3));
        engine.TakeCard(joker);
        engine.StoreWeapon(weapon);

        Assert.That(engine.CanNextRoom, Is.False);
        Assert.That(engine.CanRetrieveWeapon, Is.True);
        Assert.DoesNotThrow(() => engine.RetrieveWeapon());
    }

    [Test]
    public void RetrieveWeapon_NoPreviousWeapon_Equips_ResetsWeaponState_EmptiesPocket()
    {
        var joker  = Cards.BlackJoker();
        var weapon = Cards.Weapon(6);
        var engine = Cards.RoomOf(joker, weapon, Cards.Potion(2), Cards.Potion(3));
        engine.TakeCard(joker);
        engine.StoreWeapon(weapon);
        int takenBefore = engine.CardsTakenThisRoom;

        engine.RetrieveWeapon();

        Assert.That(engine.EquippedWeapon, Is.EqualTo(weapon));
        Assert.That(engine.WeaponFloor, Is.EqualTo(int.MaxValue));
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(0));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(0));
        Assert.That(engine.PocketedWeapon, Is.Null);
        Assert.That(engine.Discard, Does.Not.Contain(weapon));
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(takenBefore), "Retrieval is not a room pick");
    }

    [Test]
    public void RetrieveWeapon_WithPreviousWeaponEquipped_DiscardsOldWeapon_ResetsWornState()
    {
        var joker      = Cards.BlackJoker();
        var oldWeapon  = Cards.Weapon(4);
        var newWeapon  = Cards.Weapon(9);
        var monster    = Cards.Monster(3);
        var filler1 = Cards.Potion(2);
        var filler2 = Cards.Potion(3);
        var filler3 = Cards.Potion(4);
        var filler4 = Cards.Potion(5);
        // Deck bottom -> top. Room 1 = {joker, oldWeapon, newWeapon, monster}; room 2 refill
        // (dealt once room 1 empties) keeps the game alive rather than ending it in a Win,
        // so RetrieveWeapon below isn't blocked by IsOver.
        var deck = new[] { filler4, filler3, filler2, filler1, monster, newWeapon, oldWeapon, joker };
        var engine = new GameEngine(deck, extendedRules: true);
        engine.TakeCard(joker);
        engine.TakeCard(oldWeapon);       // equips oldWeapon
        engine.StoreWeapon(newWeapon);
        engine.TakeCard(monster);         // wears the old weapon: SlainMonsterCount 1, floor 3; room empties -> deals room 2

        Assert.That(engine.EquippedWeapon, Is.EqualTo(oldWeapon));
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));

        engine.RetrieveWeapon();

        Assert.That(engine.EquippedWeapon, Is.EqualTo(newWeapon));
        Assert.That(engine.Discard, Contains.Item(oldWeapon), "Previously-equipped weapon goes to discard");
        Assert.That(engine.WeaponFloor, Is.EqualTo(int.MaxValue));
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(0));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(0));
        Assert.That(engine.PocketedWeapon, Is.Null);
    }

    // ── RetrieveWeapon(activate) — chunk 10 store/retrieve UI wiring ───────────────

    [Test]
    public void RetrieveWeapon_ActivateFalse_NoPreviousWeapon_DiscardsWithoutEquipping()
    {
        var joker  = Cards.BlackJoker();
        var weapon = Cards.Weapon(6);
        var engine = Cards.RoomOf(joker, weapon, Cards.Potion(2), Cards.Potion(3));
        engine.TakeCard(joker);
        engine.StoreWeapon(weapon);
        int takenBefore = engine.CardsTakenThisRoom;

        engine.RetrieveWeapon(activate: false);

        Assert.That(engine.EquippedWeapon, Is.Null, "activate:false must not equip");
        Assert.That(engine.PocketedWeapon, Is.Null);
        Assert.That(engine.Discard, Contains.Item(weapon));
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(takenBefore), "Retrieval is not a room pick");
    }

    [Test]
    public void RetrieveWeapon_ActivateFalse_WithEquippedWeapon_LeavesEquippedWeaponUntouched()
    {
        var joker      = Cards.BlackJoker();
        var oldWeapon  = Cards.Weapon(4);
        var newWeapon  = Cards.Weapon(9);
        var monster    = Cards.Monster(3);
        var filler1 = Cards.Potion(2);
        var filler2 = Cards.Potion(3);
        var filler3 = Cards.Potion(4);
        var filler4 = Cards.Potion(5);
        var deck = new[] { filler4, filler3, filler2, filler1, monster, newWeapon, oldWeapon, joker };
        var engine = new GameEngine(deck, extendedRules: true);
        engine.TakeCard(joker);
        engine.TakeCard(oldWeapon);       // equips oldWeapon
        engine.StoreWeapon(newWeapon);
        engine.TakeCard(monster);         // wears oldWeapon: SlainMonsterCount 1, floor 3

        engine.RetrieveWeapon(activate: false);

        Assert.That(engine.EquippedWeapon, Is.EqualTo(oldWeapon), "Declining a retrieve must not disturb the equipped weapon");
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1), "Equipped weapon's wear must be untouched");
        Assert.That(engine.Discard, Does.Not.Contain(oldWeapon), "The equipped weapon is not discarded when declining");
        Assert.That(engine.Discard, Contains.Item(newWeapon), "The declined pocketed weapon is discarded instead");
        Assert.That(engine.PocketedWeapon, Is.Null);
    }

    [Test]
    public void RetrieveWeapon_ActivateFalse_StillThrowsWhenGatingFails()
    {
        var engine = Cards.RoomOf(Cards.Weapon(5), Cards.Potion(2), Cards.Potion(3), Cards.Potion(4));

        Assert.That(engine.CanRetrieveWeapon, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.RetrieveWeapon(activate: false));
    }

    [Test]
    public void RetrieveWeapon_ActivateTrueExplicit_MatchesDefaultBehavior()
    {
        var joker  = Cards.BlackJoker();
        var weapon = Cards.Weapon(6);
        var engine = Cards.RoomOf(joker, weapon, Cards.Potion(2), Cards.Potion(3));
        engine.TakeCard(joker);
        engine.StoreWeapon(weapon);

        engine.RetrieveWeapon(activate: true);

        Assert.That(engine.EquippedWeapon, Is.EqualTo(weapon));
        Assert.That(engine.WeaponFloor, Is.EqualTo(int.MaxValue));
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
        Assert.That(engine.PocketedWeapon, Is.Null);
        Assert.That(engine.Discard, Does.Not.Contain(weapon));
    }

    [Test]
    public void RetrieveWeapon_AfterWon_Throws()
    {
        var joker  = Cards.BlackJoker();
        var weapon = Cards.Weapon(5);
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), weapon, joker };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.StoreWeapon(weapon);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        Assert.That(engine.CanRetrieveWeapon, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.RetrieveWeapon());
    }

    [Test]
    public void RetrieveWeapon_AfterGameOver_Throws_WeaponStaysPocketed()
    {
        var joker         = Cards.BlackJoker();
        var weaponToStore = Cards.Weapon(5);
        var m9a = Cards.Monster(9);
        var m9b = Cards.Monster(9);
        var m9c = Cards.Monster(9);
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), m9c,
            m9b, m9a, weaponToStore, joker
        };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.StoreWeapon(weaponToStore);
        engine.TakeCard(m9a); // 20 -> 11
        engine.TakeCard(m9b); // 11 -> 2; room empties -> deals room 2
        engine.TakeCard(m9c); // 2 -> 0 -> GameOver

        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.CanRetrieveWeapon, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.RetrieveWeapon());
        Assert.That(engine.PocketedWeapon, Is.EqualTo(weaponToStore), "Pocket contents untouched by the failed retrieval");
    }
}
