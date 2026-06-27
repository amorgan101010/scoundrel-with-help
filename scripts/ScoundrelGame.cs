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
    private Node _roomContainer = null!;
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

    // ── Bounce animation (game over / win) ───────────────────────────────────
    private CanvasLayer? _bounceLayer;
    private readonly SysCollections.List<(TextureRect ghost, Vector2 velocity)> _bounceState = new();
    private bool _bounceActive;

    public bool BounceActive    => _bounceActive;
    public int  BounceCardCount => _bounceTotal;
    private int _bounceTotal;

    // ── Sound effects ─────────────────────────────────────────────────────
    private AudioStreamPlayer _sfxPunch = null!;
    private AudioStreamPlayer _sfxBubbles = null!;
    private AudioStreamPlayer _sfxPotionDiscard = null!;
    private AudioStreamPlayer _sfxSwordDrawn = null!;
    private AudioStreamPlayer _sfxWeaponDiscard = null!;
    private AudioStreamPlayer _sfxCardDealt = null!;

    // Set by PlaySfx; read by scene tests to assert which sound last played.
    public string LastSfxPlayed { get; private set; } = "";

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
        _roomContainer  = GetNode<Node>("UI/RoomContainer");
        _leftDropZone   = GetNode<Node>("UI/LeftPanel/LeftDropZone");
        _rightDropZone  = GetNode<Node>("UI/RightPanel/RightDropZone");
        _healthLabel    = GetNode<Label>("UI/HealthLabel");
        _weaponLabel    = GetNode<Label>("UI/LeftPanel/WeaponGroup/WeaponLabel");
        _statusLabel    = GetNode<Label>("UI/StatusLabel");
        _deckLabel      = GetNode<Label>("UI/RightPanel/DeckGroup/DeckLabel");
        _discardLabel   = GetNode<Label>("UI/RightPanel/DiscardGroup/DiscardLabel");
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

        _healthDie = GetNode<HealthDie>("UI/LeftPanel/HealthDie");

        // Lift retry + help buttons and status text above the bounce layer (201).
        // HudLayer: status/flavor text (display only, game-over and in-game messages)
        var hudLayer = new CanvasLayer();
        hudLayer.Name  = "HudLayer";
        hudLayer.Layer = 202;
        AddChild(hudLayer);
        _statusLabel.Reparent(hudLayer, true);

        _flavorLabel = new Label();
        _flavorLabel.AnchorLeft          = 0.5f;
        _flavorLabel.AnchorRight         = 0.5f;
        _flavorLabel.GrowHorizontal      = Control.GrowDirection.Both;
        _flavorLabel.OffsetLeft          = -200f;
        _flavorLabel.OffsetRight         =  200f;
        _flavorLabel.OffsetTop           =  103f;
        _flavorLabel.OffsetBottom        =  130f;
        _flavorLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _flavorLabel.AddThemeFontSizeOverride("font_size", 18);
        _flavorLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.5f, 0.5f));
        _flavorLabel.MouseFilter         = Control.MouseFilterEnum.Ignore;
        _flavorLabel.Visible             = false;
        hudLayer.AddChild(_flavorLabel);

        // ButtonLayer: interactive controls — must be above HudLayer so clicks are never blocked
        var buttonLayer = new CanvasLayer();
        buttonLayer.Name  = "ButtonLayer";
        buttonLayer.Layer = 203;
        AddChild(buttonLayer);
        GetNode<HBoxContainer>("UI/TopButtonGroup").Reparent(buttonLayer, true);

        // Dedicated overlay layer above everything (UI is layer 1, cards are layer 0).
        var overlayLayer = new CanvasLayer();
        overlayLayer.Layer = 128;
        AddChild(overlayLayer);
        _leftHighlight  = AddZoneHighlight(overlayLayer, 0,    320, false, new Color(0.25f, 0.8f, 0.25f, 0.30f));
        _rightHighlight = AddZoneHighlight(overlayLayer, -300, 0,   true,  new Color(0.25f, 0.5f, 1.0f,  0.30f), anchorFromRight: true);
        _leftLabel      = AddZoneLabel(overlayLayer, 0,    320, false);
        _rightLabel     = AddZoneLabel(overlayLayer, -300, 0,   true, anchorFromRight: true);

        _sfxPunch         = CreateSfxPlayer("res://samples/punch.wav");
        _sfxBubbles       = CreateSfxPlayer("res://samples/bubbles.wav");
        _sfxBubbles.VolumeDb = 8f;
        _sfxPotionDiscard = CreateSfxPlayer("res://samples/potion_discarded.wav");
        _sfxPotionDiscard.VolumeDb = -7f;
        _sfxSwordDrawn    = CreateSfxPlayer("res://samples/sword_drawn.wav");
        _sfxWeaponDiscard = CreateSfxPlayer("res://samples/weapon_discarded.wav");
        _sfxCardDealt     = CreateSfxPlayer("res://samples/card_dealt.wav");

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
            PlaySfx(_sfxCardDealt, "card_dealt");

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
                PlaySfx(_sfxPunch, "punch");
                DecrementSuit(cardModel);
                if (willUseWeapon)
                    AddSlainBadge(_godotCards[oldWeapon!.Name], cardModel);
                MoveToDiscard(card);
                break;

            case Suit.Hearts:
                if (activateCard && !potionUsedBefore)
                    PlaySfx(_sfxBubbles, "bubbles");
                else if (!activateCard)
                    PlaySfx(_sfxPotionDiscard, "potion_discarded");
                if (activateCard && !potionWastedBefore && _engine.PotionWastedThisRoom)
                    ShowBriefMessage("Potion wasted! (one per room)");
                DecrementSuit(cardModel);
                MoveToDiscard(card);
                break;

            case Suit.Diamonds:
                if (activateCard)
                {
                    PlaySfx(_sfxSwordDrawn, "sword_drawn");
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
                    PlaySfx(_sfxWeaponDiscard, "weapon_discarded");
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
        _helpDialog.PopupCentered();
    }

    // ── End states ────────────────────────────────────────────────────────
    private void ShowGameOver()
    {
        _statusLabel.Text    = "YOU DIED";
        _flavorLabel.Text    = "The monsters overrun the dungeon.";
        _flavorLabel.Visible = true;
        foreach (var cardModel in _engine.Room)
            _godotCards[cardModel.Name].Set("can_be_interacted_with", false);
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

    // ── Bounce animation ──────────────────────────────────────────────────
    private void StartBounceAnimation(bool isGameOver)
    {
        _bounceLayer = new CanvasLayer();
        _bounceLayer.Layer = 201;
        AddChild(_bounceLayer);

        var rng = new System.Random();
        const float MinSpeed   = 120f, MaxSpeed = 340f;
        const float CardW      = 150f, CardH = 210f;
        const float DealStep   = 0.45f; // seconds between each card being dealt
        const float DealSpeed  = 1200f; // px/s for the deal slide

        var vpSize  = GetViewport().GetVisibleRect().Size;
        var deckPos = (Vector2)_deckPile.Get("global_position");

        var candidates = _godotCards.Values.Where(c => {
            var suit = c.Get("card_info").AsGodotDictionary()["suit"].AsString();
            return isGameOver
                ? (suit == "clubs" || suit == "spades")
                : (suit == "hearts" || suit == "diamonds");
        }).ToList();

        for (int i = 0; i < candidates.Count; i++)
        {
            var godotCard = candidates[i];
            godotCard.Set("visible", false);

            var info    = godotCard.Get("card_info").AsGodotDictionary();
            var texture = GD.Load<Texture2D>($"res://card_assets/{info["front_image"].AsString()}");
            if (texture == null) continue;

            var ghost = new TextureRect();
            ghost.Texture           = texture;
            ghost.CustomMinimumSize = new Vector2(CardW, CardH);
            ghost.ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize;
            ghost.StretchMode       = TextureRect.StretchModeEnum.Scale;
            ghost.MouseFilter       = Control.MouseFilterEnum.Ignore;
            ghost.Size              = new Vector2(CardW, CardH);
            ghost.Position          = deckPos;
            ghost.Visible           = false;
            _bounceLayer.AddChild(ghost);
            _bounceTotal++;

            var targetPos = new Vector2(
                (float)(rng.NextDouble() * (vpSize.X - CardW)),
                (float)(rng.NextDouble() * (vpSize.Y - CardH)));
            float angle = (float)(rng.NextDouble() * System.Math.PI * 2);
            float speed = MinSpeed + (float)(rng.NextDouble() * (MaxSpeed - MinSpeed));
            var vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            // Staggered deal: show ghost after delay, slide to random spot, then bounce.
            var timer = GetTree().CreateTimer(DealStep * i);
            timer.Timeout += () => {
                if (!GodotObject.IsInstanceValid(ghost)) return;
                ghost.Visible = true;
                var sfx = new AudioStreamPlayer();
                sfx.Stream     = _sfxCardDealt.Stream;
                sfx.PitchScale = 0.84f + (float)new System.Random().NextDouble() * 0.32f;
                AddChild(sfx);
                sfx.Connect("finished", Callable.From(sfx.QueueFree));
                sfx.Play();
                var tween = CreateTween();
                tween.TweenProperty(ghost, "position", targetPos, deckPos.DistanceTo(targetPos) / DealSpeed);
                tween.TweenCallback(Callable.From(() => {
                    if (!GodotObject.IsInstanceValid(ghost)) return;
                    _bounceState.Add((ghost, vel));
                }));
            };
        }

        _bounceActive = true;
    }

    public override void _Process(double delta)
    {
        if (!_bounceActive) return;

        var vpSize = GetViewport().GetVisibleRect().Size;
        const float CardW = 150f, CardH = 210f;

        for (int i = 0; i < _bounceState.Count; i++)
        {
            var (ghost, vel) = _bounceState[i];
            var pos = ghost.Position + vel * (float)delta;

            if (pos.X < 0)               { vel.X =  Mathf.Abs(vel.X); pos.X = 0; }
            if (pos.X + CardW > vpSize.X) { vel.X = -Mathf.Abs(vel.X); pos.X = vpSize.X - CardW; }
            if (pos.Y < 0)               { vel.Y =  Mathf.Abs(vel.Y); pos.Y = 0; }
            if (pos.Y + CardH > vpSize.Y) { vel.Y = -Mathf.Abs(vel.Y); pos.Y = vpSize.Y - CardH; }

            ghost.Position  = pos;
            _bounceState[i] = (ghost, vel);
        }
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
        const float BadgeW = 30f, BadgeH = 44f, NaturalStep = 35f;
        const float CardW = 150f, CardH = 210f;

        var weaponNode = (Node)weaponCard;
        weaponNode.AddChild(CreateBadgeControl(monster.Rank));

        var badges = SlainBadges(weaponNode);
        int count = badges.Count;
        float step = count <= 1 ? NaturalStep
                                : Mathf.Min(NaturalStep, (CardW - BadgeW) / (count - 1));
        float startX = (CardW - ((count - 1) * step + BadgeW)) / 2f;
        float y = CardH - BadgeH / 3f;
        for (int i = 0; i < count; i++)
            badges[i].Position = new Vector2(startX + i * step, y);
    }

    private static Control CreateBadgeControl(int rank)
    {
        string text = rank switch { 1 => "A", 11 => "J", 12 => "Q", 13 => "K", _ => rank.ToString() };

        var badge = new Control();
        badge.Name = "slain_badge";
        badge.Size = new Vector2(30f, 44f);
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
        _deckLabel.Text    = $"DECK ({deckCount})";
        _discardLabel.Text = $"DISCARD ({discardCount})";

        _clubsLabel.Text    = $"♣  {_inPlayClubs}";
        _spadesLabel.Text   = $"♠  {_inPlaySpades}";
        _heartsLabel.Text   = $"♥  {_inPlayHearts}";
        _diamondsLabel.Text = $"♦  {_inPlayDiamonds}";

        UpdateCardTooltips();
    }

    // ── Utilities ─────────────────────────────────────────────────────────
    private AudioStreamPlayer CreateSfxPlayer(string path)
    {
        var player = new AudioStreamPlayer();
        player.Stream = GD.Load<AudioStream>(path);
        AddChild(player);
        return player;
    }

    private void PlaySfx(AudioStreamPlayer player, string name)
    {
        LastSfxPlayed = name;
        player.Play();
    }

    private static Label AddZoneLabel(Node parent, float left, float right, bool stretchRight, bool anchorFromRight = false)
    {
        var label = new Label();
        label.AnchorBottom        = 1.0f;
        label.GrowVertical        = Control.GrowDirection.Both;
        label.OffsetLeft          = left;
        if (stretchRight) {
            if (anchorFromRight) label.AnchorLeft = 1.0f;
            label.AnchorRight    = 1.0f;
            label.GrowHorizontal = Control.GrowDirection.Both;
        }
        else { label.OffsetRight = right; }
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment   = VerticalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 28);
        label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.9f));
        label.MouseFilter         = Control.MouseFilterEnum.Ignore;
        label.Visible             = false;
        parent.AddChild(label);
        return label;
    }

    private static ColorRect AddZoneHighlight(Node parent, float left, float right, bool stretchRight, Color color, bool anchorFromRight = false)
    {
        var rect = new ColorRect();
        rect.AnchorBottom = 1.0f;
        rect.GrowVertical = Control.GrowDirection.Both;
        rect.OffsetLeft   = left;
        if (stretchRight) {
            if (anchorFromRight) rect.AnchorLeft = 1.0f;
            rect.AnchorRight    = 1.0f;
            rect.GrowHorizontal = Control.GrowDirection.Both;
        }
        else { rect.OffsetRight = right; }
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
