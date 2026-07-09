using NUnit.Framework;

namespace ScoundrelTests;

// ── RoomCardContent (room-card overlay's pure content derivation) ─────────────

[TestFixture]
public class RoomCardContentBannerFamilyTests
{
    [Test] public void Monster_IsMonsterFamily()
        => Assert.That(RoomCardContent.BannerFamily(CardKind.Monster), Is.EqualTo(RoomCardBannerFamily.Monster));
    [Test] public void Weapon_IsWeaponFamily()
        => Assert.That(RoomCardContent.BannerFamily(CardKind.Weapon), Is.EqualTo(RoomCardBannerFamily.Weapon));
    [Test] public void Potion_IsPotionFamily()
        => Assert.That(RoomCardContent.BannerFamily(CardKind.Potion), Is.EqualTo(RoomCardBannerFamily.Potion));
    [Test] public void Blacksmith_IsFriendlyFamily()
        => Assert.That(RoomCardContent.BannerFamily(CardKind.Blacksmith), Is.EqualTo(RoomCardBannerFamily.Friendly));
    [Test] public void Merchant_IsFriendlyFamily()
        => Assert.That(RoomCardContent.BannerFamily(CardKind.Merchant), Is.EqualTo(RoomCardBannerFamily.Friendly));
    [Test] public void PotionJoker_IsFriendlyFamily()
        => Assert.That(RoomCardContent.BannerFamily(CardKind.PotionJoker), Is.EqualTo(RoomCardBannerFamily.Friendly));
    [Test] public void WeaponJoker_IsFriendlyFamily()
        => Assert.That(RoomCardContent.BannerFamily(CardKind.WeaponJoker), Is.EqualTo(RoomCardBannerFamily.Friendly));
}

[TestFixture]
public class RoomCardContentDisplayNameTests
{
    [Test] public void Blacksmith_IsNamedBlacksmith()
        => Assert.That(RoomCardContent.DisplayName(new CardModel(Suit.Diamonds, 1)), Is.EqualTo("BLACKSMITH"));
    [Test] public void Merchant_IsNamedMerchant()
        => Assert.That(RoomCardContent.DisplayName(new CardModel(Suit.Hearts, 11)), Is.EqualTo("MERCHANT"));
    [Test] public void PotionJoker_IsNamedRedJoker()
        => Assert.That(RoomCardContent.DisplayName(new CardModel(Suit.RedJoker, 0)), Is.EqualTo("RED JOKER"));
    [Test] public void WeaponJoker_IsNamedBlackJoker()
        => Assert.That(RoomCardContent.DisplayName(new CardModel(Suit.BlackJoker, 0)), Is.EqualTo("BLACK JOKER"));

    [Test] public void NumberMonster_IsRankOfSuit()
        => Assert.That(RoomCardContent.DisplayName(new CardModel(Suit.Spades, 10)), Is.EqualTo("10 OF SPADES"));
    [Test] public void AceMonster_IsAceOfSuit()
        => Assert.That(RoomCardContent.DisplayName(new CardModel(Suit.Clubs, 1)), Is.EqualTo("ACE OF CLUBS"));
    [Test] public void JackMonster_IsJackOfSuit()
        => Assert.That(RoomCardContent.DisplayName(new CardModel(Suit.Spades, 11)), Is.EqualTo("JACK OF SPADES"));
    [Test] public void QueenMonster_IsQueenOfSuit()
        => Assert.That(RoomCardContent.DisplayName(new CardModel(Suit.Clubs, 12)), Is.EqualTo("QUEEN OF CLUBS"));
    [Test] public void KingMonster_IsKingOfSuit()
        => Assert.That(RoomCardContent.DisplayName(new CardModel(Suit.Spades, 13)), Is.EqualTo("KING OF SPADES"));

    [Test] public void Weapon_IsRankOfDiamonds()
        => Assert.That(RoomCardContent.DisplayName(new CardModel(Suit.Diamonds, 9)), Is.EqualTo("9 OF DIAMONDS"));
    [Test] public void Potion_IsRankOfHearts()
        => Assert.That(RoomCardContent.DisplayName(new CardModel(Suit.Hearts, 4)), Is.EqualTo("4 OF HEARTS"));
}

[TestFixture]
public class RoomCardContentFooterTests
{
    [Test] public void Monster_FooterLeftIsSuitGlyphAndSingularName()
        => Assert.That(RoomCardContent.FooterLeftText(new CardModel(Suit.Spades, 4)), Is.EqualTo("♠ SPADE"));
    [Test] public void Weapon_FooterLeftIsDiamondGlyph()
        => Assert.That(RoomCardContent.FooterLeftText(new CardModel(Suit.Diamonds, 4)), Is.EqualTo("♦ DIAMOND"));
    [Test] public void Potion_FooterLeftIsHeartGlyph()
        => Assert.That(RoomCardContent.FooterLeftText(new CardModel(Suit.Hearts, 4)), Is.EqualTo("♥ HEART"));
    [Test] public void Blacksmith_FooterLeftIsBlessing()
        => Assert.That(RoomCardContent.FooterLeftText(new CardModel(Suit.Diamonds, 1)), Is.EqualTo("BLESSING"));
    [Test] public void Merchant_FooterLeftIsBlessing()
        => Assert.That(RoomCardContent.FooterLeftText(new CardModel(Suit.Hearts, 13)), Is.EqualTo("BLESSING"));
    [Test] public void PotionJoker_FooterLeftIsCompanion()
        => Assert.That(RoomCardContent.FooterLeftText(new CardModel(Suit.RedJoker, 0)), Is.EqualTo("COMPANION"));
    [Test] public void WeaponJoker_FooterLeftIsCompanion()
        => Assert.That(RoomCardContent.FooterLeftText(new CardModel(Suit.BlackJoker, 0)), Is.EqualTo("COMPANION"));

    [Test] public void Monster_FooterValueIsMonsterValue()
        => Assert.That(RoomCardContent.FooterValue(new CardModel(Suit.Clubs, ScoundrelRules.AceRank)), Is.EqualTo(ScoundrelRules.AceMonsterValue));
    [Test] public void Weapon_FooterValueIsRank()
        => Assert.That(RoomCardContent.FooterValue(new CardModel(Suit.Diamonds, 7)), Is.EqualTo(7));
    [Test] public void Potion_FooterValueIsRank()
        => Assert.That(RoomCardContent.FooterValue(new CardModel(Suit.Hearts, 6)), Is.EqualTo(6));
    [Test] public void Blacksmith_FooterValueIsNull()
        => Assert.That(RoomCardContent.FooterValue(new CardModel(Suit.Diamonds, 1)), Is.Null);
    [Test] public void PotionJoker_FooterValueIsNull()
        => Assert.That(RoomCardContent.FooterValue(new CardModel(Suit.RedJoker, 0)), Is.Null);
}

[TestFixture]
public class RoomCardContentDescriptionTests
{
    [Test] public void StripsLeadingKindPrefix()
        => Assert.That(RoomCardContent.Description("Monster — 4 damage"), Is.EqualTo("4 damage"));

    [Test] public void PreservesSecondLineAfterPrefixStrip()
        => Assert.That(RoomCardContent.Description("Monster — 4 damage\nWith weapon: 2 damage"),
                        Is.EqualTo("4 damage\nWith weapon: 2 damage"));

    [Test] public void NoDashSeparator_ReturnsTextUnchanged()
        => Assert.That(RoomCardContent.Description("no separator here"), Is.EqualTo("no separator here"));
}
