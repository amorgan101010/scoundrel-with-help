using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Red Joker — Potion Pocket (PRD §6, item 1) ─────────────────────────────────

[TestFixture]
public class PotionPocketTests
{
    [Test]
    public void CanStorePotion_FalseBeforeJokerTaken()
    {
        var potion = Cards.Potion(5);
        var engine = Cards.RoomOf(potion, Cards.Weapon(3), Cards.Potion(2), Cards.Potion(4));

        Assert.That(engine.CanStorePotion(potion), Is.False);
    }

    [Test]
    public void StorePotion_Throws_WhenJokerNeverTaken()
    {
        var potion = Cards.Potion(5);
        var engine = Cards.RoomOf(potion, Cards.Weapon(3), Cards.Potion(2), Cards.Potion(4));

        Assert.Throws<InvalidOperationException>(() => engine.StorePotion(potion));
    }

    [Test]
    public void CanStorePotion_FalseIfPocketAlreadyOccupied()
    {
        var joker = Cards.RedJoker();
        var p1 = Cards.Potion(5);
        var p2 = Cards.Potion(6);
        var engine = Cards.RoomOf(joker, p1, Cards.Weapon(3), p2);

        engine.TakeCard(joker);
        engine.StorePotion(p1);

        Assert.That(engine.CanStorePotion(p2), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.StorePotion(p2));
    }

    [Test]
    public void CanStorePotion_FalseForNonPotionCard()
    {
        var joker = Cards.RedJoker();
        var weapon = Cards.Weapon(5);
        var engine = Cards.RoomOf(joker, weapon, Cards.Potion(2), Cards.Potion(3));
        engine.TakeCard(joker);

        Assert.That(engine.CanStorePotion(weapon), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.StorePotion(weapon));
    }

    [Test]
    public void CanStorePotion_FalseForCardNotInRoom()
    {
        var joker = Cards.RedJoker();
        var engine = Cards.RoomOf(joker, Cards.Potion(2), Cards.Potion(3), Cards.Potion(4));
        engine.TakeCard(joker);

        var phantom = Cards.Potion(9); // never dealt into any room
        Assert.That(engine.CanStorePotion(phantom), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.StorePotion(phantom));
    }

    [Test]
    public void StorePotion_HappyPath_MovesFromRoomToPocket_NoHeal_CountsTowardCardsTaken()
    {
        var joker  = Cards.RedJoker();
        var potion = Cards.Potion(7);
        var engine = Cards.RoomOf(joker, potion, Cards.Weapon(3), Cards.Potion(2));
        engine.TakeCard(joker);
        int healthBefore = engine.Health;
        int takenBefore  = engine.CardsTakenThisRoom;

        engine.StorePotion(potion);

        Assert.That(engine.PocketedPotion, Is.EqualTo(potion));
        Assert.That(engine.Room, Does.Not.Contain(potion));
        Assert.That(engine.Discard, Does.Not.Contain(potion));
        Assert.That(engine.Health, Is.EqualTo(healthBefore));
        Assert.That(engine.PotionUsedThisRoom, Is.False, "Storing must not consume the room's potion limit");
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(takenBefore + 1));
    }

    [Test]
    public void CanRetrievePotion_FalseWhenJokerNeverTaken()
    {
        var engine = Cards.RoomOf(Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Weapon(5));

        Assert.That(engine.CanRetrievePotion, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.RetrievePotion());
    }

    [Test]
    public void CanRetrievePotion_FalseWhenPocketEmpty()
    {
        var joker = Cards.RedJoker();
        var engine = Cards.RoomOf(joker, Cards.Potion(2), Cards.Potion(3), Cards.Potion(4));
        engine.TakeCard(joker);

        Assert.That(engine.CanRetrievePotion, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.RetrievePotion());
    }

    [Test]
    public void CanRetrievePotion_AvailableAnytimeRegardlessOfRoomState()
    {
        // Only 2 cards taken this room (joker + stored potion) — below MinCardsTaken, so
        // NextRoom would be blocked. Retrieval is a side action, not a room pick, so it's
        // available regardless.
        var joker  = Cards.RedJoker();
        var potion = Cards.Potion(5);
        var engine = Cards.RoomOf(joker, potion, Cards.Weapon(3), Cards.Potion(2));
        engine.TakeCard(joker);
        engine.StorePotion(potion);

        Assert.That(engine.CanNextRoom, Is.False);
        Assert.That(engine.CanRetrievePotion, Is.True);
        Assert.DoesNotThrow(() => engine.RetrievePotion());
    }

    [Test]
    public void RetrievePotion_NoPotionDrunkYet_HealsCorrectly_EmptiesPocket_MovesToDiscard()
    {
        var joker   = Cards.RedJoker();
        var potion  = Cards.Potion(6);
        var monster = Cards.Monster(8);
        var engine  = Cards.RoomOf(joker, potion, monster, Cards.Weapon(3));
        engine.TakeCard(joker);
        engine.StorePotion(potion);
        engine.TakeCard(monster); // take damage so healing is observable
        int healthBeforeRetrieve = engine.Health;
        int takenBefore = engine.CardsTakenThisRoom;

        engine.RetrievePotion();

        int expectedHealth = ScoundrelRules.Heal(healthBeforeRetrieve, potion.PotionValue);
        Assert.That(engine.Health, Is.EqualTo(expectedHealth));
        Assert.That(engine.PocketedPotion, Is.Null);
        Assert.That(engine.Discard, Contains.Item(potion));
        Assert.That(engine.PotionUsedThisRoom, Is.True);
        Assert.That(engine.PotionWastedThisRoom, Is.False);
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(takenBefore), "Retrieval is not a room pick");
    }

    [Test]
    public void RetrievePotion_PotionAlreadyDrunkThisRoom_IsWasted_NoExtraHeal()
    {
        var joker         = Cards.RedJoker();
        var potionToStore = Cards.Potion(6);
        var potionToDrink = Cards.Potion(4);
        var engine = Cards.RoomOf(joker, potionToStore, potionToDrink, Cards.Weapon(3));
        engine.TakeCard(joker);
        engine.StorePotion(potionToStore);
        engine.TakeCard(potionToDrink); // uses the room's one potion
        int healthAfterDrink = engine.Health;

        engine.RetrievePotion();

        Assert.That(engine.PotionWastedThisRoom, Is.True);
        Assert.That(engine.Health, Is.EqualTo(healthAfterDrink), "No extra heal — the pocketed potion is wasted");
        Assert.That(engine.PocketedPotion, Is.Null);
        Assert.That(engine.Discard, Contains.Item(potionToStore));
    }

    [Test]
    public void StoreThenDrinkDifferentPotion_SameRoom_BothWork()
    {
        var joker         = Cards.RedJoker();
        var potionToStore = Cards.Potion(6);
        var potionToDrink = Cards.Potion(4);
        var engine = Cards.RoomOf(joker, potionToStore, potionToDrink, Cards.Weapon(3));
        engine.TakeCard(joker);

        engine.StorePotion(potionToStore);
        Assert.That(engine.PotionUsedThisRoom, Is.False, "Storing must not use the room's potion allowance");

        int healthBefore = engine.Health;
        engine.TakeCard(potionToDrink);

        Assert.That(engine.PotionUsedThisRoom, Is.True);
        Assert.That(engine.PotionWastedThisRoom, Is.False);
        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.Heal(healthBefore, potionToDrink.PotionValue)));
    }

    [Test]
    public void DrinkThenStore_SameRoom_BothWork()
    {
        var joker         = Cards.RedJoker();
        var potionToDrink = Cards.Potion(4);
        var potionToStore = Cards.Potion(6);
        var engine = Cards.RoomOf(joker, potionToDrink, potionToStore, Cards.Weapon(3));
        engine.TakeCard(joker);

        int healthBefore = engine.Health;
        engine.TakeCard(potionToDrink);
        Assert.That(engine.PotionUsedThisRoom, Is.True);
        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.Heal(healthBefore, potionToDrink.PotionValue)));

        engine.StorePotion(potionToStore);

        Assert.That(engine.PocketedPotion, Is.EqualTo(potionToStore));
        Assert.That(engine.PotionWastedThisRoom, Is.False, "Storing is independent of the room's potion limit");
    }

    // ── RetrievePotion(activate) — chunk 10 store/retrieve UI wiring ───────────────

    [Test]
    public void RetrievePotion_ActivateFalse_DiscardsWithoutHealOrWaste_EmptiesPocket()
    {
        var joker   = Cards.RedJoker();
        var potion  = Cards.Potion(6);
        var monster = Cards.Monster(8);
        var engine  = Cards.RoomOf(joker, potion, monster, Cards.Weapon(3));
        engine.TakeCard(joker);
        engine.StorePotion(potion);
        engine.TakeCard(monster); // take damage so a missed heal would be observable
        int healthBeforeRetrieve = engine.Health;
        int takenBefore = engine.CardsTakenThisRoom;

        engine.RetrievePotion(activate: false);

        Assert.That(engine.Health, Is.EqualTo(healthBeforeRetrieve), "activate:false must not heal");
        Assert.That(engine.PotionUsedThisRoom, Is.False, "activate:false must not touch PotionUsedThisRoom");
        Assert.That(engine.PotionWastedThisRoom, Is.False, "activate:false must not touch PotionWastedThisRoom");
        Assert.That(engine.PocketedPotion, Is.Null);
        Assert.That(engine.Discard, Contains.Item(potion));
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(takenBefore), "Retrieval is not a room pick");
    }

    [Test]
    public void RetrievePotion_ActivateFalse_WhenPotionAlreadyDrunk_StaysFalse_NotWasted()
    {
        var joker         = Cards.RedJoker();
        var potionToStore = Cards.Potion(6);
        var potionToDrink = Cards.Potion(4);
        var engine = Cards.RoomOf(joker, potionToStore, potionToDrink, Cards.Weapon(3));
        engine.TakeCard(joker);
        engine.StorePotion(potionToStore);
        engine.TakeCard(potionToDrink); // uses the room's one potion; PotionUsedThisRoom = true

        engine.RetrievePotion(activate: false);

        Assert.That(engine.PotionWastedThisRoom, Is.False,
            "Declining a retrieve must not mark it wasted, even if a potion was already drunk this room");
        Assert.That(engine.PocketedPotion, Is.Null);
        Assert.That(engine.Discard, Contains.Item(potionToStore));
    }

    [Test]
    public void RetrievePotion_ActivateFalse_StillThrowsWhenGatingFails()
    {
        var engine = Cards.RoomOf(Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Weapon(5));

        Assert.That(engine.CanRetrievePotion, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.RetrievePotion(activate: false));
    }

    [Test]
    public void RetrievePotion_ActivateTrueExplicit_MatchesDefaultBehavior()
    {
        var joker   = Cards.RedJoker();
        var potion  = Cards.Potion(6);
        var monster = Cards.Monster(8);
        var engine  = Cards.RoomOf(joker, potion, monster, Cards.Weapon(3));
        engine.TakeCard(joker);
        engine.StorePotion(potion);
        engine.TakeCard(monster);
        int healthBeforeRetrieve = engine.Health;

        engine.RetrievePotion(activate: true);

        int expectedHealth = ScoundrelRules.Heal(healthBeforeRetrieve, potion.PotionValue);
        Assert.That(engine.Health, Is.EqualTo(expectedHealth));
        Assert.That(engine.PocketedPotion, Is.Null);
        Assert.That(engine.Discard, Contains.Item(potion));
        Assert.That(engine.PotionUsedThisRoom, Is.True);
        Assert.That(engine.PotionWastedThisRoom, Is.False);
    }

    [Test]
    public void RetrievePotion_AfterWon_Throws()
    {
        var joker  = Cards.RedJoker();
        var potion = Cards.Potion(5);
        // 4-card deck: taking the joker + storing the potion + taking the last 2 cards
        // empties both room and deck -> Won.
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), potion, joker };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.StorePotion(potion);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        Assert.That(engine.CanRetrievePotion, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.RetrievePotion());
    }

    [Test]
    public void RetrievePotion_AfterGameOver_Throws_PotionStaysPocketed()
    {
        var joker         = Cards.RedJoker();
        var potionToStore = Cards.Potion(5);
        var m9a = Cards.Monster(9);
        var m9b = Cards.Monster(9);
        var m9c = Cards.Monster(9);
        // Deck bottom -> top. Room 1 = {joker, potionToStore, m9a, m9b}; room 2 refill
        // (dealt after room 1 empties) = {m9c, filler, filler, filler}.
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), m9c,
            m9b, m9a, potionToStore, joker
        };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);              // HasPotionJoker = true
        engine.StorePotion(potionToStore);   // pocket = potionToStore
        engine.TakeCard(m9a);                // 20 -> 11
        engine.TakeCard(m9b);                // 11 -> 2; room empties -> deals room 2
        engine.TakeCard(m9c);                // 2 -> 0 -> GameOver

        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.CanRetrievePotion, Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.RetrievePotion());
        Assert.That(engine.PocketedPotion, Is.EqualTo(potionToStore), "Pocket contents untouched by the failed retrieval");
    }
}
