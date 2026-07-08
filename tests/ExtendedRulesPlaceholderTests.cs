using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

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
