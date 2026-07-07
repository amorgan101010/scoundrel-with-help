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
/// Extended Rules Joker scene tests: taking a Joker, fighting with a Joker, and Joker death.
/// Split out of the former ScoundrelSceneTests.cs; shared helpers live in SceneTestHelpers.cs.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScoundrelJokerCombatSceneTests
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

    // ── Extended Rules: Joker UI wiring (placement, HP, fight-with-joker) ──────

    [TestCase(Description = "Taking the Red Joker moves its card into the Potion Joker slot (not left in the room) and shows its starting HP")]
    public async Task TakingRedJoker_MovesToPotionJokerSlotWithHp()
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
            new CardModel(Suit.Clubs, 4, "4_clubs"),
            new CardModel(Suit.RedJoker, 0, "joker_red"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(200);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");
        var potionJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/PotionJokerSlot");

        var joker = FindRoomCard(scene, s => s == "red_joker");
        AssertThat(joker).IsNotNull();

        ClickCard(scene, joker!);
        await _runner!.AwaitIdleFrame();

        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(1);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(3);

        var hpLabel = scene.GetNode<Label>("UI/LeftPanel/JokerGroup/PotionJokerHpLabel");
        AssertThat(hpLabel.Text).IsEqual("Potion Joker HP: 8/8");
    }

    [TestCase(Description = "Taking the Black Joker moves its card into the Weapon Joker slot (not left in the room) and shows its starting HP")]
    public async Task TakingBlackJoker_MovesToWeaponJokerSlotWithHp()
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
            new CardModel(Suit.Clubs, 4, "4_clubs"),
            new CardModel(Suit.BlackJoker, 0, "joker_black"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(200);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");
        var weaponJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/WeaponJokerSlot");

        var joker = FindRoomCard(scene, s => s == "black_joker");
        AssertThat(joker).IsNotNull();

        ClickCard(scene, joker!);
        await _runner!.AwaitIdleFrame();

        AssertThat((int)weaponJokerSlot.Call("get_card_count")).IsEqual(1);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(3);

        var hpLabel = scene.GetNode<Label>("UI/LeftPanel/JokerGroup/WeaponJokerHpLabel");
        AssertThat(hpLabel.Text).IsEqual("Weapon Joker HP: 8/8");
    }

    [TestCase(Description = "Dragging a monster onto the Potion Joker fight zone reduces the joker's HP, not the player's Health")]
    public async Task FightingMonsterWithPotionJoker_ReducesJokerHpNotPlayerHealth()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            // Room 1 (dealt first)
            new CardModel(Suit.Clubs, 5, "5_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Clubs, 4, "4_clubs"),
            new CardModel(Suit.RedJoker, 0, "joker_red"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        var joker = FindRoomCard(scene, s => s == "red_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        int hpBefore = ParseHP(scene);

        var monster = FindRoomCardByName(scene, "5_clubs");
        AssertThat(monster).IsNotNull();
        int monsterValue = MonsterDamage(monster!.Get("card_info").AsGodotDictionary());

        await MouseDragCard(_runner!, monster!, PotionJokerZoneCenter(_runner!));

        // Player Health is completely untouched by a joker-absorbed hit.
        AssertThat(ParseHP(scene)).IsEqual(hpBefore);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(2); // 3 (post-joker-take) - 1
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(1);

        var hpLabel = scene.GetNode<Label>("UI/LeftPanel/JokerGroup/PotionJokerHpLabel");
        AssertThat(hpLabel.Text).IsEqual($"Potion Joker HP: {8 - monsterValue}/8");
    }

    [TestCase(Description = "Dragging a monster onto the Weapon Joker fight zone reduces the joker's HP, not the player's Health")]
    public async Task FightingMonsterWithWeaponJoker_ReducesJokerHpNotPlayerHealth()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            // Room 1 (dealt first)
            new CardModel(Suit.Spades, 6, "6_spades"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Clubs, 4, "4_clubs"),
            new CardModel(Suit.BlackJoker, 0, "joker_black"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");

        var joker = FindRoomCard(scene, s => s == "black_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        int hpBefore = ParseHP(scene);

        var monster = FindRoomCardByName(scene, "6_spades");
        AssertThat(monster).IsNotNull();
        int monsterValue = MonsterDamage(monster!.Get("card_info").AsGodotDictionary());

        await MouseDragCard(_runner!, monster!, WeaponJokerZoneCenter(_runner!));

        AssertThat(ParseHP(scene)).IsEqual(hpBefore);
        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(2);
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(1);

        var hpLabel = scene.GetNode<Label>("UI/LeftPanel/JokerGroup/WeaponJokerHpLabel");
        AssertThat(hpLabel.Text).IsEqual($"Weapon Joker HP: {8 - monsterValue}/8");
    }

    [TestCase(Description = "A joker whose HP hits 0 is lost: its card moves to discard, its HP label clears, and the game continues without crashing")]
    public async Task PotionJokerDying_MovesToDiscardAndClearsHpLabel()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            // Room 1 (dealt first): 10_clubs (value 10) kills the 8-HP joker outright.
            new CardModel(Suit.Clubs, 10, "10_clubs"),
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Clubs, 4, "4_clubs"),
            new CardModel(Suit.RedJoker, 0, "joker_red"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");
        var potionJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/PotionJokerSlot");

        var joker = FindRoomCard(scene, s => s == "red_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        var monster = FindRoomCardByName(scene, "10_clubs");
        AssertThat(monster).IsNotNull();
        await MouseDragCard(_runner!, monster!, PotionJokerZoneCenter(_runner!));

        // Joker died: its card left the slot for discard, and the label resets.
        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(0);
        AssertThat((int)discardPile.Call("get_card_count")).IsEqual(2); // monster + dead joker

        var hpLabel = scene.GetNode<Label>("UI/LeftPanel/JokerGroup/PotionJokerHpLabel");
        AssertThat(hpLabel.Text).IsEqual("Potion Joker: —");

        // The game must continue normally — taking another card doesn't crash.
        var nextCard = FindRoomCardByName(scene, "3_clubs");
        AssertThat(nextCard).IsNotNull();
        int hpBefore = ParseHP(scene);
        ClickCard(scene, nextCard!);
        await _runner!.AwaitIdleFrame();

        AssertThat(ParseHP(scene)).IsEqual(hpBefore - 3);
    }

    [TestCase(Description = "A joker dying while holding a pocketed item doesn't strand the item's card in the (now-empty) slot — it must also move to discard and its suit count must decrement, or it's a permanently stuck, non-interactive ghost card")]
    public async Task PotionJokerDyingWithPocketedItem_AlsoDiscardsThePocketedCard()
    {
        var deck = new List<CardModel>
        {
            // Room 2 padding
            new CardModel(Suit.Hearts, 2, "2_hearts"),
            new CardModel(Suit.Hearts, 3, "3_hearts"),
            new CardModel(Suit.Hearts, 4, "4_hearts"),
            new CardModel(Suit.Hearts, 5, "5_hearts"),
            // Room 1 (dealt first): joker, a potion to pocket, and the killer monster —
            // everything happens in this one room, no room transition required.
            new CardModel(Suit.Clubs, 10, "10_clubs"), // kills the 8-HP joker outright
            new CardModel(Suit.Clubs, 3, "3_clubs"),
            new CardModel(Suit.Hearts, 6, "6_hearts"),
            new CardModel(Suit.RedJoker, 0, "joker_red"),
        };
        var game = (ScoundrelGame)_runner!.Scene();
        game.ExtendedRules = true;
        game.StartGameWithDeck(deck);
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        var scene = _runner!.Scene();
        var discardPile = scene.GetNode("UI/RightPanel/DiscardGroup/DiscardPile");
        var potionJokerSlot = scene.GetNode("UI/LeftPanel/JokerGroup/PotionJokerSlot");
        var heartsLabel = scene.GetNode<Label>("UI/LeftPanel/WeaponGroup/InPlayGroup/HeartsLabel");

        AssertThat(heartsLabel.Text).IsEqual("♥  5"); // 6,5,4,3,2 of hearts in this custom deck

        var joker = FindRoomCard(scene, s => s == "red_joker");
        AssertThat(joker).IsNotNull();
        ClickCard(scene, joker!);
        await _runner!.AwaitMillis(UITimings.InteractionDelayMs * 4);

        var potion = FindRoomCardByName(scene, "6_hearts");
        AssertThat(potion).IsNotNull();
        await MouseDragCard(_runner!, potion!, PotionJokerZoneCenter(_runner!));
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(2); // joker + pocketed potion
        AssertThat(heartsLabel.Text).IsEqual("♥  5"); // storing doesn't remove it from play

        var monster = FindRoomCardByName(scene, "10_clubs");
        AssertThat(monster).IsNotNull();
        await MouseDragCard(_runner!, monster!, PotionJokerZoneCenter(_runner!));
        await _runner!.AwaitMillis(UITimings.DragAnimationMs);

        // The joker died (10 damage >= 8 HP). Both its own card AND the pocketed potion
        // must have left the slot -- a stuck pocketed card is the bug being tested for.
        AssertThat((int)potionJokerSlot.Call("get_card_count")).IsEqual(0);
        AssertThat(heartsLabel.Text).IsEqual("♥  4"); // the lost potion must be decremented

        var pocketedCardContainerId = potion!.Get("card_container").AsGodotObject().GetInstanceId();
        AssertThat(pocketedCardContainerId).IsEqual(discardPile.GetInstanceId());
    }

    [TestCase(Description = "Dragging a monster onto a joker fight zone with no joker owned bounces it back without discarding")]
    public async Task DraggingMonsterToJokerZoneWithoutJoker_BouncesBack()
    {
        await SetupFixedDeck(_runner!, UITimings.DragAnimationMs);
        var scene = _runner!.Scene();
        var room  = scene.GetNode("UI/RoomContainer");

        // FixedDeck Room 1 has no jokers — HasPotionJoker/HasWeaponJoker are both false.
        var monster = FindRoomCard(scene, s => s == "clubs");
        AssertThat(monster).IsNotNull();

        int countBefore = ((GArray)room.Call("get_all_cards")).Count;
        int hpBefore = ParseHP(scene);

        await MouseDragCard(_runner!, monster!, PotionJokerZoneCenter(_runner!));

        AssertThat(((GArray)room.Call("get_all_cards")).Count).IsEqual(countBefore);
        AssertThat(ParseHP(scene)).IsEqual(hpBefore);
        AssertThat(scene.GetNode<Label>("HudLayer/StatusLabel").Text).IsEqual("Potion Joker can't fight!");
    }
}
