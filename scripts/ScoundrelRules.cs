public enum Suit { Clubs, Spades, Hearts, Diamonds, RedJoker, BlackJoker }

/// <summary>
/// Pure game-logic functions for Scoundrel. No Godot dependencies — fully unit-testable.
/// </summary>
public static class ScoundrelRules
{
    public const int MaxHealth   = 20;
    public const int StartHealth = 20;
    public const int RoomSize    = 4;
    public const int MinCardsTaken = 3;
    public const int AceRank     = 1;
    public const int AceMonsterValue = 14;
    public const int DeckSize    = 40;

    /// <summary>
    /// Combat value of a black-suit card. Ace counts as 14 (strongest monster).
    /// </summary>
    public static int MonsterValue(int rank) => rank == AceRank ? AceMonsterValue : rank;

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
    ///
    /// Dispatches on the card's classification properties (IsMonster/IsPotion/
    /// IsWeapon/IsBlacksmith/IsMerchant/IsPotionJoker/IsWeaponJoker) rather than
    /// raw <see cref="CardModel.Suit"/> — Blacksmith cards share Suit.Diamonds with
    /// real weapons and Merchant cards share Suit.Hearts with real potions (only
    /// Rank distinguishes them, see CardModel.cs), so a raw-Suit switch would give
    /// a Blacksmith card the weapon tooltip and a Merchant card the potion tooltip.
    /// </summary>
    public static string TooltipFor(
        CardModel card,
        CardModel? equippedWeapon,
        int weaponFloor,
        bool potionUsedThisRoom,
        int health,
        int weaponAttackBonus = 0,
        int singleUseWeaponBonus = 0)
    {
        if (card.IsMonster)
        {
            int mv = card.MonsterValue;
            if (equippedWeapon != null && CanUseWeapon(mv, weaponFloor))
            {
                int effectiveWeaponValue = equippedWeapon.WeaponValue + weaponAttackBonus + singleUseWeaponBonus;
                int dmg = CalcDamage(mv, effectiveWeaponValue);
                return $"Monster — {mv} damage\nWith weapon: {dmg} damage";
            }
            if (equippedWeapon != null)
                return $"Monster — {mv} damage\nWeapon can't block";
            return $"Monster — {mv} damage";
        }

        if (card.IsPotion)
        {
            if (potionUsedThisRoom)
                return "Potion — VOID (one per room)";
            int healed = Heal(health, card.PotionValue) - health;
            return healed < card.PotionValue
                ? $"Potion — heals {healed} HP (capped at {MaxHealth})"
                : $"Potion — heals {healed} HP";
        }

        if (card.IsWeapon)
        {
            string text = $"Weapon — value {card.WeaponValue}";
            if (equippedWeapon != null)
                text += $"\nReplaces equipped ({equippedWeapon.WeaponValue})";
            return text;
        }

        if (card.IsBlacksmith)
        {
            string effect = card.Rank switch
            {
                11      => "Removes 1 slain monster from your weapon. If it has none attached, grants +1 attack instead.",
                12      => "Removes 2 slain monsters from your weapon. If it has none attached, grants +2 attack instead.",
                13      => "Removes 3 slain monsters from your weapon. If it has none attached, grants +3 attack instead.",
                AceRank => "Removes all slain monsters from your weapon. If it has none attached, grants a one-time +4 attack bonus instead.",
                _       => "",
            };
            return equippedWeapon == null
                ? $"Blacksmith — {effect}\nNo weapon equipped — this card will recycle back into the deck."
                : $"Blacksmith — {effect}";
        }

        if (card.IsMerchant)
        {
            string effect = card.Rank switch
            {
                11      => "Sells your weapon for HP equal to its value minus attached monsters (minimum 1).",
                12      => "Sells your weapon for HP equal to its value minus attached monsters (minimum 1), plus 1.",
                13      => "Sells your weapon for HP equal to its value minus attached monsters (minimum 1), plus 3.",
                AceRank => "Sells your weapon for its full value plus 5 HP, ignoring attached monsters.",
                _       => "",
            };
            return equippedWeapon == null
                ? $"Merchant — {effect}\nNo weapon equipped — this card will recycle back into the deck."
                : $"Merchant — {effect}";
        }

        if (card.IsPotionJoker)
            return "Potion Joker — Can store a potion for later use by the player. Can fight monsters bare-handed. When HP drops to 0, the joker and its carried item are permanently lost.";

        if (card.IsWeaponJoker)
            return "Weapon Joker — Can store a weapon for later use by the player. Can fight monsters bare-handed. When HP drops to 0, the joker and its carried item are permanently lost.";

        return "";
    }
}
