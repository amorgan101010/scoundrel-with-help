using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoundrelTests;

// ── Extended Rules: friendly-NPC-only win condition (PRD §6) ───────────────────
//
// With Extended Rules active, a no-weapon player recycles every declined Blacksmith/
// Merchant card back into the deck instead of discarding it (see ApplyBlacksmithEffect/
// ApplyMerchantEffect), so if the only cards left in the deck+room are Blacksmith/Merchant,
// the deck can never actually empty via the classic win path. GameEngine.FinishRoomAction
// resolves this by winning immediately once every remaining deck+room card is a Blacksmith
// or Merchant card.

[TestFixture]
public class FriendlyNpcWinTests
{
    [Test]
    public void AllBlacksmithAndMerchant_TriggersImmediateWin()
    {
        var deck = new[]
        {
            Cards.Blacksmith(11),
            Cards.Merchant(12),
            Cards.Blacksmith(13),
            Cards.Merchant(ScoundrelRules.AceRank),
        };
        var engine = new GameEngine(deck, extendedRules: true);
        Assert.That(engine.Deck, Is.Empty, "sanity check: all 4 cards dealt into the room");

        // No weapon equipped -> the taken card is recycled back into the deck rather than
        // discarded, but it's still a Blacksmith/Merchant card, so the deck+room pool
        // remains entirely friendly NPCs.
        engine.TakeCard(engine.Room[0]);

        Assert.That(engine.Won, Is.True);
        Assert.That(engine.GameOver, Is.False);
    }

    [Test]
    public void SingleMonsterAmongFriendlyNpcs_BlocksWin()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.Monster(5) };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room.First(c => c.IsBlacksmith)); // no weapon -> recycled

        Assert.That(engine.Won, Is.False, "a monster still in the room must block the win");
    }

    [Test]
    public void SinglePotionAmongFriendlyNpcs_BlocksWin()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.Potion(5) };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room.First(c => c.IsBlacksmith));

        Assert.That(engine.Won, Is.False, "a potion still in the room must block the win");
    }

    [Test]
    public void SingleWeaponAmongFriendlyNpcs_BlocksWin()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.Weapon(5) };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room.First(c => c.IsBlacksmith));

        Assert.That(engine.Won, Is.False, "a weapon still in the room must block the win");
    }

    [Test]
    public void UndrawnRedJoker_BlocksWin()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.RedJoker() };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room.First(c => c.IsBlacksmith));

        Assert.That(engine.Won, Is.False, "the undrawn Red Joker still sits in the room and must block the win");
    }

    [Test]
    public void UndrawnBlackJoker_BlocksWin()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.BlackJoker() };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room.First(c => c.IsBlacksmith));

        Assert.That(engine.Won, Is.False, "the undrawn Black Joker still sits in the room and must block the win");
    }

    [Test]
    public void TakingRedJoker_LeavesOnlyFriendlyNpcs_WinsImmediately()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.RedJoker() };
        var engine = new GameEngine(deck, extendedRules: true);
        var joker  = engine.Room.First(c => c.IsPotionJoker);

        engine.TakeCard(joker); // becomes a companion, not a card in deck/room any more

        Assert.That(engine.HasPotionJoker, Is.True);
        Assert.That(engine.Won, Is.True,
            "an already-taken joker is a companion, not a deck/room card, so it must not block the win");
    }

    [Test]
    public void TakingBlackJoker_LeavesOnlyFriendlyNpcs_WinsImmediately()
    {
        var deck   = new[] { Cards.Blacksmith(11), Cards.Merchant(12), Cards.Blacksmith(13), Cards.BlackJoker() };
        var engine = new GameEngine(deck, extendedRules: true);
        var joker  = engine.Room.First(c => c.IsWeaponJoker);

        engine.TakeCard(joker);

        Assert.That(engine.HasWeaponJoker, Is.True);
        Assert.That(engine.Won, Is.True,
            "an already-taken joker is a companion, not a deck/room card, so it must not block the win");
    }

    [Test]
    public void EmptyDeckAndRoom_DoesNotWinViaFriendlyNpcCheck_OldExhaustedWinStillFires()
    {
        // Classic-only deck (no Blacksmith/Merchant cards at all). Exhausting it empties
        // deck and room together -- the pre-existing "deck exhausted" win path, distinct
        // from (and unaffected by) the new friendly-NPC-only check's vacuous-empty guard.
        var deck   = new[] { Cards.Potion(2), Cards.Potion(3), Cards.Potion(4), Cards.Potion(5) };
        var engine = new GameEngine(deck, extendedRules: true);

        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);
        engine.TakeCard(engine.Room[0]);

        Assert.That(engine.Deck, Is.Empty);
        Assert.That(engine.Room, Is.Empty);
        Assert.That(engine.Won, Is.True);
        Assert.That(engine.GameOver, Is.False);
    }

    [Test]
    public void GameOver_TakesPriorityOver_FriendlyNpcWin_InSameAction()
    {
        // Room = {Monster(6), Monster(Ace)=14, Merchant(12), Blacksmith(11)}, deck empty
        // once dealt. Taking the two monsters bare-handed drops Health exactly to 0 on the
        // same action that would otherwise leave only Blacksmith/Merchant cards behind.
        var monster6   = Cards.Monster(6);
        var monsterAce = Cards.Monster(ScoundrelRules.AceRank);
        var merchant   = Cards.Merchant(12);
        var blacksmith = Cards.Blacksmith(11);
        var deck       = new[] { blacksmith, merchant, monsterAce, monster6 };
        var engine     = new GameEngine(deck, extendedRules: true);
        Assert.That(engine.Deck, Is.Empty, "sanity check: all 4 cards dealt into the room");

        engine.TakeCard(monster6, useWeapon: false); // 20 -> 14
        Assert.That(engine.Health, Is.EqualTo(14), "sanity check on the damage arithmetic");
        Assert.That(engine.Won, Is.False);

        engine.TakeCard(monsterAce, useWeapon: false); // 14 -> 0 -> GameOver
        // Remaining room ({merchant, blacksmith}) is now entirely friendly NPCs, but
        // GameOver must win the race -- the same early-return FinishRoomAction already
        // uses ahead of the pre-existing "deck exhausted" win.

        Assert.That(engine.Health, Is.EqualTo(0));
        Assert.That(engine.GameOver, Is.True);
        Assert.That(engine.Won, Is.False);
    }
}
