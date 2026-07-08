using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

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
    public void UseBlacksmith_PartialRemoval_RemovesMostRecentKillFirst_ImprovingTheFloor()
    {
        // Weapon degradation forces each kill to be strictly weaker than the last (9 then
        // 8), so removal must target the most recent (lowest-value, floor-setting) kill
        // first -- otherwise the floor could never improve short of removing everything.
        // Removing the "8" here should reveal "9" as the new most-recent survivor, raising
        // the floor from 8 back to 9 (able to block another 8-or-weaker monster again).
        var weapon   = Cards.Weapon(10);
        var monster9 = Cards.Monster(9);
        var monster8 = Cards.Spade(8);
        var jack     = Cards.Blacksmith(11);
        var engine   = Cards.RoomOf(weapon, monster9, monster8, jack);

        engine.TakeCard(weapon);
        engine.TakeCard(monster9); // blocked -> slain [9], floor 9
        engine.TakeCard(monster8); // blocked -> slain [9,8], floor 8
        Assert.That(engine.SlainMonsterCount, Is.EqualTo(2));
        Assert.That(engine.WeaponFloor, Is.EqualTo(8));

        engine.UseBlacksmith(jack); // removes the most recent (8) -> slain [9]

        Assert.That(engine.SlainMonsterCount, Is.EqualTo(1));
        Assert.That(engine.WeaponFloor, Is.EqualTo(9),
            "Removing the most recent kill should reveal the next-most-recent survivor's value as the new floor");
        Assert.That(ScoundrelRules.CanUseWeapon(8, engine.WeaponFloor), Is.True,
            "The weapon should be able to block another 8 now that the kill setting the stricter floor is gone");
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
