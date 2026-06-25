using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── CardModel ─────────────────────────────────────────────────────────────────

[TestFixture]
public class CardModelTests
{
    [Test] public void Clubs_IsMonster()  => Assert.That(new CardModel(Suit.Clubs,    5).IsMonster, Is.True);
    [Test] public void Spades_IsMonster() => Assert.That(new CardModel(Suit.Spades,   5).IsMonster, Is.True);
    [Test] public void Diamonds_IsNotMonster() => Assert.That(new CardModel(Suit.Diamonds, 5).IsMonster, Is.False);
    [Test] public void Hearts_IsNotMonster()   => Assert.That(new CardModel(Suit.Hearts,   5).IsMonster, Is.False);

    [Test] public void Diamonds_IsWeapon()    => Assert.That(new CardModel(Suit.Diamonds, 5).IsWeapon, Is.True);
    [Test] public void Clubs_IsNotWeapon()    => Assert.That(new CardModel(Suit.Clubs,    5).IsWeapon, Is.False);
    [Test] public void Hearts_IsPotion()      => Assert.That(new CardModel(Suit.Hearts,   5).IsPotion, Is.True);
    [Test] public void Clubs_IsNotPotion()    => Assert.That(new CardModel(Suit.Clubs,    5).IsPotion, Is.False);

    [Test] public void Ace_MonsterValueIs14()
        => Assert.That(new CardModel(Suit.Clubs, 1).MonsterValue, Is.EqualTo(14));
    [Test] public void King_MonsterValueIs13()
        => Assert.That(new CardModel(Suit.Spades, 13).MonsterValue, Is.EqualTo(13));
    [Test] public void NumberCard_MonsterValueIsRank()
        => Assert.That(new CardModel(Suit.Clubs, 7).MonsterValue, Is.EqualTo(7));

    [Test] public void WeaponValueIsRank()
        => Assert.That(new CardModel(Suit.Diamonds, 8).WeaponValue, Is.EqualTo(8));
    [Test] public void PotionValueIsRank()
        => Assert.That(new CardModel(Suit.Hearts, 4).PotionValue, Is.EqualTo(4));
}

// ── Helpers ───────────────────────────────────────────────────────────────────

file static class Cards
{
    public static CardModel Monster(int rank) => new(Suit.Clubs,    rank);
    public static CardModel Spade(int rank)   => new(Suit.Spades,   rank);
    public static CardModel Weapon(int rank)  => new(Suit.Diamonds, rank);
    public static CardModel Potion(int rank)  => new(Suit.Hearts,   rank);

    // Pad a short card list to 4 so DealRoom fills the room immediately.
    // Extra padding cards are weak monsters that sit at the bottom of the deck.
    public static CardModel[] PadToFour(params CardModel[] cards)
    {
        var padded = new List<CardModel>(cards);
        while (padded.Count < 4) padded.Insert(0, Monster(2));
        return padded.ToArray();
    }

    // Build a deck whose top 4 cards (last in array) are the supplied room cards.
    public static GameEngine RoomOf(params CardModel[] roomCards)
    {
        var deck = PadToFour(roomCards);
        return new GameEngine(deck);
    }
}

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
    public void FightAce_MonsterValueIs14()
    {
        var ace    = Cards.Monster(1);
        var engine = Cards.RoomOf(ace, Cards.Potion(2), Cards.Weapon(3), Cards.Potion(4));

        engine.TakeCard(ace);

        Assert.That(engine.Health, Is.EqualTo(ScoundrelRules.StartHealth - 14));
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

// ── Room advancement ──────────────────────────────────────────────────────────

[TestFixture]
public class RoomProgressTests
{
    [Test]
    public void TakingAllFourCards_AutoAdvances()
    {
        var cards = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Weapon(4), Cards.Monster(5) };
        // 8 cards total: first room = last 4, second room = first 4
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), Cards.Potion(2),
            cards[0], cards[1], cards[2], cards[3]
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(cards[3]);
        engine.TakeCard(cards[2]);
        engine.TakeCard(cards[1]);
        engine.TakeCard(cards[0]); // 4th card — should auto-deal next room

        Assert.That(engine.Room.Count, Is.EqualTo(4));
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(0)); // reset
    }

    [Test]
    public void TakingThreeCards_EnablesNextRoom()
    {
        var c1 = Cards.Potion(2);
        var c2 = Cards.Potion(3);
        var c3 = Cards.Weapon(4);
        var c4 = Cards.Monster(5);
        var engine = Cards.RoomOf(c1, c2, c3, c4);

        engine.TakeCard(c4);
        engine.TakeCard(c3);
        engine.TakeCard(c2);

        Assert.That(engine.CanNextRoom, Is.True);
        Assert.That(engine.Room.Count, Is.EqualTo(1));
    }

    [Test]
    public void NextRoom_CarriesOverRemainingCard()
    {
        var leftover = Cards.Monster(5);
        var c1 = Cards.Potion(2);
        var c2 = Cards.Potion(3);
        var c3 = Cards.Weapon(4);
        // Deck: pad(4) for room 2 filler at bottom, then room 1
        var deck = new[]
        {
            Cards.Monster(2), Cards.Monster(2), Cards.Monster(2), Cards.Monster(2),
            c1, c2, c3, leftover
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(c1);
        engine.TakeCard(c2);
        engine.TakeCard(c3);
        engine.NextRoom();

        Assert.That(engine.Room, Contains.Item(leftover));
        Assert.That(engine.Room.Count, Is.EqualTo(4)); // leftover + 3 new
    }

    [Test]
    public void NextRoom_BeforeMinCardsTaken_Throws()
    {
        var engine = Cards.RoomOf(
            Cards.Potion(2), Cards.Potion(3), Cards.Weapon(4), Cards.Monster(5));

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);

        Assert.Throws<InvalidOperationException>(() => engine.NextRoom());
    }
}

// ── Run ───────────────────────────────────────────────────────────────────────

[TestFixture]
public class RunTests
{
    [Test]
    public void Run_ClearsRoom_AndDealsNew()
    {
        var deck = Enumerable.Range(0, 8).Select(_ => Cards.Potion(2)).ToArray();
        var engine = new GameEngine(deck);

        int deckBefore = engine.Deck.Count; // 4 (8 total, 4 dealt to room)
        engine.Run();

        Assert.That(engine.Room.Count, Is.EqualTo(4));      // new room dealt
        Assert.That(engine.Deck.Count, Is.EqualTo(deckBefore)); // same deck size (4 back in, 4 dealt out)
    }

    [Test]
    public void Run_SetsRanLastRoom()
    {
        var deck = Enumerable.Range(0, 8).Select(_ => Cards.Monster(2)).ToArray();
        var engine = new GameEngine(deck);

        engine.Run();

        Assert.That(engine.RanLastRoom, Is.True);
        Assert.That(engine.CanRun, Is.False);
    }

    [Test]
    public void CannotRunTwiceInARow_Throws()
    {
        var deck = Enumerable.Range(0, 16).Select(_ => Cards.Monster(2)).ToArray();
        var engine = new GameEngine(deck);

        engine.Run();

        Assert.Throws<InvalidOperationException>(() => engine.Run());
    }

    [Test]
    public void RunThenNextRoom_AllowsRunAgain()
    {
        var deck = Enumerable.Range(0, 16).Select(_ => Cards.Monster(2)).ToArray();
        var engine = new GameEngine(deck);

        engine.Run(); // run room 1

        // take 3 cards in room 2 to unlock NextRoom
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.NextRoom(); // advances, resets RanLastRoom

        Assert.That(engine.CanRun, Is.True);
    }

    [Test]
    public void RunReturnsAllFourRoomCards_ToDeck()
    {
        var roomCards = new[]
        {
            Cards.Monster(3), Cards.Monster(4), Cards.Weapon(5), Cards.Potion(6)
        };
        // 8-card deck: 4 filler at bottom, 4 room cards at top
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), Cards.Potion(2),
            roomCards[0], roomCards[1], roomCards[2], roomCards[3]
        };
        var engine = new GameEngine(deck);
        // Room is dealt; deck has 4 filler cards
        Assert.That(engine.Deck.Count, Is.EqualTo(4));

        engine.Run();

        // The 4 room cards went back to deck, then 4 were dealt to new room
        Assert.That(engine.Deck.Count, Is.EqualTo(4)); // 4 (back) + 4 filler - 4 (dealt) = 4
        Assert.That(engine.Room.Count, Is.EqualTo(4));
    }

    [Test]
    public void Run_PutsRoomCardsAtDeckBottom()
    {
        // 12-card deck: room3 (bottom), room2 (middle), room1 (top → dealt to room first)
        var room1 = new[] { Cards.Monster(3), Cards.Monster(4), Cards.Weapon(5), Cards.Potion(6) };
        var room2 = new[] { Cards.Potion(2),  Cards.Potion(2),  Cards.Potion(2),  Cards.Potion(2)  };
        var room3 = new[] { Cards.Monster(7), Cards.Monster(8), Cards.Monster(9), Cards.Monster(10) };
        var deck = room3.Concat(room2).Concat(room1).ToArray();
        var engine = new GameEngine(deck);

        engine.Run();

        // room2 is now the new room (previously the top of the remaining deck).
        // room1 cards returned to index 0 (bottom); room3 cards sit above them.
        Assert.That(engine.Deck.Take(4),  Is.EquivalentTo(room1));
        Assert.That(engine.Deck.Skip(4),  Is.EquivalentTo(room3));
    }
}

// ── Win / lose ────────────────────────────────────────────────────────────────

[TestFixture]
public class WinLoseTests
{
    [Test]
    public void TakeAllCards_Won()
    {
        // 4-card deck → one room, take all 4 → win
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5)
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);

        Assert.That(engine.Won, Is.True);
        Assert.That(engine.GameOver, Is.False);
    }

    [Test]
    public void HealthHitsZero_GameOver_NotWon()
    {
        var engine = Cards.RoomOf(
            Cards.Monster(9), Cards.Monster(9), Cards.Monster(9), Cards.Potion(2));

        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster)); // dead

        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.Won, Is.False);
    }
}

// ── Full game scenario ────────────────────────────────────────────────────────

[TestFixture]
public class FullScenarioTests
{
    /// <summary>
    /// A scripted 8-card mini-game:
    ///   Room 1: weapon(7), monster(10), potion(5), monster(6)
    ///   Room 2: monster(4), monster(3), potion(2), monster(2)
    /// Expected final state: alive, Won.
    /// </summary>
    [Test]
    public void EightCardGame_SurvivesToVictory()
    {
        var w7  = Cards.Weapon(7);
        var m10 = Cards.Monster(10);
        var p5  = Cards.Potion(5);
        var m6  = Cards.Monster(6);

        var m4  = Cards.Monster(4);
        var m3  = Cards.Monster(3);
        var p2  = Cards.Potion(2);
        var m2  = Cards.Monster(2);

        // Deck bottom→top: room 2 at bottom, room 1 at top
        var deck = new[] { m2, p2, m3, m4, m6, p5, m10, w7 };
        var engine = new GameEngine(deck);

        // Room 1: equip weapon, fight m10 (damage=3), drink potion (+5), fight m6 (damage=0 capped)
        engine.TakeCard(w7);             // equip weapon(7)
        engine.TakeCard(m10);            // damage = 10-7 = 3 → HP 17; floor → 10
        engine.TakeCard(p5);             // heal +5 → HP 20 (capped)
        engine.TakeCard(m6);             // 6 < floor(10), weapon: damage = 6-7 = 0 → HP 20; floor → 6

        // Room 2 was auto-dealt
        Assert.That(engine.Room.Count, Is.EqualTo(4));
        Assert.That(engine.Health, Is.EqualTo(20));

        // Room 2: fight all monsters and drink potion
        engine.TakeCard(m4);   // 4 < floor(6), weapon: damage = 4-7 = 0 → HP 20; floor → 4
        engine.TakeCard(p2);   // heal +2 → HP 20 (capped, already full)
        engine.TakeCard(m3);   // 3 < floor(4), weapon: damage = 3-7 = 0 → HP 20; floor → 3
        engine.TakeCard(m2);   // 2 < floor(3), weapon: damage = 2-7 = 0 → HP 20

        Assert.That(engine.Won,      Is.True);
        Assert.That(engine.GameOver, Is.False);
        Assert.That(engine.Health,   Is.EqualTo(20));
    }

    /// <summary>
    /// Run room 1, fight rooms 2 and 3 normally. Verifies run → can't run → can run again.
    /// </summary>
    [Test]
    public void RunThenFight_FlowIsCorrect()
    {
        // 12-card deck: 3 rooms of 4
        var deck = Enumerable.Range(0, 12).Select(i => Cards.Potion(2)).ToList();
        var engine = new GameEngine(deck);

        // Room 1: run
        engine.Run();
        Assert.That(engine.RanLastRoom, Is.True);

        // Room 2: take 3 cards, advance (can't run — RanLastRoom)
        Assert.That(engine.CanRun, Is.False);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.NextRoom(); // clears RanLastRoom

        // Room 3: can run again
        Assert.That(engine.CanRun, Is.True);
    }
}

// ── Bad-path / guard tests ────────────────────────────────────────────────────

[TestFixture]
public class BadPathTests
{
    [Test]
    public void TakeCard_NotInRoom_Throws()
    {
        var engine  = Cards.RoomOf(Cards.Potion(2), Cards.Potion(3), Cards.Weapon(4), Cards.Monster(5));
        var phantom = Cards.Monster(7); // was never in any room
        Assert.Throws<ArgumentException>(() => engine.TakeCard(phantom));
    }

    [Test]
    public void TakeCard_AfterWon_Throws()
    {
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5) };
        var engine = new GameEngine(deck);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        Assert.Throws<InvalidOperationException>(() => engine.TakeCard(Cards.Potion(2)));
    }

    [Test]
    public void NextRoom_AfterGameOver_Throws()
    {
        var engine = Cards.RoomOf(Cards.Monster(9), Cards.Monster(9), Cards.Monster(9), Cards.Potion(2));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        Assert.That(engine.GameOver, Is.True);

        Assert.Throws<InvalidOperationException>(() => engine.NextRoom());
    }

    [Test]
    public void NextRoom_AfterWon_Throws()
    {
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5) };
        var engine = new GameEngine(deck);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        Assert.Throws<InvalidOperationException>(() => engine.NextRoom());
    }

    [Test]
    public void Run_AfterGameOver_Throws()
    {
        var engine = Cards.RoomOf(Cards.Monster(9), Cards.Monster(9), Cards.Monster(9), Cards.Potion(2));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        engine.TakeCard(engine.Room.First(c => c.IsMonster));
        Assert.That(engine.GameOver, Is.True);

        Assert.Throws<InvalidOperationException>(() => engine.Run());
    }

    [Test]
    public void Run_AfterWon_Throws()
    {
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5) };
        var engine = new GameEngine(deck);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.Won, Is.True);

        Assert.Throws<InvalidOperationException>(() => engine.Run());
    }
}
