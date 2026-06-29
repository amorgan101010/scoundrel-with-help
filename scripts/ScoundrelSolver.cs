using System;
using System.Collections.Generic;
using System.Linq;

public class ScoundrelSolver
{
    private HashSet<string> _visitedStates = new();
    public int StatesChecked { get; private set; } = 0;
    private readonly int _maxStates;

    public ScoundrelSolver(int maxStates = 100000)
    {
        _maxStates = maxStates;
    }

    public bool IsSolvable(GameEngine engine)
    {
        StatesChecked++;
        if (StatesChecked > _maxStates) 
        {
            // Throwing an exception lets us distinguish between "Proven Unsolvable" and "Gave Up"
            throw new Exception("Timeout: State limit reached"); 
        }
        // 1. Base Cases
        if (engine.Won) return true;
        if (engine.GameOver) return false;

        // 2. Memoization Check
        string stateHash = engine.GetStateHash();
        if (_visitedStates.Contains(stateHash))
        {
            return false; // We already explored this exact game state and it didn't result in a win
        }
        _visitedStates.Add(stateHash);

        // 3. Branching: Try all valid moves in the current state

        // Move A: Run from the room
        if (engine.CanRun)
        {
            var runClone = engine.Clone();
            runClone.Run();
            if (IsSolvable(runClone)) return true;
        }

        // Move B: Advance to the next room
        if (engine.CanNextRoom)
        {
            var nextRoomClone = engine.Clone();
            nextRoomClone.NextRoom();
            if (IsSolvable(nextRoomClone)) return true;
        }

        // Move C: Take a card from the current room
        // We iterate over a copy of the room list because taking a card removes it
        var currentRoom = engine.Room.ToList();
        foreach (var card in currentRoom)
        {
            // Branch C1: Take the card and USE the weapon (default behavior)
            var takeClone = engine.Clone();
            takeClone.TakeCard(card, useWeapon: true, activateCard: true);
            if (IsSolvable(takeClone)) return true;

            // Branch C2: Take the card but DO NOT use the weapon 
            // (Only matters for Monsters, where taking barehanded might be a strategic choice)
            if (card.Suit == Suit.Clubs || card.Suit == Suit.Spades)
            {
                var barehandClone = engine.Clone();
                barehandClone.TakeCard(card, useWeapon: false, activateCard: true);
                if (IsSolvable(barehandClone)) return true;
            }

            // Branch C3: If your rules allow discarding a card without activating it
            // (Based on your `activateCard = false` parameter)
            var discardClone = engine.Clone();
            discardClone.TakeCard(card, useWeapon: false, activateCard: false);
            if (IsSolvable(discardClone)) return true;
        }

        // If all branches fail, this shuffle/state is unsolvable
        return false;
    }
}