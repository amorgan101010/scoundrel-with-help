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
/// Sound-effect scene tests for card dealing, combat, potions, and weapons.
/// Split out of the former ScoundrelSceneTests.cs; shared helpers live in SceneTestHelpers.cs.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScoundrelAudioSceneTests
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

    // ── Sound effect tests ────────────────────────────────────────────────────

    [TestCase(Description = "Dealing cards plays the card-dealt sound")]
    public async Task DealingCards_PlaysCardDealtSound()
    {
        await SetupFixedDeck(_runner!);
        var game = (ScoundrelGame)_runner!.Scene();
        AssertThat(game.AudioManager.LastSfxPlayed).IsEqual(nameof(game.AudioManager._sfxCardDealt));
    }

    [TestCase(Description = "Fighting a monster plays the punch sound")]
    public async Task FightingMonster_PlaysPunchSound()
    {
        await SetupFixedDeck(_runner!);
        var scene = _runner!.Scene();

        var monster = FindRoomCard(scene, s => s == "clubs");
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitIdleFrame();

        var game = (ScoundrelGame)scene;

        AssertThat(game.AudioManager.LastSfxPlayed).IsEqual(nameof(game.AudioManager._sfxPunch));
    }

    [TestCase(Description = "Drinking a potion plays the bubbles sound")]
    public async Task DrinkingPotion_PlaysBubblesSound()
    {
        await SetupFixedDeck(_runner!);
        var scene = _runner!.Scene();

        var potion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(potion).IsNotNull();
        ClickCard(scene, potion!);
        await _runner!.AwaitIdleFrame();

        var game = (ScoundrelGame)scene;
        AssertThat(game.AudioManager.LastSfxPlayed).IsEqual(nameof(game.AudioManager._sfxBubbles));
    }

    [TestCase(Description = "Discarding a potion to the right zone plays the potion-discard sound")]
    public async Task DiscardingPotion_PlaysDiscardSound()
    {
        await SetupFixedDeck(_runner!, 1200u);
        var scene = _runner!.Scene();

        var potion = FindRoomCard(scene, s => s == "hearts");
        AssertThat(potion).IsNotNull();
        await MouseDragCard(_runner!, potion!, RightZoneCenter(_runner!));

        var game = (ScoundrelGame)scene;
        AssertThat(game.AudioManager.LastSfxPlayed).IsEqual(nameof(game.AudioManager._sfxPotionDiscard));
    }

    [TestCase(Description = "Equipping a weapon plays the sword-drawn sound")]
    public async Task EquippingWeapon_PlaysSwordDrawnSound()
    {
        await SetupFixedDeck(_runner!);
        var scene = _runner!.Scene();

        var weapon = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitIdleFrame();

        var game = (ScoundrelGame)scene;
        AssertThat(game.AudioManager.LastSfxPlayed).IsEqual(nameof(game.AudioManager._sfxSwordDrawn));
    }

    [TestCase(Description = "Discarding a weapon to the right zone plays the weapon-discard sound")]
    public async Task DiscardingWeapon_PlaysWeaponDiscardSound()
    {
        await SetupFixedDeck(_runner!, 1200u);
        var scene = _runner!.Scene();

        var weapon = FindRoomCard(scene, s => s == "diamonds");
        AssertThat(weapon).IsNotNull();
        await MouseDragCard(_runner!, weapon!, RightZoneCenter(_runner!));

        var game = (ScoundrelGame)scene;
        AssertThat(game.AudioManager.LastSfxPlayed).IsEqual(nameof(game.AudioManager._sfxWeaponDiscard));
    }

    [TestCase(Description = "Taking the last room card triggers auto-deal, ending on the card-dealt sound")]
    public async Task TakingLastRoomCard_PlaysCardDealtSound()
    {
        await SetupFixedDeck(_runner!);
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
        var game = (ScoundrelGame)scene;
        AssertThat(game.AudioManager.LastSfxPlayed).IsEqual(nameof(game.AudioManager._sfxCardDealt));
    }
}
