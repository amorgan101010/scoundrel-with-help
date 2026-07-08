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
/// Room/game-flow scene tests: initial state, Run/Next Room/Retry/Help controls, and the drag-vs-click input mechanic.
/// Split out of the former ScoundrelSceneTests.cs; shared helpers live in SceneTestHelpers.cs.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScoundrelRoomFlowSceneTests
{
    private ISceneRunner? _runner;

    [BeforeTest]
    public async Task Setup()
    {
        _runner = ISceneRunner.Load("res://scenes/Game.tscn", true);
        // Let _Ready() run and the initial deal animations settle.
        await _runner.AwaitMillis(AnimationSettleMs);
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

    [TestCase(Description = "Game starts with full HP, 40-card deck, no weapon, 4 room cards")]
    public void InitialState()
    {
        var scene = _runner!.Scene();

        AssertThat(scene.GetNode<Label>("UI/HealthLabel").Text).IsEqual($"HP: {ScoundrelRules.MaxHealth} / {ScoundrelRules.MaxHealth}");
        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsEqual("Weapon: none");
        AssertThat((int)scene.GetNode("UI/RightPanel/DeckGroup/DeckPile").Call("get_card_count")).IsEqual(ScoundrelRules.DeckSize);
        AssertThat((int)scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile").Call("get_card_count")).IsEqual(0);

        var roomCards = (GArray)scene.GetNode("UI/RoomContainer").Call("get_all_cards");
        AssertThat(roomCards.Count).IsEqual(ScoundrelRules.RoomSize);
    }

    [TestCase(Description = "Weapon label stays legible and InPlayGroup never overlaps WeaponSlot after viewport shrink")]
    public async Task WeaponGroup_ResponsiveLayoutOnViewportResize()
    {
        await _runner!.AwaitMillis(AnimationSettleMs);

        var scene = _runner!.Scene();
        var weaponGroup = scene.GetNode<Control>("UI/LeftPanel/WeaponGroup");
        var weaponSlot = scene.GetNode<Control>("UI/LeftPanel/WeaponGroup/WeaponSlot");
        var inPlayGroup = scene.GetNode<Control>("UI/LeftPanel/WeaponGroup/InPlayGroup");
        var weaponLabel = scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel");

        int weaponFontSize = weaponLabel.GetThemeFontSize("font_size");
        AssertThat(weaponFontSize).IsGreaterEqual(14);

        var slotRect = new Rect2(weaponSlot.GlobalPosition, weaponSlot.Size);
        var inPlayRect = new Rect2(inPlayGroup.GlobalPosition, inPlayGroup.Size);
        AssertThat(inPlayRect.Intersects(slotRect)).IsFalse();

        bool rightOfSlot = inPlayRect.Position.X >= slotRect.End.X + 1f;
        bool belowSlot = inPlayRect.Position.Y >= slotRect.End.Y + 1f;
        AssertThat(rightOfSlot || belowSlot).IsTrue();

        const float viewportTolerance = 6f;
        var viewportRect = scene.GetViewport().GetVisibleRect();
        AssertThat(inPlayRect.Position.X).IsGreaterEqual(viewportRect.Position.X - viewportTolerance);
        AssertThat(inPlayRect.Position.Y).IsGreaterEqual(viewportRect.Position.Y - viewportTolerance);
        AssertThat(inPlayRect.End.X).IsLessEqual(viewportRect.End.X + viewportTolerance);
        AssertThat(inPlayRect.End.Y).IsLessEqual(viewportRect.End.Y + viewportTolerance);
    }

    [TestCase(Description = "Run button shuffles the room back and deals a fresh room of 4")]
    public async Task RunShufflesRoomAndDealsNext()
    {
        var scene = _runner!.Scene();
        var runButton = scene.GetNode<Button>("ButtonLayer/BottomButtonGroup/RunButton");
        AssertThat(runButton.Disabled).IsFalse();

        runButton.EmitSignal("pressed");
        await _runner!.AwaitIdleFrame();

        // 4 room cards returned to deck, then 4 new ones dealt → still 40 in deck
        AssertThat((int)scene.GetNode("UI/RightPanel/DeckGroup/DeckPile").Call("get_card_count")).IsEqual(ScoundrelRules.DeckSize);

        // Room must have 4 new cards
        var roomCards = (GArray)scene.GetNode("UI/RoomContainer").Call("get_all_cards");
        AssertThat(roomCards.Count).IsEqual(ScoundrelRules.RoomSize);
    }

    [TestCase(Description = "Run button is disabled for the room immediately after a run")]
    public async Task CannotRunTwiceInARow()
    {
        var scene = _runner!.Scene();
        var runButton = scene.GetNode<Button>("ButtonLayer/BottomButtonGroup/RunButton");

        AssertThat(runButton.Disabled).IsFalse();
        runButton.EmitSignal("pressed");
        await _runner!.AwaitIdleFrame();

        AssertThat(runButton.Disabled).IsTrue();
    }

    [TestCase(Description = "Next Room button appears after 3 cards are taken, then advances the room when clicked")]
    public async Task NextRoomButtonAppearsAndAdvancesRoom()
    {
        await SetupFixedDeck(_runner!);
        var scene = _runner!.Scene();
        var nextRoomButton = scene.GetNode<Button>("ButtonLayer/BottomButtonGroup/NextRoomButton");
        var room = scene.GetNode("UI/RoomContainer");

        AssertThat(nextRoomButton.Visible).IsFalse();

        // Take weapon + potion + weaker monster.
        // With weapon equipped, 4_clubs does 0 damage — player cannot die.
        ClickCard(scene, FindRoomCard(scene, s => s == "diamonds")!); // 6_diamonds (weapon)
        await _runner!.AwaitMillis(50);
        ClickCard(scene, FindRoomCard(scene, s => s == "hearts")!);   // 5_hearts (potion)
        await _runner!.AwaitMillis(InteractionDelayMs);
        ClickCard(scene, FindRoomCard(scene, s => s == "clubs")!);    // 4_clubs (0 dmg with weapon)
        await _runner!.AwaitMillis(InteractionDelayMs);

        AssertThat(nextRoomButton.Visible).IsTrue();
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(1);

        // Click the button — should deal leftover + 3 new cards
        nextRoomButton.EmitSignal("pressed");
        await _runner!.AwaitIdleFrame();

        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(ScoundrelRules.RoomSize);
        AssertThat(nextRoomButton.Visible).IsFalse();
    }

    [TestCase(Description = "Retry button resets the game to full HP with a fresh 4-card room")]
    public async Task RetryButton_ResetsGame()
    {
        await SetupFixedDeck(_runner!, DragAnimationMs);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Take one card so the room is no longer in the initial 4-card state.
        ClickCard(scene, FindRoomCard(scene, s => s == "diamonds")!); // equip weapon
        await _runner!.AwaitMillis(InteractionDelayMs * 4);  // 200ms
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(ScoundrelRules.RoomSize - 1);

        // Retry — TopButtonGroup was reparented to ButtonLayer in _Ready().
        var retryButton = scene.GetNode<Button>("ButtonLayer/TopButtonGroup/RetryButton");
        retryButton.EmitSignal("pressed");
        await _runner!.AwaitMillis(DragAnimationMs); // wait for new deal to settle

        AssertThat(ParseHP(scene)).IsEqual(ScoundrelRules.MaxHealth);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(ScoundrelRules.RoomSize);
    }

    [TestCase(Description = "Help button opens the help dialog")]
    public async Task HelpButton_OpensDialog()
    {
        var scene      = _runner!.Scene();
        var helpDialog = scene.GetNode<AcceptDialog>("UI/HelpDialog");
        AssertThat(helpDialog.Visible).IsFalse();

        // HelpButton is in TopButtonGroup which was reparented to ButtonLayer.
        var helpButton = scene.GetNode<Button>("ButtonLayer/TopButtonGroup/HelpButton");
        helpButton.EmitSignal("pressed");
        await _runner!.AwaitIdleFrame();

        AssertThat(helpDialog.Visible).IsTrue();
    }

    [TestCase(Description = "Real mouse click on a room card does nothing (drag-only controls)")]
    public async Task MouseClickDoesNotTakeCard()
    {
        await SetupFixedDeck(_runner!, DragAnimationMs);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        var cards = (GArray)room.Call("get_all_cards");
        AssertThat(cards.Count).IsEqual(ScoundrelRules.RoomSize);

        // cards[0] is always 6_diamonds (weapon) in the fixed deck.
        await MouseClickCard(_runner!, cards[0].AsGodotObject());

        // Room should still have RoomSize cards — a bare click is ignored.
        var after = (GArray)room.Call("get_all_cards");
        AssertThat(after.Count).IsEqual(ScoundrelRules.RoomSize);
    }

    [TestCase(Description = "Drag to the correct zone takes the card; click leaves the room unchanged")]
    public async Task DragTakesCard_ClickDoesNot()
    {
        await SetupFixedDeck(_runner!, DragAnimationMs);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Part 1: click — room count must stay at RoomSize (no zone reached).
        var cards = (GArray)room.Call("get_all_cards");
        await MouseClickCard(_runner!, cards[0].AsGodotObject());
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(ScoundrelRules.RoomSize);

        // Part 2: drag 6_diamonds (weapon) to LEFT zone — room count drops to RoomSize - 1.
        await MouseDragCard(_runner!, cards[0].AsGodotObject(), new Vector2(192f, 345f));
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(ScoundrelRules.RoomSize - 1);
    }

    [TestCase(Description = "After Run and animations settle, every room card is at a valid room slot position (regression: bug-014)")]
    public async Task RunPositionsCardsAtRoomSlots()
    {
        var scene     = _runner!.Scene();
        var runButton = scene.GetNode<Button>("ButtonLayer/BottomButtonGroup/RunButton");
        var room      = scene.GetNode("UI/RoomContainer");

        runButton.EmitSignal("pressed");
        // Default moving_speed = 2000 px/s; max travel ~1400 px → tween ≤ 700 ms.
        // 1200 ms gives headroom for the initial deal tweens to also complete.
        await _runner!.AwaitMillis(1200);

        var roomPos = (Vector2)room.Get("global_position");
        // Slot offsets from RoomContainer.gd: SLOTS = [V2(0,0), V2(245,0), V2(0,335), V2(245,335)]
        Vector2[] validSlots =
        {
            roomPos,
            roomPos + new Vector2(245f, 0f),
            roomPos + new Vector2(0f,   335f),
            roomPos + new Vector2(245f, 335f),
        };

        var roomCards = (GArray)room.Call("get_all_cards");
        AssertThat(roomCards.Count).IsEqual(4);

        var occupiedSlots = new bool[4];
        foreach (var obj in roomCards)
        {
            var card    = obj.AsGodotObject();
            var cardPos = (Vector2)card.Get("global_position");

            int matchedSlot = -1;
            for (int i = 0; i < validSlots.Length; i++)
            {
                if (cardPos.DistanceTo(validSlots[i]) < 2f)
                {
                    matchedSlot = i;
                    break;
                }
            }
            AssertThat(matchedSlot).IsNotEqual(-1);            // card must be at a valid slot
            AssertThat(occupiedSlots[matchedSlot]).IsFalse();  // each slot occupied at most once
            occupiedSlots[matchedSlot] = true;
        }
    }
}
