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
    /// True once the Black Joker (Weapon Pocket companion) has been taken. The joker is a
    /// permanent companion, not a card in play — it is never discarded.
    /// </summary>
    public bool HasWeaponJoker { get; private set; }

    /// <summary>
    /// The weapon currently held in the Weapon Pocket, or null if the pocket is empty.
    /// </summary>
    public CardModel? PocketedWeapon { get; private set; }

    /// <summary>
    /// Degradation floor for the pocketed weapon. Fully independent of the main
    /// <see cref="WeaponFloor"/> — reset when a weapon is stored, tightened after each
    /// successful pocket-weapon fight.
    /// </summary>
    public int PocketWeaponFloor { get; private set; } = int.MaxValue;

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
    /// True iff the pocketed weapon can be used against the given monster right now: the
    /// joker has been taken, the pocket holds a weapon, the monster is in the room, the game
    /// isn't over, the pocket weapon's own floor allows it, AND — the stricter constraint
    /// that distinguishes the pocket weapon from the main one — using it would deal exactly
    /// zero damage.
    /// </summary>
    public bool CanFightWithPocketWeapon(CardModel monsterCard)
        => HasWeaponJoker && PocketedWeapon != null && monsterCard.IsMonster
           && _room.Contains(monsterCard) && !IsOver
           && ScoundrelRules.CanUseWeapon(monsterCard.MonsterValue, PocketWeaponFloor)
           && ScoundrelRules.CalcDamage(monsterCard.MonsterValue, PocketedWeapon!.WeaponValue) == 0;

    /// <summary>
    /// True iff the given Blacksmith card can be used right now: it's a Blacksmith card
    /// currently in the room and the game isn't over. Usable regardless of whether a
    /// weapon is equipped — the "cannot be used" case (no weapon) is handled inside
    /// <see cref="UseBlacksmith"/> by recycling the card, not by blocking the call.
    /// </summary>
    public bool CanUseBlacksmith(CardModel card)
        => card.IsBlacksmith && _room.Contains(card) && !IsOver;

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
    /// </param>
    public void TakeCard(CardModel card, bool useWeapon = true, bool activateCard = true)
    {
        if (IsOver) throw new InvalidOperationException("Game is over.");
        if (!_room.Remove(card)) throw new ArgumentException("Card is not in the room.");

        if (!activateCard)
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
                // play. It is never discarded — see StorePotion/RetrievePotion below.
                HasPotionJoker = true;
            }
            else if (card.IsWeaponJoker)
            {
                // Black Joker (Weapon Pocket): becomes a permanent companion, not a card in
                // play. It is never discarded — see StoreWeapon/FightWithPocketWeapon below.
                HasWeaponJoker = true;
            }
            else
            {
                // Blacksmith and Merchant: placeholder discard-only behavior pending their
                // own chunks.
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
    public void RetrievePotion()
    {
        if (!CanRetrievePotion)
            throw new InvalidOperationException("Cannot retrieve a potion right now.");

        var potion = PocketedPotion!;
        PocketedPotion = null;
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
        PocketWeaponFloor = int.MaxValue;
        FinishRoomAction();
    }

    /// <summary>
    /// Fight a monster using the pocketed weapon. Only ever available when it would deal
    /// exactly zero damage (see CanFightWithPocketWeapon) — health is therefore never
    /// touched. Degrades PocketWeaponFloor independently of the main WeaponFloor. Counts as
    /// one of the room's taken cards.
    /// </summary>
    public void FightWithPocketWeapon(CardModel monsterCard)
    {
        if (!CanFightWithPocketWeapon(monsterCard))
            throw new InvalidOperationException("Cannot fight with the pocketed weapon right now.");

        _room.Remove(monsterCard);
        _discard.Add(monsterCard);
        PocketWeaponFloor = ScoundrelRules.NextWeaponFloor(monsterCard.MonsterValue);
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

        if (!activate || EquippedWeapon == null)
        {
            RecycleIntoDeck(card);
        }
        else if (SlainMonsterCount == 0)
        {
            switch (card.Rank)
            {
                case 11: WeaponAttackBonus += 1; break;                     // Jack
                case 12: WeaponAttackBonus += 2; break;                     // Queen
                case 13: WeaponAttackBonus += 3; break;                     // King
                case ScoundrelRules.AceRank: SingleUseWeaponBonus += 4; break; // Ace — Excalibur
            }
            _discard.Add(card);
        }
        else
        {
            int removal = card.Rank switch
            {
                11 => 1,                              // Jack
                12 => 2,                              // Queen
                13 => 3,                              // King
                ScoundrelRules.AceRank => SlainMonsterCount, // Ace — remove all
                _ => 0,
            };
            SlainMonsterCount = Math.Max(0, SlainMonsterCount - removal);
            _discard.Add(card);
        }

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
}
