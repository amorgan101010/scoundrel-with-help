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
/// Extended Rules scene tests: deck loading plus Blacksmith/Merchant dispatch, zone labels, and effects.
/// Split out of the former ScoundrelSceneTests.cs; shared helpers live in SceneTestHelpers.cs.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScoundrelExtendedRulesSceneTests
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

    [TestCase(Description = "Extended Rules deck (Blacksmith/Merchant/Jokers) loads without error")]
    public async Task ExtendedRules_NewCardKindsLoadWithoutError()
    {
        // Room 2 (bottom — dealt second): plain Classic padding cards.
        // Room 1 (top — dealt first): one of each new Extended Rules card kind.
        // This chunk only wires up data model + deck infrastructure — no gameplay
        // effects for these cards yet, so this test only checks the scene loads and
        // the card nodes are created, not that clicking them does anything special.
        var deck = new List<CardModel>
        {
            new CardModel(Suit.Clubs,      2,  "2_clubs"),
            new CardModel(Suit.Hearts,     3,  "3_hearts"),
            new CardModel(Suit.Spades,     4,  "4_spades"),
            new CardModel(Suit.Diamonds,   5,  "5_diamonds"),
            new CardModel(Suit.Diamonds,   11, "jack_diamonds"),  // Blacksmith
            new CardModel(Suit.Hearts,     11, "jack_hearts"),    // Merchant
            new CardModel(Suit.RedJoker,   0,  "joker_red"),      // Potion Joker
            new CardModel(Suit.BlackJoker, 0,  "joker_black"),    // Weapon Joker
        };

        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(200);

        AssertThat(game.ExtendedRules).IsTrue();

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");
        var roomCards = (GArray)room.Call("get_all_cards");
        AssertThat(roomCards.Count).IsEqual(ScoundrelRules.RoomSize);

        var names = new HashSet<string>();
        foreach (var obj in roomCards)
        {
            var card = obj.AsGodotObject();
            names.Add(card.Get("card_info").AsGodotDictionary()["name"].AsString());
        }

        AssertThat(names.Contains("jack_diamonds")).IsTrue();
        AssertThat(names.Contains("jack_hearts")).IsTrue();
        AssertThat(names.Contains("joker_red")).IsTrue();
        AssertThat(names.Contains("joker_black")).IsTrue();
    }

    // ── Bug fix: Blacksmith/Merchant dispatch must not key off raw Suit ────────
    //
    // Blacksmith cards share Suit.Diamonds with real weapons and Merchant cards
    // share Suit.Hearts with real potions (only Rank distinguishes them — see
    // CardModel.cs). OnCardSelected's visual side-effect dispatch previously
    // switched on cardModel.Suit, which mis-equipped a Blacksmith card as a
    // weapon and mis-drank a Merchant card as a potion.

    [TestCase(Description = "Dropping a Blacksmith card into the left zone applies its effect without touching the equipped weapon's slot or playing weapon audio")]
    public async Task DroppingBlacksmithCard_DoesNotTouchWeaponSlot()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            // Room 1 (dealt first)
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Diamonds, 11, "jack_diamonds"),
            new CardModel(Suit.Diamonds, 6, "6_diamonds"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(DragAnimationMs);

        var scene = _runner!.Scene();
        var weaponSlot = scene.GetNode("UI/LeftPanel/WeaponGroup/WeaponSlot");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        // Equip the real weapon first.
        var weapon = FindRoomCardByName(scene, "6_diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        AssertThat((int)weaponSlot.Call("get_card_count")).IsEqual(1);
        AssertThat(game.AudioManager.LastSfxPlayed).IsEqual(nameof(game.AudioManager._sfxSwordDrawn));

        // Drop the Blacksmith card into the (top-half) left zone.
        var blacksmith = FindRoomCardByName(scene, "jack_diamonds");
        AssertThat(blacksmith).IsNotNull();
        await MouseDragCard(_runner!, blacksmith!, new Vector2(192f, 345f));

        // Weapon slot must still hold only the original weapon card — untouched.
        AssertThat((int)weaponSlot.Call("get_card_count")).IsEqual(1);
        var weaponCards = (GArray)weaponSlot.Call("get_top_cards", 1);
        var weaponName = weaponCards[0].AsGodotObject().Get("card_info").AsGodotDictionary()["name"].AsString();
        AssertThat(weaponName).IsEqual("6_diamonds");

        // No new weapon-equip (or any other) audio fired for the Blacksmith card.
        AssertThat(game.AudioManager.LastSfxPlayed).IsEqual(nameof(game.AudioManager._sfxSwordDrawn));

        // The Blacksmith card applied its effect (weapon equipped, nothing to
        // remove → attack bonus) and was discarded, not left inert or equipped.
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(1);
    }

    [TestCase(Description = "Selling the equipped weapon via a Merchant card clears the visual weapon slot")]
    public async Task MerchantSale_ClearsVisualWeaponSlot()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            // Room 1 (dealt first)
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            new CardModel(Suit.Clubs, 10, "10_clubs"),
            new CardModel(Suit.Hearts, 11, "jack_hearts"),
            new CardModel(Suit.Diamonds, 6, "6_diamonds"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(DragAnimationMs);

        var scene = _runner!.Scene();
        var weaponSlot = scene.GetNode("UI/LeftPanel/WeaponGroup/WeaponSlot");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        // Take 10 damage bare-handed first so the sale's heal is observable
        // (starting from full HP would saturate at the cap).
        var monster = FindRoomCardByName(scene, "10_clubs");
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);
        int hpAfterDamage = ParseHP(scene);

        var weapon = FindRoomCardByName(scene, "6_diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);
        AssertThat((int)weaponSlot.Call("get_card_count")).IsEqual(1);

        var merchant = FindRoomCardByName(scene, "jack_hearts");
        AssertThat(merchant).IsNotNull();
        await MouseDragCard(_runner!, merchant!, new Vector2(192f, 345f));

        // Weapon slot must be cleared out — the sold weapon is gone.
        AssertThat((int)weaponSlot.Call("get_card_count")).IsEqual(0);
        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsEqual("Weapon: none");

        // Sale formula: max(1, WeaponValue(6) - SlainMonsterCount(0)) + Jack bonus(0) = 6 HP.
        AssertThat(ParseHP(scene)).IsEqual(Math.Min(ScoundrelRules.MaxHealth, hpAfterDamage + 6));

        // The sold weapon and the merchant card both ended up in discard (plus the earlier monster).
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(3);
    }

    // ── Bug fix: drop-zone labels must not key off raw suit either ─────────────
    //
    // Reported directly by the designer while playtesting: "the drop zone labels
    // are inaccurate for the new cards... using a merchant says 'drink' in the
    // drop zone." OnCardDragStarted's zone-label dispatch previously switched on
    // the raw "suit" string from card_info, so a Blacksmith card (suit "diamonds")
    // got the real-weapon "Equip"/"Discard" labels and a Merchant card (suit
    // "hearts") got the real-potion "Drink"/"Discard" labels.

    [TestCase(Description = "Dragging a Blacksmith card with a weapon equipped shows accurate zone labels, not 'Equip'/'Discard' text meant for real weapons")]
    public async Task DraggingBlacksmithCard_ShowsAccurateZoneLabels()
    {
        var deck = new List<CardModel>
        {
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Diamonds, 11, "jack_diamonds"),
            new CardModel(Suit.Diamonds, 6, "6_diamonds"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(DragAnimationMs);

        var scene = _runner!.Scene();

        var weapon = FindRoomCardByName(scene, "6_diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        var blacksmith = FindRoomCardByName(scene, "jack_diamonds");
        AssertThat(blacksmith).IsNotNull();

        var pos = (Vector2)blacksmith!.Get("global_position");
        _runner!.SimulateMouseMove(pos);
        await _runner!.AwaitMillis(MouseHoverDelayMs);
        _runner!.SimulateMouseButtonPress(MouseButton.Left, false);
        await _runner!.AwaitIdleFrame();

        AssertThat(game.LeftZoneLabelText).IsEqual("Blacksmith (repair weapon)");
        AssertThat(game.RightZoneLabelText).IsEqual("Decline (recycle)");
        AssertThat(game.LeftZoneLabelText).IsNotEqual("Equip");
        AssertThat(game.RightZoneLabelText).IsNotEqual("Discard");

        // Release over the card's own position (room dead-zone) so the card
        // bounces back to its slot instead of being taken by this test.
        _runner!.SimulateMouseButtonRelease(MouseButton.Left);
        await _runner!.AwaitMillis(PostInputSettleMs);
    }

    [TestCase(Description = "Dragging a Merchant card with a weapon equipped shows accurate zone labels, not 'Drink'/'Discard' text meant for real potions")]
    public async Task DraggingMerchantCard_ShowsAccurateZoneLabels()
    {
        var deck = new List<CardModel>
        {
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 11, "jack_hearts"),
            new CardModel(Suit.Diamonds, 6, "6_diamonds"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(DragAnimationMs);

        var scene = _runner!.Scene();

        var weapon = FindRoomCardByName(scene, "6_diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        var merchant = FindRoomCardByName(scene, "jack_hearts");
        AssertThat(merchant).IsNotNull();

        var pos = (Vector2)merchant!.Get("global_position");
        _runner!.SimulateMouseMove(pos);
        await _runner!.AwaitMillis(MouseHoverDelayMs);
        _runner!.SimulateMouseButtonPress(MouseButton.Left, false);
        await _runner!.AwaitIdleFrame();

        AssertThat(game.LeftZoneLabelText).IsEqual("Merchant (sell weapon)");
        AssertThat(game.RightZoneLabelText).IsEqual("Decline (recycle)");
        AssertThat(game.LeftZoneLabelText).IsNotEqual("Drink");
        AssertThat(game.RightZoneLabelText).IsNotEqual("Discard");

        _runner!.SimulateMouseButtonRelease(MouseButton.Left);
        await _runner!.AwaitMillis(PostInputSettleMs);
    }

    // ── Root-cause fix: "Blacksmiths currently don't seem to do anything" ──────
    //
    // Two things were found while investigating this report:
    //
    // 1. A real dispatch bug: OnCardSelected's `activateCard` computation
    //    (`!((IsPotion || IsWeapon) && droppedRight)`) never included
    //    IsBlacksmith/IsMerchant, so dropping either on the RIGHT zone still
    //    unconditionally activated it — the engine's decline/recycle path
    //    (ApplyBlacksmithEffect/ApplyMerchantEffect with activate:false) was
    //    fully implemented and unit-tested but was unreachable from the UI.
    // 2. Missing UI feedback (the more likely cause of "doesn't seem to do
    //    anything" specifically): a Blacksmith's WeaponAttackBonus/
    //    SingleUseWeaponBonus was never shown anywhere (WeaponLabel only
    //    displayed the weapon's raw name), and partial slain-monster removal
    //    never updated the visible badge row on the weapon card. The engine-side
    //    effect was always correct (see BlacksmithTests in GameEngineTests.cs);
    //    it just had no visible representation.

    [TestCase(Description = "Dropping a Blacksmith card on the right zone declines it (recycles into the deck) instead of always activating")]
    public async Task DroppingBlacksmithCard_OnRightZone_DeclinesInsteadOfActivating()
    {
        var deck = new List<CardModel>
        {
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Diamonds, 11, "jack_diamonds"),
            new CardModel(Suit.Diamonds, 6, "6_diamonds"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(DragAnimationMs);

        var scene = _runner!.Scene();
        var deckPile = scene.GetNode("UI/RightPanel/DeckGroup/DeckPile");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        var weapon = FindRoomCardByName(scene, "6_diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        int deckCountBefore = (int)deckPile.Call("get_card_count");

        var blacksmith = FindRoomCardByName(scene, "jack_diamonds");
        AssertThat(blacksmith).IsNotNull();
        await MouseDragCard(_runner!, blacksmith!); // default target: RightZoneCenter(_runner!)

        // Declined: recycled into the deck (not discarded), and — since a Jack
        // would otherwise grant +1 weapon attack with SlainMonsterCount at 0 —
        // the weapon label shows no attack bonus.
        AssertThat((int)deckPile.Call("get_card_count")).IsEqual(deckCountBefore + 1);
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(0);
        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsEqual("Weapon: 6_diamonds  (next: any)");
    }

    [TestCase(Description = "A Blacksmith card granting an attack bonus (SlainMonsterCount at 0) is surfaced in the weapon label and a status message — previously invisible")]
    public async Task BlacksmithAttackBonus_ShowsInWeaponLabelAndStatusMessage()
    {
        var deck = new List<CardModel>
        {
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Diamonds, 11, "jack_diamonds"),
            new CardModel(Suit.Diamonds, 6, "6_diamonds"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(DragAnimationMs);

        var scene = _runner!.Scene();

        var weapon = FindRoomCardByName(scene, "6_diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsEqual("Weapon: 6_diamonds  (next: any)");

        // SlainMonsterCount is 0 (nothing fought yet), so the Jack grants a
        // permanent +1 weapon attack bonus instead of removing anything.
        var blacksmith = FindRoomCardByName(scene, "jack_diamonds");
        AssertThat(blacksmith).IsNotNull();
        ClickCard(scene, blacksmith!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        AssertThat(scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel").Text).IsEqual("Weapon: 6_diamonds  [+1 atk]  (next: any)");
        AssertThat(scene.GetNode<Label>("HudLayer/StatusLabel").Text).IsEqual("Blacksmith granted +1 weapon attack!");
    }

    [TestCase(Description = "A Blacksmith card removing slain monsters updates the weapon's visible badge row — previously left stale")]
    public async Task BlacksmithRemovingSlainMonster_UpdatesBadgeRow()
    {
        var deck = new List<CardModel>
        {
            new CardModel(Suit.Hearts, 6, "6_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.Diamonds, 10, "10_diamonds"),
            new CardModel(Suit.Clubs, 2, "2_clubs"),
            new CardModel(Suit.Diamonds, 11, "jack_diamonds"),
            new CardModel(Suit.Hearts, 7, "7_hearts"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(DragAnimationMs);

        var scene = _runner!.Scene();

        var weapon = FindRoomCardByName(scene, "10_diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        var monster = FindRoomCardByName(scene, "2_clubs");
        AssertThat(monster).IsNotNull();
        ClickCard(scene, monster!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        int BadgeCount() => ((Node)weapon!).GetChildren().Count(n => n.IsInGroup("slain_badge"));
        AssertThat(BadgeCount()).IsEqual(1);

        var blacksmith = FindRoomCardByName(scene, "jack_diamonds");
        AssertThat(blacksmith).IsNotNull();
        ClickCard(scene, blacksmith!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        AssertThat(BadgeCount()).IsEqual(0);
        AssertThat(scene.GetNode<Label>("HudLayer/StatusLabel").Text).IsEqual("Blacksmith removed 1 slain monster from your weapon!");
    }

    [TestCase(Description = "A partial Blacksmith removal removes the visible badge for the most recent (lowest-value) kill, not the oldest — must match GameEngine removing from the same end of its kill history")]
    public async Task BlacksmithPartialRemoval_RemovesMostRecentKillBadge_NotOldest()
    {
        var deck = new List<CardModel>
        {
            new CardModel(Suit.Hearts, 6, "6_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            new CardModel(Suit.Diamonds, 10, "10_diamonds"),
            new CardModel(Suit.Clubs, 6, "6_clubs"),
            new CardModel(Suit.Clubs, 4, "4_clubs"),
            new CardModel(Suit.Diamonds, 11, "jack_diamonds"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(DragAnimationMs);

        var scene = _runner!.Scene();

        var weapon = FindRoomCardByName(scene, "10_diamonds");
        AssertThat(weapon).IsNotNull();
        ClickCard(scene, weapon!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        // Kill order matters: 6 first (floor -> 6), then 4 (4 < 6, floor -> 4). Badges are
        // added oldest-first, so badge[0] is "6" (the earlier, higher-value kill) and
        // badge[1] is "4" (the later, lower-value kill that currently sets the floor).
        var monster6 = FindRoomCardByName(scene, "6_clubs");
        AssertThat(monster6).IsNotNull();
        ClickCard(scene, monster6!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        var monster4 = FindRoomCardByName(scene, "4_clubs");
        AssertThat(monster4).IsNotNull();
        ClickCard(scene, monster4!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        List<string> BadgeTexts() =>
            ((Node)weapon!).GetChildren()
                .Where(n => n.IsInGroup("slain_badge"))
                .Select(n => n.GetChildren().OfType<Label>().First().Text)
                .ToList();

        AssertThat(BadgeTexts().Count).IsEqual(2);

        var blacksmith = FindRoomCardByName(scene, "jack_diamonds");
        AssertThat(blacksmith).IsNotNull();
        ClickCard(scene, blacksmith!);
        await _runner!.AwaitMillis(InteractionDelayMs * 4);

        var remaining = BadgeTexts();
        AssertThat(remaining.Count).IsEqual(1);
        AssertThat(remaining[0]).IsEqual("6"); // the higher-value, earlier kill must survive
    }
}
