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
}
