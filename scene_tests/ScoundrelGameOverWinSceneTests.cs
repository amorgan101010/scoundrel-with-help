using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using GArray = Godot.Collections.Array;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SceneTestHelpers;

/// <summary>
/// Game-over and win-state scene tests, including bounce animations.
/// Split out of the former ScoundrelSceneTests.cs; shared helpers live in SceneTestHelpers.cs.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScoundrelGameOverWinSceneTests
{
    private ISceneRunner? _runner;

    [BeforeTest]
    public async Task Setup()
    {
        _runner = ISceneRunner.Load("res://scenes/Game.tscn", true);
        // Let _Ready() run and the initial deal animations settle.
        await _runner.AwaitMillis(UITimings.AnimationSettleMs);
    }

    [AfterTest]
    public async Task Teardown()
    {
        if (_runner != null)
        {
            // Move the simulated mouse off-screen before tearing down the scene.
            // Card.hovering_card_count and Card.holding_card_count are GDScript static
            // vars — they are NOT reset when a scene is freed. If a card is left in
            // HOVERING state (which happens when _on_drag_dropped restores mouse_filter
            // while the mouse is still inside the card, firing a spurious mouse_entered),
            // the count stays at 1 across scene reloads and _can_start_hovering() returns
            // false in every subsequent test, silently breaking all drags.
            _runner.SimulateMouseMove(new Vector2(-1000f, -1000f));
            await _runner.AwaitIdleFrame();
            _runner.Dispose();
        }
        _runner = null;
    }

    [TestCase(Description = "HP hitting zero shows YOU DIED status and disables the run button")]
    public async Task GameOver_ShowsDeathState()
    {
        // Deck with two high monsters in Room 1: king_clubs(13) + king_spades(13) = 26 total,
        // guaranteed to kill from 20 HP.
        var deathDeck = new List<CardModel>
        {
            // Padding for Room 2 (won't be reached)
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            // Room 1 (top of deck, dealt first)
            new CardModel(Suit.Clubs,  11, "jack_clubs"),
            new CardModel(Suit.Clubs,  12, "queen_clubs"),
            new CardModel(Suit.Spades, 13, "king_spades"),
            new CardModel(Suit.Clubs,  13, "king_clubs"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.StartGameWithDeck(deathDeck);
        await _runner!.AwaitMillis(200);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Find the two kings by name
        GodotObject? kingClubs = null, kingSpades = null;
        foreach (var obj in (GArray)room.Call("get_all_cards"))
        {
            var card = obj.AsGodotObject();
            var name = card.Get("card_info").AsGodotDictionary()["name"].AsString();
            if (name == "king_clubs")  kingClubs  = card;
            if (name == "king_spades") kingSpades = card;
        }
        AssertThat(kingClubs).IsNotNull();
        AssertThat(kingSpades).IsNotNull();

        ClickCard(scene, kingClubs!);   // -13 → HP 7
        await _runner!.AwaitIdleFrame();

        ClickCard(scene, kingSpades!);  // -13 → HP 0, game over
        await _runner!.AwaitIdleFrame();

        AssertThat(ParseHP(scene)).IsEqual(0);
        AssertThat(scene.GetNode<Label>("HudLayer/StatusLabel").Text).IsEqual("YOU DIED");
        AssertThat(scene.GetNode<Button>("ButtonLayer/BottomButtonGroup/RunButton").Disabled).IsTrue();
    }

    [TestCase(Description = "Game-over bounce must not hide cards currently in DeckPile (regression: deck stack appears uneven)")]
    public async Task GameOverBounce_KeepsDeckCardsVisible()
    {
        // Keep many monsters in the undealt portion of the deck, so bounce candidate
        // selection includes cards currently inside DeckPile.
        var deathDeck = new List<CardModel>
        {
            // Undealt deck content (remains in DeckPile when game over is triggered)
            new CardModel(Suit.Clubs, 2,  "2_clubs"),
            new CardModel(Suit.Spades, 3, "3_spades"),
            new CardModel(Suit.Clubs, 4,  "4_clubs"),
            new CardModel(Suit.Spades, 5, "5_spades"),
            new CardModel(Suit.Clubs, 6,  "6_clubs"),
            new CardModel(Suit.Spades, 7, "7_spades"),
            new CardModel(Suit.Clubs, 8,  "8_clubs"),
            new CardModel(Suit.Spades, 9, "9_spades"),

            // Room 1 (dealt first): two kings are enough to kill from full HP.
            new CardModel(Suit.Clubs, 11, "jack_clubs"),
            new CardModel(Suit.Clubs, 12, "queen_clubs"),
            new CardModel(Suit.Spades, 13, "king_spades"),
            new CardModel(Suit.Clubs, 13, "king_clubs"),
        };

        var game = (ScoundrelGame)_runner!.Scene();
        game.StartGameWithDeck(deathDeck);
        await _runner!.AwaitMillis(200);

        var scene = _runner!.Scene();
        var room = scene.GetNode("UI/RoomContainer");

        GodotObject? kingClubs = null;
        GodotObject? kingSpades = null;
        foreach (var obj in (GArray)room.Call("get_all_cards"))
        {
            var card = obj.AsGodotObject();
            var name = card.Get("card_info").AsGodotDictionary()["name"].AsString();
            if (name == "king_clubs") kingClubs = card;
            if (name == "king_spades") kingSpades = card;
        }

        AssertThat(kingClubs).IsNotNull();
        AssertThat(kingSpades).IsNotNull();

        ClickCard(scene, kingClubs!);
        await _runner!.AwaitIdleFrame();
        ClickCard(scene, kingSpades!);
        await _runner!.AwaitIdleFrame();

        AssertThat(scene.GetNode<Label>("HudLayer/StatusLabel").Text).IsEqual("YOU DIED");

        var deckPile = scene.GetNode("UI/RightPanel/DeckGroup/DeckPile");
        int deckCount = (int)deckPile.Call("get_card_count");
        var deckCards = (GArray)deckPile.Call("get_top_cards", deckCount);

        AssertThat(deckCards.Count).IsEqual(deckCount);
        foreach (var obj in deckCards)
        {
            var card = obj.AsGodotObject();
            AssertThat(card.Get("visible").AsBool()).IsTrue();
        }
    }

    [TestCase(Description = "Taking all cards from the last room shows YOU WIN")]
    public async Task Win_ShowsWinState()
    {
        // 4-card all-potion deck: deck is empty after initial deal, taking all 4 → Won.
        var winDeck = new List<CardModel>
        {
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.StartGameWithDeck(winDeck);
        await _runner!.AwaitMillis(200);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Take all 4 potions
        for (int i = 0; i < 4; i++)
        {
            var cards = (GArray)room.Call("get_all_cards");
            ClickCard(scene, cards[0].AsGodotObject());
            await _runner!.AwaitIdleFrame();
        }

        AssertThat(scene.GetNode<Label>("HudLayer/StatusLabel").Text).IsEqual("YOU WIN!");
    }

    [TestCase(Description = "Bounce animation activates on game over with one ghost per monster card in the deck")]
    public async Task BounceAnimation_ActivatesOnGameOver()
    {
        // 4 monsters. King♣ + King♠ = 26 damage > 20 HP → GameOver after 2 clicks.
        // All 4 cards in _godotCards are monsters → expect BounceCardCount = 4.
        var deathDeck = new List<CardModel>
        {
            new CardModel(Suit.Clubs,  11, "jack_clubs"),
            new CardModel(Suit.Clubs,  12, "queen_clubs"),
            new CardModel(Suit.Spades, 13, "king_spades"),
            new CardModel(Suit.Clubs,  13, "king_clubs"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.StartGameWithDeck(deathDeck);
        await _runner!.AwaitMillis(200);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Find the two kings by name
        GodotObject? kingClubs = null, kingSpades = null;
        foreach (var obj in (GArray)room.Call("get_all_cards"))
        {
            var card = obj.AsGodotObject();
            var name = card.Get("card_info").AsGodotDictionary()["name"].AsString();
            if (name == "king_clubs")  kingClubs  = card;
            if (name == "king_spades") kingSpades = card;
        }
        AssertThat(kingClubs).IsNotNull();
        AssertThat(kingSpades).IsNotNull();

        ClickCard(scene, kingClubs!);   // -13 → HP 7
        ClickCard(scene, kingSpades!);  // -13 → HP 0 → GameOver

        AssertThat(game.BounceActive).IsTrue();
        AssertThat(game.BounceCardCount).IsEqual(4);
    }

    [TestCase(Description = "Bounce animation activates on win with one ghost per loot card in the deck")]
    public async Task BounceAnimation_ActivatesOnWin()
    {
        // 4 loot cards (2 hearts + 2 diamonds): taking all 4 empties the room and deck → Won.
        // All 4 cards in _godotCards are loot → expect BounceCardCount = 4.
        var lootDeck = new List<CardModel>
        {
            new CardModel(Suit.Diamonds, 2, "2_diamonds"),
            new CardModel(Suit.Hearts,   3, "3_hearts"),
            new CardModel(Suit.Diamonds, 4, "4_diamonds"),
            new CardModel(Suit.Hearts,   5, "5_hearts"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.StartGameWithDeck(lootDeck);
        await _runner!.AwaitMillis(200);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        for (int i = 0; i < 4; i++)
        {
            var cards = (GArray)room.Call("get_all_cards");
            ClickCard(scene, cards[0].AsGodotObject());
            await _runner!.AwaitIdleFrame();
        }

        AssertThat(game.BounceActive).IsTrue();
        AssertThat(game.BounceCardCount).IsEqual(4);
    }
}
