using NUnit.Framework;

namespace ScoundrelTests;

[TestFixture]
public class MonsterValueTests
{
    [Test]
    public void Ace_Returns14()
        => Assert.That(ScoundrelRules.MonsterValue(ScoundrelRules.AceRank), Is.EqualTo(ScoundrelRules.AceMonsterValue));

    [TestCase(2)]
    [TestCase(5)]
    [TestCase(10)]
    public void NumberCard_ReturnsFaceValue(int rank)
        => Assert.That(ScoundrelRules.MonsterValue(rank), Is.EqualTo(rank));

    [TestCase(11)]
    [TestCase(12)]
    [TestCase(13)]
    public void FaceCard_ReturnsFaceValue(int rank)
        => Assert.That(ScoundrelRules.MonsterValue(rank), Is.EqualTo(rank));
}

[TestFixture]
public class CanUseWeaponTests
{
    [Test]
    public void FreshWeapon_AnyMonsterQualifies()
        // Weapon floor starts at int.MaxValue; every monster is below it.
        => Assert.That(ScoundrelRules.CanUseWeapon(14, int.MaxValue), Is.True);

    [Test]
    public void MonsterBelowFloor_CanUse()
        => Assert.That(ScoundrelRules.CanUseWeapon(5, 8), Is.True);

    [Test]
    public void MonsterAtFloor_CannotUse()
        => Assert.That(ScoundrelRules.CanUseWeapon(8, 8), Is.False);

    [Test]
    public void MonsterAboveFloor_CannotUse()
        => Assert.That(ScoundrelRules.CanUseWeapon(10, 8), Is.False);
}

[TestFixture]
public class CalcDamageTests
{
    [Test]
    public void WeaponWeakerThanMonster_ReducesDamageByWeaponValue()
        => Assert.That(ScoundrelRules.CalcDamage(10, 6), Is.EqualTo(4));

    [Test]
    public void WeaponStrongerThanMonster_ZeroDamage()
        => Assert.That(ScoundrelRules.CalcDamage(3, 7), Is.EqualTo(0));

    [Test]
    public void WeaponEqualsMonster_ZeroDamage()
        => Assert.That(ScoundrelRules.CalcDamage(6, 6), Is.EqualTo(0));

    [Test]
    public void AceMonster_ReducedByWeapon()
        // Ace = AceMonsterValue monster value; weapon 10 → 4 damage
        => Assert.That(ScoundrelRules.CalcDamage(ScoundrelRules.AceMonsterValue, 10), Is.EqualTo(4));

    [Test]
    public void DamageNeverNegative()
        => Assert.That(ScoundrelRules.CalcDamage(2, 10), Is.EqualTo(0));
}

[TestFixture]
public class NextWeaponFloorTests
{
    [TestCase(3)]
    [TestCase(7)]
    [TestCase(14)]
    public void FloorSetToMonsterValue(int monsterValue)
        => Assert.That(ScoundrelRules.NextWeaponFloor(monsterValue), Is.EqualTo(monsterValue));

    [TestCase(3)]
    [TestCase(7)]
    [TestCase(14)]
    public void NewFloor_BlocksSameMonster(int monsterValue)
    {
        int floor = ScoundrelRules.NextWeaponFloor(monsterValue);
        Assert.That(ScoundrelRules.CanUseWeapon(monsterValue, floor), Is.False);
    }

    [TestCase(3)]
    [TestCase(7)]
    [TestCase(14)]
    public void NewFloor_AllowsStrictlyWeakerMonster(int monsterValue)
    {
        int floor = ScoundrelRules.NextWeaponFloor(monsterValue);
        Assert.That(ScoundrelRules.CanUseWeapon(monsterValue - 1, floor), Is.True);
    }
}

[TestFixture]
public class HealTests
{
    [Test]
    public void NormalHeal_AddsPotion()
        => Assert.That(ScoundrelRules.Heal(10, 5), Is.EqualTo(15));

    [Test]
    public void HealAtMax_StaysAtMax()
        => Assert.That(ScoundrelRules.Heal(ScoundrelRules.MaxHealth, 5), Is.EqualTo(ScoundrelRules.MaxHealth));

    [Test]
    public void HealWouldExceedMax_CapsAtMax()
        => Assert.That(ScoundrelRules.Heal(17, 6), Is.EqualTo(ScoundrelRules.MaxHealth));

    [Test]
    public void HealFromZero_AddsPotion()
        => Assert.That(ScoundrelRules.Heal(0, 8), Is.EqualTo(8));

    [Test]
    public void SmallestPotion_HealsByTwo()
        // Smallest heart in Scoundrel deck is 2
        => Assert.That(ScoundrelRules.Heal(10, 2), Is.EqualTo(12));
}

[TestFixture]
public class WeaponDegradationScenarioTests
{
    // Verify the weapon-floor mechanic end-to-end through ScoundrelRules calls.

    [Test]
    public void UsedAgainstStrongMonster_FloorDropsForNextUse()
    {
        // Weapon value 7, initial floor = MaxInt.
        // Fight a 10: CanUse? Yes (10 < MaxInt). Damage = 10-7 = 3. New floor = 10.
        Assert.That(ScoundrelRules.CanUseWeapon(10, int.MaxValue), Is.True);
        Assert.That(ScoundrelRules.CalcDamage(10, 7), Is.EqualTo(3));

        int newFloor = ScoundrelRules.NextWeaponFloor(10);

        // Next fight: monster 10 — floor is now 10, can't use (not strictly less).
        Assert.That(ScoundrelRules.CanUseWeapon(10, newFloor), Is.False);
        // Monster 9 — still usable.
        Assert.That(ScoundrelRules.CanUseWeapon(9, newFloor), Is.True);
    }

    [Test]
    public void WeaponCannotHitEqualOrStrongerMonsterAfterUse()
    {
        int floor = ScoundrelRules.NextWeaponFloor(8); // fought an 8
        Assert.That(ScoundrelRules.CanUseWeapon(8, floor),  Is.False);
        Assert.That(ScoundrelRules.CanUseWeapon(9, floor),  Is.False);
        Assert.That(ScoundrelRules.CanUseWeapon(7, floor),  Is.True);
    }
}

[TestFixture]
public class TooltipForTests
{
    private static CardModel Monster(int rank) => new(Suit.Clubs, rank, $"monster_{rank}");
    private static CardModel Potion(int rank)  => new(Suit.Hearts, rank, $"potion_{rank}");
    private static CardModel Weapon(int rank)  => new(Suit.Diamonds, rank, $"weapon_{rank}");

    [Test]
    public void Monster_NoWeapon_ShowsBareDamage()
        => Assert.That(
            ScoundrelRules.TooltipFor(Monster(7), null, int.MaxValue, false, 20),
            Is.EqualTo("Monster — 7 damage"));

    [Test]
    public void Monster_UsableWeapon_ShowsBothDamageValues()
        => Assert.That(
            ScoundrelRules.TooltipFor(Monster(7), Weapon(4), int.MaxValue, false, 20),
            Is.EqualTo("Monster — 7 damage\nWith weapon: 3 damage"));

    [Test]
    public void Monster_WeaponTooWeak_ShowsCantBlock()
        // weaponFloor = 8: can't use against monster 9 (9 < 8 is false)
        => Assert.That(
            ScoundrelRules.TooltipFor(Monster(9), Weapon(4), 8, false, 20),
            Is.EqualTo("Monster — 9 damage\nWeapon can't block"));

    [Test]
    public void Monster_WeaponStrongerThanMonster_ShowsZeroDamage()
        => Assert.That(
            ScoundrelRules.TooltipFor(Monster(3), Weapon(7), int.MaxValue, false, 20),
            Is.EqualTo("Monster — 3 damage\nWith weapon: 0 damage"));

    [Test]
    public void Potion_NotVoid_ShowsHealAmount()
        => Assert.That(
            ScoundrelRules.TooltipFor(Potion(5), null, int.MaxValue, false, 10),
            Is.EqualTo("Potion — heals 5 HP"));

    [Test]
    public void HealCapped_ShowsCap()
        // Health 18 + rank 5 = 23, caps at MaxHealth → healed = 2
        => Assert.That(
            ScoundrelRules.TooltipFor(Potion(5), null, int.MaxValue, false, 18),
            Is.EqualTo($"Potion — heals 2 HP (capped at {ScoundrelRules.MaxHealth})"));

    [Test]
    public void Potion_Void_ShowsVoidMessage()
        => Assert.That(
            ScoundrelRules.TooltipFor(Potion(5), null, int.MaxValue, true, 10),
            Is.EqualTo("Potion — VOID (one per room)"));

    [Test]
    public void Weapon_NoEquipped_ShowsValueOnly()
        => Assert.That(
            ScoundrelRules.TooltipFor(Weapon(7), null, int.MaxValue, false, 20),
            Is.EqualTo("Weapon — value 7"));

    [Test]
    public void Weapon_ReplacesExisting_ShowsReplacement()
        => Assert.That(
            ScoundrelRules.TooltipFor(Weapon(7), Weapon(4), int.MaxValue, false, 20),
            Is.EqualTo("Weapon — value 7\nReplaces equipped (4)"));

    [Test]
    public void Monster_WeaponWithAttackBonus_IncludesBonusInDamage()
        // Weapon 4 + WeaponAttackBonus 2 = effective 6. Monster 9 - 6 = 3 damage.
        => Assert.That(
            ScoundrelRules.TooltipFor(Monster(9), Weapon(4), int.MaxValue, false, 20, weaponAttackBonus: 2),
            Is.EqualTo("Monster — 9 damage\nWith weapon: 3 damage"));

    [Test]
    public void Monster_WeaponWithSingleUseBonus_IncludesBonusInDamage()
        // Weapon 4 + SingleUseWeaponBonus 4 = effective 8. Monster 9 - 8 = 1 damage.
        => Assert.That(
            ScoundrelRules.TooltipFor(Monster(9), Weapon(4), int.MaxValue, false, 20, singleUseWeaponBonus: 4),
            Is.EqualTo("Monster — 9 damage\nWith weapon: 1 damage"));
}

// Blacksmith/Merchant cards share Suit.Diamonds/Suit.Hearts with real weapons/
// potions (only Rank distinguishes them — see CardModel.cs). TooltipFor used to
// switch on raw card.Suit, so a Blacksmith card got the weapon tooltip and a
// Merchant card got the potion tooltip. These fixtures cover every rank of
// both, with and without an equipped weapon, plus both Jokers.
[TestFixture]
public class BlacksmithTooltipForTests
{
    private static CardModel Blacksmith(int rank) => new(Suit.Diamonds, rank, $"blacksmith_{rank}");
    private static CardModel Weapon(int rank)      => new(Suit.Diamonds, rank, $"weapon_{rank}");

    [TestCase(11, "Removes 1 slain monster from your weapon. If it has none attached, grants +1 attack instead.")]
    [TestCase(12, "Removes 2 slain monsters from your weapon. If it has none attached, grants +2 attack instead.")]
    [TestCase(13, "Removes 3 slain monsters from your weapon. If it has none attached, grants +3 attack instead.")]
    [TestCase(1,  "Removes all slain monsters from your weapon. If it has none attached, grants a one-time +4 attack bonus instead.")]
    public void WithWeaponEquipped_ShowsEffectText(int rank, string effect)
        => Assert.That(
            ScoundrelRules.TooltipFor(Blacksmith(rank), Weapon(6), int.MaxValue, false, 20),
            Is.EqualTo($"Blacksmith — {effect}"));

    [TestCase(11, "Removes 1 slain monster from your weapon. If it has none attached, grants +1 attack instead.")]
    [TestCase(12, "Removes 2 slain monsters from your weapon. If it has none attached, grants +2 attack instead.")]
    [TestCase(13, "Removes 3 slain monsters from your weapon. If it has none attached, grants +3 attack instead.")]
    [TestCase(1,  "Removes all slain monsters from your weapon. If it has none attached, grants a one-time +4 attack bonus instead.")]
    public void NoWeaponEquipped_ShowsRecycleNote(int rank, string effect)
        => Assert.That(
            ScoundrelRules.TooltipFor(Blacksmith(rank), null, int.MaxValue, false, 20),
            Is.EqualTo($"Blacksmith — {effect}\nNo weapon equipped — this card will recycle back into the deck."));
}

[TestFixture]
public class MerchantTooltipForTests
{
    private static CardModel Merchant(int rank) => new(Suit.Hearts, rank, $"merchant_{rank}");
    private static CardModel Weapon(int rank)    => new(Suit.Diamonds, rank, $"weapon_{rank}");

    [TestCase(11, "Sells your weapon for HP equal to its value minus attached monsters (minimum 1).")]
    [TestCase(12, "Sells your weapon for HP equal to its value minus attached monsters (minimum 1), plus 1.")]
    [TestCase(13, "Sells your weapon for HP equal to its value minus attached monsters (minimum 1), plus 3.")]
    [TestCase(1,  "Sells your weapon for its full value plus 5 HP, ignoring attached monsters.")]
    public void WithWeaponEquipped_ShowsEffectText(int rank, string effect)
        => Assert.That(
            ScoundrelRules.TooltipFor(Merchant(rank), Weapon(6), int.MaxValue, false, 20),
            Is.EqualTo($"Merchant — {effect}"));

    [TestCase(11, "Sells your weapon for HP equal to its value minus attached monsters (minimum 1).")]
    [TestCase(12, "Sells your weapon for HP equal to its value minus attached monsters (minimum 1), plus 1.")]
    [TestCase(13, "Sells your weapon for HP equal to its value minus attached monsters (minimum 1), plus 3.")]
    [TestCase(1,  "Sells your weapon for its full value plus 5 HP, ignoring attached monsters.")]
    public void NoWeaponEquipped_ShowsRecycleNote(int rank, string effect)
        => Assert.That(
            ScoundrelRules.TooltipFor(Merchant(rank), null, int.MaxValue, false, 20),
            Is.EqualTo($"Merchant — {effect}\nNo weapon equipped — this card will recycle back into the deck."));
}

[TestFixture]
public class JokerTooltipForTests
{
    private static CardModel RedJoker()   => new(Suit.RedJoker, 0, "joker_red");
    private static CardModel BlackJoker() => new(Suit.BlackJoker, 0, "joker_black");

    [Test]
    public void PotionJoker_ShowsPocketAndFightText()
        => Assert.That(
            ScoundrelRules.TooltipFor(RedJoker(), null, int.MaxValue, false, 20),
            Is.EqualTo("Potion Joker — Can store a potion for later use by the player. Can fight monsters bare-handed. When HP drops to 0, the joker and its carried item are permanently lost."));

    [Test]
    public void WeaponJoker_ShowsPocketAndFightText()
        => Assert.That(
            ScoundrelRules.TooltipFor(BlackJoker(), null, int.MaxValue, false, 20),
            Is.EqualTo("Weapon Joker — Can store a weapon for later use by the player. Can fight monsters bare-handed. When HP drops to 0, the joker and its carried item are permanently lost."));
}

[TestFixture]
public class ConstantsTests
{
    [Test]
    public void MaxHealth_Is20() => Assert.That(ScoundrelRules.MaxHealth, Is.EqualTo(20));

    [Test]
    public void StartHealth_Is20() => Assert.That(ScoundrelRules.StartHealth, Is.EqualTo(20));

    [Test]
    public void RoomSize_Is4() => Assert.That(ScoundrelRules.RoomSize, Is.EqualTo(4));

    [Test]
    public void MinCardsTaken_Is3() => Assert.That(ScoundrelRules.MinCardsTaken, Is.EqualTo(3));
}
