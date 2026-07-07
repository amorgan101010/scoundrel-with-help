using GdUnit4;
using Godot;
using GArray = Godot.Collections.Array;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Shared fixed deck and input/lookup helpers for the Scoundrel scene-test suites
/// (ScoundrelRoomFlowSceneTests, ScoundrelCombatSceneTests, etc.). Not itself a
/// [TestSuite] — a plain static class, same pattern as UITimings.cs — so gdUnit4's
/// directory scan of scene_tests/ skips it while every suite gets it via
/// `using static SceneTestHelpers;`.
/// </summary>
public static class SceneTestHelpers
{
    // ── Fixed deck ────────────────────────────────────────────────────────────
    //
    // Deck is bottom→top; last 4 (indices 4-7) are dealt to Room 1 first.
    //
    // Room 1: 6♦ W(6) | 4♣ M(4) | 5♥ P(5) | 8♠ M(8)
    //   get_all_cards() order matches deal order: [6♦, 4♣, 5♥, 8♠]
    //   cards[0] is always 6_diamonds (weapon, 0 damage) — safe for click/drag tests.
    //   Max single-card damage in Room 1 is 8 (8_spades) — never lethal from 20 HP.
    //
    // Room 2: 2♣ M(2) | 7♥ P(7) | 4♠ M(4) | 3♣ M(3)
    //
    public static readonly List<CardModel> FixedDeck = new()
    {
        // Room 2 (bottom of deck)
        new CardModel(Suit.Clubs,    2, "2_clubs"),
        new CardModel(Suit.Hearts,   7, "7_hearts"),
        new CardModel(Suit.Spades,   4, "4_spades"),
        new CardModel(Suit.Clubs,    3, "3_clubs"),
        // Room 1 (top of deck, dealt first)
        new CardModel(Suit.Spades,   8, "8_spades"),
        new CardModel(Suit.Hearts,   5, "5_hearts"),
        new CardModel(Suit.Clubs,    4, "4_clubs"),
        new CardModel(Suit.Diamonds, 6, "6_diamonds"),
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static int ParseHP(Node scene)
    {
        // Label text format: "HP: X / 20"
        var parts = scene.GetNode<Label>("UI/HealthLabel").Text.Split(' ');
        return int.Parse(parts[1]);
    }

    public static GodotObject? FindRoomCard(Node scene, Func<string, bool> suitMatch)
    {
        var room = scene.GetNode("UI/RoomContainer");
        foreach (var obj in (GArray)room.Call("get_all_cards"))
        {
            var card = obj.AsGodotObject();
            var suit = card.Get("card_info").AsGodotDictionary()["suit"].AsString();
            if (suitMatch(suit)) return card;
        }
        return null;
    }

    // Blacksmith/Merchant cards share the "diamonds"/"hearts" suit string with real
    // weapons/potions (only Rank distinguishes them), so tests needing one specific
    // card must match by exact name rather than by suit.
    public static GodotObject? FindRoomCardByName(Node scene, string name)
    {
        var room = scene.GetNode("UI/RoomContainer");
        foreach (var obj in (GArray)room.Call("get_all_cards"))
        {
            var card = obj.AsGodotObject();
            if (card.Get("card_info").AsGodotDictionary()["name"].AsString() == name) return card;
        }
        return null;
    }

    public static void ClickCard(Node scene, GodotObject card) =>
        scene.GetNode("UI/RoomContainer").EmitSignal("card_selected", card);

    public static int MonsterDamage(Godot.Collections.Dictionary info)
    {
        int rank = info["rank"].AsInt32();
        return rank == 1 ? 14 : rank;  // ace counts as 14
    }

    // Reset the scene to FixedDeck and wait for deal animations to settle.
    // Pass settleMs=UITimings.DragAnimationMs for tests that send real mouse input — DraggableObject silently
    // rejects clicks while a card is in MOVING state (animating to its slot), and a fresh
    // StartGameWithDeck call gets no free frames from the loader like BeforeTest does.
    public static async Task SetupFixedDeck(ISceneRunner runner, uint settleMs = UITimings.AnimationSettleMs)
    {
        var game = (ScoundrelGame)runner.Scene();
        game.StartGameWithDeck(new List<CardModel>(FixedDeck));
        await runner.AwaitMillis(settleMs);
    }

    // Simulate a real mouse click on a card using the scene runner's input API.
    // Uses separate press + idle frame + release rather than SimulateMouseButtonPressed,
    // because the combined call fires both events atomically — no frame in between for
    // the HOLDING state and _holding_cards to be populated before release_holding_cards fires.
    public static async Task MouseClickCard(ISceneRunner runner, GodotObject card)
    {
        var pos = (Vector2)card.Get("global_position");
        runner.SimulateMouseMove(pos);
        await runner.AwaitMillis(UITimings.MouseHoverDelayMs);  // let hover state register
        runner.SimulateMouseButtonPress(MouseButton.Left, false);
        await runner.AwaitIdleFrame();  // let HOLDING state register in _holding_cards
        runner.SimulateMouseButtonRelease(MouseButton.Left);
        await runner.AwaitMillis(UITimings.PostInputSettleMs);  // wait for game logic + animation
    }

    // Returns the centre of RightDropZone in viewport coordinates. Used as the
    // default drag target so tests aren't coupled to a fixed viewport width.
    public static Vector2 RightZoneCenter(ISceneRunner runner)
    {
        var zone = runner.Scene().GetNode<Control>("UI/RightPanel/RightDropZone");
        return zone.GlobalPosition + zone.Size * 0.5f;
    }

    // Returns the centre of the Potion/Weapon Joker fight sub-zones (the bottom
    // half of LeftPanel, split left/right) in viewport coordinates.
    public static Vector2 PotionJokerZoneCenter(ISceneRunner runner)
    {
        var zone = runner.Scene().GetNode<Control>("UI/LeftPanel/PotionJokerDropZone");
        return zone.GlobalPosition + zone.Size * 0.5f;
    }

    public static Vector2 WeaponJokerZoneCenter(ISceneRunner runner)
    {
        var zone = runner.Scene().GetNode<Control>("UI/LeftPanel/WeaponJokerDropZone");
        return zone.GlobalPosition + zone.Size * 0.5f;
    }

    // Simulate a mouse drag: press on card, move to a drop zone, release.
    // Default target is the centre of RightDropZone (right third of viewport).
    public static async Task MouseDragCard(ISceneRunner runner, GodotObject card, Vector2? dropTarget = null)
    {
        var pos    = (Vector2)card.Get("global_position");
        var target = dropTarget ?? RightZoneCenter(runner);
        runner.SimulateMouseMove(pos);
        await runner.AwaitMillis(UITimings.MouseHoverDelayMs);
        runner.SimulateMouseButtonPress(MouseButton.Left, false);
        await runner.AwaitMillis(UITimings.MouseDragDelayMs);
        runner.SimulateMouseMove(target);
        await runner.AwaitMillis(UITimings.MouseDragDelayMs);
        runner.SimulateMouseButtonRelease(MouseButton.Left);
        await runner.AwaitMillis(UITimings.PostInputSettleMs);
    }

    // Computes a card's actual on-screen bounding rect, accounting for `scale`.
    // A Control scales around `pivot_offset` (Card.gd always sets this to
    // unscaled card_size / 2), so `global_position`/`size` alone don't reflect a
    // shrunk card's real footprint — the pivot point is fixed under scaling, so
    // the visual top-left shifts toward it by pivot * (1 - scale). Used to assert
    // a pocketed (shrunk) potion/weapon never overlaps the HP label above its
    // slot — see ShrinkCardForPocket in ScoundrelGame.cs.
    public static Rect2 VisualRect(GodotObject card)
    {
        var position = (Vector2)card.Get("global_position");
        var pivot    = (Vector2)card.Get("pivot_offset");
        var scale    = (Vector2)card.Get("scale");
        var size     = (Vector2)card.Get("size");

        var visualTopLeft = position + pivot * (Vector2.One - scale);
        var visualSize    = size * scale;
        return new Rect2(visualTopLeft, visualSize);
    }
}
