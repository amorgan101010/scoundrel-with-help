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

    // ── Kind (CardKind classification, one case per of the 7 kinds) ────────────

    [Test] public void Kind_Monster()
        => Assert.That(new CardModel(Suit.Clubs, 5).Kind, Is.EqualTo(CardKind.Monster));
    [Test] public void Kind_Weapon()
        => Assert.That(new CardModel(Suit.Diamonds, 5).Kind, Is.EqualTo(CardKind.Weapon));
    [Test] public void Kind_Potion()
        => Assert.That(new CardModel(Suit.Hearts, 5).Kind, Is.EqualTo(CardKind.Potion));
    [Test] public void Kind_Blacksmith()
        => Assert.That(new CardModel(Suit.Diamonds, 11).Kind, Is.EqualTo(CardKind.Blacksmith));
    [Test] public void Kind_Merchant()
        => Assert.That(new CardModel(Suit.Hearts, 11).Kind, Is.EqualTo(CardKind.Merchant));
    [Test] public void Kind_PotionJoker()
        => Assert.That(new CardModel(Suit.RedJoker, 0).Kind, Is.EqualTo(CardKind.PotionJoker));
    [Test] public void Kind_WeaponJoker()
        => Assert.That(new CardModel(Suit.BlackJoker, 0).Kind, Is.EqualTo(CardKind.WeaponJoker));
}
