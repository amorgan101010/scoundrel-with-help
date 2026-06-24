using Godot;
using Godot.Collections;

/// <summary>
/// Central game controller for Scoundrel solitaire.
/// Owns all mutable game state; the GDScript card-framework handles visuals.
/// Attached to the root Game node in Game.tscn.
/// </summary>
public partial class ScoundrelGame : Node
{
    // ── Node references (resolved in _Ready via GetNode) ──────────────────
    private Node _cardManager = null!;
    private Node _deckPile = null!;
    private Node _discardPile = null!;
    private Node _weaponSlot = null!;
    private Node _roomContainer = null!;
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

    // ── Game state ────────────────────────────────────────────────────────
    private int _health = ScoundrelRules.StartHealth;

    // ── Suit tracking (cards still in play = deck + room + weapon slot) ───
    private int _inPlayClubs;
    private int _inPlaySpades;
    private int _inPlayHearts;
    private int _inPlayDiamonds;

    private CardData? _equippedWeapon;
    // Weapon degrades: can only hit a monster WEAKER than the last one fought with this weapon.
    private int _weaponFloor = int.MaxValue;

    private bool _potionUsedThisRoom;
    private bool _ranLastRoom;
    private int _cardsTakenThisRoom;
    private bool _gameOver;

    // ── Cached factory reference ──────────────────────────────────────────
    private GodotObject _cardFactory = null!;

    // ── Card names (all 44 in deck order before shuffle) ─────────────────
    private static readonly string[] Ranks =
        { "ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "jack", "queen", "king" };
    private static readonly string[] MonsterSuits  = { "clubs", "spades" };
    private static readonly string[] RedRanks      = { "2", "3", "4", "5", "6", "7", "8", "9", "10" };
    private static readonly string[] RedSuits      = { "hearts", "diamonds" };

    // ── Godot lifecycle ───────────────────────────────────────────────────
    public override void _Ready()
    {
        _cardManager    = GetNode<Node>("CardManager");
        _deckPile       = GetNode<Node>("UI/DeckPile");
        _discardPile    = GetNode<Node>("UI/DiscardPile");
        _weaponSlot     = GetNode<Node>("UI/WeaponSlot");
        _roomContainer  = GetNode<Node>("UI/RoomContainer");
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

        _roomContainer.Connect("card_selected", Callable.From<GodotObject>(OnCardSelected));
        _runButton.Connect("pressed", Callable.From(OnRunPressed));
        _nextRoomButton.Connect("pressed", Callable.From(OnNextRoomPressed));
        _retryButton.Connect("pressed", Callable.From(OnRetryPressed));
        _helpButton.Connect("pressed", Callable.From(OnHelpPressed));

        _nextRoomButton.Visible = false;

        StartGame();
        UpdateUI();
    }

    // ── Setup ─────────────────────────────────────────────────────────────
    private void StartGame()
    {
        _health = ScoundrelRules.MaxHealth;
        _equippedWeapon = null;
        _weaponFloor = int.MaxValue;
        _potionUsedThisRoom = false;
        _ranLastRoom = false;
        _cardsTakenThisRoom = 0;
        _gameOver = false;
        _statusLabel.Text = "";
        _nextRoomButton.Visible = false;
        _runButton.Disabled = false;

        _inPlayClubs    = 13;
        _inPlaySpades   = 13;
        _inPlayHearts   = 9;
        _inPlayDiamonds = 9;

        _roomContainer.Call("clear_cards");
        _deckPile.Call("clear_cards");
        _discardPile.Call("clear_cards");
        _weaponSlot.Call("clear_cards");

        foreach (var suit in MonsterSuits)
            foreach (var rank in Ranks)
                _cardFactory.Call("create_card", $"{rank}_{suit}", _deckPile);

        foreach (var suit in RedSuits)
            foreach (var rank in RedRanks)
                _cardFactory.Call("create_card", $"{rank}_{suit}", _deckPile);

        _deckPile.Call("shuffle");
        DealRoom();
    }

    // ── Room management ───────────────────────────────────────────────────
    private void DealRoom()
    {
        _potionUsedThisRoom = false;
        _cardsTakenThisRoom = 0;
        _nextRoomButton.Visible = false;
        ResetRoomCardTints();

        int alreadyInRoom = (int)_roomContainer.Call("get_card_count");
        int needed = 4 - alreadyInRoom;

        for (int i = 0; i < needed; i++)
        {
            var top = (Array)_deckPile.Call("get_top_cards", 1);
            if (top.Count == 0) break;
            var card = top[0].AsGodotObject();
            _roomContainer.Call("move_cards", new Array { card }, -1, false);
        }

        if ((int)_roomContainer.Call("get_card_count") == 0)
            Win();
    }

    private void AdvanceRoom()
    {
        _ranLastRoom = false;
        DealRoom();
        UpdateUI();
    }

    // ── Card selection (fired by RoomContainer on mouse press) ────────────
    private void OnCardSelected(GodotObject card)
    {
        if (_gameOver) return;

        var data = CardData.FromGodotCard(card);

        switch (data.Suit)
        {
            case Suit.Clubs:
            case Suit.Spades:
                ApplyMonsterDamage(data);
                DecrementSuit(data);
                MoveToDiscard(card);
                break;

            case Suit.Hearts:
                if (!_potionUsedThisRoom)
                {
                    _health = ScoundrelRules.Heal(_health, data.Rank);
                    _potionUsedThisRoom = true;
                    TintRemainingPotions();
                }
                else
                {
                    ShowBriefMessage("Potion wasted! (one per room)");
                }
                DecrementSuit(data);
                MoveToDiscard(card);
                break;

            case Suit.Diamonds:
                EquipWeapon(card, data);
                break;
        }

        _cardsTakenThisRoom++;
        CheckRoomProgress();
        UpdateUI();
    }

    private void ApplyMonsterDamage(CardData data)
    {
        int damage = data.MonsterValue;

        if (_equippedWeapon != null && ScoundrelRules.CanUseWeapon(data.MonsterValue, _weaponFloor))
        {
            damage = ScoundrelRules.CalcDamage(data.MonsterValue, _equippedWeapon.WeaponValue);
            _weaponFloor = ScoundrelRules.NextWeaponFloor(data.MonsterValue);
        }

        _health = System.Math.Max(0, _health - damage);
    }

    private void EquipWeapon(GodotObject card, CardData data)
    {
        // Move old weapon to discard
        if (_equippedWeapon != null)
        {
            DecrementSuit(_equippedWeapon);
            var old = (Array)_weaponSlot.Call("get_top_cards", 1);
            if (old.Count > 0)
                MoveToDiscard(old[0].AsGodotObject());
        }

        _equippedWeapon = data;
        _weaponFloor = int.MaxValue;

        ResetCardScale(card);
        _weaponSlot.Call("move_cards", new Array { card }, -1, false);
    }

    private void CheckRoomProgress()
    {
        if (_health <= 0)
        {
            GameOver();
            return;
        }

        int left = (int)_roomContainer.Call("get_card_count");

        if (left == 0)
        {
            // All cards taken — advance immediately
            int deckLeft = (int)_deckPile.Call("get_card_count");
            if (deckLeft == 0)
                Win();
            else
                AdvanceRoom();
        }
        else if (_cardsTakenThisRoom >= 3)
        {
            // Player may end room now (1 card carries over) or take the last card
            _nextRoomButton.Visible = true;
        }
    }

    // ── Buttons ───────────────────────────────────────────────────────────
    private void OnNextRoomPressed()
    {
        if (_gameOver) return;
        AdvanceRoom();
    }

    private void OnRunPressed()
    {
        if (_gameOver || _ranLastRoom) return;

        var runCards = (Array)_roomContainer.Call("get_all_cards");
        var monsters = new Array();
        var others = new Array();

        foreach (var cardVariant in runCards)
        {
            var c = cardVariant.AsGodotObject();
            ResetCardScale(c);
            var data = CardData.FromGodotCard(c);
            if (data.Suit == Suit.Clubs || data.Suit == Suit.Spades)
                monsters.Add(c);
            else
                others.Add(c);
        }

        // Potions and weapons settle to the bottom of the deck
        foreach (var cardVariant in others)
        {
            var c = cardVariant.AsGodotObject();
            c.Set("modulate", new Color(1f, 1f, 1f));
            c.Set("show_front", false);
            c.Set("can_be_interacted_with", false);
            _deckPile.Call("move_cards", new Array { c }, 0, false);
        }

        // Monsters wander into random positions in the deck
        int deckSize = (int)_deckPile.Call("get_card_count");
        foreach (var cardVariant in monsters)
        {
            var c = cardVariant.AsGodotObject();
            c.Set("modulate", new Color(1f, 1f, 1f));
            c.Set("show_front", false);
            c.Set("can_be_interacted_with", false);
            int insertAt = (int)GD.RandRange(0L, (long)deckSize);
            _deckPile.Call("move_cards", new Array { c }, insertAt, false);
            deckSize++;
        }

        _ranLastRoom = true;
        DealRoom();
        UpdateUI();
    }

    private void OnRetryPressed()
    {
        StartGame();
        UpdateUI();
    }

    private void OnHelpPressed()
    {
        _helpDialog.PopupCentered();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private void DecrementSuit(CardData data)
    {
        switch (data.Suit)
        {
            case Suit.Clubs:    _inPlayClubs--;    break;
            case Suit.Spades:   _inPlaySpades--;   break;
            case Suit.Hearts:   _inPlayHearts--;   break;
            case Suit.Diamonds: _inPlayDiamonds--; break;
        }
    }

    private void TintRemainingPotions()
    {
        var dimmed = new Color(0.55f, 0.55f, 0.55f);
        foreach (var cardVar in (Array)_roomContainer.Call("get_all_cards"))
        {
            var c = cardVar.AsGodotObject();
            var info = c.Get("card_info").AsGodotDictionary();
            if (info["suit"].AsString() == "hearts")
                c.Set("modulate", dimmed);
        }
    }

    private void ResetRoomCardTints()
    {
        foreach (var cardVar in (Array)_roomContainer.Call("get_all_cards"))
            cardVar.AsGodotObject().Set("modulate", new Color(1f, 1f, 1f));
    }

    private void MoveToDiscard(GodotObject card)
    {
        ResetCardScale(card);
        card.Set("modulate", new Color(1f, 1f, 1f));
        _discardPile.Call("move_cards", new Array { card }, -1, false);
    }

    private static void ResetCardScale(GodotObject card)
    {
        card.Set("scale", new Vector2(1f, 1f));
    }

    private void ShowBriefMessage(string text)
    {
        _statusLabel.Text = text;
        var timer = GetTree().CreateTimer(2.0);
        timer.Timeout += () => { if (_statusLabel.Text == text) _statusLabel.Text = ""; };
    }

    private void UpdateUI()
    {
        _healthLabel.Text = $"HP: {_health} / {ScoundrelRules.MaxHealth}";

        if (_equippedWeapon != null)
        {
            string floor = _weaponFloor == int.MaxValue ? "any" : $"< {_weaponFloor}";
            _weaponLabel.Text = $"Weapon: {_equippedWeapon.CardName}  (next: {floor})";
        }
        else
        {
            _weaponLabel.Text = "Weapon: none";
        }

        _runButton.Disabled = _ranLastRoom;

        int deckCount    = (int)_deckPile.Call("get_card_count");
        int discardCount = (int)_discardPile.Call("get_card_count");
        _deckLabel.Text    = $"DECK ({deckCount})";
        _discardLabel.Text = $"DISCARD ({discardCount})";

        _clubsLabel.Text    = $"♣  {_inPlayClubs}";
        _spadesLabel.Text   = $"♠  {_inPlaySpades}";
        _heartsLabel.Text   = $"♥  {_inPlayHearts}";
        _diamondsLabel.Text = $"♦  {_inPlayDiamonds}";
    }

    private void GameOver()
    {
        _gameOver = true;
        _statusLabel.Text = "YOU DIED";
        _runButton.Disabled = true;
        _nextRoomButton.Visible = false;

        // Freeze all room cards
        foreach (var card in (Array)_roomContainer.Call("get_all_cards"))
            card.AsGodotObject().Set("can_be_interacted_with", false);
    }

    private void Win()
    {
        _gameOver = true;
        _statusLabel.Text = "YOU WIN!";
        _runButton.Disabled = true;
        _nextRoomButton.Visible = false;
    }
}
