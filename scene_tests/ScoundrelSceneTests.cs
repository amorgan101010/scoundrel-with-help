using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using GArray = Godot.Collections.Array;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Scene-level integration tests for Scoundrel. Run via the gdUnit4 test runner
/// (editor toolbar or: godot --headless -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd).
///
/// These tests exercise the full Godot→C# signal chain that pure NUnit tests can't reach:
/// card clicks → ScoundrelGame.OnCardSelected → GameEngine → UI label updates.
///
/// Tests that depend on specific card types use SetupFixedDeck() to inject a deterministic
/// 8-card deck, guaranteeing the room composition. Tests must always assert — never silently
/// return to fake a pass when a required card isn't present.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScoundrelSceneTests
{
    private ISceneRunner? _runner;

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
    private static readonly List<CardModel> FixedDeck = new()
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

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [BeforeTest]
    public async Task Setup()
    {
        _runner = ISceneRunner.Load("res://scenes/Game.tscn", true);
        // Let _Ready() run and the initial deal animations settle.
        await _runner.AwaitMillis(200);
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

    // Reset the scene to FixedDeck and wait for deal animations to settle.
    // Pass settleMs=1200 for tests that send real mouse input — DraggableObject silently
    // rejects clicks while a card is in MOVING state (animating to its slot), and a fresh
    // StartGameWithDeck call gets no free frames from the loader like BeforeTest does.
    private async Task SetupFixedDeck(uint settleMs = 200)
    {
        var game = (ScoundrelGame)_runner!.Scene();
        game.StartGameWithDeck(new List<CardModel>(FixedDeck));
        await _runner!.AwaitMillis(settleMs);
    }

    // Simulate a real mouse click on a card using the scene runner's input API.
    // Uses separate press + idle frame + release rather than SimulateMouseButtonPressed,
    // because the combined call fires both events atomically — no frame in between for
    // the HOLDING state and _holding_cards to be populated before release_holding_cards fires.
    private async Task MouseClickCard(GodotObject card)
    {
        var pos = (Vector2)card.Get("global_position");
        _runner!.SimulateMouseMove(pos);
        await _runner!.AwaitMillis(100);  // let hover state register
        _runner!.SimulateMouseButtonPress(MouseButton.Left, false);
        await _runner!.AwaitIdleFrame();  // let HOLDING state register in _holding_cards
        _runner!.SimulateMouseButtonRelease(MouseButton.Left);
        await _runner!.AwaitMillis(500);  // wait for game logic + animation
    }

    // Returns the centre of RightDropZone in viewport coordinates. Used as the
    // default drag target so tests aren't coupled to a fixed viewport width.
    private Vector2 RightZoneCenter()
    {
        var zone = _runner!.Scene().GetNode<Control>("UI/RightPanel/RightDropZone");
        return zone.GlobalPosition + zone.Size * 0.5f;
    }

    // Simulate a mouse drag: press on card, move to a drop zone, release.
    // Default target is the centre of RightDropZone (right third of viewport).
    private async Task MouseDragCard(GodotObject card, Vector2? dropTarget = null)
    {
        var pos    = (Vector2)card.Get("global_position");
        var target = dropTarget ?? RightZoneCenter();
        _runner!.SimulateMouseMove(pos);
        await _runner!.AwaitMillis(100);
        _runner!.SimulateMouseButtonPress(MouseButton.Left, false);
        await _runner!.AwaitMillis(80);
        _runner!.SimulateMouseMove(target);
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
        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsEqual("Weapon: none");
        AssertThat((int)scene.GetNode("UI/RightPanel/DeckGroup/DeckPile").Call("get_card_count")).IsEqual(40);
        AssertThat((int)scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile").Call("get_card_count")).IsEqual(0);

        var roomCards = (GArray)scene.GetNode("UI/RoomContainer").Call("get_all_cards");
        AssertThat(roomCards.Count).IsEqual(4);
    }

    [TestCase(Description = "Clicking a monster card with no weapon reduces HP by its combat value")]
    public async Task TakingMonsterReducesHP()
    {
        await SetupFixedDeck();
        var scene = _runner!.Scene();

        // FixedDeck Room 1 always contains 4_clubs (monster, rank 4, value 4).
        var monster = FindRoomCard(scene, s => s == "clubs");
        AssertThat(monster).IsNotNull();

        int expectedDamage = MonsterDamage(monster!.Get("card_info").AsGodotDictionary());

        ClickCard(scene, monster);
        await _runner!.AwaitIdleFrame();

        AssertThat(ParseHP(scene)).IsEqual(20 - expectedDamage);
        AssertThat((int)scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile").Call("get_card_count")).IsEqual(1);
    }

    [TestCase(Description = "Clicking a diamond weapon equips it and updates the weapon label")]
    public async Task TakingWeaponEquipsIt()
    {
        await SetupFixedDeck();
        var scene = _runner!.Scene();

        // FixedDeck Room 1 always contains 6_diamonds (weapon, rank 6).
        var weapon = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon).IsNotNull();

        string cardName = weapon!.Get("card_info").AsGodotDictionary()["name"].AsString();

        ClickCard(scene, weapon);
        await _runner!.AwaitIdleFrame();

        var weaponLabel = scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text;
        AssertThat(weaponLabel).IsNotEqual("Weapon: none");
        AssertThat(weaponLabel).Contains(cardName);
        // Weapon goes to weapon slot, not discard
        AssertThat((int)scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile").Call("get_card_count")).IsEqual(0);
    }

    [TestCase(Description = "Clicking a potion card restores HP (capped at 20)")]
    public async Task TakingPotionRestoresHP()
    {
        await SetupFixedDeck();
        var scene = _runner!.Scene();

        // FixedDeck Room 1: take 8_spades first to take damage, then 5_hearts to heal.
        var monster = FindRoomCard(scene, s => s == "spades");
        AssertThat(monster).IsNotNull();

        int damage = MonsterDamage(monster!.Get("card_info").AsGodotDictionary());
        ClickCard(scene, monster);
        await _runner!.AwaitIdleFrame();

        int hpAfterHit = ParseHP(scene);
        AssertThat(hpAfterHit).IsEqual(20 - damage);

        var potion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(potion).IsNotNull();

        int potionRank = potion!.Get("card_info").AsGodotDictionary()["rank"].AsInt32();
        int expectedHP = Math.Min(20, hpAfterHit + potionRank);

        ClickCard(scene, potion);
        await _runner!.AwaitIdleFrame();

        AssertThat(ParseHP(scene)).IsEqual(expectedHP);
    }

    [TestCase(Description = "Run button shuffles the room back and deals a fresh room of 4")]
    public async Task RunShufflesRoomAndDealsNext()
    {
        var scene = _runner!.Scene();
        var runButton = scene.GetNode<Button>("UI/BottomButtonGroup/RunButton");
        AssertThat(runButton.Disabled).IsFalse();

        runButton.EmitSignal("pressed");
        await _runner!.AwaitIdleFrame();

        // 4 room cards returned to deck, then 4 new ones dealt → still 40 in deck
        AssertThat((int)scene.GetNode("UI/RightPanel/DeckGroup/DeckPile").Call("get_card_count")).IsEqual(40);

        // Room must have 4 new cards
        var roomCards = (GArray)scene.GetNode("UI/RoomContainer").Call("get_all_cards");
        AssertThat(roomCards.Count).IsEqual(4);
    }

    [TestCase(Description = "Run button is disabled for the room immediately after a run")]
    public async Task CannotRunTwiceInARow()
    {
        var scene = _runner!.Scene();
        var runButton = scene.GetNode<Button>("UI/BottomButtonGroup/RunButton");

        AssertThat(runButton.Disabled).IsFalse();
        runButton.EmitSignal("pressed");
        await _runner!.AwaitIdleFrame();

        AssertThat(runButton.Disabled).IsTrue();
    }

    [TestCase(Description = "Next Room button appears after 3 cards are taken, then advances the room when clicked")]
    public async Task NextRoomButtonAppearsAndAdvancesRoom()
    {
        await SetupFixedDeck();
        var scene = _runner!.Scene();
        var nextRoomButton = scene.GetNode<Button>("UI/BottomButtonGroup/NextRoomButton");
        var room = scene.GetNode("UI/RoomContainer");

        AssertThat(nextRoomButton.Visible).IsFalse();

        // Take weapon + potion + weaker monster.
        // With weapon equipped, 4_clubs does 0 damage — player cannot die.
        ClickCard(scene, FindRoomCard(scene, s => s == "diamonds")!); // 6_diamonds (weapon)
        await _runner!.AwaitMillis(50);
        ClickCard(scene, FindRoomCard(scene, s => s == "hearts")!);   // 5_hearts (potion)
        await _runner!.AwaitMillis(50);
        ClickCard(scene, FindRoomCard(scene, s => s == "clubs")!);    // 4_clubs (0 dmg with weapon)
        await _runner!.AwaitMillis(50);

        AssertThat(nextRoomButton.Visible).IsTrue();
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(1); // 8_spades remains

        // Click the button — should deal leftover + 3 new cards
        nextRoomButton.EmitSignal("pressed");
        await _runner!.AwaitIdleFrame();

        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(4);
        AssertThat(nextRoomButton.Visible).IsFalse(); // reset after advancing
    }

    [TestCase(Description = "Retry button resets the game to full HP with a fresh 4-card room")]
    public async Task RetryButton_ResetsGame()
    {
        await SetupFixedDeck(1200u);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Take one card so the room is no longer in the initial 4-card state.
        ClickCard(scene, FindRoomCard(scene, s => s == "diamonds")!); // equip weapon
        await _runner!.AwaitMillis(200u);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(3);

        // Retry — TopButtonGroup was reparented to ButtonLayer in _Ready().
        var retryButton = scene.GetNode<Button>("ButtonLayer/TopButtonGroup/RetryButton");
        retryButton.EmitSignal("pressed");
        await _runner!.AwaitMillis(1200u); // wait for new deal to settle

        AssertThat(ParseHP(scene)).IsEqual(20);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(4);
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

    [TestCase(Description = "Monster killed with weapon goes to discard and adds a badge to the weapon card")]
    public async Task WeaponedMonsterGetsSlainBadgeOnWeapon()
    {
        await SetupFixedDeck();
        var scene       = _runner!.Scene();
        var weaponSlot  = scene.GetNode("UI/LeftPanel/WeaponGroup/WeaponSlot");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        // Equip 6_diamonds
        var weapon = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitIdleFrame();

        // Fight 4_clubs with weapon (4 < MaxValue → can use weapon)
        var monster = FindRoomCard(scene, s => s == "clubs");
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitIdleFrame();

        // Monster goes to discard immediately; weapon card gets a badge child.
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(1);

        var weaponCards = (GArray)weaponSlot.Call("get_top_cards", 1);
        var weaponNode  = (Node)weaponCards[0].AsGodotObject();
        Node? badgeNode = null;
        foreach (var child in weaponNode.GetChildren())
            if (child.IsInGroup("slain_badge")) { badgeNode = child; break; }
        AssertThat(badgeNode).IsNotNull();

        string badgeText = "";
        foreach (var grandchild in badgeNode!.GetChildren())
            if (grandchild is Label lbl) { badgeText = lbl.Text; break; }
        AssertThat(badgeText).IsEqual("4"); // 4_clubs → rank 4
    }

    [TestCase(Description = "Discard pile ordering: most recently added monster is the top card")]
    public async Task DiscardPileTopCardIsNewest()
    {
        await SetupFixedDeck();
        var scene       = _runner!.Scene();
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        // No weapon equipped. FixedDeck Room 1 has 4_clubs and 8_spades.
        var clubs  = FindRoomCard(scene, s => s == "clubs");
        var spades = FindRoomCard(scene, s => s == "spades");
        AssertThat(clubs).IsNotNull();
        AssertThat(spades).IsNotNull();

        ClickCard(scene, clubs!);   // first to discard (4 damage — still alive)
        await _runner!.AwaitIdleFrame();

        ClickCard(scene, spades!);  // second to discard (8 more damage — still alive: 8 HP remaining)
        await _runner!.AwaitIdleFrame();

        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(2);

        var topCards = (GArray)discardPile.Call("get_top_cards", 1);
        AssertThat(topCards.Count).IsEqual(1);
        var topName  = topCards[0].AsGodotObject().Get("card_info").AsGodotDictionary()["name"].AsString();
        var expected = spades!.Get("card_info").AsGodotDictionary()["name"].AsString();
        AssertThat(topName).IsEqual(expected);
    }

    [TestCase(Description = "Real mouse click on a room card does nothing (drag-only controls)")]
    public async Task MouseClickDoesNotTakeCard()
    {
        await SetupFixedDeck(1200u);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        var cards = (GArray)room.Call("get_all_cards");
        AssertThat(cards.Count).IsEqual(4);

        // cards[0] is always 6_diamonds (weapon) in the fixed deck.
        await MouseClickCard(cards[0].AsGodotObject());

        // Room should still have 4 cards — a bare click is ignored.
        var after = (GArray)room.Call("get_all_cards");
        AssertThat(after.Count).IsEqual(4);
    }

    [TestCase(Description = "Dragging a weapon to the left zone equips it")]
    public async Task MouseDragTakesCard()
    {
        await SetupFixedDeck(1200u);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        var cards = (GArray)room.Call("get_all_cards");
        AssertThat(cards.Count).IsEqual(4);

        // cards[0] is always 6_diamonds (weapon) — drag to LEFT zone (fight/equip side).
        await MouseDragCard(cards[0].AsGodotObject(), new Vector2(192f, 345f));

        var after = (GArray)room.Call("get_all_cards");
        AssertThat(after.Count).IsEqual(3);
    }

    [TestCase(Description = "Drag to the correct zone takes the card; click leaves the room unchanged")]
    public async Task DragTakesCard_ClickDoesNot()
    {
        await SetupFixedDeck(1200u);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Part 1: click — room count must stay at 4 (no zone reached).
        var cards = (GArray)room.Call("get_all_cards");
        await MouseClickCard(cards[0].AsGodotObject());
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(4);

        // Part 2: drag 6_diamonds (weapon) to LEFT zone — room count drops to 3.
        await MouseDragCard(cards[0].AsGodotObject(), new Vector2(192f, 345f));
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(3);
    }

    [TestCase(Description = "Dragging a weapon to the right zone discards it without equipping")]
    public async Task DragWeaponToRightZoneDiscards()
    {
        await SetupFixedDeck(1200u);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // cards[0] = 6_diamonds (weapon). Right zone (x:820-1120) is the discard side.
        var cards = (GArray)room.Call("get_all_cards");
        await MouseDragCard(cards[0].AsGodotObject(), RightZoneCenter());

        // Card discarded — room has 3 cards, weapon slot still empty.
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(3);
        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsEqual("Weapon: none");
        AssertThat((int)scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile").Call("get_card_count")).IsEqual(1);
    }

    [TestCase(Description = "Dragging a potion to the left zone drinks it, healing the player")]
    public async Task DragPotionToLeftZoneDrinks()
    {
        await SetupFixedDeck(1200u);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Take 8_spades damage first so the heal is detectable (HP 20 → 12).
        var monster = FindRoomCard(scene, s => s == "spades");
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitMillis(200);

        int hpAfterDamage = ParseHP(scene);
        var potion = FindRoomCard(scene, s => s == "hearts"); // 5_hearts
        AssertThat(potion).IsNotNull();

        int potionRank = potion!.Get("card_info").AsGodotDictionary()["rank"].AsInt32();
        int expectedHP = Math.Min(20, hpAfterDamage + potionRank);

        // Drag left — LEFT zone is now "Drink"
        await MouseDragCard(potion, new Vector2(192f, 345f));

        AssertThat(ParseHP(scene)).IsEqual(expectedHP);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(2);
    }

    [TestCase(Description = "Dragging a potion to the right zone discards it without healing")]
    public async Task DragPotionToRightZoneDiscards()
    {
        await SetupFixedDeck(1200u);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Take damage so a heal would be detectable.
        var monster = FindRoomCard(scene, s => s == "spades"); // 8_spades
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitMillis(200);

        int hpAfterDamage = ParseHP(scene);
        var potion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(potion).IsNotNull();

        // Drag right — RIGHT zone is now "Discard" (no healing)
        await MouseDragCard(potion!, RightZoneCenter());

        // HP unchanged — potion discarded, not drunk.
        AssertThat(ParseHP(scene)).IsEqual(hpAfterDamage);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(2);
    }

    [TestCase(Description = "Dragging a monster to the right zone fights bare-handed, ignoring equipped weapon")]
    public async Task DragMonsterToRightZoneIsBarehanded()
    {
        await SetupFixedDeck(1200u);
        var scene = _runner!.Scene();

        // Equip 6_diamonds via direct signal so the weapon floor is fresh.
        var weapon = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitMillis(800); // let weapon move animation finish

        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsNotEqual("Weapon: none");

        // Drag 4_clubs (monster, value 4) to RIGHT zone — bare-handed, no weapon applied.
        var monster = FindRoomCard(scene, s => s == "clubs");
        AssertThat(monster).IsNotNull();
        await MouseDragCard(monster!, RightZoneCenter());

        // With weapon (value 6): damage would be 0.  Bare-handed: damage = 4.
        AssertThat(ParseHP(scene)).IsEqual(16);
    }

    [TestCase(Description = "Dragging a void potion to the (hidden) left zone bounces it back without discarding")]
    public async Task DragVoidPotionToLeftZoneBounces()
    {
        // 3 potions + 1 monster in Room 1 so we can drink one, then try to drag a void one left.
        var potionDeck = new List<CardModel>
        {
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Clubs, 4, "4_clubs"),
            new CardModel(Suit.Clubs, 5, "5_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Clubs,  6, "6_clubs"),
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.StartGameWithDeck(potionDeck);
        await _runner!.AwaitMillis(1200u); // settle for real mouse input

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Drink first potion via signal — PotionUsedThisRoom becomes true.
        // Remaining hearts potions are now void; left zone highlight is hidden for them.
        var firstPotion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(firstPotion).IsNotNull();
        ClickCard(scene, firstPotion!);
        await _runner!.AwaitMillis(200);

        var voidPotion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(voidPotion).IsNotNull();

        int countBefore = ((GArray)room.Call("get_all_cards")).Count;

        // Drag void potion to the left zone — bug-053 caused it to be discarded here.
        await MouseDragCard(voidPotion!, new Vector2(192f, 345f));

        // Card must bounce back: room count unchanged.
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(countBefore);
    }

    [TestCase(Description = "Dragging a monster to the left zone when weapon floor is exceeded bounces it back")]
    public async Task DragMonsterExceedingFloorToLeftZoneBounces()
    {
        await SetupFixedDeck(1200u);
        var scene = _runner!.Scene();

        // Equip 6_diamonds (value 6), then fight 4_clubs with weapon (floor → 4).
        ClickCard(scene, FindRoomCard(scene, s => s == "diamonds")!);
        await _runner!.AwaitMillis(400);
        ClickCard(scene, FindRoomCard(scene, s => s == "clubs")!);
        await _runner!.AwaitMillis(800);

        // 8_spades has value 8; CanUseWeapon(8, floor=4) = false → left zone blocked.
        var monster = FindRoomCard(scene, s => s == "spades");
        AssertThat(monster).IsNotNull();
        int hpBefore = ParseHP(scene);

        await MouseDragCard(monster!, new Vector2(192f, 345f)); // LEFT zone centre

        // Card bounced back — room unchanged, no damage taken.
        var room = scene.GetNode("UI/RoomContainer");
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(2); // hearts + spades remain
        AssertThat(ParseHP(scene)).IsEqual(hpBefore);
    }

    [TestCase(Description = "After Run and animations settle, every room card is at a valid room slot position (regression: bug-014)")]
    public async Task RunPositionsCardsAtRoomSlots()
    {
        var scene     = _runner!.Scene();
        var runButton = scene.GetNode<Button>("UI/BottomButtonGroup/RunButton");
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

    [TestCase(Description = "Monster too high for weapon floor goes to discard bare-handed; earlier weapon kill badge is preserved")]
    public async Task ExpiredWeaponFloorMonsterGoesToDiscard()
    {
        await SetupFixedDeck();
        // Room 1: 6♦(W,6), 4♣(M,4), 5♥(P,5), 8♠(M,8)
        // Strategy: equip weapon(6), fight 4_clubs (floor→4, badge on weapon, goes to discard),
        //           fight 8_spades (8 >= floor(4), weapon blocked → goes to discard bare-handed).
        var scene       = _runner!.Scene();
        var weaponSlot  = scene.GetNode("UI/LeftPanel/WeaponGroup/WeaponSlot");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        var weapon = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitIdleFrame();

        var monster1 = FindRoomCard(scene, s => s == "clubs");
        AssertThat(monster1).IsNotNull();
        ClickCard(scene, monster1!);  // 4 < MaxValue → badge added, goes to discard
        await _runner!.AwaitIdleFrame();
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(1);

        var weaponCards = (GArray)weaponSlot.Call("get_top_cards", 1);
        var weaponNode  = (Node)weaponCards[0].AsGodotObject();
        int badgeCount  = 0;
        foreach (var child in weaponNode.GetChildren())
            if (child.IsInGroup("slain_badge")) badgeCount++;
        AssertThat(badgeCount).IsEqual(1);

        var monster2 = FindRoomCard(scene, s => s == "spades");
        AssertThat(monster2).IsNotNull();
        ClickCard(scene, monster2!);  // 8 >= floor(4) → not blocked, goes to discard
        await _runner!.AwaitIdleFrame();

        // Both monsters in discard; weapon still has its 1 badge.
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(2);
        badgeCount = 0;
        foreach (var child in weaponNode.GetChildren())
            if (child.IsInGroup("slain_badge")) badgeCount++;
        AssertThat(badgeCount).IsEqual(1);

        var topCards = (GArray)discardPile.Call("get_top_cards", 1);
        var topName  = topCards[0].AsGodotObject().Get("card_info").AsGodotDictionary()["name"].AsString();
        var expected = monster2!.Get("card_info").AsGodotDictionary()["name"].AsString();
        AssertThat(topName).IsEqual(expected);
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
        AssertThat(scene.GetNode<Button>("UI/BottomButtonGroup/RunButton").Disabled).IsTrue();
    }

    [TestCase(Description = "Taking first potion tints remaining room potions; wasted potion keeps tint")]
    public async Task PotionVoidedVisualFeedback()
    {
        // 3 potions + 1 monster in Room 1; monster value 6 is never lethal from 20 HP.
        var potionDeck = new List<CardModel>
        {
            // Room 2 padding (never reached)
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Clubs, 4, "4_clubs"),
            new CardModel(Suit.Clubs, 5, "5_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Clubs,  6, "6_clubs"),
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.StartGameWithDeck(potionDeck);
        await _runner!.AwaitMillis(200);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");
        var white = new Color(1f, 1f, 1f);

        var allPotions = new List<GodotObject>();
        foreach (var obj in (GArray)room.Call("get_all_cards"))
        {
            var card = obj.AsGodotObject();
            if (card.Get("card_info").AsGodotDictionary()["suit"].AsString() == "hearts")
                allPotions.Add(card);
        }
        AssertThat(allPotions.Count).IsEqual(3);

        // All potions start untinted.
        foreach (var p in allPotions)
            AssertThat((Color)p.Get("modulate")).IsEqual(white);

        // Take first potion — remaining 2 should be tinted.
        ClickCard(scene, allPotions[0]);
        await _runner!.AwaitIdleFrame();

        var remaining = new List<GodotObject>();
        foreach (var obj in (GArray)room.Call("get_all_cards"))
        {
            var card = obj.AsGodotObject();
            if (card.Get("card_info").AsGodotDictionary()["suit"].AsString() == "hearts")
                remaining.Add(card);
        }
        AssertThat(remaining.Count).IsEqual(2);
        foreach (var p in remaining)
            AssertThat((Color)p.Get("modulate")).IsNotEqual(white);

        // Take second potion (wasted) — status message shown and last potion still tinted.
        ClickCard(scene, remaining[0]);
        await _runner!.AwaitIdleFrame();

        AssertThat(scene.GetNode<Label>("HudLayer/StatusLabel").Text).IsEqual("Potion wasted! (one per room)");

        var last = new List<GodotObject>();
        foreach (var obj in (GArray)room.Call("get_all_cards"))
        {
            var card = obj.AsGodotObject();
            if (card.Get("card_info").AsGodotDictionary()["suit"].AsString() == "hearts")
                last.Add(card);
        }
        AssertThat(last.Count).IsEqual(1);
        AssertThat((Color)last[0].Get("modulate")).IsNotEqual(white);
    }

    // ── Sound effect tests ────────────────────────────────────────────────────

    [TestCase(Description = "Dealing cards plays the card-dealt sound")]
    public async Task DealingCards_PlaysCardDealtSound()
    {
        await SetupFixedDeck();
        var game = (ScoundrelGame)_runner!.Scene();
        AssertThat(game.LastSfxPlayed).IsEqual("card_dealt");
    }

    [TestCase(Description = "Fighting a monster plays the punch sound")]
    public async Task FightingMonster_PlaysPunchSound()
    {
        await SetupFixedDeck();
        var scene = _runner!.Scene();

        var monster = FindRoomCard(scene, s => s == "clubs");
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitIdleFrame();

        AssertThat(((ScoundrelGame)scene).LastSfxPlayed).IsEqual("punch");
    }

    [TestCase(Description = "Drinking a potion plays the bubbles sound")]
    public async Task DrinkingPotion_PlaysBubblesSound()
    {
        await SetupFixedDeck();
        var scene = _runner!.Scene();

        var potion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(potion).IsNotNull();
        ClickCard(scene, potion!);
        await _runner!.AwaitIdleFrame();

        AssertThat(((ScoundrelGame)scene).LastSfxPlayed).IsEqual("bubbles");
    }

    [TestCase(Description = "Discarding a potion to the right zone plays the potion-discard sound")]
    public async Task DiscardingPotion_PlaysDiscardSound()
    {
        await SetupFixedDeck(1200u);
        var scene = _runner!.Scene();

        var potion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(potion).IsNotNull();
        await MouseDragCard(potion!, RightZoneCenter());

        AssertThat(((ScoundrelGame)scene).LastSfxPlayed).IsEqual("potion_discarded");
    }

    [TestCase(Description = "Equipping a weapon plays the sword-drawn sound")]
    public async Task EquippingWeapon_PlaysSwordDrawnSound()
    {
        await SetupFixedDeck();
        var scene = _runner!.Scene();

        var weapon = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitIdleFrame();

        AssertThat(((ScoundrelGame)scene).LastSfxPlayed).IsEqual("sword_drawn");
    }

    [TestCase(Description = "Discarding a weapon to the right zone plays the weapon-discard sound")]
    public async Task DiscardingWeapon_PlaysWeaponDiscardSound()
    {
        await SetupFixedDeck(1200u);
        var scene = _runner!.Scene();

        var weapon = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon).IsNotNull();
        await MouseDragCard(weapon!, RightZoneCenter());

        AssertThat(((ScoundrelGame)scene).LastSfxPlayed).IsEqual("weapon_discarded");
    }

    [TestCase(Description = "Taking the last room card triggers auto-deal, ending on the card-dealt sound")]
    public async Task TakingLastRoomCard_PlaysCardDealtSound()
    {
        await SetupFixedDeck();
        var scene = _runner!.Scene();

        // FixedDeck Room 1: 6♦(W), 4♣(M), 5♥(P), 8♠(M).
        // Take in this order: equip weapon, fight with it (floor→4), drink potion, fight 8♠
        // bare-handed (8 > floor 4) for 8 damage. HP ends at 12 — alive.
        var weapon   = FindRoomCard(scene, s => s == "diamonds");
        var monster1 = FindRoomCard(scene, s => s == "clubs");
        var potion   = FindRoomCard(scene, s => s == "hearts");
        var monster2 = FindRoomCard(scene, s => s == "spades");

        AssertThat(weapon).IsNotNull();
        AssertThat(monster1).IsNotNull();
        AssertThat(potion).IsNotNull();
        AssertThat(monster2).IsNotNull();

        ClickCard(scene, weapon!);
        await _runner!.AwaitIdleFrame();
        ClickCard(scene, monster1!);
        await _runner!.AwaitIdleFrame();
        ClickCard(scene, potion!);
        await _runner!.AwaitIdleFrame();
        ClickCard(scene, monster2!);
        await _runner!.AwaitIdleFrame();

        // Taking the 4th card empties the room → auto-deal → SyncRoomToGodot fires
        // card_dealt after the action sound, so card_dealt is the last sound heard.
        AssertThat(((ScoundrelGame)scene).LastSfxPlayed).IsEqual("card_dealt");
    }

    [TestCase(Description = "Slain badges are cleared from the old weapon when it is replaced by a new one")]
    public async Task WeaponReplaced_SlainBadgesCleared()
    {
        // Room 1: 6♦(W6), 4♣(M4), 5♥(P5), 6♠(M6)  — weapon covers M4 damage, M6 bare-handed.
        // Room 2: 3♦(W3), 2♣(M2), 3♥(P3), 2♠(M2)  — equipping W3 replaces W6.
        var deck = new List<CardModel>
        {
            // Room 2 (bottom — dealt second)
            new CardModel(Suit.Spades,   2, "2_spades"),
            new CardModel(Suit.Hearts,   3, "3_hearts"),
            new CardModel(Suit.Clubs,    2, "2_clubs"),
            new CardModel(Suit.Diamonds, 3, "3_diamonds"),
            // Room 1 (top — dealt first)
            new CardModel(Suit.Spades,   6, "6_spades"),
            new CardModel(Suit.Hearts,   5, "5_hearts"),
            new CardModel(Suit.Clubs,    4, "4_clubs"),
            new CardModel(Suit.Diamonds, 6, "6_diamonds"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(200);

        var scene      = _runner!.Scene();
        var weaponSlot = scene.GetNode("UI/LeftPanel/WeaponGroup/WeaponSlot");

        // Equip 6_diamonds (weapon1)
        var weapon1 = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon1).IsNotNull();
        ClickCard(scene, weapon1!);
        await _runner!.AwaitIdleFrame();

        // Fight 4_clubs with weapon1 → badge on weapon1
        var monster1 = FindRoomCard(scene, s => s == "clubs");
        AssertThat(monster1).IsNotNull();
        ClickCard(scene, monster1!);
        await _runner!.AwaitIdleFrame();

        var weapon1Node = (Node)weapon1!;
        int badgesBefore = 0;
        foreach (var child in weapon1Node.GetChildren())
            if (child.IsInGroup("slain_badge")) badgesBefore++;
        AssertThat(badgesBefore).IsEqual(1);

        // Clear Room 1: drink potion, fight 6_spades bare-handed (6 >= floor 4)
        var potion  = FindRoomCard(scene, s => s == "hearts");
        var monster2 = FindRoomCard(scene, s => s == "spades");
        AssertThat(potion).IsNotNull();
        AssertThat(monster2).IsNotNull();
        ClickCard(scene, potion!);
        await _runner!.AwaitIdleFrame();
        ClickCard(scene, monster2!);  // 6 >= floor(4) → bare-handed, 6 damage → HP 14
        await _runner!.AwaitIdleFrame();

        // Room 2 auto-dealt (all 4 Room 1 cards taken).
        // Equip 3_diamonds (weapon2) — this replaces weapon1 and clears its badges.
        var weapon2 = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon2).IsNotNull();
        ClickCard(scene, weapon2!);
        await _runner!.AwaitIdleFrame();

        // Weapon1 is now in discard; its badge should have been freed.
        int badgesAfter = 0;
        foreach (var child in weapon1Node.GetChildren())
            if (child.IsInGroup("slain_badge")) badgesAfter++;
        AssertThat(badgesAfter).IsEqual(0);

        // Weapon2 in the weapon slot should have no badges yet.
        var w2Cards  = (GArray)weaponSlot.Call("get_top_cards", 1);
        var w2Node   = (Node)w2Cards[0].AsGodotObject();
        int w2Badges = 0;
        foreach (var child in w2Node.GetChildren())
            if (child.IsInGroup("slain_badge")) w2Badges++;
        AssertThat(w2Badges).IsEqual(0);
    }

    [TestCase(Description = "Two weapon kills produce two badges with correct rank text")]
    public async Task TwoWeaponKills_TwoBadgesWithCorrectText()
    {
        // Room 1: 8♦(W8), 6♣(M6), 4♠(M4), 9♥(P9)
        // Kill M6 first (floor→6), then M4 (4 < 6, floor→4). Both use weapon → 0 HP damage.
        var deck = new List<CardModel>
        {
            // Room 2 padding (all potions)
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            // Room 1
            new CardModel(Suit.Hearts,   9, "9_hearts"),
            new CardModel(Suit.Spades,   4, "4_spades"),
            new CardModel(Suit.Clubs,    6, "6_clubs"),
            new CardModel(Suit.Diamonds, 8, "8_diamonds"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(200);

        var scene      = _runner!.Scene();
        var weaponSlot = scene.GetNode("UI/LeftPanel/WeaponGroup/WeaponSlot");

        var weapon = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitIdleFrame();

        var monster1 = FindRoomCard(scene, s => s == "clubs");   // 6_clubs → rank 6
        AssertThat(monster1).IsNotNull();
        ClickCard(scene, monster1!);
        await _runner!.AwaitIdleFrame();

        var monster2 = FindRoomCard(scene, s => s == "spades");  // 4_spades → rank 4
        AssertThat(monster2).IsNotNull();
        ClickCard(scene, monster2!);
        await _runner!.AwaitIdleFrame();

        var weaponCards = (GArray)weaponSlot.Call("get_top_cards", 1);
        var weaponNode  = (Node)weaponCards[0].AsGodotObject();

        var badgeNodes = new List<Node>();
        foreach (var child in weaponNode.GetChildren())
            if (child.IsInGroup("slain_badge")) badgeNodes.Add(child);
        AssertThat(badgeNodes.Count).IsEqual(2);

        string text0 = "", text1 = "";
        foreach (var gc in badgeNodes[0].GetChildren())
            if (gc is Label l0) { text0 = l0.Text; break; }
        foreach (var gc in badgeNodes[1].GetChildren())
            if (gc is Label l1) { text1 = l1.Text; break; }
        AssertThat(text0).IsEqual("6");
        AssertThat(text1).IsEqual("4");
    }

    [TestCase(Description = "Five weapon kills across two rooms produce five badges (compression layout)")]
    public async Task FiveWeaponKills_FiveBadgesCompressed()
    {
        // Room 1: 10♦(W10), 9♣(M9), 8♠(M8), 7♣(M7)  — all weapon-killable, 0 HP damage.
        // Room 2: 6♣(M6), 5♠(M5), 2♥(P2), 3♥(P3)    — kills 4 and 5, compression kicks in.
        var deck = new List<CardModel>
        {
            // Room 2 (bottom)
            new CardModel(Suit.Hearts,   3, "3_hearts"),
            new CardModel(Suit.Hearts,   2, "2_hearts"),
            new CardModel(Suit.Spades,   5, "5_spades"),
            new CardModel(Suit.Clubs,    6, "6_clubs"),
            // Room 1 (top)
            new CardModel(Suit.Clubs,    7, "7_clubs"),
            new CardModel(Suit.Spades,   8, "8_spades"),
            new CardModel(Suit.Clubs,    9, "9_clubs"),
            new CardModel(Suit.Diamonds, 10, "10_diamonds"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(200);

        var scene      = _runner!.Scene();
        var weaponSlot = scene.GetNode("UI/LeftPanel/WeaponGroup/WeaponSlot");

        var weapon = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitIdleFrame();

        // Kill 3 monsters in Room 1 (values 9, 8, 7 in deal order)
        for (int i = 0; i < 3; i++)
        {
            var monster = FindRoomCard(scene, s => s == "clubs" || s == "spades");
            AssertThat(monster).IsNotNull();
            ClickCard(scene, monster!);
            await _runner!.AwaitIdleFrame();
        }
        // Room 2 auto-dealt (all 4 Room 1 cards taken)

        // Kill 2 monsters in Room 2 (values 6, 5)
        for (int i = 0; i < 2; i++)
        {
            var monster = FindRoomCard(scene, s => s == "clubs" || s == "spades");
            AssertThat(monster).IsNotNull();
            ClickCard(scene, monster!);
            await _runner!.AwaitIdleFrame();
        }

        var weaponCards = (GArray)weaponSlot.Call("get_top_cards", 1);
        var weaponNode  = (Node)weaponCards[0].AsGodotObject();
        int badgeCount  = 0;
        foreach (var child in weaponNode.GetChildren())
            if (child.IsInGroup("slain_badge")) badgeCount++;
        AssertThat(badgeCount).IsEqual(5);
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
