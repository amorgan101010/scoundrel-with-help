using NUnit.Framework;

namespace ScoundrelTests;

[TestFixture]
public class MonsterValueTests
{
    [Test]
    public void Ace_Returns14()
        => Assert.That(ScoundrelRules.MonsterValue(1), Is.EqualTo(14));

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
        // Ace = 14 monster value; weapon 10 → 4 damage
        => Assert.That(ScoundrelRules.CalcDamage(14, 10), Is.EqualTo(4));

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
        => Assert.That(ScoundrelRules.Heal(20, 5), Is.EqualTo(20));

    [Test]
    public void HealWouldExceedMax_CapsAtMax()
        => Assert.That(ScoundrelRules.Heal(17, 6), Is.EqualTo(20));

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
