using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure C# game-state machine for Scoundrel. No Godot dependencies — fully integration-testable.
/// Deck is passed in pre-ordered; index 0 = bottom, last index = top (dealt first).
/// </summary>
public class GameEngine
{
    private readonly List<CardModel> _deck;
    private readonly List<CardModel> _discard = new();
    private readonly List<CardModel> _room    = new();
    private readonly Random _rng;

    /// <summary>Flat starting HP for either joker's own pool — no randomness.</summary>
    private const int JokerStartingHealth = 8;

    public int Health { get; private set; } = ScoundrelRules.StartHealth;
    public CardModel? EquippedWeapon { get; private set; }
    public int WeaponFloor { get; private set; } = int.MaxValue;
    public bool PotionUsedThisRoom { get; private set; }
    public bool PotionWastedThisRoom { get; private set; }
    public bool RanLastRoom { get; private set; }
    public int CardsTakenThisRoom { get; private set; }
    public bool GameOver { get; private set; }
    public bool Won { get; private set; }

    /// <summary>
    /// Number of monsters slain with the currently-equipped weapon. Resets to 0 whenever
    /// a new weapon is equipped (including the first).
    /// </summary>
    public int SlainMonsterCount { get; private set; }

    /// <summary>
    /// Permanent bonus added to the equipped weapon's effective value, granted by a
    /// Jack/Queen/King Blacksmith card used while <see cref="SlainMonsterCount"/> is 0
    /// (nothing to remove). Resets to 0 whenever a new weapon is equipped.
    /// </summary>
    public int WeaponAttackBonus { get; private set; }

    /// <summary>
    /// Single-use bonus added to the equipped weapon's effective value, granted by the Ace
    /// of Diamonds Blacksmith card ("Excalibur") used while <see cref="SlainMonsterCount"/>
    /// is 0. Consumed after the very next weapon-blocked fight (see
    /// <see cref="ApplyMonsterDamage"/>), then reverts to 0. Also resets to 0 whenever a
    /// new weapon is equipped.
    /// </summary>
    public int SingleUseWeaponBonus { get; private set; }

    /// <summary>
    /// When true, the deck/room may contain Extended Rules cards (Blacksmith, Merchant,
    /// Jokers) in addition to the Classic monster/weapon/potion cards.
    /// </summary>
    public bool ExtendedRules { get; }

    /// <summary>
    /// True once the Red Joker (Potion Pocket companion) has been taken. The joker is a
    /// permanent companion, not a card in play — it is never discarded.
    /// </summary>
    public bool HasPotionJoker { get; private set; }

    /// <summary>
    /// The potion currently held in the Potion Pocket, or null if the pocket is empty.
    /// </summary>
    public CardModel? PocketedPotion { get; private set; }

    /// <summary>
    /// The Red Joker's own HP pool. Set to a flat 8 (no randomness) the moment the joker is
    /// taken. Absorbs a monster's full value when the player chooses to handle the monster
    /// with the joker instead of fighting it — see <see cref="FightWithPotionJoker"/>.
    /// Floored at 0; hitting 0 loses the joker entirely (<see cref="HasPotionJoker"/> becomes
    /// false and any pocketed potion is lost, not discarded).
    /// </summary>
    public int PotionJokerHealth { get; private set; }

    /// <summary>
    /// True once the Black Joker (Weapon Pocket companion) has been taken. The joker is a
    /// permanent companion, not a card in play — it is never discarded.
    /// </summary>
    public bool HasWeaponJoker { get; private set; }

    /// <summary>
    /// The weapon currently held in the Weapon Pocket, or null if the pocket is empty. Has no
    /// combat role of its own — see <see cref="RetrieveWeapon"/> to equip it as the player's
    /// actual weapon.
    /// </summary>
    public CardModel? PocketedWeapon { get; private set; }

    /// <summary>
    /// The Black Joker's own HP pool. Set to a flat 8 (no randomness) the moment the joker is
    /// taken. Absorbs a monster's full value when the player chooses to handle the monster
    /// with the joker instead of fighting it — see <see cref="FightWithWeaponJoker"/>.
    /// Floored at 0; hitting 0 loses the joker entirely (<see cref="HasWeaponJoker"/> becomes
    /// false and any pocketed weapon is lost, not discarded).
    /// </summary>
    public int WeaponJokerHealth { get; private set; }

    public IReadOnlyList<CardModel> Deck    => _deck;
    public IReadOnlyList<CardModel> Discard => _discard;
    public IReadOnlyList<CardModel> Room    => _room;

    public bool IsOver     => GameOver || Won;
    public bool CanRun     => !IsOver && !RanLastRoom;
    public bool CanNextRoom => !IsOver && CardsTakenThisRoom >= ScoundrelRules.MinCardsTaken && _room.Count > 0;

    /// <summary>
    /// True iff a potion can be stored in the Potion Pocket right now: the joker has been
    /// taken, the pocket is empty, the given card is a potion currently in the room, and
    /// the game isn't over. Storing does NOT consume the room's one-potion-per-room limit.
    /// </summary>
    public bool CanStorePotion(CardModel card)
        => HasPotionJoker && PocketedPotion == null && card.IsPotion && _room.Contains(card) && !IsOver;

    /// <summary>
    /// True iff there is a pocketed potion that can be retrieved right now. Retrieval is a
    /// side action (not a room pick), so it is available any time regardless of room state.
    /// </summary>
    public bool CanRetrievePotion => HasPotionJoker && PocketedPotion != null && !IsOver;

    /// <summary>
    /// True iff a weapon can be stored in the Weapon Pocket right now: the joker has been
    /// taken, the pocket is empty, the given card is a weapon currently in the room, and
    /// the game isn't over.
    /// </summary>
    public bool CanStoreWeapon(CardModel card)
        => HasWeaponJoker && PocketedWeapon == null && card.IsWeapon && _room.Contains(card) && !IsOver;

    /// <summary>
    /// True iff a weapon can be retrieved from the Weapon Pocket right now: the joker has
    /// been taken, the pocket holds a weapon, and the game isn't over. A side action (not a
    /// room pick), so it doesn't depend on room state.
    /// </summary>
    public bool CanRetrieveWeapon => HasWeaponJoker && PocketedWeapon != null && !IsOver;

    /// <summary>
    /// True iff a monster in the room can be handled by the Red Joker right now: the joker
    /// has been taken, its HP pool is above 0, the given card is a monster currently in the
    /// room, and the game isn't over.
    /// </summary>
    public bool CanFightWithPotionJoker(CardModel monster)
        => HasPotionJoker && PotionJokerHealth > 0 && monster.IsMonster
           && _room.Contains(monster) && !IsOver;

    /// <summary>
    /// True iff a monster in the room can be handled by the Black Joker right now: the joker
    /// has been taken, its HP pool is above 0, the given card is a monster currently in the
    /// room, and the game isn't over.
    /// </summary>
    public bool CanFightWithWeaponJoker(CardModel monster)
        => HasWeaponJoker && WeaponJokerHealth > 0 && monster.IsMonster
           && _room.Contains(monster) && !IsOver;

    /// <summary>
    /// True iff the given Blacksmith card can be used right now: it's a Blacksmith card
    /// currently in the room and the game isn't over. Usable regardless of whether a
    /// weapon is equipped — the "cannot be used" case (no weapon) is handled inside
    /// <see cref="UseBlacksmith"/> by recycling the card, not by blocking the call.
    /// </summary>
    public bool CanUseBlacksmith(CardModel card)
        => card.IsBlacksmith && _room.Contains(card) && !IsOver;

    /// <summary>
    /// True iff the given Merchant card can be used right now: it's a Merchant card
    /// currently in the room and the game isn't over. Usable regardless of whether a
    /// weapon is equipped — the "cannot be used" case (no weapon) is handled inside
    /// <see cref="UseMerchant"/> by recycling the card, not by blocking the call.
    /// </summary>
    public bool CanUseMerchant(CardModel card)
        => card.IsMerchant && _room.Contains(card) && !IsOver;

    public GameEngine(IEnumerable<CardModel> deck, bool extendedRules = false, Random? rng = null)
    {
        _deck = deck.ToList();
        ExtendedRules = extendedRules;
        _rng = rng ?? new Random();
        DealRoom();
    }

    // ── Actions ───────────────────────────────────────────────────────────

    /// <param name="activateCard">
    /// When false, the card is discarded without its type-specific effect
    /// (no equip for weapons, no heal for potions). Useful for player-chosen discards.
    /// For a Blacksmith or Merchant card, "declined" means recycled into the deck rather
    /// than discarded — see <see cref="ApplyBlacksmithEffect"/> and
    /// <see cref="ApplyMerchantEffect"/> — so both are handled before the general
    /// activateCard/discard branch below, not inside it.
    /// </param>
    public void TakeCard(CardModel card, bool useWeapon = true, bool activateCard = true)
    {
        if (IsOver) throw new InvalidOperationException("Game is over.");
        if (!_room.Remove(card)) throw new ArgumentException("Card is not in the room.");

        if (card.IsBlacksmith)
        {
            ApplyBlacksmithEffect(card, activateCard);
        }
        else if (card.IsMerchant)
        {
            ApplyMerchantEffect(card, activateCard);
        }
        else if (!activateCard)
        {
            _discard.Add(card);
        }
        else
        {
            if (card.IsMonster)
            {
                ApplyMonsterDamage(card, useWeapon);
                _discard.Add(card);
            }
            else if (card.IsWeapon)
            {
                EquipWeapon(card);
            }
            else if (card.IsPotion)
            {
                ApplyPotionHealOrWaste(card);
                _discard.Add(card);
            }
            else if (card.IsPotionJoker)
            {
                // Red Joker (Potion Pocket): becomes a permanent companion, not a card in
                // play. It is never discarded — see StorePotion/RetrievePotion/
                // FightWithPotionJoker below. Its own HP pool starts at a flat 8.
                HasPotionJoker = true;
                PotionJokerHealth = JokerStartingHealth;
            }
            else if (card.IsWeaponJoker)
            {
                // Black Joker (Weapon Pocket): becomes a permanent companion, not a card in
                // play. It is never discarded — see StoreWeapon/RetrieveWeapon/
                // FightWithWeaponJoker below. Its own HP pool starts at a flat 8.
                HasWeaponJoker = true;
                WeaponJokerHealth = JokerStartingHealth;
            }
            else
            {
                // Unreachable: every Extended Rules card kind is classified as Blacksmith,
                // Merchant, a Joker, or a Classic monster/weapon/potion above. Kept as a
                // defensive fallback rather than an assert.
                _discard.Add(card);
            }
        }

        FinishRoomAction();
    }

    /// <summary>
    /// Store a potion from the room into the Potion Pocket instead of drinking it.
    /// Independent of the room's one-potion-per-room limit — storing doesn't heal, so it
    /// doesn't consume the allowance. Counts as one of the room's taken cards.
    /// </summary>
    public void StorePotion(CardModel card)
    {
        if (!CanStorePotion(card))
            throw new InvalidOperationException("Cannot store this potion right now.");

        _room.Remove(card);
        PocketedPotion = card;
        FinishRoomAction();
    }

    /// <summary>
    /// Retrieve the pocketed potion. Counts as the room's one potion — if a potion was
    /// already drunk this room, the retrieved potion is wasted instead of healing. This is
    /// a side action: it does not affect CardsTakenThisRoom and does not deal a new room or
    /// trigger a win check (room state doesn't change).
    /// </summary>
    /// <param name="activate">
    /// When true (default), retrieving heals (or wastes, per the room's one-potion limit)
    /// exactly like the original behavior. When false, the potion is simply discarded from
    /// the pocket with no heal/waste side effect — <see cref="PotionUsedThisRoom"/> and
    /// <see cref="PotionWastedThisRoom"/> are left untouched. Either way the pocket is
    /// emptied and the existing <see cref="CanRetrievePotion"/> gating still applies.
    /// </param>
    public void RetrievePotion(bool activate = true)
    {
        if (!CanRetrievePotion)
            throw new InvalidOperationException("Cannot retrieve a potion right now.");

        var potion = PocketedPotion!;
        PocketedPotion = null;

        if (activate)
            ApplyPotionHealOrWaste(potion);

        _discard.Add(potion);

        CheckGameOver();
    }

    /// <summary>
    /// Store a weapon from the room into the Weapon Pocket. Fully independent of the main
    /// weapon system — does not touch EquippedWeapon/WeaponFloor/SlainMonsterCount. Counts
    /// as one of the room's taken cards.
    /// </summary>
    public void StoreWeapon(CardModel card)
    {
        if (!CanStoreWeapon(card))
            throw new InvalidOperationException("Cannot store this weapon right now.");

        _room.Remove(card);
        PocketedWeapon = card;
        FinishRoomAction();
    }

    /// <summary>
    /// Retrieve the pocketed weapon and equip it as the player's actual weapon, via the same
    /// <see cref="EquipWeapon"/> path a room weapon uses (discarding the previously-equipped
    /// weapon, if any, and resetting WeaponFloor/SlainMonsterCount/WeaponAttackBonus/
    /// SingleUseWeaponBonus). A side action: it does not affect CardsTakenThisRoom and does
    /// not deal a new room or trigger a win check (room state doesn't change).
    /// </summary>
    /// <param name="activate">
    /// When true (default), retrieving equips the weapon via <see cref="EquipWeapon"/>
    /// exactly like the original behavior. When false, the pocketed weapon is simply
    /// discarded without equipping — the currently-equipped weapon (if any) is left
    /// completely untouched. Either way the pocket is emptied and the existing
    /// <see cref="CanRetrieveWeapon"/> gating still applies.
    /// </param>
    public void RetrieveWeapon(bool activate = true)
    {
        if (!CanRetrieveWeapon)
            throw new InvalidOperationException("Cannot retrieve a weapon right now.");

        var weapon = PocketedWeapon!;
        PocketedWeapon = null;

        if (activate)
            EquipWeapon(weapon);
        else
            _discard.Add(weapon);
    }

    /// <summary>
    /// Handle a monster with the Red Joker instead of fighting it: the joker absorbs the
    /// monster's full value into its own HP pool (<see cref="PotionJokerHealth"/>), never
    /// reduced by a weapon and never touching the player's <see cref="Health"/>. The monster
    /// still leaves the room to the discard pile and counts toward CardsTakenThisRoom. If the
    /// joker's HP hits 0, the joker is lost entirely — <see cref="HasPotionJoker"/> becomes
    /// false and any pocketed potion is lost (not discarded), matching the "companion, not a
    /// card" framing used elsewhere for the jokers.
    /// </summary>
    public void FightWithPotionJoker(CardModel monster)
    {
        if (!CanFightWithPotionJoker(monster))
            throw new InvalidOperationException("Cannot handle this monster with the Potion Joker right now.");

        _room.Remove(monster);
        _discard.Add(monster);
        PotionJokerHealth = Math.Max(0, PotionJokerHealth - monster.MonsterValue);

        if (PotionJokerHealth == 0)
        {
            HasPotionJoker = false;
            PocketedPotion = null;
        }

        FinishRoomAction();
    }

    /// <summary>
    /// Handle a monster with the Black Joker instead of fighting it: the joker absorbs the
    /// monster's full value into its own HP pool (<see cref="WeaponJokerHealth"/>), never
    /// reduced by a weapon and never touching the player's <see cref="Health"/>. The monster
    /// still leaves the room to the discard pile and counts toward CardsTakenThisRoom. If the
    /// joker's HP hits 0, the joker is lost entirely — <see cref="HasWeaponJoker"/> becomes
    /// false and any pocketed weapon is lost (not discarded), matching the "companion, not a
    /// card" framing used elsewhere for the jokers.
    /// </summary>
    public void FightWithWeaponJoker(CardModel monster)
    {
        if (!CanFightWithWeaponJoker(monster))
            throw new InvalidOperationException("Cannot handle this monster with the Weapon Joker right now.");

        _room.Remove(monster);
        _discard.Add(monster);
        WeaponJokerHealth = Math.Max(0, WeaponJokerHealth - monster.MonsterValue);

        if (WeaponJokerHealth == 0)
        {
            HasWeaponJoker = false;
            PocketedWeapon = null;
        }

        FinishRoomAction();
    }

    /// <summary>
    /// Apply a Blacksmith (diamond face card / Ace) to the equipped weapon (PRD §6, item 3).
    /// If there's no equipped weapon to blacksmith, or the player declines
    /// (<paramref name="activate"/> false), the card is recycled into a random position in
    /// the deck instead of being discarded — unlike Run, which returns cards to the back.
    /// Otherwise: if <see cref="SlainMonsterCount"/> is already 0 (nothing to remove), the
    /// card instead grants a rank-based attack bonus (Jack +1, Queen +2, King +3 —
    /// permanent; Ace +4 — single-use "Excalibur", see <see cref="SingleUseWeaponBonus"/>).
    /// If <see cref="SlainMonsterCount"/> is positive, the card removes from it instead
    /// (Jack 1, Queen 2, King 3, Ace all), clamped at 0.
    /// </summary>
    public void UseBlacksmith(CardModel card, bool activate = true)
    {
        if (!CanUseBlacksmith(card))
            throw new InvalidOperationException("Cannot use this Blacksmith card right now.");

        _room.Remove(card);
        ApplyBlacksmithEffect(card, activate);
        FinishRoomAction();
    }

    /// <summary>
    /// Sell the equipped weapon for HP using a Heart face card / Ace (PRD §6, item 4). If
    /// there's no equipped weapon to sell, or the player declines (<paramref
    /// name="activate"/> false), the card is recycled into a random position in the deck
    /// instead of being discarded — the same mechanism <see cref="UseBlacksmith"/> uses.
    /// Otherwise: HP gained = max(1, EquippedWeapon.WeaponValue - SlainMonsterCount) plus a
    /// rank bonus (Jack +0, Queen +1, King +3), applied via <see cref="ScoundrelRules.Heal"/>
    /// (capped at MaxHealth). The Ace of Hearts is a special case that ignores
    /// SlainMonsterCount entirely: HP gained = EquippedWeapon.WeaponValue + 5. Selling clears
    /// the main weapon system back to its no-weapon defaults and discards the old weapon —
    /// see <see cref="ApplyMerchantEffect"/>.
    /// </summary>
    public void UseMerchant(CardModel card, bool activate = true)
    {
        if (!CanUseMerchant(card))
            throw new InvalidOperationException("Cannot use this Merchant card right now.");

        _room.Remove(card);
        ApplyMerchantEffect(card, activate);
        FinishRoomAction();
    }

    /// <summary>
    /// Advance to the next room, carrying over any remaining card.
    /// Requires at least MinCardsTaken cards to have been taken this room.
    /// </summary>
    public void NextRoom()
    {
        if (IsOver) throw new InvalidOperationException("Game is over.");
        if (CardsTakenThisRoom < ScoundrelRules.MinCardsTaken)
            throw new InvalidOperationException(
                $"Must take at least {ScoundrelRules.MinCardsTaken} cards before advancing.");

        RanLastRoom = false;
        DealRoom();
    }

    /// <summary>
    /// Return all room cards to the bottom of the deck and deal a new room.
    /// Cannot be used two rooms in a row.
    /// </summary>
    public void Run()
    {
        if (IsOver) throw new InvalidOperationException("Game is over.");
        if (RanLastRoom) throw new InvalidOperationException("Cannot run two rooms in a row.");

        var runCards = _room.ToList();
        _room.Clear();
        _deck.InsertRange(0, runCards);

        RanLastRoom = true;
        DealRoom();
    }

    // ── Internal ──────────────────────────────────────────────────────────

    /// <summary>
    /// Heal from a potion, or mark it wasted if one was already drunk this room.
    /// Shared by TakeCard's Hearts branch and RetrievePotion.
    /// </summary>
    private void ApplyPotionHealOrWaste(CardModel potion)
    {
        if (!PotionUsedThisRoom)
        {
            Health = ScoundrelRules.Heal(Health, potion.PotionValue);
            PotionUsedThisRoom = true;
        }
        else
        {
            PotionWastedThisRoom = true;
        }
    }

    private void CheckGameOver()
    {
        if (Health <= 0)
            GameOver = true;
    }

    /// <summary>
    /// Common tail for actions that consume one of the room's card slots (TakeCard,
    /// StorePotion): counts toward CardsTakenThisRoom, checks for game over, and
    /// refills/wins the room when empty. Not used by RetrievePotion, which is a side
    /// action that doesn't touch room state.
    /// </summary>
    private void FinishRoomAction()
    {
        CardsTakenThisRoom++;

        CheckGameOver();
        if (GameOver) return;

        if (_room.Count == 0)
        {
            if (_deck.Count == 0)
                Won = true;
            else
                DealRoom();
        }
    }

    private void DealRoom()
    {
        PotionUsedThisRoom  = false;
        PotionWastedThisRoom = false;
        CardsTakenThisRoom  = 0;

        int needed = ScoundrelRules.RoomSize - _room.Count;
        for (int i = 0; i < needed && _deck.Count > 0; i++)
        {
            _room.Add(_deck[^1]);
            _deck.RemoveAt(_deck.Count - 1);
        }
    }

    private void ApplyMonsterDamage(CardModel card, bool useWeapon)
    {
        int damage = card.MonsterValue;
        if (useWeapon && EquippedWeapon != null && ScoundrelRules.CanUseWeapon(card.MonsterValue, WeaponFloor))
        {
            int effectiveWeaponValue = EquippedWeapon.WeaponValue + WeaponAttackBonus + SingleUseWeaponBonus;
            damage = ScoundrelRules.CalcDamage(card.MonsterValue, effectiveWeaponValue);
            WeaponFloor = ScoundrelRules.NextWeaponFloor(card.MonsterValue);
            SlainMonsterCount++;
            SingleUseWeaponBonus = 0;
        }
        Health = Math.Max(0, Health - damage);
    }

    private void EquipWeapon(CardModel card)
    {
        if (EquippedWeapon != null)
            _discard.Add(EquippedWeapon);
        EquippedWeapon = card;
        WeaponFloor = int.MaxValue;
        SlainMonsterCount = 0;
        WeaponAttackBonus = 0;
        SingleUseWeaponBonus = 0;
    }

    /// <summary>
    /// Recycle a Blacksmith/Merchant card that couldn't or wouldn't be used into a random
    /// position in the deck (PRD §6, item 3/4) — distinct from Run, which always returns
    /// cards to the bottom (index 0).
    /// </summary>
    private void RecycleIntoDeck(CardModel card)
    {
        int index = _rng.Next(0, _deck.Count + 1);
        _deck.Insert(index, card);
    }

    /// <summary>
    /// Core Blacksmith effect (PRD §6, item 3), shared by <see cref="UseBlacksmith"/> and
    /// <see cref="TakeCard"/>'s Blacksmith branch. Assumes the card has already been
    /// removed from the room; does not touch room/CardsTakenThisRoom bookkeeping — callers
    /// are responsible for that (via FinishRoomAction).
    /// </summary>
    private void ApplyBlacksmithEffect(CardModel card, bool activate)
    {
        if (!activate || EquippedWeapon == null)
        {
            RecycleIntoDeck(card);
            return;
        }

        if (SlainMonsterCount == 0)
        {
            // Nothing to remove — grant a rank-based bonus instead. J/Q/K bonuses are
            // permanent; the Ace's is single-use ("Excalibur"), consumed after the next
            // weapon-blocked fight (see ApplyMonsterDamage).
            switch (card.Rank)
            {
                case 11: WeaponAttackBonus += 1; break;                        // Jack
                case 12: WeaponAttackBonus += 2; break;                        // Queen
                case 13: WeaponAttackBonus += 3; break;                        // King
                case ScoundrelRules.AceRank: SingleUseWeaponBonus += 4; break; // Ace
            }
        }
        else
        {
            int removal = card.Rank switch
            {
                11 => 1,                                     // Jack
                12 => 2,                                     // Queen
                13 => 3,                                     // King
                ScoundrelRules.AceRank => SlainMonsterCount,  // Ace — remove all
                _ => 0,
            };
            SlainMonsterCount = Math.Max(0, SlainMonsterCount - removal);
        }

        _discard.Add(card);
    }

    /// <summary>
    /// Core Merchant effect (PRD §6, item 4), shared by <see cref="UseMerchant"/> and
    /// <see cref="TakeCard"/>'s Merchant branch. Assumes the card has already been removed
    /// from the room; does not touch room/CardsTakenThisRoom bookkeeping — callers are
    /// responsible for that (via FinishRoomAction).
    /// </summary>
    private void ApplyMerchantEffect(CardModel card, bool activate)
    {
        if (!activate || EquippedWeapon == null)
        {
            RecycleIntoDeck(card);
            return;
        }

        var oldWeapon = EquippedWeapon;
        int hpGain;
        if (card.Rank == ScoundrelRules.AceRank)
        {
            // Ace of Hearts: full unreduced weapon value + 5, ignoring SlainMonsterCount
            // wear entirely — distinct from the J/Q/K formula below.
            hpGain = oldWeapon.WeaponValue + 5;
        }
        else
        {
            int bonus = card.Rank switch
            {
                11 => 0, // Jack
                12 => 1, // Queen
                13 => 3, // King
                _ => 0,
            };
            hpGain = Math.Max(1, oldWeapon.WeaponValue - SlainMonsterCount) + bonus;
        }

        Health = ScoundrelRules.Heal(Health, hpGain);

        _discard.Add(oldWeapon);
        EquippedWeapon = null;
        WeaponFloor = int.MaxValue;
        SlainMonsterCount = 0;
        WeaponAttackBonus = 0;
        SingleUseWeaponBonus = 0;

        _discard.Add(card);
    }
}
