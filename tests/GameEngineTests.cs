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
        => Assert.That(new CardModel(Suit.Clubs, ScoundrelRules.AceRank).MonsterValue, Is.EqualTo(ScoundrelRules.AceMonsterValue));
    [Test] public void King_MonsterValueIs13()
        => Assert.That(new CardModel(Suit.Spades, 13).MonsterValue, Is.EqualTo(13));
    [Test] public void NumberCard_MonsterValueIsRank()
        => Assert.That(new CardModel(Suit.Clubs, 7).MonsterValue, Is.EqualTo(7));

    [Test] public void WeaponValueIsRank()
        => Assert.That(new CardModel(Suit.Diamonds, 8).WeaponValue, Is.EqualTo(8));
    [Test] public void PotionValueIsRank()
        => Assert.That(new CardModel(Suit.Hearts, 4).PotionValue, Is.EqualTo(4));

    // ── Extended Rules classification (Diamonds/Hearts are rank-aware) ─────────

    [TestCase(2)]
    [TestCase(6)]
    [TestCase(10)]
    public void Diamonds_RankInWeaponRange_IsWeaponNotBlacksmith(int rank)
    {
        var card = new CardModel(Suit.Diamonds, rank);
        Assert.That(card.IsWeapon,    Is.True);
        Assert.That(card.IsBlacksmith, Is.False);
    }

    [TestCase(1)]
    [TestCase(11)]
    [TestCase(12)]
    [TestCase(13)]
    public void Diamonds_RankOutsideWeaponRange_IsBlacksmithNotWeapon(int rank)
    {
        var card = new CardModel(Suit.Diamonds, rank);
        Assert.That(card.IsBlacksmith, Is.True);
        Assert.That(card.IsWeapon,     Is.False);
    }

    [TestCase(2)]
    [TestCase(6)]
    [TestCase(10)]
    public void Hearts_RankInPotionRange_IsPotionNotMerchant(int rank)
    {
        var card = new CardModel(Suit.Hearts, rank);
        Assert.That(card.IsPotion,   Is.True);
        Assert.That(card.IsMerchant, Is.False);
    }

    [TestCase(1)]
    [TestCase(11)]
    [TestCase(12)]
    [TestCase(13)]
    public void Hearts_RankOutsidePotionRange_IsMerchantNotPotion(int rank)
    {
        var card = new CardModel(Suit.Hearts, rank);
        Assert.That(card.IsMerchant, Is.True);
        Assert.That(card.IsPotion,   Is.False);
    }

    [Test]
    public void RedJoker_IsPotionJokerOnly()
    {
        var card = new CardModel(Suit.RedJoker, 0);
        Assert.That(card.IsPotionJoker, Is.True);
        Assert.That(card.IsWeaponJoker, Is.False);
        Assert.That(card.IsMonster,     Is.False);
        Assert.That(card.IsWeapon,      Is.False);
        Assert.That(card.IsPotion,      Is.False);
        Assert.That(card.IsBlacksmith,  Is.False);
        Assert.That(card.IsMerchant,    Is.False);
    }

    [Test]
    public void BlackJoker_IsWeaponJokerOnly()
    {
        var card = new CardModel(Suit.BlackJoker, 0);
        Assert.That(card.IsWeaponJoker, Is.True);
        Assert.That(card.IsPotionJoker, Is.False);
        Assert.That(card.IsMonster,     Is.False);
        Assert.That(card.IsWeapon,      Is.False);
        Assert.That(card.IsPotion,      Is.False);
        Assert.That(card.IsBlacksmith,  Is.False);
        Assert.That(card.IsMerchant,    Is.False);
    }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

file static class Cards
{
    public static CardModel Monster(int rank) => new(Suit.Clubs,    rank);
    public static CardModel Spade(int rank)   => new(Suit.Spades,   rank);
    public static CardModel Weapon(int rank)  => new(Suit.Diamonds, rank);
    public static CardModel Potion(int rank)  => new(Suit.Hearts,   rank);
    public static CardModel RedJoker()        => new(Suit.RedJoker, 0, "joker_red");
    public static CardModel BlackJoker()      => new(Suit.BlackJoker, 0, "joker_black");

    public static CardModel Blacksmith(int rank) => new(Suit.Diamonds, rank, BlacksmithName(rank));
    public static CardModel Merchant(int rank)   => new(Suit.Hearts,   rank, MerchantName(rank));

    private static string BlacksmithName(int rank) => rank switch
    {
        11 => "jack_diamonds",
        12 => "queen_diamonds",
        13 => "king_diamonds",
        ScoundrelRules.AceRank => "ace_diamonds",
        _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Not a Blacksmith rank"),
    };

    private static string MerchantName(int rank) => rank switch
    {
        11 => "jack_hearts",
        12 => "queen_hearts",
        13 => "king_hearts",
        ScoundrelRules.AceRank => "ace_hearts",
        _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Not a Merchant rank"),
    };

    // Pad a short card list to 4 so DealRoom fills the room immediately.
    // Extra padding cards are weak monsters that sit at the bottom of the deck.
    public static CardModel[] PadToFour(params CardModel[] cards)
    {
        var padded = new List<CardModel>(cards);
        while (padded.Count < ScoundrelRules.RoomSize) padded.Insert(0, Monster(2));
        return padded.ToArray();
    }

    // Build a deck whose top RoomSize cards (last in array) are the supplied room cards.
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

        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize));
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
        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize - ScoundrelRules.MinCardsTaken));
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
        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize)); // leftover + 3 new
    }

    [Test]
    public void NextRoom_ResetsCardsTakenThisRoom()
    {
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), // room 2
            Cards.Potion(3), Cards.Potion(4), Cards.Potion(5), Cards.Potion(6)  // room 1
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(ScoundrelRules.MinCardsTaken));

        engine.NextRoom();

        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(0));
    }

    [Test]
    public void NextRoom_BeforeMinCardsTaken_Throws()
    {
        var engine = Cards.RoomOf(
            Cards.Potion(2), Cards.Potion(3), Cards.Weapon(4), Cards.Monster(5));

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);

        Assert.Throws<InvalidOperationException>(() => engine.NextRoom(), $"Must take at least {ScoundrelRules.MinCardsTaken} cards");
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

        int deckBefore = engine.Deck.Count; // RoomSize (8 total, RoomSize dealt to room)
        engine.Run();

        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize));      // new room dealt
        Assert.That(engine.Deck.Count, Is.EqualTo(deckBefore)); // same deck size (RoomSize back in, RoomSize dealt out)
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

        Assert.That(engine.CanRun, Is.True, "Should be able to run after NextRoom");
    }

    [Test]
    public void RunReturnsAllFourRoomCards_ToDeck()
    {
        var roomCards = new[]
        {
            Cards.Monster(3), Cards.Monster(4), Cards.Weapon(5), Cards.Potion(6)
        };
        // 8-card deck: RoomSize filler at bottom, RoomSize room cards at top
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), Cards.Potion(2),
            roomCards[0], roomCards[1], roomCards[2], roomCards[3]
        };
        var engine = new GameEngine(deck);
        // Room is dealt; deck has RoomSize filler cards
        Assert.That(engine.Deck.Count, Is.EqualTo(ScoundrelRules.RoomSize));

        engine.Run();

        // The RoomSize room cards went back to deck, then RoomSize were dealt to new room
        Assert.That(engine.Deck.Count, Is.EqualTo(ScoundrelRules.RoomSize)); // RoomSize (back) + RoomSize filler - RoomSize (dealt) = RoomSize
        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize));
    }

    [Test]
    public void Run_PartialRoom_OnlyRemainingCardsSinkToBottom()
    {
        var m3 = Cards.Monster(3);
        var m4 = Cards.Monster(4);
        var w5 = Cards.Weapon(5);
        var p6 = Cards.Potion(6);
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), Cards.Potion(2), // filler
            m3, m4, w5, p6  // room 1 (top, dealt first)
        };
        var engine = new GameEngine(deck);

        // Take one card — 3 remain in the room.
        engine.TakeCard(p6);
        Assert.That(engine.Room.Count, Is.EqualTo(3));

        engine.Run();

        // New room was dealt (RoomSize cards).
        Assert.That(engine.Room.Count, Is.EqualTo(ScoundrelRules.RoomSize));
        // Only the 3 remaining cards sank; the taken potion is NOT in the deck.
        Assert.That(engine.Deck.Count, Is.EqualTo(3)); // 3 sank + RoomSize filler - RoomSize dealt
        Assert.That(engine.Deck.Take(3), Is.EquivalentTo(new[] { m3, m4, w5 }));
        Assert.That(engine.Deck, Does.Not.Contain(p6));
    }

    [Test]
    public void Run_ResetsPotionUsedThisRoom()
    {
        var p5 = Cards.Potion(5);
        var deck = new[]
        {
            Cards.Monster(2), Cards.Monster(2), Cards.Monster(2), Cards.Monster(2), // room 2
            Cards.Monster(3), Cards.Weapon(4), Cards.Monster(5), p5                 // room 1
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(p5);
        Assert.That(engine.PotionUsedThisRoom, Is.True, "Potion should be marked as used");

        engine.Run();

        Assert.That(engine.PotionUsedThisRoom, Is.False, "Potion used flag should reset after run");
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
        Assert.That(engine.Deck.Take(ScoundrelRules.RoomSize),  Is.EquivalentTo(room1));
        Assert.That(engine.Deck.Skip(ScoundrelRules.RoomSize),  Is.EquivalentTo(room3));
    }
}

// ── Win / lose ────────────────────────────────────────────────────────────────

[TestFixture]
public class WinLoseTests
{
    [Test]
    public void TakeAllCards_Won()
    {
        // RoomSize-card deck → one room, take all RoomSize → win
        var deck = new[]
        {
            Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5)
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);

        Assert.That(engine.Won, Is.True, "Should win after taking all deck cards");
        Assert.That(engine.GameOver, Is.False, "GameOver should be false when winning");
    }

    [Test]
    public void TakeLastCard_InPartialFinalRoom_Wins()
    {
        // (RoomSize+1)-card deck: room 1 gets RoomSize, the extra card is alone in room 2.
        var finalCard = Cards.Potion(2);
        var deck = new[]
        {
            finalCard,
            Cards.Potion(3), Cards.Potion(4), Cards.Potion(5), Cards.Potion(6) // room 1
        };
        var engine = new GameEngine(deck);

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]); // auto-deals room 2 (just finalCard)

        Assert.That(engine.Room.Count, Is.EqualTo(1), "Final room should have 1 card");
        Assert.That(engine.Room[0], Is.EqualTo(finalCard), "Final card should be the remaining card");

        engine.TakeCard(finalCard);

        Assert.That(engine.Won, Is.True);
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

// ── Extended Rules infrastructure (Blacksmith/Merchant/Joker placeholders) ─────

[TestFixture]
public class ExtendedRulesPlaceholderTests
{
    [Test]
    public void ExtendedRulesFlag_DefaultsFalse()
    {
        var engine = Cards.RoomOf(Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Monster(5));
        Assert.That(engine.ExtendedRules, Is.False);
    }

    [Test]
    public void ExtendedRulesFlag_IsExposedWhenTrue()
    {
        var deck   = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5) };
        var engine = new GameEngine(deck, extendedRules: true);
        Assert.That(engine.ExtendedRules, Is.True);
    }

    // NOTE: Superseded by BlacksmithTests below (chunk 4 implements the real Blacksmith
    // mechanic). Updated in place rather than left contradicting the new behavior: the
    // chunk-1 placeholder discarded the Blacksmith card unconditionally; the real mechanic
    // recycles it into the deck (rather than discarding) whenever there's no equipped
    // weapon to blacksmith.
    [Test]
    public void TakingBlacksmithCard_WithNoWeaponEquipped_RecyclesIntoDeck_NotDiscard()
    {
        var blacksmith = new CardModel(Suit.Diamonds, 11, "jack_diamonds");
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), blacksmith };
        var engine = new GameEngine(deck, extendedRules: true);
        int healthBefore = engine.Health;

        Assert.DoesNotThrow(() => engine.TakeCard(blacksmith));

        Assert.That(engine.Deck, Contains.Item(blacksmith));
        Assert.That(engine.Discard, Does.Not.Contain(blacksmith));
        Assert.That(engine.Health, Is.EqualTo(healthBefore));
        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(1));
    }

    // NOTE: Superseded by MerchantTests below (chunk 5 implements the real Merchant
    // mechanic). Updated in place rather than left contradicting the new behavior: the
    // chunk-1 placeholder discarded the Merchant card unconditionally; the real mechanic
    // recycles it into the deck (rather than discarding) whenever there's no equipped
    // weapon to sell.
    [Test]
    public void TakingMerchantCard_WithNoWeaponEquipped_RecyclesIntoDeck_NotDiscard()
    {
        var merchant = Cards.Merchant(12);
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), merchant };
        var engine = new GameEngine(deck, extendedRules: true);
        int healthBefore = engine.Health;

        Assert.DoesNotThrow(() => engine.TakeCard(merchant));

        Assert.That(engine.Deck, Contains.Item(merchant));
        Assert.That(engine.Discard, Does.Not.Contain(merchant));
        Assert.That(engine.Health, Is.EqualTo(healthBefore));
        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(1));
    }

    // NOTE: Superseded by PotionPocketTests below (chunk 2 implements the real Red Joker
    // mechanic). Updated in place rather than left contradicting the new behavior: the
    // chunk-1 placeholder discarded the joker; the real mechanic keeps it as a permanent
    // companion that is never discarded.
    [Test]
    public void TakingRedJoker_SetsHasPotionJoker_DoesNotDiscard_CountsTowardRoom()
    {
        var joker = new CardModel(Suit.RedJoker, 0, "joker_red");
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), joker };
        var engine = new GameEngine(deck, extendedRules: true);
        int healthBefore = engine.Health;

        Assert.DoesNotThrow(() => engine.TakeCard(joker));

        Assert.That(engine.HasPotionJoker, Is.True);
        Assert.That(engine.PotionJokerHealth, Is.EqualTo(8), "Flat starting HP, no randomness");
        Assert.That(engine.Discard, Does.Not.Contain(joker));
        Assert.That(engine.Health, Is.EqualTo(healthBefore));
        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(1));
    }

    // NOTE: Superseded by WeaponPocketTests below (chunk 3 implements the real Black Joker
    // mechanic). Updated in place rather than left contradicting the new behavior: the
    // chunk-1 placeholder discarded the joker; the real mechanic keeps it as a permanent
    // companion that is never discarded.
    [Test]
    public void TakingBlackJoker_SetsHasWeaponJoker_DoesNotDiscard_CountsTowardRoom()
    {
        var joker = new CardModel(Suit.BlackJoker, 0, "joker_black");
        var deck = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), joker };
        var engine = new GameEngine(deck, extendedRules: true);
        int healthBefore = engine.Health;

        Assert.DoesNotThrow(() => engine.TakeCard(joker));

        Assert.That(engine.HasWeaponJoker, Is.True);
        Assert.That(engine.WeaponJokerHealth, Is.EqualTo(8), "Flat starting HP, no randomness");
        Assert.That(engine.Discard, Does.Not.Contain(joker));
        Assert.That(engine.Health, Is.EqualTo(healthBefore));
        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.CardsTakenThisRoom, Is.EqualTo(1));
    }
}

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

// ── Blacksmith — Diamond Face Cards (PRD §6, item 3) ───────────────────────────

[TestFixture]
public class BlacksmithTests
{
    [Test]
    public void CanUseBlacksmith_FalseForCardNotInRoom()
    {
        var engine = Cards.RoomOf(Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Weapon(5));
        var phantom = Cards.Blacksmith(11); // never dealt into any room

        Assert.That(engine.CanUseBlacksmith(phantom), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.UseBlacksmith(phantom));
    }

    [Test]
    public void CanUseBlacksmith_TrueEvenWithoutEquippedWeapon()
    {
        var jack = Cards.Blacksmith(11);
        var engine = Cards.RoomOf(jack, Cards.Potion(2), Cards.Potion(3), Cards.Potion(4));

        Assert.That(engine.EquippedWeapon, Is.Null);
        Assert.That(engine.CanUseBlacksmith(jack), Is.True,
            "'Cannot be used' (no weapon) is handled by recycling inside UseBlacksmith, not by blocking the call");
    }

    // ── Removal from a nonzero SlainMonsterCount ────────────────────────────────

    [Test]
    public void UseBlacksmith_Jack_RemovesOne_FromNonzeroSlainCount()
    {
        var weapon   = Cards.Weapon(10);
        var monster9 = Cards.Monster(9);
        var monster8 = Cards.Spade(8);
        var jack     = Cards.Blacksmith(11);
        var engine   = Cards.RoomOf(weapon, monster9, monster8, jack);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // blocked -> slain 1, floor 9
        engine.TakeCard(monster8); // blocked -> slain 2, floor 8
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(2));

        engine.UseBlacksmith(jack);

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));
        Assert.That(engine.Discard, Contains.Item(jack));
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(0), "No bonus is granted when there was something to remove");
    }

    [Test]
    public void UseBlacksmith_RemovalReachingZero_ResetsWeaponFloor()
    {
        // Weapon blocks a 9, degrading WeaponFloor to 9 -- it can no longer block
        // anything >= 9. Removing the only slain monster should restore the floor
        // to unrestricted, since nothing is left degrading the weapon.
        var weapon    = Cards.Weapon(10);
        var monster9  = Cards.Monster(9);
        var monster10 = Cards.Spade(10);
        var jack      = Cards.Blacksmith(11);
        var engine    = Cards.RoomOf(weapon, monster9, monster10, jack);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // blocked -> slain 1, floor 9
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));
        Assert.That(engine.WeaponFloor, Is.EqualTo(9));

        engine.UseBlacksmith(jack); // removes the only slain monster -> count 0

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
        Assert.That(engine.WeaponFloor, Is.EqualTo(int.MaxValue),
            "A weapon with nothing attached should be fully fresh, not still restricted by past use");
        Assert.That(ScoundrelRules.CanUseWeapon(monster10.MonsterValue, engine.WeaponFloor), Is.True,
            "The weapon must now be able to block a monster the stale floor would have rejected");
    }

    [Test]
    public void UseBlacksmith_PartialRemoval_LeavesWeaponFloorDegraded()
    {
        // Removing some (not all) slain monsters shouldn't un-degrade the floor --
        // there's still recent-use history left attached to the weapon.
        var weapon   = Cards.Weapon(10);
        var monster9 = Cards.Monster(9);
        var monster8 = Cards.Spade(8);
        var jack     = Cards.Blacksmith(11);
        var engine   = Cards.RoomOf(weapon, monster9, monster8, jack);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // blocked -> slain 1, floor 9
        engine.TakeCard(monster8); // blocked -> slain 2, floor 8
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(2));

        engine.UseBlacksmith(jack); // removes 1 of 2 -> count 1, still nonzero

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));
        Assert.That(engine.WeaponFloor, Is.EqualTo(8), "Floor is untouched while the weapon still has slain monsters attached");
    }

    [Test]
    public void UseBlacksmith_Queen_RemovesTwo_FromNonzeroSlainCount()
    {
        var weapon   = Cards.Weapon(10);
        var monster9 = Cards.Monster(9);
        var monster8 = Cards.Spade(8);
        var monster7 = Cards.Monster(7);
        var queen    = Cards.Blacksmith(12);
        var filler1  = Cards.Potion(2);
        var filler2  = Cards.Potion(3);
        var filler3  = Cards.Potion(4);

        // Deck bottom -> top. Room 1 = {weapon, monster9, monster8, filler1}; room 2 refill
        // (dealt once room 1 empties) = {monster7, queen, filler2, filler3}.
        var deck = new[] { monster7, queen, filler2, filler3, weapon, monster9, monster8, filler1 };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // slain 1, floor 9
        engine.TakeCard(monster8); // slain 2, floor 8
        engine.TakeCard(filler1);  // empties room 1 -> deals room 2
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(2));

        engine.TakeCard(monster7); // slain 3, floor 7
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(3));

        engine.UseBlacksmith(queen);

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));
        Assert.That(engine.Discard, Contains.Item(queen));
    }

    [Test]
    public void UseBlacksmith_King_RemovesThree_FromNonzeroSlainCount()
    {
        var weapon   = Cards.Weapon(10);
        var monster9 = Cards.Monster(9);
        var monster8 = Cards.Spade(8);
        var monster7 = Cards.Monster(7);
        var monster6 = Cards.Spade(6);
        var king     = Cards.Blacksmith(13);
        var filler1  = Cards.Potion(2);
        var filler2  = Cards.Potion(3);

        // Deck bottom -> top. Room 1 = {weapon, monster9, monster8, monster7}; room 2 refill
        // (dealt once room 1 empties) = {monster6, king, filler1, filler2}.
        var deck = new[] { monster6, king, filler1, filler2, weapon, monster9, monster8, monster7 };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // slain 1, floor 9
        engine.TakeCard(monster8); // slain 2, floor 8
        engine.TakeCard(monster7); // empties room 1 -> deals room 2; slain 3, floor 7
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(3));

        engine.TakeCard(monster6); // slain 4, floor 6
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(4));

        engine.UseBlacksmith(king);

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));
        Assert.That(engine.Discard, Contains.Item(king));
    }

    [Test]
    public void UseBlacksmith_King_ClampsAtZero_DoesNotGoNegative()
    {
        var weapon   = Cards.Weapon(10);
        var monster9 = Cards.Monster(9);
        var king     = Cards.Blacksmith(13);
        var filler1  = Cards.Potion(2);
        var engine   = Cards.RoomOf(weapon, monster9, king, filler1);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // slain 1
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));

        engine.UseBlacksmith(king); // would remove 3, but must clamp at 0

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
    }

    [Test]
    public void UseBlacksmith_Ace_RemovesAll_FromNonzeroSlainCount()
    {
        var weapon   = Cards.Weapon(10);
        var monster9 = Cards.Monster(9);
        var monster8 = Cards.Spade(8);
        var monster7 = Cards.Monster(7);
        var ace      = Cards.Blacksmith(ScoundrelRules.AceRank);
        var filler1  = Cards.Potion(2);
        var filler2  = Cards.Potion(3);
        var filler3  = Cards.Potion(4);

        // Deck bottom -> top. Room 1 = {weapon, monster9, monster8, filler1}; room 2 refill
        // (dealt once room 1 empties) = {monster7, ace, filler2, filler3}.
        var deck = new[] { monster7, ace, filler2, filler3, weapon, monster9, monster8, filler1 };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // slain 1, floor 9
        engine.TakeCard(monster8); // slain 2, floor 8
        engine.TakeCard(filler1);  // empties room 1 -> deals room 2
        engine.TakeCard(monster7); // slain 3, floor 7
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(3));

        engine.UseBlacksmith(ace);

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(0));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(0), "No bonus branch taken since count was nonzero");
    }

    // ── Bonus grant when SlainMonsterCount is already 0 ─────────────────────────

    [TestCase(11, 1)]
    [TestCase(12, 2)]
    [TestCase(13, 3)]
    public void UseBlacksmith_FaceCard_OnZeroSlainCount_GrantsPermanentWeaponAttackBonus(int rank, int expectedBonus)
    {
        var weapon    = Cards.Weapon(5);
        var faceCard  = Cards.Blacksmith(rank);
        var fillerA   = Cards.Potion(2);
        var fillerB   = Cards.Potion(3);
        var monster1  = Cards.Monster(9);
        var monster2  = Cards.Spade(8);
        var fillerC   = Cards.Potion(4);
        var fillerD   = Cards.Potion(6);

        // Deck bottom -> top. Room 1 = {weapon, faceCard, fillerA, fillerB}; room 2 refill
        // (dealt once room 1 empties) = {monster1, monster2, fillerC, fillerD}.
        var deck = new[] { monster2, monster1, fillerC, fillerD, weapon, faceCard, fillerA, fillerB };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(weapon);
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));

        engine.UseBlacksmith(faceCard); // slain 0 -> nothing to remove, grants the bonus instead

        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(expectedBonus));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(0));
        Assert.That(engine.Discard, Contains.Item(faceCard));

        engine.TakeCard(fillerA);
        engine.TakeCard(fillerB); // empties room 1 -> deals room 2

        int healthBeforeFirstFight = engine.Health;
        engine.TakeCard(monster1); // blocked; effective weapon = 5 + bonus
        int expectedDamage1 = ScoundrelRules.CalcDamage(monster1.MonsterValue, weapon.WeaponValue + expectedBonus);
        Assert.That(engine.Health, Is.EqualTo(Math.Max(0, healthBeforeFirstFight - expectedDamage1)));
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));

        int healthBeforeSecondFight = engine.Health;
        engine.TakeCard(monster2); // second fight — bonus must still apply (permanent)
        int expectedDamage2 = ScoundrelRules.CalcDamage(monster2.MonsterValue, weapon.WeaponValue + expectedBonus);
        Assert.That(engine.Health, Is.EqualTo(Math.Max(0, healthBeforeSecondFight - expectedDamage2)));
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(expectedBonus), "Bonus is permanent, survives multiple fights");
    }

    [Test]
    public void UseBlacksmith_Ace_OnZeroSlainCount_GrantsSingleUseBonus_ConsumedAfterOneFight()
    {
        var weapon   = Cards.Weapon(5);
        var ace      = Cards.Blacksmith(ScoundrelRules.AceRank);
        var fillerA  = Cards.Potion(2);
        var fillerB  = Cards.Potion(3);
        var monster1 = Cards.Monster(9);
        var monster2 = Cards.Spade(8);
        var fillerC  = Cards.Potion(4);
        var fillerD  = Cards.Potion(6);

        // Deck bottom -> top. Room 1 = {weapon, ace, fillerA, fillerB}; room 2 refill (dealt
        // once room 1 empties) = {monster1, monster2, fillerC, fillerD}.
        var deck = new[] { monster2, monster1, fillerC, fillerD, weapon, ace, fillerA, fillerB };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(weapon);
        engine.UseBlacksmith(ace); // slain 0 -> grants the single-use "Excalibur" bonus

        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(4));
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(0), "Ace's bonus is single-use, not the permanent track");

        engine.TakeCard(fillerA);
        engine.TakeCard(fillerB); // empties room 1 -> deals room 2

        int healthBeforeFirstFight = engine.Health;
        engine.TakeCard(monster1); // weapon-blocked fight consumes the single-use bonus
        int expectedDamage1 = ScoundrelRules.CalcDamage(monster1.MonsterValue, weapon.WeaponValue + 4);
        Assert.That(engine.Health, Is.EqualTo(Math.Max(0, healthBeforeFirstFight - expectedDamage1)));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(0), "Consumed after the fight that used it");

        int healthBeforeSecondFight = engine.Health;
        engine.TakeCard(monster2); // second fight — bonus must NOT apply again
        int expectedDamage2 = ScoundrelRules.CalcDamage(monster2.MonsterValue, weapon.WeaponValue);
        Assert.That(engine.Health, Is.EqualTo(Math.Max(0, healthBeforeSecondFight - expectedDamage2)));
    }

    [Test]
    public void ApplyMonsterDamage_UsesWeaponValue_PlusAttackBonus_PlusSingleUseBonus_Combined()
    {
        var weapon   = Cards.Weapon(3);
        var jack     = Cards.Blacksmith(11);
        var ace      = Cards.Blacksmith(ScoundrelRules.AceRank);
        var fillerA  = Cards.Potion(2);
        var monster1 = Cards.Monster(9);
        var monster2 = Cards.Spade(6);
        var fillerB  = Cards.Potion(4);
        var fillerC  = Cards.Potion(5);

        // Deck bottom -> top. Room 1 = {weapon, jack, ace, fillerA}; room 2 refill (dealt
        // once room 1 empties) = {fillerB, fillerC, monster2, monster1}.
        var deck = new[] { fillerC, fillerB, monster2, monster1, weapon, jack, ace, fillerA };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(weapon);
        engine.UseBlacksmith(jack); // slain 0 -> WeaponAttackBonus = 1
        engine.UseBlacksmith(ace);  // slain still 0 -> SingleUseWeaponBonus = 4
        engine.TakeCard(fillerA);   // empties room 1 -> deals room 2

        int healthBefore = engine.Health;
        engine.TakeCard(monster1); // MonsterValue 9; effective weapon = 3 + 1 + 4 = 8
        int expectedDamage = ScoundrelRules.CalcDamage(monster1.MonsterValue, weapon.WeaponValue + 1 + 4);
        Assert.That(expectedDamage, Is.EqualTo(1), "sanity check on the arithmetic");
        Assert.That(engine.Health, Is.EqualTo(healthBefore - expectedDamage));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(0), "Single-use bonus consumed after this fight");
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(1), "Permanent bonus unaffected");

        int healthBefore2 = engine.Health;
        engine.TakeCard(monster2); // MonsterValue 6; effective weapon = 3 + 1 = 4 (no single-use left)
        int expectedDamage2 = ScoundrelRules.CalcDamage(monster2.MonsterValue, weapon.WeaponValue + 1);
        Assert.That(engine.Health, Is.EqualTo(healthBefore2 - expectedDamage2));
    }

    [Test]
    public void EquipWeapon_ResetsWeaponAttackBonusAndSingleUseWeaponBonus()
    {
        var weapon1 = Cards.Weapon(5);
        var jack    = Cards.Blacksmith(11);
        var ace     = Cards.Blacksmith(ScoundrelRules.AceRank);
        var weapon2 = Cards.Weapon(7);
        var engine  = Cards.RoomOf(weapon1, jack, ace, weapon2);

        engine.TakeCard(weapon1);
        engine.UseBlacksmith(jack); // slain 0 -> WeaponAttackBonus = 1
        engine.UseBlacksmith(ace);  // slain 0 -> SingleUseWeaponBonus = 4
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(1));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(4));

        engine.TakeCard(weapon2); // equip a new weapon

        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(0));
        Assert.That(engine.SingleUseWeaponBonus, Is.EqualTo(0));
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
    }

    // ── Recycle instead of discard ───────────────────────────────────────────────

    [TestCase(true)]
    [TestCase(false)]
    public void UseBlacksmith_NoWeaponEquipped_RecyclesToDeck_RegardlessOfActivateFlag(bool activate)
    {
        var jack    = Cards.Blacksmith(11);
        var filler1 = Cards.Potion(2);
        var filler2 = Cards.Potion(3);
        var filler3 = Cards.Potion(4);
        var engine  = Cards.RoomOf(jack, filler1, filler2, filler3);

        Assert.That(engine.EquippedWeapon, Is.Null);

        engine.UseBlacksmith(jack, activate);

        Assert.That(engine.Deck, Contains.Item(jack));
        Assert.That(engine.Discard, Does.Not.Contain(jack));
        Assert.That(engine.Room, Does.Not.Contain(jack));
    }

    [Test]
    public void UseBlacksmith_ActivateFalse_WithWeaponEquipped_Recycles_DoesNotApplyEffect()
    {
        var weapon  = Cards.Weapon(5);
        var jack    = Cards.Blacksmith(11);
        var filler1 = Cards.Potion(2);
        var filler2 = Cards.Potion(3);
        var engine  = Cards.RoomOf(weapon, jack, filler1, filler2);

        engine.TakeCard(weapon);
        int bonusBefore = engine.WeaponAttackBonus;

        engine.UseBlacksmith(jack, activate: false);

        Assert.That(engine.Deck, Contains.Item(jack));
        Assert.That(engine.Discard, Does.Not.Contain(jack));
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(bonusBefore), "Declined card must not apply its effect");
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
    }

    [Test]
    public void UseBlacksmith_Recycle_UsesInjectedRng_ForDeterministicPlacement()
    {
        var jack    = Cards.Blacksmith(11);
        var filler1 = Cards.Potion(2);
        var filler2 = Cards.Potion(3);
        var filler3 = Cards.Potion(4);
        var pad1    = Cards.Potion(5);
        var pad2    = Cards.Potion(6);
        var pad3    = Cards.Potion(7);
        var pad4    = Cards.Potion(8);

        // 8-card deck. Room 1 = {jack, filler1, filler2, filler3}; pad1..4 remain in the
        // deck (4 cards), so recycling jack has 5 possible insertion positions — enough to
        // make the seeded RNG's choice observable.
        var deck = new[] { pad1, pad2, pad3, pad4, jack, filler1, filler2, filler3 };
        const int seed = 42;
        var engine = new GameEngine(deck, extendedRules: true, rng: new Random(seed));

        engine.UseBlacksmith(jack, activate: false);

        // Recompute the expected index with a fresh Random seeded identically: GameEngine's
        // only RNG call during this action is rng.Next(0, deckCountBeforeInsert + 1), which
        // equals Next(0, engine.Deck.Count) once the insert has happened.
        var expectedRng = new Random(seed);
        int expectedIndex = expectedRng.Next(0, engine.Deck.Count);

        Assert.That(engine.Deck[expectedIndex], Is.EqualTo(jack));
        Assert.That(engine.Discard, Does.Not.Contain(jack));
        Assert.That(engine.Room, Does.Not.Contain(jack));
    }

    [Test]
    public void UseBlacksmith_Recycle_UsesRngRatherThanHardcodedIndex()
    {
        int IndexForSeed(int seed)
        {
            var jack = Cards.Blacksmith(11);
            var deck = new[]
            {
                Cards.Potion(3), Cards.Potion(4), Cards.Potion(5), Cards.Potion(6), // stays in deck
                jack, Cards.Potion(2), Cards.Potion(7), Cards.Potion(8),            // room 1
            };
            var engine = new GameEngine(deck, extendedRules: true, rng: new Random(seed));
            engine.UseBlacksmith(jack, activate: false);
            return engine.Deck.ToList().IndexOf(jack);
        }

        var indices = Enumerable.Range(0, 20).Select(IndexForSeed).ToList();

        Assert.That(indices.Distinct().Count(), Is.GreaterThan(1),
            "Different seeds should be able to land the recycled card at different indices, " +
            "proving the RNG is actually consulted rather than a hardcoded position being used");
    }

    // ── Independence from the Black Joker's pocket weapon ───────────────────────

    [Test]
    public void UseBlacksmith_DoesNotAffect_BlackJokerPocketWeapon()
    {
        var blackJoker   = Cards.BlackJoker();
        var pocketWeapon = Cards.Weapon(6);
        var mainWeapon   = Cards.Weapon(5);
        var jack         = Cards.Blacksmith(11);
        var engine       = Cards.RoomOf(blackJoker, pocketWeapon, mainWeapon, jack);

        engine.TakeCard(blackJoker);      // HasWeaponJoker = true
        engine.StoreWeapon(pocketWeapon); // pocket weapon set
        engine.TakeCard(mainWeapon);      // equip main weapon

        Assert.That(engine.PocketedWeapon, Is.EqualTo(pocketWeapon));

        engine.UseBlacksmith(jack); // slain 0 on the main weapon -> grants WeaponAttackBonus

        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(1));
        Assert.That(engine.PocketedWeapon, Is.EqualTo(pocketWeapon), "Blacksmith must not touch the pocket weapon");
    }

    // ── TakeCard integration (routes to the same ApplyBlacksmithEffect as UseBlacksmith) ─

    [Test]
    public void TakingBlacksmithCard_ThroughTakeCard_WithWeaponEquipped_AppliesEffect()
    {
        var weapon  = Cards.Weapon(10);
        var monster = Cards.Monster(9);
        var jack    = Cards.Blacksmith(11);
        var filler  = Cards.Potion(2);
        var engine  = Cards.RoomOf(weapon, monster, jack, filler);

        engine.TakeCard(weapon);
        engine.TakeCard(monster); // blocked -> slain 1
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));

        engine.TakeCard(jack); // routed through TakeCard's IsBlacksmith branch, not UseBlacksmith directly

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(0));
        Assert.That(engine.Discard, Contains.Item(jack));
    }

    [Test]
    public void TakingBlacksmithCard_ThroughTakeCard_ActivateFalse_RecyclesEvenWithWeaponEquipped()
    {
        var weapon  = Cards.Weapon(10);
        var jack    = Cards.Blacksmith(11);
        var filler1 = Cards.Potion(2);
        var filler2 = Cards.Potion(3);
        var engine  = Cards.RoomOf(weapon, jack, filler1, filler2);

        engine.TakeCard(weapon);
        engine.TakeCard(jack, activateCard: false); // declined -> recycle, not TakeCard's plain discard

        Assert.That(engine.Deck, Contains.Item(jack));
        Assert.That(engine.Discard, Does.Not.Contain(jack));
        Assert.That(engine.WeaponAttackBonus, Is.EqualTo(0));
    }

    // ── Game-over / won guards ───────────────────────────────────────────────────

    [Test]
    public void UseBlacksmith_AfterWon_Throws()
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

        // Won implies an empty room, so a Blacksmith card can never legitimately still be
        // "in the room" at this point — CanUseBlacksmith is false via _room.Contains as well
        // as !IsOver. The GameOver test below isolates the !IsOver gate with a card that
        // does remain in the room.
        var jack = Cards.Blacksmith(11); // never dealt into any room
        Assert.That(engine.CanUseBlacksmith(jack), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.UseBlacksmith(jack));
    }

    [Test]
    public void UseBlacksmith_AfterGameOver_Throws_CardStaysInRoom()
    {
        var jack     = Cards.Blacksmith(11);
        var m9a      = Cards.Monster(9);
        var m9b      = Cards.Monster(9);
        var m9c      = Cards.Monster(9);
        var engine   = Cards.RoomOf(jack, m9a, m9b, m9c);

        engine.TakeCard(m9a); // 20 -> 11
        engine.TakeCard(m9b); // 11 -> 2
        engine.TakeCard(m9c); // 2 -> 0 -> GameOver; jack was never taken

        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.Room, Contains.Item(jack), "Jack remains in the room, untouched");
        Assert.That(engine.CanUseBlacksmith(jack), Is.False);
        Assert.Throws<InvalidOperationException>(() => engine.UseBlacksmith(jack));
    }
}

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

// ── Extended Rules: friendly-NPC-only win condition (PRD §6) ───────────────────
//
// With Extended Rules active, a no-weapon player recycles every declined Blacksmith/
// Merchant card back into the deck instead of discarding it (see ApplyBlacksmithEffect/
// ApplyMerchantEffect), so if the only cards left in the deck+room are Blacksmith/Merchant,
// the deck can never actually empty via the classic win path. GameEngine.FinishRoomAction
// resolves this by winning immediately once every remaining deck+room card is a Blacksmith
// or Merchant card.

[TestFixture]
public class FriendlyNpcWinTests
{
    [Test]
    public void AllBlacksmithAndMerchant_TriggersImmediateWin()
    {
        var deck = new[]
        {
            Cards.Blacksmith(11),
            Cards.Merchant(12),
            Cards.Blacksmith(13),
            Cards.Merchant(ScoundrelRules.AceRank),
        };
        var engine = new GameEngine(deck, extendedRules: true);
        Assert.That(engine.Deck, Is.Empty, "sanity check: all 4 cards dealt into the room");

        // No weapon equipped -> the taken card is recycled back into the deck rather than
        // discarded, but it's still a Blacksmith/Merchant card, so the deck+room pool
        // remains entirely friendly NPCs.
        engine.TakeCard(engine.Room[0]);

        Assert.That(engine.Won, Is.True);
        Assert.That(engine.GameOver, Is.False);
    }

    [Test]
    public void SingleMonsterAmongFriendlyNpcs_BlocksWin()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.Monster(5) };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room.First(c => c.IsBlacksmith)); // no weapon -> recycled

        Assert.That(engine.Won, Is.False, "a monster still in the room must block the win");
    }

    [Test]
    public void SinglePotionAmongFriendlyNpcs_BlocksWin()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.Potion(5) };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room.First(c => c.IsBlacksmith));

        Assert.That(engine.Won, Is.False, "a potion still in the room must block the win");
    }

    [Test]
    public void SingleWeaponAmongFriendlyNpcs_BlocksWin()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.Weapon(5) };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room.First(c => c.IsBlacksmith));

        Assert.That(engine.Won, Is.False, "a weapon still in the room must block the win");
    }

    [Test]
    public void UndrawnRedJoker_BlocksWin()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.RedJoker() };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room.First(c => c.IsBlacksmith));

        Assert.That(engine.Won, Is.False, "the undrawn Red Joker still sits in the room and must block the win");
    }

    [Test]
    public void UndrawnBlackJoker_BlocksWin()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.BlackJoker() };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room.First(c => c.IsBlacksmith));

        Assert.That(engine.Won, Is.False, "the undrawn Black Joker still sits in the room and must block the win");
    }

    [Test]
    public void TakingRedJoker_LeavesOnlyFriendlyNpcs_WinsImmediately()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.RedJoker() };
        var engine = new GameEngine(deck, extendedRules: true);
        var joker  = engine.Room.First(c => c.IsPotionJoker);

        engine.TakeCard(joker); // becomes a companion, not a card in deck/room any more

        Assert.That(engine.HasPotionJoker, Is.True);
        Assert.That(engine.Won, Is.True,
            "an already-taken joker is a companion, not a deck/room card, so it must not block the win");
    }

    [Test]
    public void TakingBlackJoker_LeavesOnlyFriendlyNpcs_WinsImmediately()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.BlackJoker() };
        var engine = new GameEngine(deck, extendedRules: true);
        var joker  = engine.Room.First(c => c.IsWeaponJoker);

        engine.TakeCard(joker);

        Assert.That(engine.HasWeaponJoker, Is.True);
        Assert.That(engine.Won, Is.True,
            "an already-taken joker is a companion, not a deck/room card, so it must not block the win");
    }

    [Test]
    public void EmptyDeckAndRoom_DoesNotWinViaFriendlyNpcCheck_OldExhaustedWinStillFires()
    {
        // Classic-only deck (no Blacksmith/Merchant cards at all). Exhausting it empties
        // deck and room together -- the pre-existing "deck exhausted" win path, distinct
        // from (and unaffected by) the new friendly-NPC-only check's vacuous-empty guard.
        var deck   = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5) };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);

        Assert.That(engine.Deck, Is.Empty);
        Assert.That(engine.Room, Is.Empty);
        Assert.That(engine.Won, Is.True);
        Assert.That(engine.GameOver, Is.False);
    }

    [Test]
    public void GameOver_TakesPriorityOver_FriendlyNpcWin_InSameAction()
    {
        // Room = {Monster(6), Monster(Ace)=14, Merchant(12), Blacksmith(11)}, deck empty
        // once dealt. Taking the two monsters bare-handed drops Health exactly to 0 on the
        // same action that would otherwise leave only Blacksmith/Merchant cards behind.
        var monster6   = Cards.Monster(6);
        var monsterAce = Cards.Monster(ScoundrelRules.AceRank);
        var merchant   = Cards.Merchant(12);
        var blacksmith = Cards.Blacksmith(11);
        var deck       = new[] { blacksmith, merchant, monsterAce, monster6 };
        var engine     = new GameEngine(deck, extendedRules: true);
        Assert.That(engine.Deck, Is.Empty, "sanity check: all 4 cards dealt into the room");

        engine.TakeCard(monster6, useWeapon: false); // 20 -> 14
        Assert.That(engine.Health, Is.EqualTo(14), "sanity check on the damage arithmetic");
        Assert.That(engine.Won, Is.False);

        engine.TakeCard(monsterAce, useWeapon: false); // 14 -> 0 -> GameOver
        // Remaining room ({merchant, blacksmith}) is now entirely friendly NPCs, but
        // GameOver must win the race -- the same early-return FinishRoomAction already
        // uses ahead of the pre-existing "deck exhausted" win.

        Assert.That(engine.Health, Is.EqualTo(0));
        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.Won, Is.False);
    }
}
