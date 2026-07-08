using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Joker Combat: handling a monster with a joker's own HP pool (PRD §6 follow-up,
//    chunk 6 — replaces chunk 3's zero-damage pocket-weapon fight) ──────────────────

[TestFixture]
public class JokerCombatTests
{
    // ── Red Joker (Potion Joker) ─────────────────────────────────────────────────

    [Test]
    public void CanFightWithPotionJoker_FalseWhenJokerNeverTaken()
    {
        var monster = Cards.Monster(5);
        var engine = Cards.RoomOf(monster, Cards.Potion(2), Cards.Potion(3), Cards.Weapon(4));

        Assert.That(engine.CanFightWithPotionJoker(monster), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithPotionJoker(monster));
    }

    [Test]
    public void CanFightWithPotionJoker_FalseForNonMonsterCard()
    {
        var joker = Cards.RedJoker();
        var potion = Cards.Potion(5);
        var engine = Cards.RoomOf(joker, potion, Cards.Weapon(3), Cards.Potion(4));
        engine.TakeCard(joker);

        Assert.That(engine.CanFightWithPotionJoker(potion), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithPotionJoker(potion));
    }

    [Test]
    public void CanFightWithPotionJoker_FalseForCardNotInRoom()
    {
        var joker = Cards.RedJoker();
        var engine = Cards.RoomOf(joker, Cards.Potion(2), Cards.Potion(3), Cards.Potion(4));
        engine.TakeCard(joker);

        var phantom = Cards.Monster(6); // never dealt into any room
        Assert.That(engine.CanFightWithPotionJoker(phantom), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithPotionJoker(phantom));
    }

    [Test]
    public void FightWithPotionJoker_HappyPath_ReducesJokerHealth_MovesMonsterToDiscard_CountsTowardCardsTaken_NoHealthOrWeaponEffect()
    {
        var joker   = Cards.RedJoker();
        var monster = Cards.Monster(5);
        var weapon  = Cards.Weapon(3);
        var engine  = Cards.RoomOf(joker, monster, weapon, Cards.Potion(2));
        engine.TakeCard(joker);
        engine.TakeCard(weapon); // equip a weapon so we can assert it's untouched
        int healthBefore = engine.Health;
        int takenBefore  = engine.CardsTakenThisRoom;

        Assert.That(engine.CanFightWithPotionJoker(monster), Is.True);
        engine.FightWithPotionJoker(monster);

        Assert.That(engine.PotionJokerHealth, Is.EqualTo(3), "8 - 5 = 3");
        Assert.That(engine.Room, Does.Not.Contain(monster));
        Assert.That(engine.Discard, Contains.Item(monster));
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(takenBefore + 1));
        Assert.That(engine.Health, Is.EqualTo(healthBefore), "Joker combat never touches player Health");
        Assert.That(engine.EquippedWeapon, Is.EqualTo(weapon), "Joker combat never touches the equipped weapon");
        Assert.That(engine.WeaponFloor, Is.EqualTo(int.MaxValue), "Joker combat never touches weapon wear");
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
        Assert.That(engine.HasPotionJoker, Is.True, "8 - 5 = 3 > 0, joker survives");
    }

    [Test]
    public void CanFightWithPotionJoker_FalseWhenHealthAlreadyZero()
    {
        var joker    = Cards.RedJoker();
        var monster1 = Cards.Monster(8); // exactly kills the joker (8 - 8 = 0)
        var monster2 = Cards.Spade(2);
        var filler1  = Cards.Potion(2);
        var filler2  = Cards.Potion(3);
        var filler3  = Cards.Potion(4);

        // Deck bottom -> top. Room 1 = {filler1, monster1, filler2, joker}; room 2 refill
        // (dealt once room 1 empties) = {monster2, filler3, x, y} -- keep it simple with a
        // 2x2 room where monster2 stays available after room1 empties.
        var deck = new[] { filler3, monster2, filler1, monster1, filler2, joker };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.FightWithPotionJoker(monster1);
        Assert.That(engine.PotionJokerHealth, Is.EqualTo(0));
        Assert.That(engine.HasPotionJoker, Is.False, "Joker lost when HP hits 0");

        // Even though a new Red Joker HasPotionJoker flag is gone, sanity-check the gate
        // directly: with HasPotionJoker false, CanFightWithPotionJoker must be false too.
        Assert.That(engine.CanFightWithPotionJoker(monster2), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithPotionJoker(monster2));
    }

    [Test]
    public void FightWithPotionJoker_HealthClampsAtZero_NeverNegative()
    {
        var joker   = Cards.RedJoker();
        var monster = new CardModel(Suit.Clubs, ScoundrelRules.AceRank); // MonsterValue 14, far stronger than the joker's 8 HP
        var engine  = Cards.RoomOf(joker, monster, Cards.Potion(2), Cards.Potion(3));
        engine.TakeCard(joker);

        engine.FightWithPotionJoker(monster);

        Assert.That(engine.PotionJokerHealth, Is.EqualTo(0), "Clamped at 0, never negative");
        Assert.That(engine.HasPotionJoker, Is.False);
    }

    [Test]
    public void FightWithPotionJoker_JokerDies_WithPocketedPotion_LosesPotion_NotDiscarded()
    {
        var joker   = Cards.RedJoker();
        var potion  = Cards.Potion(6);
        var monster = Cards.Monster(9); // kills the joker outright (9 > 8)
        var engine  = Cards.RoomOf(joker, potion, monster, Cards.Weapon(3));
        engine.TakeCard(joker);
        engine.StorePotion(potion);

        engine.FightWithPotionJoker(monster);

        Assert.That(engine.HasPotionJoker, Is.False);
        Assert.That(engine.PocketedPotion, Is.Null, "Lost, not discarded");
        Assert.That(engine.Discard, Does.Not.Contain(potion));
    }

    [Test]
    public void FightWithPotionJoker_JokerDies_WithEmptyPocket_StaysNull_NoCrash()
    {
        var joker   = Cards.RedJoker();
        var monster = Cards.Monster(9);
        var engine  = Cards.RoomOf(joker, monster, Cards.Potion(2), Cards.Potion(3));
        engine.TakeCard(joker);

        Assert.DoesNotThrow(() => engine.FightWithPotionJoker(monster));

        Assert.That(engine.HasPotionJoker, Is.False);
        Assert.That(engine.PocketedPotion, Is.Null);
    }

    [Test]
    public void FightWithPotionJoker_AfterWon_Throws()
    {
        var joker   = Cards.RedJoker();
        var monster = Cards.Monster(3);
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), monster, joker };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.TakeCard(monster);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        var phantom = Cards.Monster(4);
        Assert.That(engine.CanFightWithPotionJoker(phantom), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithPotionJoker(phantom));
    }

    [Test]
    public void FightWithPotionJoker_AfterGameOver_Throws()
    {
        var joker = Cards.RedJoker();
        var filler = Cards.Potion(4);
        var m9a = Cards.Monster(9);
        var m9b = Cards.Monster(9);
        var m9c = Cards.Monster(9);
        var monsterX = Cards.Monster(3);
        var fillerP = Cards.Potion(2);
        var fillerQ = Cards.Potion(3);
        // Deck bottom -> top. Room 1 = {joker, filler, m9a, m9b}; room 2 refill (dealt once
        // room 1 empties) = {m9c, monsterX, fillerP, fillerQ}.
        var deck = new[] { fillerQ, fillerP, monsterX, m9c, m9b, m9a, filler, joker };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.TakeCard(filler); // taken=2; room={m9a,m9b}
        engine.TakeCard(m9a);    // 20 -> 11
        engine.TakeCard(m9b);    // 11 -> 2; room empties -> deals room 2
        engine.TakeCard(m9c);    // 2 -> 0 -> GameOver; monsterX left untouched in room 2

        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.CanFightWithPotionJoker(monsterX), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithPotionJoker(monsterX));
    }

    // ── Black Joker (Weapon Joker) ───────────────────────────────────────────────

    [Test]
    public void CanFightWithWeaponJoker_FalseWhenJokerNeverTaken()
    {
        var monster = Cards.Monster(5);
        var engine = Cards.RoomOf(monster, Cards.Potion(2), Cards.Potion(3), Cards.Weapon(4));

        Assert.That(engine.CanFightWithWeaponJoker(monster), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithWeaponJoker(monster));
    }

    [Test]
    public void CanFightWithWeaponJoker_FalseForNonMonsterCard()
    {
        var joker = Cards.BlackJoker();
        var weaponCard = Cards.Weapon(5);
        var engine = Cards.RoomOf(joker, weaponCard, Cards.Potion(3), Cards.Potion(4));
        engine.TakeCard(joker);

        Assert.That(engine.CanFightWithWeaponJoker(weaponCard), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithWeaponJoker(weaponCard));
    }

    [Test]
    public void CanFightWithWeaponJoker_FalseForCardNotInRoom()
    {
        var joker = Cards.BlackJoker();
        var engine = Cards.RoomOf(joker, Cards.Weapon(3), Cards.Potion(4), Cards.Potion(5));
        engine.TakeCard(joker);

        var phantom = Cards.Monster(6); // never dealt into any room
        Assert.That(engine.CanFightWithWeaponJoker(phantom), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithWeaponJoker(phantom));
    }

    [Test]
    public void FightWithWeaponJoker_HappyPath_ReducesJokerHealth_MovesMonsterToDiscard_CountsTowardCardsTaken_NoHealthOrWeaponEffect()
    {
        var joker   = Cards.BlackJoker();
        var monster = Cards.Monster(5);
        var weapon  = Cards.Weapon(3);
        var engine  = Cards.RoomOf(joker, monster, weapon, Cards.Potion(2));
        engine.TakeCard(joker);
        engine.TakeCard(weapon); // equip a weapon so we can assert it's untouched
        int healthBefore = engine.Health;
        int takenBefore  = engine.CardsTakenThisRoom;

        Assert.That(engine.CanFightWithWeaponJoker(monster), Is.True);
        engine.FightWithWeaponJoker(monster);

        Assert.That(engine.WeaponJokerHealth, Is.EqualTo(3), "8 - 5 = 3");
        Assert.That(engine.Room, Does.Not.Contain(monster));
        Assert.That(engine.Discard, Contains.Item(monster));
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(takenBefore + 1));
        Assert.That(engine.Health, Is.EqualTo(healthBefore), "Joker combat never touches player Health");
        Assert.That(engine.EquippedWeapon, Is.EqualTo(weapon), "Joker combat never touches the equipped weapon");
        Assert.That(engine.WeaponFloor, Is.EqualTo(int.MaxValue), "Joker combat never touches weapon wear");
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
        Assert.That(engine.HasWeaponJoker, Is.True, "8 - 5 = 3 > 0, joker survives");
    }

    [Test]
    public void CanFightWithWeaponJoker_FalseWhenHealthAlreadyZero()
    {
        var joker    = Cards.BlackJoker();
        var monster1 = Cards.Monster(8); // exactly kills the joker (8 - 8 = 0)
        var monster2 = Cards.Spade(2);
        var filler1  = Cards.Potion(2);
        var filler2  = Cards.Potion(3);
        var filler3  = Cards.Potion(4);

        var deck = new[] { filler3, monster2, filler1, monster1, filler2, joker };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.FightWithWeaponJoker(monster1);
        Assert.That(engine.WeaponJokerHealth, Is.EqualTo(0));
        Assert.That(engine.HasWeaponJoker, Is.False, "Joker lost when HP hits 0");

        Assert.That(engine.CanFightWithWeaponJoker(monster2), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithWeaponJoker(monster2));
    }

    [Test]
    public void FightWithWeaponJoker_HealthClampsAtZero_NeverNegative()
    {
        var joker   = Cards.BlackJoker();
        var monster = new CardModel(Suit.Clubs, ScoundrelRules.AceRank); // MonsterValue 14, far stronger than the joker's 8 HP
        var engine  = Cards.RoomOf(joker, monster, Cards.Potion(2), Cards.Potion(3));
        engine.TakeCard(joker);

        engine.FightWithWeaponJoker(monster);

        Assert.That(engine.WeaponJokerHealth, Is.EqualTo(0), "Clamped at 0, never negative");
        Assert.That(engine.HasWeaponJoker, Is.False);
    }

    [Test]
    public void FightWithWeaponJoker_JokerDies_WithPocketedWeapon_LosesWeapon_NotDiscarded()
    {
        var joker         = Cards.BlackJoker();
        var pocketWeapon  = Cards.Weapon(6);
        var monster       = Cards.Monster(9); // kills the joker outright (9 > 8)
        var engine        = Cards.RoomOf(joker, pocketWeapon, monster, Cards.Potion(3));
        engine.TakeCard(joker);
        engine.StoreWeapon(pocketWeapon);

        engine.FightWithWeaponJoker(monster);

        Assert.That(engine.HasWeaponJoker, Is.False);
        Assert.That(engine.PocketedWeapon, Is.Null, "Lost, not discarded");
        Assert.That(engine.Discard, Does.Not.Contain(pocketWeapon));
    }

    [Test]
    public void FightWithWeaponJoker_JokerDies_WithEmptyPocket_StaysNull_NoCrash()
    {
        var joker   = Cards.BlackJoker();
        var monster = Cards.Monster(9);
        var engine  = Cards.RoomOf(joker, monster, Cards.Potion(2), Cards.Potion(3));
        engine.TakeCard(joker);

        Assert.DoesNotThrow(() => engine.FightWithWeaponJoker(monster));

        Assert.That(engine.HasWeaponJoker, Is.False);
        Assert.That(engine.PocketedWeapon, Is.Null);
    }

    [Test]
    public void FightWithWeaponJoker_AfterWon_Throws()
    {
        var joker   = Cards.BlackJoker();
        var monster = Cards.Monster(3);
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), monster, joker };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.TakeCard(monster);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        var phantom = Cards.Monster(4);
        Assert.That(engine.CanFightWithWeaponJoker(phantom), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithWeaponJoker(phantom));
    }

    [Test]
    public void FightWithWeaponJoker_AfterGameOver_Throws()
    {
        var joker = Cards.BlackJoker();
        var filler = Cards.Potion(4);
        var m9a = Cards.Monster(9);
        var m9b = Cards.Monster(9);
        var m9c = Cards.Monster(9);
        var monsterX = Cards.Monster(3);
        var fillerP = Cards.Potion(2);
        var fillerQ = Cards.Potion(3);
        // Deck bottom -> top. Room 1 = {joker, filler, m9a, m9b}; room 2 refill (dealt once
        // room 1 empties) = {m9c, monsterX, fillerP, fillerQ}.
        var deck = new[] { fillerQ, fillerP, monsterX, m9c, m9b, m9a, filler, joker };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(joker);
        engine.TakeCard(filler); // taken=2; room={m9a,m9b}
        engine.TakeCard(m9a);    // 20 -> 11
        engine.TakeCard(m9b);    // 11 -> 2; room empties -> deals room 2
        engine.TakeCard(m9c);    // 2 -> 0 -> GameOver; monsterX left untouched in room 2

        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.CanFightWithWeaponJoker(monsterX), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.FightWithWeaponJoker(monsterX));
    }
}
