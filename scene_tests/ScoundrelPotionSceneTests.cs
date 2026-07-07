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
/// Potion scene tests: drinking, discarding, void potions, one-per-room tinting.
/// Split out of the former ScoundrelSceneTests.cs; shared helpers live in SceneTestHelpers.cs.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScoundrelPotionSceneTests
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

    [TestCase(Description = "Clicking a potion card restores HP (capped at 20)")]
    public async Task TakingPotionRestoresHP()
    {
        await SetupFixedDeck(_runner!);
        var scene = _runner!.Scene();

        // FixedDeck Room 1: take 8_spades first to take damage, then 5_hearts to heal.
        var monster = FindRoomCard(scene, s => s == "spades");
        AssertThat(monster).IsNotNull();

        int damage = MonsterDamage(monster!.Get("card_info").AsGodotDictionary());
        ClickCard(scene, monster);
        await _runner!.AwaitIdleFrame();

        int hpAfterHit = ParseHP(scene);
        AssertThat(hpAfterHit).IsEqual(ScoundrelRules.MaxHealth - damage);

        var potion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(potion).IsNotNull();

        int potionRank = potion!.Get("card_info").AsGodotDictionary()["rank"].AsInt32();
        int expectedHP = Math.Min(ScoundrelRules.MaxHealth, hpAfterHit + potionRank);

        ClickCard(scene, potion);
        await _runner!.AwaitIdleFrame();

        AssertThat(ParseHP(scene)).IsEqual(expectedHP);
    }

    [TestCase(Description = "Dragging a potion to the left zone drinks it, healing the player")]
    public async Task DragPotionToLeftZoneDrinks()
    {
        await SetupFixedDeck(_runner!, UITimings.DragAnimationMs);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Take 8_spades damage first so the heal is detectable (HP MaxHealth → MaxHealth - 8).
        var monster = FindRoomCard(scene, s => s == "spades");
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);  // 200ms

        int hpAfterDamage = ParseHP(scene);
        var potion = FindRoomCard(scene, s => s == "hearts"); // 5_hearts
        AssertThat(potion).IsNotNull();

        int potionRank = potion!.Get("card_info").AsGodotDictionary()["rank"].AsInt32();
        int expectedHP = Math.Min(ScoundrelRules.MaxHealth, hpAfterDamage + potionRank);

        // Drag left — LEFT zone is now "Drink"
        await MouseDragCard(_runner!, potion, new Vector2(192f, 345f));

        AssertThat(ParseHP(scene)).IsEqual(expectedHP);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(ScoundrelRules.RoomSize - 2);
    }

    [TestCase(Description = "Dragging a potion to the right zone discards it without healing")]
    public async Task DragPotionToRightZoneDiscards()
    {
        await SetupFixedDeck(_runner!, 1200u);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Take damage so a heal would be detectable.
        var monster = FindRoomCard(scene, s => s == "spades"); // 8_spades
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);  // 200ms

        int hpAfterDamage = ParseHP(scene);
        var potion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(potion).IsNotNull();

        // Drag right — RIGHT zone is now "Discard" (no healing)
        await MouseDragCard(_runner!, potion!, RightZoneCenter(_runner!));

        // HP unchanged — potion discarded, not drunk.
        AssertThat(ParseHP(scene)).IsEqual(hpAfterDamage);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(ScoundrelRules.RoomSize - 2);
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
        await _runner!.AwaitMillis(UITimings.DragAnimationMs); // settle for real mouse input

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // Drink first potion via signal — PotionUsedThisRoom becomes true.
        // Remaining hearts potions are now void; left zone highlight is hidden for them.
        var firstPotion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(firstPotion).IsNotNull();
        ClickCard(scene, firstPotion!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);  // 200ms

        var voidPotion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(voidPotion).IsNotNull();

        int countBefore = ((GArray)room.Call("get_all_cards")).Count;

        // Drag void potion to the left zone — bug-053 caused it to be discarded here.
        await MouseDragCard(_runner!, voidPotion!, new Vector2(192f, 345f));

        // Card must bounce back: room count unchanged.
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(countBefore);
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
}
