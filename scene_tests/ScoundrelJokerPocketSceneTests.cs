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
/// Extended Rules Joker pocket scene tests: storing/retrieving potions and weapons, giving the equipped weapon to the Weapon Joker.
/// Split out of the former ScoundrelSceneTests.cs; shared helpers live in SceneTestHelpers.cs.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScoundrelJokerPocketSceneTests
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

    // ── Extended Rules: storing/retrieving via the joker pockets (chunk 10) ────
    //
    // Storing reuses the fight-with-joker drop zones (PotionJokerDropZone/
    // WeaponJokerDropZone). Retrieving/discarding drags the already-pocketed card
    // (now draggable via JokerPocketSlot.gd) to the top zone (retrieve/activate)
    // or the right zone (discard without activating).

    [TestCase(Description = "Dragging a room potion onto the Potion Joker zone stores it in the pocket instead of drinking it")]
    public async Task StoringPotion_ViaPotionJokerZone_MovesToSlotAndOutOfRoom()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Diamonds, 6, "6_diamonds"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.RedJoker, 0, "joker_red"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");
        var potionJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/PotionJokerSlot");

        var joker = FindRoomCard(scene, s => s == "red_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        int hpBefore = ParseHP(scene);
        int countBeforeStore = ((GArray)room.Call("get_all_cards")).Count;
        var potion = FindRoomCardByName(scene, "5_hearts");
        AssertThat(potion).IsNotNull();

        await MouseDragCard(_runner!, potion!, PotionJokerZoneCenter(_runner!));

        // Stored, not drunk: HP unchanged, potion left the room, pocket now holds it
        // alongside the joker's own card (slot count 2: joker + stored potion).
        AssertThat(ParseHP(scene)).IsEqual(hpBefore);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(countBeforeStore - 1);
        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(2);
        var pocketed = (GArray)potionJokerSlot.Call("get_top_cards", 1);
        var pocketedName = pocketed[0].AsGodotObject().Get("card_info").AsGodotDictionary()["name"].AsString();
        AssertThat(pocketedName).IsEqual("5_hearts");
    }

    [TestCase(Description = "Dragging a weapon onto the Potion Joker zone (wrong pocket) bounces it back")]
    public async Task StoringMismatchedWeapon_OnPotionJokerZone_BouncesBack()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Diamonds, 6, "6_diamonds"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.RedJoker, 0, "joker_red"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");
        var potionJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/PotionJokerSlot");

        var joker = FindRoomCard(scene, s => s == "red_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        int countBefore = ((GArray)room.Call("get_all_cards")).Count;

        var weapon = FindRoomCardByName(scene, "6_diamonds");
        AssertThat(weapon).IsNotNull();
        await MouseDragCard(_runner!, weapon!, PotionJokerZoneCenter(_runner!));

        // Bounced back: room count unchanged, pocket still holds only the joker
        // itself (count 1) — nothing was stored.
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(countBefore);
        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(1);
        AssertThat(scene.GetNode<Label>("HudLayer/StatusLabel").Text).IsEqual("Wrong pocket!");
    }

    [TestCase(Description = "Dragging the pocketed potion to the top zone retrieves and drinks it, healing the player")]
    public async Task RetrievingPocketedPotion_ViaTopZone_HealsAndEmptiesPocket()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Clubs, 8, "8_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.RedJoker, 0, "joker_red"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var potionJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/PotionJokerSlot");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        var joker = FindRoomCard(scene, s => s == "red_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        // Take monster damage first so the heal is observable.
        var monster = FindRoomCardByName(scene, "8_clubs");
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);
        int hpAfterDamage = ParseHP(scene);

        var potion = FindRoomCardByName(scene, "5_hearts");
        AssertThat(potion).IsNotNull();
        await MouseDragCard(_runner!, potion!, PotionJokerZoneCenter(_runner!));
        // Let the store-move settle before dragging the pocketed card back out —
        // DraggableObject silently rejects input while a card is mid-tween.
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        // Slot count 2: the joker's own card plus the stored potion.
        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(2);
        var pocketed = ((GArray)potionJokerSlot.Call("get_top_cards", 1))[0].AsGodotObject();

        int expectedHP = Math.Min(ScoundrelRules.MaxHealth, hpAfterDamage + 5);

        await MouseDragCard(_runner!, pocketed, new Vector2(192f, 345f)); // top zone

        AssertThat(ParseHP(scene)).IsEqual(expectedHP);
        // Slot count back down to 1 — the joker stays, only the potion left.
        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(1);
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(2); // monster + retrieved potion
    }

    [TestCase(Description = "Dragging the pocketed potion to the right zone discards it without healing")]
    public async Task DiscardingPocketedPotion_ViaRightZone_NoHeal()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Clubs, 8, "8_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.RedJoker, 0, "joker_red"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var potionJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/PotionJokerSlot");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        var joker = FindRoomCard(scene, s => s == "red_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        // Take monster damage first so a missed heal would be observable.
        var monster = FindRoomCardByName(scene, "8_clubs");
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);
        int hpAfterDamage = ParseHP(scene);

        var potion = FindRoomCardByName(scene, "5_hearts");
        AssertThat(potion).IsNotNull();
        await MouseDragCard(_runner!, potion!, PotionJokerZoneCenter(_runner!));
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        // Slot count 2: the joker's own card plus the stored potion.
        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(2);
        var pocketed = ((GArray)potionJokerSlot.Call("get_top_cards", 1))[0].AsGodotObject();

        await MouseDragCard(_runner!, pocketed, RightZoneCenter(_runner!));

        AssertThat(ParseHP(scene)).IsEqual(hpAfterDamage);
        // Slot count back down to 1 — the joker stays, only the potion left.
        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(1);
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(2); // monster + declined potion
    }

    [TestCase(Description = "Dragging the pocketed weapon to the top zone retrieves and equips it")]
    public async Task RetrievingPocketedWeapon_ViaTopZone_Equips()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Diamonds, 7, "7_diamonds"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.BlackJoker, 0, "joker_black"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var weaponJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/WeaponJokerSlot");
        var weaponSlot = scene.GetNode("UI/LeftPanel/WeaponGroup/WeaponSlot");

        var joker = FindRoomCard(scene, s => s == "black_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        var weapon = FindRoomCardByName(scene, "7_diamonds");
        AssertThat(weapon).IsNotNull();
        await MouseDragCard(_runner!, weapon!, WeaponJokerZoneCenter(_runner!));
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        // Slot count 2: the joker's own card plus the stored weapon.
        AssertThat((int)weaponJokerSlot.Call("get_card_count")).IsEqual(2);
        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsEqual("Weapon: none");

        var pocketed = ((GArray)weaponJokerSlot.Call("get_top_cards", 1))[0].AsGodotObject();

        await MouseDragCard(_runner!, pocketed, new Vector2(192f, 345f)); // top zone

        // Slot count back down to 1 — the joker stays, the weapon moved to be equipped.
        AssertThat((int)weaponJokerSlot.Call("get_card_count")).IsEqual(1);
        AssertThat((int)weaponSlot.Call("get_card_count")).IsEqual(1);
        var equipped = (GArray)weaponSlot.Call("get_top_cards", 1);
        var equippedName = equipped[0].AsGodotObject().Get("card_info").AsGodotDictionary()["name"].AsString();
        AssertThat(equippedName).IsEqual("7_diamonds");
        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsEqual("Weapon: 7_diamonds  (next: any)");
    }

    [TestCase(Description = "Dragging the pocketed weapon to the right zone discards it without equipping")]
    public async Task DiscardingPocketedWeapon_ViaRightZone_DoesNotEquip()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Diamonds, 7, "7_diamonds"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.BlackJoker, 0, "joker_black"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var weaponJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/WeaponJokerSlot");
        var weaponSlot = scene.GetNode("UI/LeftPanel/WeaponGroup/WeaponSlot");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        var joker = FindRoomCard(scene, s => s == "black_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        var weapon = FindRoomCardByName(scene, "7_diamonds");
        AssertThat(weapon).IsNotNull();
        await MouseDragCard(_runner!, weapon!, WeaponJokerZoneCenter(_runner!));
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        // Slot count 2: the joker's own card plus the stored weapon.
        AssertThat((int)weaponJokerSlot.Call("get_card_count")).IsEqual(2);
        var pocketed = ((GArray)weaponJokerSlot.Call("get_top_cards", 1))[0].AsGodotObject();

        await MouseDragCard(_runner!, pocketed, RightZoneCenter(_runner!));

        // Slot count back down to 1 — the joker stays, only the weapon left (discarded).
        AssertThat((int)weaponJokerSlot.Call("get_card_count")).IsEqual(1);
        AssertThat((int)weaponSlot.Call("get_card_count")).IsEqual(0);
        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsEqual("Weapon: none");
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(1);
    }

    // ── Give equipped weapon to Weapon Joker (PRD §6 follow-up, chunk 13) ──────
    // The reverse direction of RetrievingPocketedWeapon_ViaTopZone_Equips above:
    // instead of pulling a pocketed weapon out to equip, the player drags the
    // already-equipped weapon (WeaponSlot.gd) onto the Weapon Joker's pocket zone.

    [TestCase(Description = "Dragging the equipped weapon onto the Weapon Joker zone stores it in the pocket as a shrunk badge and clears the weapon UI")]
    public async Task GivingEquippedWeaponToWeaponJoker_ViaDrag_MovesToPocketAsBadge_ClearsWeaponSlot()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Diamonds, 7, "7_diamonds"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.BlackJoker, 0, "joker_black"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var weaponJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/WeaponJokerSlot");
        var weaponSlot = scene.GetNode("UI/LeftPanel/WeaponGroup/WeaponSlot");
        var weaponLabel = scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel");

        var joker = FindRoomCard(scene, s => s == "black_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        var weaponRoomCard = FindRoomCardByName(scene, "7_diamonds");
        AssertThat(weaponRoomCard).IsNotNull();
        ClickCard(scene, weaponRoomCard!); // equips into WeaponSlot
        await _runner!.AwaitMillis(UITimings.DragAnimationMs); // let the equip tween settle

        AssertThat((int)weaponSlot.Call("get_card_count")).IsEqual(1);
        var equippedWeapon = ((GArray)weaponSlot.Call("get_top_cards", 1))[0].AsGodotObject();

        // Fight a weak monster with the equipped weapon first, so it carries a
        // slain-monster badge into the Give — proving the give path actually clears
        // it (ClearSlainBadges), not just that a badge-free weapon looks clean.
        var monster = FindRoomCardByName(scene, "3_clubs");
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!); // weapon (value 7) fully blocks value-3 monster
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        int BadgeCount() => ((Node)equippedWeapon!).GetChildren().Count(n => n.IsInGroup("slain_badge"));
        AssertThat(BadgeCount()).IsEqual(1);

        // Capture the joker's own card position before anything is stored on top of it.
        var jokerCard = ((GArray)weaponJokerSlot.Call("get_top_cards", 1))[0].AsGodotObject();
        float jokerGlobalY = ((Vector2)jokerCard.Get("global_position")).Y;

        await MouseDragCard(_runner!, equippedWeapon, WeaponJokerZoneCenter(_runner!));
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        // Slot count 2: the joker's own card plus the newly-pocketed weapon.
        AssertThat((int)weaponJokerSlot.Call("get_card_count")).IsEqual(2);
        AssertThat((int)weaponSlot.Call("get_card_count")).IsEqual(0);
        AssertThat(weaponLabel.Text).IsEqual("Weapon: none");

        // Slain-monster badge cleared, mirroring the Merchant-sale/RetrieveWeapon
        // equip-replace path (this is the same weapon card node, now pocketed).
        AssertThat(BadgeCount()).IsEqual(0);

        var stored = ((GArray)weaponJokerSlot.Call("get_top_cards", 1))[0].AsGodotObject();

        // Shrunk to a badge fraction of full size (ScoundrelLayoutController.
        // PocketedItemScale = 0.42), not left at full scale (1.0) — same treatment
        // chunk 12 built for storing a room weapon.
        var scale = (Vector2)stored.Get("scale");
        AssertThat(scale.X).IsLessEqual(0.6f);
        AssertThat(scale.X).IsGreaterEqual(0.1f);

        // Positioned below the joker's own card, not overlapping/above it.
        float storedGlobalY = ((Vector2)stored.Get("global_position")).Y;
        AssertThat(storedGlobalY).IsGreaterEqual(jokerGlobalY + 1f);
    }

    [TestCase(Description = "Dragging the equipped weapon anywhere other than the Weapon Joker zone bounces it back without changing engine state")]
    public async Task DraggingEquippedWeaponElsewhere_BouncesBackToWeaponSlot()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Diamonds, 7, "7_diamonds"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.BlackJoker, 0, "joker_black"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var weaponJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/WeaponJokerSlot");
        var weaponSlot = scene.GetNode("UI/LeftPanel/WeaponGroup/WeaponSlot");
        var weaponLabel = scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        var joker = FindRoomCard(scene, s => s == "black_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        var weaponRoomCard = FindRoomCardByName(scene, "7_diamonds");
        AssertThat(weaponRoomCard).IsNotNull();
        ClickCard(scene, weaponRoomCard!); // equips into WeaponSlot
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        AssertThat((int)weaponSlot.Call("get_card_count")).IsEqual(1);
        string labelBefore = weaponLabel.Text;
        int discardCountBefore = (int)discardPile.Call("get_card_count");

        var equippedWeapon = ((GArray)weaponSlot.Call("get_top_cards", 1))[0].AsGodotObject();
        await MouseDragCard(_runner!, equippedWeapon, RightZoneCenter(_runner!));

        // Bounced back: weapon stays equipped, Weapon Joker's pocket stays empty
        // (only the joker's own card), and nothing was discarded.
        AssertThat((int)weaponSlot.Call("get_card_count")).IsEqual(1);
        AssertThat((int)weaponJokerSlot.Call("get_card_count")).IsEqual(1);
        AssertThat(weaponLabel.Text).IsEqual(labelBefore);
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(discardCountBefore);

        var stillEquipped = ((GArray)weaponSlot.Call("get_top_cards", 1))[0].AsGodotObject();
        string equippedName = stillEquipped.Get("card_info").AsGodotDictionary()["name"].AsString();
        AssertThat(equippedName).IsEqual("7_diamonds");
        // Bounced card must not be left shrunk — it stays the real, full-size weapon.
        var scale = (Vector2)stillEquipped.Get("scale");
        AssertThat(scale.X).IsEqual(1f);
    }

    // ── Extended Rules: pocketed item badge visuals (playtest bug fix) ─────────
    //
    // Designer report: storing a potion/weapon in a joker pocket covered the
    // joker's own card and its HP label, since the stored card previously
    // stayed full-size (see ShrinkCardForPocket + ScoundrelLayoutController.
    // UpdateJokerGroupLayout for the fix — the stored card is shrunk via `scale`
    // and repositioned via the pocket slot's own Pile `layout`/`stack_display_gap`
    // so it lands as a badge flush with the slot's bottom edge instead).

    [TestCase(Description = "Storing a potion shrinks it into a badge below the joker's own card, never overlapping the HP label above the slot")]
    public async Task StoringPotion_ShrinksIntoBadge_BelowJokerAndClearOfHpLabel()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Diamonds, 6, "6_diamonds"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.RedJoker, 0, "joker_red"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var potionJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/PotionJokerSlot");
        var hpLabel = scene.GetNode<Label>("UI/LeftPanel/JokerGroup/PotionJokerHpLabel");

        var joker = FindRoomCard(scene, s => s == "red_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        // Capture the joker's own card (the pocket's only occupant so far, index 0)
        // before storing anything on top of it.
        var jokerCard = ((GArray)potionJokerSlot.Call("get_top_cards", 1))[0].AsGodotObject();
        float jokerGlobalY = ((Vector2)jokerCard.Get("global_position")).Y;

        var potion = FindRoomCardByName(scene, "5_hearts");
        AssertThat(potion).IsNotNull();
        await MouseDragCard(_runner!, potion!, PotionJokerZoneCenter(_runner!));
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(2);
        var stored = ((GArray)potionJokerSlot.Call("get_top_cards", 1))[0].AsGodotObject();

        // Shrunk to a badge fraction of full size (ScoundrelLayoutController.
        // PocketedItemScale = 0.42), not left at full scale (1.0).
        var scale = (Vector2)stored.Get("scale");
        AssertThat(scale.X).IsLessEqual(0.6f);
        AssertThat(scale.X).IsGreaterEqual(0.1f);

        // Positioned BELOW the joker's own card, not above it. This is the
        // discriminating check: the pre-fix default (Pile layout=UP, ~8px gap)
        // would have placed the stored card's Y at or above the joker's Y; only
        // a working layout=DOWN configuration pushes it strictly lower.
        float storedGlobalY = ((Vector2)stored.Get("global_position")).Y;
        AssertThat(storedGlobalY).IsGreaterEqual(jokerGlobalY + 1f);

        // Never overlaps the HP label above the slot — the exact reported bug
        // ("covers up the joker's HP").
        var storedRect = VisualRect(stored);
        var labelRect = new Rect2(hpLabel.GlobalPosition, hpLabel.Size);
        AssertThat(storedRect.Intersects(labelRect)).IsFalse();
    }

    [TestCase(Description = "Store-then-retrieve round trip still works exactly as chunk 10 built it after the badge-shrink visual change: top zone activates (heals) and resets scale back to full size")]
    public async Task StoringThenRetrievingPotion_ViaTopZone_StillHealsAndResetsToFullScale()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            // Room 1 (dealt first)
            new CardModel(Suit.Clubs, 8, "8_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.RedJoker, 0, "joker_red"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var potionJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/PotionJokerSlot");

        var joker = FindRoomCard(scene, s => s == "red_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        // Take monster damage first so the heal is observable.
        var monster = FindRoomCardByName(scene, "8_clubs");
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);
        int hpAfterDamage = ParseHP(scene);

        var potion = FindRoomCardByName(scene, "5_hearts");
        AssertThat(potion).IsNotNull();
        await MouseDragCard(_runner!, potion!, PotionJokerZoneCenter(_runner!));
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var pocketed = ((GArray)potionJokerSlot.Call("get_top_cards", 1))[0].AsGodotObject();
        // Confirm it's actually shrunk before retrieving, so the post-retrieve
        // reset assertion below is meaningful.
        AssertThat(((Vector2)pocketed.Get("scale")).X).IsLessEqual(0.6f);

        int expectedHP = Math.Min(ScoundrelRules.MaxHealth, hpAfterDamage + 5);
        await MouseDragCard(_runner!, pocketed, new Vector2(192f, 345f)); // top zone

        AssertThat(ParseHP(scene)).IsEqual(expectedHP);
        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(1);
        // Retrieval must reset the badge shrink back to full size — a retrieved/
        // discarded card should never stay visually tiny once it's left the pocket.
        AssertThat(((Vector2)pocketed.Get("scale")).X).IsEqual(1f);
    }
}
