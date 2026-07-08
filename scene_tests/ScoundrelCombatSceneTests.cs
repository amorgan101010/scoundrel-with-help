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
/// Monster and weapon combat scene tests: damage, equip, weapon floor, slain badges.
/// Split out of the former ScoundrelSceneTests.cs; shared helpers live in SceneTestHelpers.cs.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScoundrelCombatSceneTests
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

    [TestCase(Description = "Clicking a monster card with no weapon reduces HP by its combat value")]
    public async Task TakingMonsterReducesHP()
    {
        await SetupFixedDeck(_runner!);
        var scene = _runner!.Scene();

        // FixedDeck Room 1 always contains 4_clubs (monster, rank 4, value 4).
        var monster = FindRoomCard(scene, s => s == "clubs");
        AssertThat(monster).IsNotNull();

        int expectedDamage = MonsterDamage(monster!.Get("card_info").AsGodotDictionary());

        ClickCard(scene, monster);
        await _runner!.AwaitIdleFrame();

        AssertThat(ParseHP(scene)).IsEqual(ScoundrelRules.MaxHealth - expectedDamage);
        AssertThat((int)scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile").Call("get_card_count")).IsEqual(1);
    }

    [TestCase(Description = "Clicking a diamond weapon equips it and updates the weapon label")]
    public async Task TakingWeaponEquipsIt()
    {
        await SetupFixedDeck(_runner!);
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

    [TestCase(Description = "Monster killed with weapon goes to discard and adds a badge to the weapon card")]
    public async Task WeaponedMonsterGetsSlainBadgeOnWeapon()
    {
        await SetupFixedDeck(_runner!);
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
        await SetupFixedDeck(_runner!);
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

    [TestCase(Description = "Dragging a weapon to the left zone equips it")]
    public async Task MouseDragTakesCard()
    {
        await SetupFixedDeck(_runner!, DragAnimationMs);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        var cards = (GArray)room.Call("get_all_cards");
        AssertThat(cards.Count).IsEqual(ScoundrelRules.RoomSize);

        // cards[0] is always 6_diamonds (weapon) — drag to LEFT zone (fight/equip side).
        await MouseDragCard(_runner!, cards[0].AsGodotObject(), new Vector2(192f, 345f));

        var after = (GArray)room.Call("get_all_cards");
        AssertThat(after.Count).IsEqual(ScoundrelRules.RoomSize - 1);
    }

    [TestCase(Description = "Dragging a weapon to the right zone discards it without equipping")]
    public async Task DragWeaponToRightZoneDiscards()
    {
        await SetupFixedDeck(_runner!, DragAnimationMs);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // cards[0] = 6_diamonds (weapon). Right zone (x:820-1120) is the discard side.
        var cards = (GArray)room.Call("get_all_cards");
        await MouseDragCard(_runner!, cards[0].AsGodotObject(), RightZoneCenter(_runner!));

        // Card discarded — room has RoomSize - 1 cards, weapon slot still empty.
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(ScoundrelRules.RoomSize - 1);
        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsEqual("Weapon: none");
        AssertThat((int)scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile").Call("get_card_count")).IsEqual(1);
    }

    [TestCase(Description = "Dragging a monster to the right zone fights bare-handed, ignoring equipped weapon")]
    public async Task DragMonsterToRightZoneIsBarehanded()
    {
        await SetupFixedDeck(_runner!, DragAnimationMs);
        var scene = _runner!.Scene();

        // Equip 6_diamonds via direct signal so the weapon floor is fresh.
        var weapon = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitMillis((uint)(DragAnimationMs / 1.5f)); // wait shorter since weapon animates briefly

        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsNotEqual("Weapon: none");

        // Drag 4_clubs (monster, value 4) to RIGHT zone — bare-handed, no weapon applied.
        var monster = FindRoomCard(scene, s => s == "clubs");
        AssertThat(monster).IsNotNull();
        await MouseDragCard(_runner!, monster!, RightZoneCenter(_runner!));

        // With weapon (value 6): damage would be 0.  Bare-handed: damage = 4.
        AssertThat(ParseHP(scene)).IsEqual(ScoundrelRules.MaxHealth - 4);
    }

    [TestCase(Description = "Dragging a monster to the left zone when weapon floor is exceeded bounces it back")]
    public async Task DragMonsterExceedingFloorToLeftZoneBounces()
    {
        await SetupFixedDeck(_runner!, DragAnimationMs);
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

        await MouseDragCard(_runner!, monster!, new Vector2(192f, 345f)); // LEFT zone centre

        // Card bounced back — room unchanged, no damage taken.
        var room = scene.GetNode("UI/RoomContainer");
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(2);
        AssertThat(ParseHP(scene)).IsEqual(hpBefore);
    }

    [TestCase(Description = "Monster too high for weapon floor goes to discard bare-handed; earlier weapon kill badge is preserved")]
    public async Task ExpiredWeaponFloorMonsterGoesToDiscard()
    {
        await SetupFixedDeck(_runner!);
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
}
