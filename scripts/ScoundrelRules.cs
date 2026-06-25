public enum Suit { Clubs, Spades, Hearts, Diamonds }

/// <summary>
/// Pure game-logic functions for Scoundrel. No Godot dependencies — fully unit-testable.
/// </summary>
public static class ScoundrelRules
{
    public const int MaxHealth   = 20;
    public const int StartHealth = 20;
    public const int RoomSize    = 4;
    public const int MinCardsTaken = 3;

    /// <summary>
    /// Combat value of a black-suit card. Ace counts as 14 (strongest monster).
    /// </summary>
    public static int MonsterValue(int rank) => rank == 1 ? 14 : rank;

    /// <summary>
    /// True when the equipped weapon can be used against a monster.
    /// Requires the monster to be strictly weaker than the current weapon floor.
    /// </summary>
    public static bool CanUseWeapon(int monsterValue, int weaponFloor)
        => monsterValue < weaponFloor;

    /// <summary>
    /// Damage taken after applying the weapon. Assumes CanUseWeapon was already checked.
    /// Result is clamped to zero (weapon can never heal).
    /// </summary>
    public static int CalcDamage(int monsterValue, int weaponValue)
        => System.Math.Max(0, monsterValue - weaponValue);

    /// <summary>
    /// Weapon floor after successfully using the weapon against a monster.
    /// Next use must be against a weaker monster (strictly less than this value).
    /// </summary>
    public static int NextWeaponFloor(int monsterValue) => monsterValue;

    /// <summary>
    /// Health after drinking a potion, capped at MaxHealth.
    /// </summary>
    public static int Heal(int currentHealth, int potionValue)
        => System.Math.Min(MaxHealth, currentHealth + potionValue);

    /// <summary>
    /// Tooltip text for a room card given the current game state.
    /// All inputs are plain values (Godot-free and unit-testable).
    /// </summary>
    public static string TooltipFor(
        CardModel card,
        CardModel? equippedWeapon,
        int weaponFloor,
        bool potionUsedThisRoom,
        int health)
    {
        switch (card.Suit)
        {
            case Suit.Clubs:
            case Suit.Spades:
            {
                int mv = card.MonsterValue;
                if (equippedWeapon != null && CanUseWeapon(mv, weaponFloor))
                {
                    int dmg = CalcDamage(mv, equippedWeapon.WeaponValue);
                    return $"Monster — {mv} damage\nWith weapon: {dmg} damage";
                }
                if (equippedWeapon != null)
                    return $"Monster — {mv} damage\nWeapon can't block";
                return $"Monster — {mv} damage";
            }
            case Suit.Hearts:
            {
                if (potionUsedThisRoom)
                    return "Potion — VOID (one per room)";
                int healed = Heal(health, card.PotionValue) - health;
                return healed < card.PotionValue
                    ? $"Potion — heals {healed} HP (capped at {MaxHealth})"
                    : $"Potion — heals {healed} HP";
            }
            case Suit.Diamonds:
            {
                string text = $"Weapon — value {card.WeaponValue}";
                if (equippedWeapon != null)
                    text += $"\nReplaces equipped ({equippedWeapon.WeaponValue})";
                return text;
            }
            default:
                return "";
        }
    }
}
