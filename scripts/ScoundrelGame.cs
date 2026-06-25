using Godot;
using Godot.Collections;
using System.Linq;
using SysCollections = System.Collections.Generic;

/// <summary>
/// Godot bridge for Scoundrel. Owns node references and visual card movements.
/// All game-state decisions are delegated to GameEngine.
/// </summary>
public partial class ScoundrelGame : Node
{
    // ── Node references ───────────────────────────────────────────────────
    private Node _cardManager = null!;
    private Node _deckPile = null!;
    private Node _discardPile = null!;
    private Node _weaponSlot = null!;
    private Node _slainPile = null!;
    private Node _roomContainer = null!;
    private Node _leftDropZone = null!;
    private Node _rightDropZone = null!;

    // ── Drop-zone highlight overlays (created at runtime) ─────────────────
    private ColorRect _leftHighlight = null!;
    private ColorRect _rightHighlight = null!;
    private Label _healthLabel = null!;
    private Label _weaponLabel = null!;
    private Label _statusLabel = null!;
    private Label _deckLabel = null!;
    private Label _discardLabel = null!;
    private Label _clubsLabel = null!;
    private Label _spadesLabel = null!;
    private Label _heartsLabel = null!;
    private Label _diamondsLabel = null!;
    private Button _runButton = null!;
    private Button _nextRoomButton = null!;
    private Button _retryButton = null!;
    private Button _helpButton = null!;
    private AcceptDialog _helpDialog = null!;

    // ── Game engine + Godot card bridge ───────────────────────────────────
    private GameEngine _engine = null!;
    // Maps card name (e.g. "ace_clubs") → its live GodotObject node
    private readonly SysCollections.Dictionary<string, GodotObject> _godotCards = new();
    // Godot card nodes currently displayed in the slain pile (killed with weapon)
    private readonly SysCollections.List<GodotObject> _slainGodotCards = new();

    // ── Suit tracking (UI labels only — not game logic) ───────────────────
    private int _inPlayClubs;
    private int _inPlaySpades;
    private int _inPlayHearts;
    private int _inPlayDiamonds;

    // ── Cached factory reference ──────────────────────────────────────────
    private GodotObject _cardFactory = null!;

    // ── Card name tables ──────────────────────────────────────────────────
    private static readonly string[] Ranks =
        { "ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "jack", "queen", "king" };
    private static readonly string[] MonsterSuits = { "clubs", "spades" };
    private static readonly string[] RedRanks     = { "2", "3", "4", "5", "6", "7", "8", "9", "10" };
    private static readonly string[] RedSuits     = { "hearts", "diamonds" };

    // ── Godot lifecycle ───────────────────────────────────────────────────
    public override void _Ready()
    {
        _cardManager    = GetNode<Node>("CardManager");
        _deckPile       = GetNode<Node>("UI/DeckPile");
        _discardPile    = GetNode<Node>("UI/DiscardPile");
        _weaponSlot     = GetNode<Node>("UI/WeaponSlot");
        _slainPile      = GetNode<Node>("UI/SlainPile");
        _roomContainer  = GetNode<Node>("UI/RoomContainer");
        _leftDropZone   = GetNode<Node>("UI/LeftDropZone");
        _rightDropZone  = GetNode<Node>("UI/RightDropZone");
        _healthLabel    = GetNode<Label>("UI/HealthLabel");
        _weaponLabel    = GetNode<Label>("UI/WeaponLabel");
        _statusLabel    = GetNode<Label>("UI/StatusLabel");
        _deckLabel      = GetNode<Label>("UI/DeckLabel");
        _discardLabel   = GetNode<Label>("UI/DiscardLabel");
        _clubsLabel     = GetNode<Label>("UI/ClubsLabel");
        _spadesLabel    = GetNode<Label>("UI/SpadesLabel");
        _heartsLabel    = GetNode<Label>("UI/HeartsLabel");
        _diamondsLabel  = GetNode<Label>("UI/DiamondsLabel");
        _runButton      = GetNode<Button>("UI/RunButton");
        _nextRoomButton = GetNode<Button>("UI/NextRoomButton");
        _retryButton    = GetNode<Button>("UI/RetryButton");
        _helpButton     = GetNode<Button>("UI/HelpButton");
        _helpDialog     = GetNode<AcceptDialog>("UI/HelpDialog");

        _cardFactory = (GodotObject)_cardManager.Get("card_factory");

        var ui = GetNode<CanvasLayer>("UI");
        _leftHighlight  = AddZoneHighlight(ui, new Vector2(0, 70),   new Vector2(385, 550), new Color(0.25f, 0.8f, 0.25f, 0.25f));
        _rightHighlight = AddZoneHighlight(ui, new Vector2(740, 70), new Vector2(380, 550), new Color(0.25f, 0.5f, 1.0f,  0.25f));

        _roomContainer.Connect("card_drag_started", Callable.From<GodotObject>(OnCardDragStarted));
        _roomContainer.Connect("card_drag_ended",   Callable.From(OnCardDragEnded));
        _roomContainer.Connect("card_selected",     Callable.From<GodotObject>(OnCardSelected));
        _runButton.Connect("pressed",     Callable.From(OnRunPressed));
        _nextRoomButton.Connect("pressed", Callable.From(OnNextRoomPressed));
        _retryButton.Connect("pressed",   Callable.From(OnRetryPressed));
        _helpButton.Connect("pressed",    Callable.From(OnHelpPressed));

        StartGame();
    }

    // ── Setup ─────────────────────────────────────────────────────────────
    private void StartGame()
    {
        var deck = BuildDeck();
        var rng  = new System.Random();
        InitGameWithDeck(deck.OrderBy(_ => rng.Next()).ToList());
    }

    // Called by scene tests to start with a known, deterministic deck.
    public void StartGameWithDeck(SysCollections.List<CardModel> deck) => InitGameWithDeck(deck);

    private void InitGameWithDeck(SysCollections.List<CardModel> deck)
    {
        _godotCards.Clear();
        _slainGodotCards.Clear();
        _statusLabel.Text = "";

        _roomContainer.Call("clear_cards");
        _deckPile.Call("clear_cards");
        _discardPile.Call("clear_cards");
        _weaponSlot.Call("clear_cards");
        _slainPile.Call("clear_cards");

        _inPlayClubs    = deck.Count(c => c.Suit == Suit.Clubs);
        _inPlaySpades   = deck.Count(c => c.Suit == Suit.Spades);
        _inPlayHearts   = deck.Count(c => c.Suit == Suit.Hearts);
        _inPlayDiamonds = deck.Count(c => c.Suit == Suit.Diamonds);

        // Create matching Godot card nodes (all start in the deck pile).
        foreach (var cardModel in deck)
        {
            var godotCard = _cardFactory.Call("create_card", cardModel.Name, _deckPile).AsGodotObject();
            _godotCards[cardModel.Name] = godotCard;
        }

        // Engine auto-deals room 1 in its constructor; sync Godot visuals to match.
        _engine = new GameEngine(deck);
        SyncRoomToGodot();
        UpdateUI();
    }

    private SysCollections.List<CardModel> BuildDeck()
    {
        var deck = new SysCollections.List<CardModel>();
        foreach (var suit in MonsterSuits)
            foreach (var rank in Ranks)
            {
                var s = suit == "clubs" ? Suit.Clubs : Suit.Spades;
                deck.Add(new CardModel(s, RankToInt(rank), $"{rank}_{suit}"));
            }
        foreach (var suit in RedSuits)
            foreach (var rank in RedRanks)
            {
                var s = suit == "hearts" ? Suit.Hearts : Suit.Diamonds;
                deck.Add(new CardModel(s, int.Parse(rank), $"{rank}_{suit}"));
            }
        return deck;
    }

    // ── Room sync ─────────────────────────────────────────────────────────

    /// <summary>
    /// Moves any engine.Room cards that aren't already in the Godot room container.
    /// Called after every engine action that may have dealt new cards.
    /// </summary>
    private void SyncRoomToGodot()
    {
        ResetRoomCardTints();

        // Anchor new cards at the deck pile position before moving them to the room.
        // Without this, a card that was JUST moved to the deck in the same frame (its
        // tween has started but _process hasn't run yet) still sits at its old room-slot
        // position in global space. card.move() in draggable_object.gd would then see
        // global_position == target and early-return, leaving the deck-bound tween
        // running — stranding the card at the deck position while it's logically in
        // the room (face-up and interactive). Pre-setting to the deck anchor ensures
        // global_position != room_slot_pos, so the room tween always starts and the
        // stale deck tween is killed.
        var deckAnchor = (Vector2)_deckPile.Get("global_position");

        var alreadyInRoom = ((Array)_roomContainer.Call("get_all_cards"))
            .Select(v => v.AsGodotObject().Get("card_info").AsGodotDictionary()["name"].AsString())
            .ToHashSet();

        foreach (var cardModel in _engine.Room)
        {
            if (!alreadyInRoom.Contains(cardModel.Name))
            {
                var godotCard = _godotCards[cardModel.Name];
                godotCard.Set("global_position", deckAnchor);
                _roomContainer.Call("move_cards", new Array { godotCard }, -1, false);
            }
        }

        if (_engine.PotionUsedThisRoom)
            TintRemainingPotions();
    }

    // ── Card selection ────────────────────────────────────────────────────
    private void OnCardSelected(GodotObject card)
    {
        if (_engine.IsOver) return;

        var name = card.Get("card_info").AsGodotDictionary()["name"].AsString();
        var cardModel = _engine.Room.FirstOrDefault(c => c.Name == name);
        if (cardModel is null) return;

        // Determine which zone accepted the drop (0 when emitted directly, e.g. in tests).
        ulong containerId = card.Get("card_container").AsGodotObject().GetInstanceId();
        bool droppedLeft  = containerId == _leftDropZone.GetInstanceId();
        bool droppedRight = containerId == _rightDropZone.GetInstanceId();

        // Validate routing when a real zone drop occurred.
        if (droppedLeft || droppedRight)
        {
            if (cardModel.IsPotion && droppedLeft)
            {
                _roomContainer.Call("move_cards", new Array { card }, -1, false);
                ShowBriefMessage("Potions go on the right!");
                return;
            }
            if (cardModel.IsWeapon && droppedRight)
            {
                _roomContainer.Call("move_cards", new Array { card }, -1, false);
                ShowBriefMessage("Weapons go on the left!");
                return;
            }
        }

        // Monster dropped into right zone = fight bare-handed (skip equipped weapon).
        bool useWeapon = !(cardModel.IsMonster && droppedRight);

        var oldWeapon           = _engine.EquippedWeapon;
        bool potionWastedBefore = _engine.PotionWastedThisRoom;
        bool willUseWeapon      = useWeapon
            && cardModel.IsMonster
            && _engine.EquippedWeapon != null
            && ScoundrelRules.CanUseWeapon(cardModel.MonsterValue, _engine.WeaponFloor);

        _engine.TakeCard(cardModel, useWeapon);

        // Visual side-effects per card type
        switch (cardModel.Suit)
        {
            case Suit.Clubs:
            case Suit.Spades:
                DecrementSuit(cardModel);
                if (willUseWeapon)
                    MoveToSlain(card);
                else
                    MoveToDiscard(card);
                break;

            case Suit.Hearts:
                if (!potionWastedBefore && _engine.PotionWastedThisRoom)
                    ShowBriefMessage("Potion wasted! (one per room)");
                DecrementSuit(cardModel);
                MoveToDiscard(card);
                break;

            case Suit.Diamonds:
                if (oldWeapon != null)
                {
                    DecrementSuit(oldWeapon);
                    DiscardSlainCards();
                    MoveToDiscard(_godotCards[oldWeapon.Name]);
                }
                ResetCardScale(card);
                _weaponSlot.Call("move_cards", new Array { card }, -1, false);
                break;
        }

        if (_engine.GameOver) { ShowGameOver(); UpdateUI(); return; }
        if (_engine.Won)      { ShowWin();      UpdateUI(); return; }

        // Sync any new room cards the engine may have dealt (auto-advance).
        SyncRoomToGodot();
        UpdateUI();
    }

    // ── Drag zone highlights ──────────────────────────────────────────────
    private void OnCardDragStarted(GodotObject card)
    {
        var suit = card.Get("card_info").AsGodotDictionary()["suit"].AsString();
        switch (suit)
        {
            case "clubs":
            case "spades":
                _leftHighlight.Visible  = true;   // can fight with weapon
                _rightHighlight.Visible = true;   // can fight bare-handed
                break;
            case "hearts":
                _leftHighlight.Visible  = false;  // potions only go right
                _rightHighlight.Visible = true;
                break;
            case "diamonds":
                _leftHighlight.Visible  = true;   // weapons only go left
                _rightHighlight.Visible = false;
                break;
        }
    }

    private void OnCardDragEnded()
    {
        _leftHighlight.Visible  = false;
        _rightHighlight.Visible = false;
    }

    // ── Buttons ───────────────────────────────────────────────────────────
    private void OnRunPressed()
    {
        if (!_engine.CanRun) return;

        // Capture room Godot cards before engine clears them.
        var roomGodotCards = _engine.Room.Select(c => _godotCards[c.Name]).ToList();

        _engine.Run();

        // Return old room cards to the deck pile (face-down, any position is fine).
        foreach (var godotCard in roomGodotCards)
        {
            godotCard.Set("modulate", new Color(1f, 1f, 1f));
            _deckPile.Call("move_cards", new Array { godotCard }, 0, false);
        }

        SyncRoomToGodot();
        UpdateUI();
    }

    private void OnNextRoomPressed()
    {
        if (!_engine.CanNextRoom) return;
        _engine.NextRoom();
        SyncRoomToGodot();
        UpdateUI();
    }

    private void OnRetryPressed()
    {
        StartGame();
    }

    private void OnHelpPressed()
    {
        _helpDialog.PopupCentered();
    }

    // ── End states ────────────────────────────────────────────────────────
    private void ShowGameOver()
    {
        _statusLabel.Text = "YOU DIED";
        foreach (var cardModel in _engine.Room)
            _godotCards[cardModel.Name].Set("can_be_interacted_with", false);
    }

    private void ShowWin()
    {
        _statusLabel.Text = "YOU WIN!";
    }

    // ── Visual helpers ────────────────────────────────────────────────────
    private void TintRemainingPotions()
    {
        var dimmed = new Color(0.55f, 0.55f, 0.55f);
        foreach (var cardModel in _engine.Room.Where(c => c.IsPotion))
            _godotCards[cardModel.Name].Set("modulate", dimmed);
    }

    private void ResetRoomCardTints()
    {
        foreach (var cardModel in _engine.Room)
            _godotCards[cardModel.Name].Set("modulate", new Color(1f, 1f, 1f));
    }

    private void MoveToDiscard(GodotObject card)
    {
        ResetCardScale(card);
        card.Set("modulate", new Color(1f, 1f, 1f));
        _discardPile.Call("move_cards", new Array { card }, -1, false);
    }

    private void MoveToSlain(GodotObject card)
    {
        ResetCardScale(card);
        card.Set("modulate", new Color(1f, 1f, 1f));
        _slainPile.Call("move_cards", new Array { card }, 0, false);
        _slainGodotCards.Insert(0, card);
        FixSlainZOrder();
    }

    // The pile assigns stored_z_index = array_index, so the newest card (index 0)
    // gets z=0 and ends up buried. Invert so the newest card always has the highest z.
    private void FixSlainZOrder()
    {
        int count = _slainGodotCards.Count;
        for (int i = 0; i < count; i++)
            _slainGodotCards[i].Set("stored_z_index", count - 1 - i);
    }

    private void DiscardSlainCards()
    {
        foreach (var slainCard in _slainGodotCards)
            MoveToDiscard(slainCard);
        _slainGodotCards.Clear();
    }

    private static void ResetCardScale(GodotObject card)
        => card.Set("scale", new Vector2(1f, 1f));

    private void DecrementSuit(CardModel card)
    {
        switch (card.Suit)
        {
            case Suit.Clubs:    _inPlayClubs--;    break;
            case Suit.Spades:   _inPlaySpades--;   break;
            case Suit.Hearts:   _inPlayHearts--;   break;
            case Suit.Diamonds: _inPlayDiamonds--; break;
        }
    }

    private void ShowBriefMessage(string text)
    {
        _statusLabel.Text = text;
        var timer = GetTree().CreateTimer(2.0);
        timer.Timeout += () => { if (_statusLabel.Text == text) _statusLabel.Text = ""; };
    }

    private void UpdateUI()
    {
        _healthLabel.Text = $"HP: {_engine.Health} / {ScoundrelRules.MaxHealth}";

        if (_engine.EquippedWeapon != null)
        {
            string floor = _engine.WeaponFloor == int.MaxValue ? "any" : $"< {_engine.WeaponFloor}";
            _weaponLabel.Text = $"Weapon: {_engine.EquippedWeapon.Name}  (next: {floor})";
        }
        else
        {
            _weaponLabel.Text = "Weapon: none";
        }

        _runButton.Disabled     = !_engine.CanRun;
        _nextRoomButton.Visible = _engine.CanNextRoom;

        int deckCount    = (int)_deckPile.Call("get_card_count");
        int discardCount = (int)_discardPile.Call("get_card_count");
        _deckLabel.Text    = $"DECK ({deckCount})";
        _discardLabel.Text = $"DISCARD ({discardCount})";

        _clubsLabel.Text    = $"♣  {_inPlayClubs}";
        _spadesLabel.Text   = $"♠  {_inPlaySpades}";
        _heartsLabel.Text   = $"♥  {_inPlayHearts}";
        _diamondsLabel.Text = $"♦  {_inPlayDiamonds}";
    }

    // ── Utilities ─────────────────────────────────────────────────────────
    private static ColorRect AddZoneHighlight(CanvasLayer parent, Vector2 pos, Vector2 size, Color color)
    {
        var rect = new ColorRect();
        rect.Position    = pos;
        rect.Size        = size;
        rect.Color       = color;
        rect.MouseFilter = Control.MouseFilterEnum.Ignore;
        rect.ZIndex      = 5;
        rect.Visible     = false;
        parent.AddChild(rect);
        return rect;
    }

    private static int RankToInt(string rank) => rank switch
    {
        "ace"   => 1,
        "jack"  => 11,
        "queen" => 12,
        "king"  => 13,
        _       => int.Parse(rank),
    };
}
