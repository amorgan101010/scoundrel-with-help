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

    public GameEngine(IEnumerable<CardModel> deck, bool extendedRules = false)
    {
        _deck = deck.ToList();
        ExtendedRules = extendedRules;
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
            else
            {
                // Blacksmith, Merchant, and the Black Joker: placeholder discard-only
                // behavior pending their own chunks.
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
            damage = ScoundrelRules.CalcDamage(card.MonsterValue, EquippedWeapon.WeaponValue);
            WeaponFloor = ScoundrelRules.NextWeaponFloor(card.MonsterValue);
            SlainMonsterCount++;
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
    }
}
