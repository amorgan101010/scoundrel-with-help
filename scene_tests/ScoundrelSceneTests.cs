using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using GArray = Godot.Collections.Array;
using System;
using System.Threading.Tasks;

/// <summary>
/// Scene-level integration tests for Scoundrel. Run via the gdUnit4 test runner
/// (editor toolbar or: godot --headless -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd).
///
/// These tests exercise the full Godot→C# signal chain that pure NUnit tests can't reach:
/// card clicks → ScoundrelGame.OnCardSelected → GameEngine → UI label updates.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScoundrelSceneTests
{
    private ISceneRunner? _runner;

    [BeforeTest]
    public async Task Setup()
    {
        _runner = ISceneRunner.Load("res://scenes/Game.tscn", true);
        // Let _Ready() run and card layout settle before each test.
        await _runner.AwaitMillis(200);
    }

    [AfterTest]
    public void Teardown()
    {
        _runner?.Dispose();
        _runner = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int ParseHP(Node scene)
    {
        // Label text format: "HP: X / 20"
        var parts = scene.GetNode<Label>("UI/HealthLabel").Text.Split(' ');
        return int.Parse(parts[1]);
    }

    private static GodotObject? FindRoomCard(Node scene, Func<string, bool> suitMatch)
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

    private static void ClickCard(Node scene, GodotObject card) =>
        scene.GetNode("UI/RoomContainer").EmitSignal("card_selected", card);

    private static int MonsterDamage(Godot.Collections.Dictionary info)
    {
        int rank = info["rank"].AsInt32();
        return rank == 1 ? 14 : rank;  // ace counts as 14
    }

    // Simulate a real mouse click on a card using the scene runner's input API.
    private async Task MouseClickCard(GodotObject card)
    {
        var pos = (Vector2)card.Get("global_position");
        _runner!.SimulateMouseMove(pos);
        await _runner!.AwaitMillis(100);  // let hover state register
        _runner!.SimulateMouseButtonPressed(MouseButton.Left, false);
        await _runner!.AwaitMillis(500);  // wait for game logic + animation
    }

    // Simulate a mouse drag: press on card, move 80 px right, release.
    private async Task MouseDragCard(GodotObject card)
    {
        var pos = (Vector2)card.Get("global_position");
        _runner!.SimulateMouseMove(pos);
        await _runner!.AwaitMillis(100);
        _runner!.SimulateMouseButtonPress(MouseButton.Left, false);
        await _runner!.AwaitMillis(80);
        _runner!.SimulateMouseMove(pos + new Vector2(80f, 0f));
        await _runner!.AwaitMillis(80);
        _runner!.SimulateMouseButtonRelease(MouseButton.Left);
        await _runner!.AwaitMillis(500);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [TestCase(Description = "Game starts with full HP, 40-card deck, no weapon, 4 room cards")]
    public void InitialState()
    {
        var scene = _runner!.Scene();

        AssertThat(scene.GetNode<Label>("UI/HealthLabel").Text).IsEqual("HP: 20 / 20");
        AssertThat(scene.GetNode<Label>("UI/WeaponLabel").Text).IsEqual("Weapon: none");
        AssertThat(scene.GetNode<Label>("UI/DeckLabel").Text).IsEqual("DECK (40)");
        AssertThat(scene.GetNode<Label>("UI/DiscardLabel").Text).IsEqual("DISCARD (0)");

        var roomCards = (GArray)scene.GetNode("UI/RoomContainer").Call("get_all_cards");
        AssertThat(roomCards.Count).IsEqual(4);
    }

    [TestCase(Description = "Clicking a monster card with no weapon reduces HP by its combat value")]
    public async Task TakingMonsterReducesHP()
    {
        var scene = _runner!.Scene();

        var monster = FindRoomCard(scene, s => s == "clubs" || s == "spades");
        if (monster == null) return;  // rare but possible — skip rather than fail

        var info = monster.Get("card_info").AsGodotDictionary();
        int expectedDamage = MonsterDamage(info);

        ClickCard(scene, monster);
        await _runner!.AwaitIdleFrame();

        AssertThat(ParseHP(scene)).IsEqual(20 - expectedDamage);
        AssertThat(scene.GetNode<Label>("UI/DiscardLabel").Text).IsEqual("DISCARD (1)");
    }

    [TestCase(Description = "Clicking a diamond weapon equips it and updates the weapon label")]
    public async Task TakingWeaponEquipsIt()
    {
        var scene = _runner!.Scene();

        var weapon = FindRoomCard(scene, s => s == "diamonds");
        if (weapon == null) return;  // 9/44 cards — skip if none in this room

        var info = weapon.Get("card_info").AsGodotDictionary();
        string cardName = info["name"].AsString();

        ClickCard(scene, weapon);
        await _runner!.AwaitIdleFrame();

        var weaponLabel = scene.GetNode<Label>("UI/WeaponLabel").Text;
        AssertThat(weaponLabel).IsNotEqual("Weapon: none");
        AssertThat(weaponLabel).Contains(cardName);
        // Weapon goes to weapon slot, not discard
        AssertThat(scene.GetNode<Label>("UI/DiscardLabel").Text).IsEqual("DISCARD (0)");
    }

    [TestCase(Description = "Clicking a potion card restores HP (capped at 20)")]
    public async Task TakingPotionRestoresHP()
    {
        var scene = _runner!.Scene();

        // Need to be below max HP first. Take a monster to take some damage.
        var monster = FindRoomCard(scene, s => s == "clubs" || s == "spades");
        if (monster == null) return;  // rare but possible — skip rather than fail

        int damage = MonsterDamage(monster.Get("card_info").AsGodotDictionary());
        ClickCard(scene, monster);
        await _runner!.AwaitIdleFrame();

        int hpAfterHit = ParseHP(scene);
        AssertThat(hpAfterHit).IsEqual(20 - damage);

        var potion = FindRoomCard(scene, s => s == "hearts");
        if (potion == null) return;  // no potion in this room — test is inconclusive but not a failure

        int potionRank = potion.Get("card_info").AsGodotDictionary()["rank"].AsInt32();
        int expectedHP = Math.Min(20, hpAfterHit + potionRank);

        ClickCard(scene, potion);
        await _runner!.AwaitIdleFrame();

        AssertThat(ParseHP(scene)).IsEqual(expectedHP);
    }

    [TestCase(Description = "Run button shuffles the room back and deals a fresh room of 4")]
    public async Task RunShufflesRoomAndDealsNext()
    {
        var scene = _runner!.Scene();
        var runButton = scene.GetNode<Button>("UI/RunButton");
        AssertThat(runButton.Disabled).IsFalse();

        runButton.EmitSignal("pressed");
        await _runner!.AwaitIdleFrame();

        // 4 room cards returned to deck, then 4 new ones dealt → still 40 in deck
        AssertThat(scene.GetNode<Label>("UI/DeckLabel").Text).IsEqual("DECK (40)");

        // Room must have 4 new cards
        var roomCards = (GArray)scene.GetNode("UI/RoomContainer").Call("get_all_cards");
        AssertThat(roomCards.Count).IsEqual(4);
    }

    [TestCase(Description = "Run button is disabled for the room immediately after a run")]
    public async Task CannotRunTwiceInARow()
    {
        var scene = _runner!.Scene();
        var runButton = scene.GetNode<Button>("UI/RunButton");

        AssertThat(runButton.Disabled).IsFalse();
        runButton.EmitSignal("pressed");
        await _runner!.AwaitIdleFrame();

        AssertThat(runButton.Disabled).IsTrue();
    }

    [TestCase(Description = "Next Room button is hidden initially and appears after 3 cards are taken")]
    public async Task NextRoomButtonAppearsAfterThreeCards()
    {
        var scene = _runner!.Scene();
        var nextRoomButton = scene.GetNode<Button>("UI/NextRoomButton");

        AssertThat(nextRoomButton.Visible).IsFalse();

        // Take 3 cards. Prefer non-monster cards to avoid dying from monster damage
        // totaling 20 (which causes game over and hides the button).
        // Fall back to any card if non-monster cards aren't available.
        for (int i = 0; i < 3; i++)
        {
            // Prefer weapons (0 damage) or potions (heals), fall back to any card.
            var card = FindRoomCard(scene, s => s == "diamonds" || s == "hearts")
                    ?? FindRoomCard(scene, _ => true);
            AssertThat(card).IsNotNull();
            if (card == null) return;
            ClickCard(scene, card);
            await _runner!.AwaitMillis(50);
        }

        // If all 3 cards were monsters and their total damage killed the player,
        // the game-over state hides the Next Room button — skip rather than fail.
        if (ParseHP(scene) <= 0) return;

        AssertThat(nextRoomButton.Visible).IsTrue();
    }

    [TestCase(Description = "Monster killed with weapon goes to slain pile, not discard")]
    public async Task WeaponedMonsterGoesToSlainPile()
    {
        var scene       = _runner!.Scene();
        var slainPile   = scene.GetNode("UI/SlainPile");
        var discardPile = scene.GetNode("UI/DiscardPile");

        var weapon = FindRoomCard(scene, s => s == "diamonds");
        if (weapon == null) return;
        ClickCard(scene, weapon);
        await _runner!.AwaitIdleFrame();

        var monster = FindRoomCard(scene, s => s == "clubs" || s == "spades");
        if (monster == null) return;
        ClickCard(scene, monster);
        await _runner!.AwaitIdleFrame();
        if (ParseHP(scene) <= 0) return;

        AssertThat((int)slainPile.Call("get_card_count")).IsEqual(1);
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(0);
    }

    [TestCase(Description = "Discard pile ordering: most recently added monster is the top card")]
    public async Task DiscardPileTopCardIsNewest()
    {
        var scene       = _runner!.Scene();
        var discardPile = scene.GetNode("UI/DiscardPile");

        // Collect two monsters from the room (no weapon → both go to discard).
        GodotObject? first = null, second = null;
        foreach (var obj in (GArray)scene.GetNode("UI/RoomContainer").Call("get_all_cards"))
        {
            var card = obj.AsGodotObject();
            var suit = card.Get("card_info").AsGodotDictionary()["suit"].AsString();
            if (suit != "clubs" && suit != "spades") continue;
            if (first == null) first = card;
            else { second = card; break; }
        }
        if (first == null || second == null) return;  // fewer than 2 monsters — skip

        ClickCard(scene, first);
        await _runner!.AwaitIdleFrame();
        if (ParseHP(scene) <= 0) return;

        ClickCard(scene, second);
        await _runner!.AwaitIdleFrame();
        if (ParseHP(scene) <= 0) return;

        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(2);

        var topCards = (GArray)discardPile.Call("get_top_cards", 1);
        AssertThat(topCards.Count).IsEqual(1);
        var topName  = topCards[0].AsGodotObject().Get("card_info").AsGodotDictionary()["name"].AsString();
        var expected = second.Get("card_info").AsGodotDictionary()["name"].AsString();
        AssertThat(topName).IsEqual(expected);
    }

    [TestCase(Description = "Real mouse click on a room card (via SimulateMouseButtonPressed) takes the card")]
    public async Task MouseClickTakesCard()
    {
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        var cards = (GArray)room.Call("get_all_cards");
        AssertThat(cards.Count).IsEqual(4);

        await MouseClickCard(cards[0].AsGodotObject());

        if (ParseHP(scene) <= 0) return;  // monster killed us — outcome valid but can't check room
        var after = (GArray)room.Call("get_all_cards");
        AssertThat(after.Count).IsEqual(3);
    }

    [TestCase(Description = "Dragging a room card and releasing it takes the card (same outcome as clicking)")]
    public async Task MouseDragTakesCard()
    {
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        var cards = (GArray)room.Call("get_all_cards");
        AssertThat(cards.Count).IsEqual(4);

        await MouseDragCard(cards[0].AsGodotObject());

        if (ParseHP(scene) <= 0) return;
        var after = (GArray)room.Call("get_all_cards");
        AssertThat(after.Count).IsEqual(3);
    }

    [TestCase(Description = "Drag and click produce identical game outcomes for the same card type")]
    public async Task DragAndClickProduceSameOutcome()
    {
        // Test 1: click a card, record outcome.
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        var cards = (GArray)room.Call("get_all_cards");
        var card  = cards[0].AsGodotObject();
        int hpBefore = ParseHP(scene);

        await MouseClickCard(card);
        int hpAfterClick = ParseHP(scene);
        int deckAfterClick = (int)scene.GetNode("UI/DeckPile").Call("get_card_count");

        // Reset for the drag test via Retry
        scene.GetNode<Button>("UI/RetryButton").EmitSignal("pressed");
        await _runner!.AwaitMillis(200);

        // Test 2: drag the same-index card (different instance, same room slot).
        var cards2 = (GArray)room.Call("get_all_cards");
        var card2  = cards2[0].AsGodotObject();
        AssertThat(ParseHP(scene)).IsEqual(hpBefore);  // HP reset

        await MouseDragCard(card2);
        int hpAfterDrag = ParseHP(scene);
        int deckAfterDrag = (int)scene.GetNode("UI/DeckPile").Call("get_card_count");

        // Both methods produce the same HP change and deck size.
        AssertThat(hpAfterDrag).IsEqual(hpAfterClick);
        AssertThat(deckAfterDrag).IsEqual(deckAfterClick);
    }

    [TestCase(Description = "After Run and animations settle, every room card is at a valid room slot position (regression: bug-014)")]
    public async Task RunPositionsCardsAtRoomSlots()
    {
        var scene     = _runner!.Scene();
        var runButton = scene.GetNode<Button>("UI/RunButton");
        var room      = scene.GetNode("UI/RoomContainer");

        runButton.EmitSignal("pressed");
        // Default moving_speed = 2000 px/s; max travel ~1400 px → tween ≤ 700 ms.
        // 1200 ms gives headroom for the initial deal tweens to also complete.
        await _runner!.AwaitMillis(1200);

        var roomPos = (Vector2)room.Get("global_position");
        // Slot offsets from RoomContainer.gd: SLOTS = [V2(0,0), V2(170,0), V2(0,230), V2(170,230)]
        Vector2[] validSlots =
        {
            roomPos,
            roomPos + new Vector2(170f, 0f),
            roomPos + new Vector2(0f,   230f),
            roomPos + new Vector2(170f, 230f),
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

    [TestCase(Description = "Monster taken when weapon floor is too low goes to discard, not slain")]
    public async Task ExpiredWeaponFloorMonsterGoesToDiscard()
    {
        var scene       = _runner!.Scene();
        var slainPile   = scene.GetNode("UI/SlainPile");
        var discardPile = scene.GetNode("UI/DiscardPile");

        var weapon = FindRoomCard(scene, s => s == "diamonds");
        if (weapon == null) return;
        ClickCard(scene, weapon);
        await _runner!.AwaitIdleFrame();

        // Kill first monster with weapon → goes to slain; weapon floor = monster1Value
        var monster1 = FindRoomCard(scene, s => s == "clubs" || s == "spades");
        if (monster1 == null) return;
        int monster1Value = MonsterDamage(monster1.Get("card_info").AsGodotDictionary());
        ClickCard(scene, monster1);
        await _runner!.AwaitIdleFrame();
        if (ParseHP(scene) <= 0) return;
        AssertThat((int)slainPile.Call("get_card_count")).IsEqual(1);

        // Need a second monster with value >= monster1Value so weapon floor blocks its use
        var monster2 = FindRoomCard(scene, s => s == "clubs" || s == "spades");
        if (monster2 == null) return;
        int monster2Value = MonsterDamage(monster2.Get("card_info").AsGodotDictionary());
        if (monster2Value < monster1Value) return;  // weapon still usable — skip

        ClickCard(scene, monster2);
        await _runner!.AwaitIdleFrame();
        if (ParseHP(scene) <= 0) return;

        // monster2 should be in discard (weapon floor too low); slain count unchanged
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(1);
        AssertThat((int)slainPile.Call("get_card_count")).IsEqual(1);

        var topCards = (GArray)discardPile.Call("get_top_cards", 1);
        var topName  = topCards[0].AsGodotObject().Get("card_info").AsGodotDictionary()["name"].AsString();
        var expected = monster2.Get("card_info").AsGodotDictionary()["name"].AsString();
        AssertThat(topName).IsEqual(expected);
    }
}
