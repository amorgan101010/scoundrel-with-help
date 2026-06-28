using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure C# game-state machine for Scoundrel. No Godot dependencies — fully integration-testable.
/// Deck is passed in pre-ordered; index 0 = bottom, last index = top (dealt first).
/// </summary>
public class GameEngine
{
    private List<CardModel> _deck;
    private List<CardModel> _discard = new();
    private List<CardModel> _room    = new();

    public int Health { get; private set; } = ScoundrelRules.StartHealth;
    public CardModel? EquippedWeapon { get; private set; }
    public int WeaponFloor { get; private set; } = int.MaxValue;
    public bool PotionUsedThisRoom { get; private set; }
    public bool PotionWastedThisRoom { get; private set; }
    public bool RanLastRoom { get; private set; }
    public int CardsTakenThisRoom { get; private set; }
    public bool GameOver { get; private set; }
    public bool Won { get; private set; }

    public IReadOnlyList<CardModel> Deck    => _deck;
    public IReadOnlyList<CardModel> Discard => _discard;
    public IReadOnlyList<CardModel> Room    => _room;

    public bool IsOver     => GameOver || Won;
    public bool CanRun     => !IsOver && !RanLastRoom;
    public bool CanNextRoom => !IsOver && CardsTakenThisRoom >= ScoundrelRules.MinCardsTaken && _room.Count > 0;

    public GameEngine(IEnumerable<CardModel> deck)
    {
        _deck = deck.ToList();
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
            switch (card.Suit)
            {
                case Suit.Clubs:
                case Suit.Spades:
                    ApplyMonsterDamage(card, useWeapon);
                    _discard.Add(card);
                    break;

                case Suit.Hearts:
                    if (!PotionUsedThisRoom)
                    {
                        Health = ScoundrelRules.Heal(Health, card.PotionValue);
                        PotionUsedThisRoom = true;
                    }
                    else
                    {
                        PotionWastedThisRoom = true;
                    }
                    _discard.Add(card);
                    break;

                case Suit.Diamonds:
                    EquipWeapon(card);
                    break;
            }
        }

        CardsTakenThisRoom++;

        if (Health <= 0)
        {
            GameOver = true;
            return;
        }

        if (_room.Count == 0)
        {
            if (_deck.Count == 0)
                Won = true;
            else
                DealRoom();
        }
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

    // -- Monte Carlo
    public GameEngine Clone()
    {
        var clone = (GameEngine)this.MemberwiseClone();
    
        // Deep copy the collections to prevent reference sharing
        clone._deck = new List<CardModel>(this._deck);
        clone._discard = new List<CardModel>(this._discard);
        clone._room = new List<CardModel>(this._room);
    
        // EquippedWeapon is a CardModel. Assuming CardModel is a class, 
        // you might want to clone it too, or if it's treated immutably, sharing the reference is fine.
    
        return clone;
    }

    // Generate a unique hash or string to represent the exact state of the game
    public string GetStateHash()
    {
        var deckString = string.Join(",", _deck.Select(c => c.GetHashCode()));
        var roomString = string.Join(",", _room.Select(c => c.GetHashCode()));
        var weaponVal = EquippedWeapon?.WeaponValue ?? 0;
    
        return $"{Health}_{weaponVal}_{WeaponFloor}_{PotionUsedThisRoom}_{RanLastRoom}_{CardsTakenThisRoom}|{deckString}|{roomString}";
    }

    // ── Internal ──────────────────────────────────────────────────────────

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
        }
        Health = Math.Max(0, Health - damage);
    }

    private void EquipWeapon(CardModel card)
    {
        if (EquippedWeapon != null)
            _discard.Add(EquippedWeapon);
        EquippedWeapon = card;
        WeaponFloor = int.MaxValue;
    }
}
