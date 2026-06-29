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
    private Control _roomContainer = null!;
    private HBoxContainer _bottomButtonGroup = null!;
    private HBoxContainer _topButtonGroup = null!;
    private Node _leftDropZone = null!;
    private Node _rightDropZone = null!;

    // ── Drop-zone overlays (highlights + labels, created at runtime) ─────
    private ColorRect _leftHighlight = null!;
    private ColorRect _rightHighlight = null!;
    private Label _leftLabel = null!;
    private Label _rightLabel = null!;
    private Label _healthLabel = null!;
    private HealthDie _healthDie = null!;
    private Label _weaponLabel = null!;
    private Label _statusLabel = null!;
    private Label _clubsLabel = null!;
    private Label _spadesLabel = null!;
    private Label _heartsLabel = null!;
    private Label _diamondsLabel = null!;
    private Button _runButton = null!;
    private Button _nextRoomButton = null!;
    private Button _retryButton = null!;
    private Button _helpButton = null!;
    private AcceptDialog _helpDialog = null!;
    private Label _flavorLabel = null!;

    // ── Game engine + Godot card bridge ───────────────────────────────────
    private GameEngine _engine = null!;
    // Maps card name (e.g. "ace_clubs") → its live GodotObject node
    private readonly SysCollections.Dictionary<string, GodotObject> _godotCards = new();
    // ── Suit tracking (UI labels only — not game logic) ───────────────────
    private int _inPlayClubs;
    private int _inPlaySpades;
    private int _inPlayHearts;
    private int _inPlayDiamonds;

    // ── Cached factory reference ──────────────────────────────────────────
    private GodotObject _cardFactory = null!;

    // ── Sound effects ─────────────────────────────────────────────────────
    required public AudioManager AudioManager {get; set;}

    // ── Layout constants ──────────────────────────────────────────────────
    // Base card dimensions for a 1080p viewport. These are scaled at runtime
    // to match the current viewport size and applied to the card factory.
    private const float BaseCardWidth  = 225f;
    private const float BaseCardHeight = 315f;

    // Current runtime card size (updated on startup and when viewport resizes)
    private Vector2 _cardSize = new Vector2(BaseCardWidth, BaseCardHeight);
    private float CardW => _cardSize.X;
    private float CardH => _cardSize.Y;
    // Viewport resize debouncing (time-based)
    private const float ResizeDebounceSeconds = 0.12f; // debounce interval
    private Timer _resizeTimer = null!;

    // CanvasLayer Z-order. Bounce ghosts sit at LayerBounce; interactive UI must be ≥ LayerHud.
    private const int LayerOverlay = 128;
    private const int LayerBounce  = 201;
    private const int LayerHud     = 202;
    private const int LayerButtons = 203;

    // Slain-monster badge geometry: layout slot dimensions vs. visible control size.
    private const float BadgeLayoutWidth  = 45f;
    private const float BadgeLayoutHeight = 66f;
    private const float BadgeVisualWidth  = 30f;
    private const float BadgeVisualHeight = 44f;
    private const float BadgeNaturalStep  = 52.5f;

    // Flavor label: 400px wide centered strip near the top of the screen.
    private const float FlavorLabelHalfWidth    = 200f;
    private const float FlavorLabelOffsetTop    = 103f;
    private const float FlavorLabelOffsetBottom = 130f;
    private const int   FlavorLabelFontSize     = 18;

    // Zone label font size
    private const int ZoneLabelFontSize = 28;

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
        _deckPile       = GetNode<Node>("UI/RightPanel/DeckGroup/DeckPile");
        _discardPile    = GetNode<Node>("UI/RightPanel/DiscardGroup/DiscardPile");
        _weaponSlot     = GetNode<Node>("UI/LeftPanel/WeaponGroup/WeaponSlot");
        _roomContainer  = GetNode<Control>("UI/RoomContainer");
        _bottomButtonGroup = GetNode<HBoxContainer>("UI/BottomButtonGroup");
        _topButtonGroup = GetNode<HBoxContainer>("UI/TopButtonGroup");
        _leftDropZone   = GetNode<Node>("UI/LeftPanel/LeftDropZone");
        _rightDropZone  = GetNode<Node>("UI/RightPanel/RightDropZone");
        _healthLabel    = GetNode<Label>("UI/HealthLabel");
        _weaponLabel    = GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel");
        _statusLabel    = GetNode<Label>("UI/StatusLabel");
        _clubsLabel     = GetNode<Label>("UI/LeftPanel/WeaponGroup/InPlayGroup/ClubsLabel");
        _spadesLabel    = GetNode<Label>("UI/LeftPanel/WeaponGroup/InPlayGroup/SpadesLabel");
        _heartsLabel    = GetNode<Label>("UI/LeftPanel/WeaponGroup/InPlayGroup/HeartsLabel");
        _diamondsLabel  = GetNode<Label>("UI/LeftPanel/WeaponGroup/InPlayGroup/DiamondsLabel");
        _runButton      = GetNode<Button>("UI/BottomButtonGroup/RunButton");
        _nextRoomButton = GetNode<Button>("UI/BottomButtonGroup/NextRoomButton");
        _retryButton    = GetNode<Button>("UI/TopButtonGroup/RetryButton");
        _helpButton     = GetNode<Button>("UI/TopButtonGroup/HelpButton");
        _helpDialog     = GetNode<AcceptDialog>("UI/HelpDialog");

        _cardFactory = (GodotObject)_cardManager.Get("card_factory");
        UpdateCardSize();
        // Create resize debounce timer used to delay expensive layout work until
        // the user has finished resizing. Start stopped; we'll start it on events.
        _resizeTimer = new Timer();
        _resizeTimer.OneShot = true;
        _resizeTimer.WaitTime = ResizeDebounceSeconds;
        AddChild(_resizeTimer);
        _resizeTimer.Connect("timeout", Callable.From(OnResizeDebounceTimeout));

        _healthDie = GetNode<HealthDie>("UI/LeftPanel/HealthDie");

        // Lift retry + help buttons and status text above the bounce layer (201).
        // HudLayer: status/flavor text (display only, game-over and in-game messages)
        var hudLayer = new CanvasLayer();
        hudLayer.Name  = "HudLayer";
        hudLayer.Layer = LayerHud;
        AddChild(hudLayer);
        _statusLabel.Reparent(hudLayer, true);

        _flavorLabel = new Label();
        _flavorLabel.AnchorLeft          = 0.5f;
        _flavorLabel.AnchorRight         = 0.5f;
        _flavorLabel.GrowHorizontal      = Control.GrowDirection.Both;
        _flavorLabel.OffsetLeft          = -FlavorLabelHalfWidth;
        _flavorLabel.OffsetRight         =  FlavorLabelHalfWidth;
        _flavorLabel.OffsetTop           =  FlavorLabelOffsetTop;
        _flavorLabel.OffsetBottom        =  FlavorLabelOffsetBottom;
        _flavorLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _flavorLabel.AddThemeFontSizeOverride("font_size", FlavorLabelFontSize);
        _flavorLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.5f, 0.5f));
        _flavorLabel.MouseFilter         = Control.MouseFilterEnum.Ignore;
        _flavorLabel.Visible             = false;
        hudLayer.AddChild(_flavorLabel);

        // ButtonLayer: interactive controls — must be above HudLayer so clicks are never blocked
        var buttonLayer = new CanvasLayer();
        buttonLayer.Name  = "ButtonLayer";
        buttonLayer.Layer = LayerButtons;
        AddChild(buttonLayer);
        _topButtonGroup.Reparent(buttonLayer, true);
        _bottomButtonGroup.Reparent(buttonLayer, true);

        // Dedicated overlay layer above everything (UI is layer 1, cards are layer 0).
        var overlayLayer = new CanvasLayer();
        overlayLayer.Layer = LayerOverlay;
        AddChild(overlayLayer);
        _leftHighlight  = AddZoneHighlight(overlayLayer, 0f,    1f/3f, new Color(0.25f, 0.8f, 0.25f, 0.30f));
        _rightHighlight = AddZoneHighlight(overlayLayer, 2f/3f, 1f,    new Color(0.25f, 0.5f, 1.0f,  0.30f));
        _leftLabel      = AddZoneLabel(overlayLayer, 0f,    1f/3f);
        _rightLabel     = AddZoneLabel(overlayLayer, 2f/3f, 1f);

        AudioManager = new AudioManager
        {
            Name = "AudioManager"
        };
        AddChild(AudioManager);

        _roomContainer.Connect("card_drag_started", Callable.From<GodotObject>(OnCardDragStarted));
        _roomContainer.Connect("card_drag_ended",   Callable.From(OnCardDragEnded));
        _roomContainer.Connect("card_selected",     Callable.From<GodotObject>(OnCardSelected));
        _runButton.Connect("pressed",     Callable.From(OnRunPressed));
        _nextRoomButton.Connect("pressed", Callable.From(OnNextRoomPressed));
        _retryButton.Connect("pressed",   Callable.From(OnRetryPressed));
        _helpButton.Connect("pressed",    Callable.From(OnHelpPressed));
        GetViewport().Connect("size_changed", Callable.From(OnViewportResized));

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
        if (_bounceLayer != null) { _bounceLayer.QueueFree(); _bounceLayer = null; }
        _bounceActive = false;
        _bounceState.Clear();
        _bounceTotal  = 0;

        _godotCards.Clear();
        _statusLabel.Text    = "";
        _flavorLabel.Visible = false;
        _flavorLabel.Text    = "";

        _roomContainer.Call("clear_cards");
        _deckPile.Call("clear_cards");
        _discardPile.Call("clear_cards");
        _weaponSlot.Call("clear_cards");

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

        bool anyMoved = false;
        foreach (var cardModel in _engine.Room)
        {
            if (!alreadyInRoom.Contains(cardModel.Name))
            {
                var godotCard = _godotCards[cardModel.Name];
                godotCard.Set("global_position", deckAnchor);
                _roomContainer.Call("move_cards", new Array { godotCard }, -1, false);
                anyMoved = true;
            }
        }

        if (anyMoved)
            AudioManager.PlayCardDealt();

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

        // Monster + left zone: validate weapon usability before accepting.
        if (cardModel.IsMonster && droppedLeft)
        {
            bool canUse = _engine.EquippedWeapon != null
                && ScoundrelRules.CanUseWeapon(cardModel.MonsterValue, _engine.WeaponFloor);
            if (!canUse)
            {
                _roomContainer.Call("move_cards", new Array { card }, -1, false);
                ShowBriefMessage("Weapon can't block that!");
                return;
            }
        }

        // Void potion + left zone: highlight is hidden; bounce silently.
        if (cardModel.IsPotion && droppedLeft && _engine.PotionUsedThisRoom)
        {
            _roomContainer.Call("move_cards", new Array { card }, -1, false);
            return;
        }

        // Monster right = bare-handed; potion/weapon right = discard without activating.
        bool useWeapon   = !(cardModel.IsMonster && droppedRight);
        bool activateCard = !((cardModel.IsPotion || cardModel.IsWeapon) && droppedRight);

        var oldWeapon           = _engine.EquippedWeapon;
        bool potionUsedBefore   = _engine.PotionUsedThisRoom;
        bool potionWastedBefore = _engine.PotionWastedThisRoom;
        bool willUseWeapon      = useWeapon
            && cardModel.IsMonster
            && _engine.EquippedWeapon != null
            && ScoundrelRules.CanUseWeapon(cardModel.MonsterValue, _engine.WeaponFloor);

        _engine.TakeCard(cardModel, useWeapon, activateCard);

        // Visual side-effects per card type
        switch (cardModel.Suit)
        {
            case Suit.Clubs:
            case Suit.Spades:
                AudioManager.PlayPunch();
                DecrementSuit(cardModel);
                if (willUseWeapon)
                    AddSlainBadge(_godotCards[oldWeapon!.Name], cardModel);
                MoveToDiscard(card);
                break;

            case Suit.Hearts:
                if (activateCard && !potionUsedBefore)
                    AudioManager.PlayBubbles();
                else if (!activateCard)
                    AudioManager.PlayPotionDiscard();
                if (activateCard && !potionWastedBefore && _engine.PotionWastedThisRoom)
                    ShowBriefMessage("Potion wasted! (one per room)");
                DecrementSuit(cardModel);
                MoveToDiscard(card);
                break;

            case Suit.Diamonds:
                if (activateCard)
                {
                    AudioManager.PlaySwordDrawn();
                    if (oldWeapon != null)
                    {
                        DecrementSuit(oldWeapon);
                        ClearSlainBadges(_godotCards[oldWeapon.Name]);
                        MoveToDiscard(_godotCards[oldWeapon.Name]);
                    }
                    ResetCardScale(card);
                    card.Set("tooltip_text", "");
                    _weaponSlot.Call("move_cards", new Array { card }, -1, false);
                }
                else
                {
                    AudioManager.PlayWeaponDiscard();
                    DecrementSuit(cardModel);
                    MoveToDiscard(card);
                }
                break;
        }

        if (_engine.GameOver) { ShowGameOver(); UpdateUI(); return; }
        if (_engine.Won)      { ShowWin();      UpdateUI(); return; }

        // Sync any new room cards the engine may have dealt (auto-advance).
        SyncRoomToGodot();
        UpdateUI();
    }

    // ── Drag zone highlights + labels ────────────────────────────────────
    private void OnCardDragStarted(GodotObject card)
    {
        var info = card.Get("card_info").AsGodotDictionary();
        var suit = info["suit"].AsString();

        switch (suit)
        {
            case "clubs":
            case "spades":
            {
                int rank         = info["rank"].AsInt32();
                int monsterValue = rank == 1 ? 14 : rank;
                bool canUseWeapon = _engine.EquippedWeapon != null
                    && ScoundrelRules.CanUseWeapon(monsterValue, _engine.WeaponFloor);

                _leftHighlight.Visible  = canUseWeapon;
                _leftLabel.Text         = "Fight (Weapon)";
                _leftLabel.Visible      = canUseWeapon;
                _rightHighlight.Visible = true;
                _rightLabel.Text        = "Fight (Fists)";
                _rightLabel.Visible     = true;
                break;
            }
            case "hearts":
            {
                bool canDrink = !_engine.PotionUsedThisRoom;
                _leftHighlight.Visible  = canDrink;
                _leftLabel.Text         = "Drink";
                _leftLabel.Visible      = canDrink;
                _rightHighlight.Visible = true;
                _rightLabel.Text        = "Discard";
                _rightLabel.Visible     = true;
                break;
            }
            case "diamonds":
                _leftHighlight.Visible  = true;
                _leftLabel.Text         = "Equip";
                _leftLabel.Visible      = true;
                _rightHighlight.Visible = true;
                _rightLabel.Text        = "Discard";
                _rightLabel.Visible     = true;
                break;
        }
    }

    private void OnCardDragEnded()
    {
        _leftHighlight.Visible  = false;
        _rightHighlight.Visible = false;
        _leftLabel.Visible      = false;
        _rightLabel.Visible     = false;
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
            godotCard.Set("tooltip_text", "");
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
        ResizeHelpDialog();
        _helpDialog.PopupCentered();
    }



    private void ShowGameOver()
    {
        _statusLabel.Text    = "YOU DIED";
        _flavorLabel.Text    = "The monsters overrun the dungeon.";
        _flavorLabel.Visible = true;
        StartBounceAnimation(isGameOver: true);
    }

    private void ShowWin()
    {
        _statusLabel.Text = "YOU WIN!";
        _flavorLabel.Text = "The loot is yours!";
        _flavorLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.3f));
        _flavorLabel.Visible = true;
        StartBounceAnimation(isGameOver: false);
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
        card.Set("tooltip_text", "");
        _discardPile.Call("move_cards", new Array { card }, -1, false);
    }

    private void AddSlainBadge(GodotObject weaponCard, CardModel monster)
    {
        var weaponNode = (Node)weaponCard;
        weaponNode.AddChild(CreateBadgeControl(monster.Rank));

        var badges = SlainBadges(weaponNode);
        int count = badges.Count;
        float step = count <= 1 ? BadgeNaturalStep
                    : Mathf.Min(BadgeNaturalStep, (CardW - BadgeLayoutWidth) / (count - 1));
        float startX = (CardW - ((count - 1) * step + BadgeLayoutWidth)) / 2f;
        float y = CardH - BadgeLayoutHeight / 3f;
        for (int i = 0; i < count; i++)
            badges[i].Position = new Vector2(startX + i * step, y);
    }

    private static Control CreateBadgeControl(int rank)
    {
        string text = rank switch { 1 => "A", 11 => "J", 12 => "Q", 13 => "K", _ => rank.ToString() };

        var badge = new Control();
        badge.Name = "slain_badge";
        badge.Size = new Vector2(BadgeVisualWidth, BadgeVisualHeight);
        badge.MouseFilter = Control.MouseFilterEnum.Ignore;
        badge.ZIndex = 1;
        badge.AddToGroup("slain_badge");

        var bg = new ColorRect();
        bg.Color = new Color(0.08f, 0.08f, 0.08f, 0.88f);
        bg.AnchorRight = 1f; bg.AnchorBottom = 1f;
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        badge.AddChild(bg);

        var label = new Label();
        label.Text = text;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AnchorRight = 1f; label.AnchorBottom = 1f;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        badge.AddChild(label);

        return badge;
    }

    private void ClearSlainBadges(GodotObject weaponCard)
    {
        foreach (var badge in SlainBadges((Node)weaponCard))
        {
            badge.Visible = false;
            badge.QueueFree();
        }
    }

    private static SysCollections.List<Control> SlainBadges(Node weaponNode)
        => weaponNode.GetChildren()
            .Where(n => n.IsInGroup("slain_badge"))
            .Cast<Control>()
            .ToList();

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

    private void UpdateCardTooltips()
    {
        foreach (var cardModel in _engine.Room)
        {
            if (!_godotCards.TryGetValue(cardModel.Name, out var godotCard)) continue;
            godotCard.Set("tooltip_text", ScoundrelRules.TooltipFor(
                cardModel,
                _engine.EquippedWeapon,
                _engine.WeaponFloor,
                _engine.PotionUsedThisRoom,
                _engine.Health));
        }
    }

    private void UpdateUI()
    {
        _healthLabel.Text = $"HP: {_engine.Health} / {ScoundrelRules.MaxHealth}";
        _healthDie.SetHealth(_engine.Health, ScoundrelRules.MaxHealth);

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
        _deckPile.Set("tooltip_text",     $"Deck: {deckCount} card{(deckCount == 1 ? "" : "s")}");
        _discardPile.Set("tooltip_text",  $"Discard: {discardCount} card{(discardCount == 1 ? "" : "s")}");

        _clubsLabel.Text    = $"♣  {_inPlayClubs}";
        _spadesLabel.Text   = $"♠  {_inPlaySpades}";
        _heartsLabel.Text   = $"♥  {_inPlayHearts}";
        _diamondsLabel.Text = $"♦  {_inPlayDiamonds}";

        UpdateCardTooltips();
    }

    // ── Utilities ─────────────────────────────────────────────────────────
    private static Label AddZoneLabel(Node parent, float anchorLeft, float anchorRight)
    {
        var label = new Label
        {
            AnchorLeft = anchorLeft,
            AnchorRight = anchorRight,
            AnchorBottom = 1.0f,
            GrowVertical = Control.GrowDirection.Both,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", ZoneLabelFontSize);
        label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.9f));
        label.MouseFilter         = Control.MouseFilterEnum.Ignore;
        label.Visible             = false;
        parent.AddChild(label);
        return label;
    }

    private static ColorRect AddZoneHighlight(Node parent, float anchorLeft, float anchorRight, Color color)
    {
        var rect = new ColorRect();
        rect.AnchorLeft   = anchorLeft;
        rect.AnchorRight  = anchorRight;
        rect.AnchorBottom = 1.0f;
        rect.GrowVertical = Control.GrowDirection.Both;
        rect.Color        = color;
        rect.MouseFilter  = Control.MouseFilterEnum.Ignore;
        rect.Visible      = false;
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
