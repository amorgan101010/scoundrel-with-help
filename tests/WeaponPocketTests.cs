using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Black Joker — Weapon Pocket (PRD §6, item 2) ───────────────────────────────

[TestFixture]
public class WeaponPocketTests
{
    [Test]
    public void CanStoreWeapon_FalseBeforeJokerTaken()
    {
        var weapon = Cards.Weapon(5);
        var engine = Cards.RoomOf(weapon, Cards.Potion(3), Cards.Potion(2), Cards.Potion(4));

        Assert.That(engine.CanStoreWeapon(weapon), Is.False);
    }

    [Test]
    public void StoreWeapon_Throws_WhenJokerNeverTaken()
    {
        var weapon = Cards.Weapon(5);
        var engine = Cards.RoomOf(weapon, Cards.Potion(3), Cards.Potion(2), Cards.Potion(4));

        Assert.Throws<InvalidOperationException>(() => engine.StoreWeapon(weapon));
    }

    [Test]
    public void CanStoreWeapon_FalseIfPocketAlreadyOccupied()
    {
        var joker = Cards.BlackJoker();
        var w1 = Cards.Weapon(5);
        var w2 = Cards.Weapon(6);
        var engine = Cards.RoomOf(joker, w1, Cards.Potion(3), w2);

        engine.TakeCard(joker);
        engine.StoreWeapon(w1);

        Assert.That(engine.CanStoreWeapon(w2), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.StoreWeapon(w2));
    }

    [Test]
    public void CanStoreWeapon_FalseForNonWeaponCard()
    {
        var joker = Cards.BlackJoker();
        var potion = Cards.Potion(5);
        var engine = Cards.RoomOf(joker, potion, Cards.Weapon(3), Cards.Potion(4));
        engine.TakeCard(joker);

        Assert.That(engine.CanStoreWeapon(potion), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.StoreWeapon(potion));
    }

    [Test]
    public void CanStoreWeapon_FalseForCardNotInRoom()
    {
        var joker = Cards.BlackJoker();
        var engine = Cards.RoomOf(joker, Cards.Weapon(3), Cards.Potion(4), Cards.Potion(5));
        engine.TakeCard(joker);

        var phantom = Cards.Weapon(9); // never dealt into any room
        Assert.That(engine.CanStoreWeapon(phantom), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.StoreWeapon(phantom));
    }

    [Test]
    public void StoreWeapon_HappyPath_MovesFromRoomToPocket_CountsTowardCardsTaken()
    {
        var joker  = Cards.BlackJoker();
        var weapon = Cards.Weapon(7);
        var engine = Cards.RoomOf(joker, weapon, Cards.Potion(2), Cards.Potion(3));
        engine.TakeCard(joker);
        int takenBefore = engine.CardsTakenThisRoom;

        engine.StoreWeapon(weapon);

        Assert.That(engine.PocketedWeapon, Is.EqualTo(weapon));
        Assert.That(engine.Room, Does.Not.Contain(weapon));
        Assert.That(engine.Discard, Does.Not.Contain(weapon));
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(takenBefore + 1));
        Assert.That(engine.EquippedWeapon, Is.Null, "Pocket storage must not touch the main weapon slot");
    }

    [Test]
    public void StoreWeapon_AfterWon_Throws()
    {
        var joker  = Cards.BlackJoker();
        var weapon = Cards.Weapon(5);
        // 4-card deck: taking the joker + storing the weapon + taking the last 2 cards
        // empties both room and deck -> Won.
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), weapon, joker };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.StoreWeapon(weapon);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        Assert.That(engine.CanStoreWeapon(weapon), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.StoreWeapon(weapon));
    }
}
