using Godot;
using System;
using System.Linq;
using SysCollections = System.Collections.Generic;

public sealed class ScoundrelBounceController
{
    private readonly Node _owner;
    private readonly int _layerBounce;
    private readonly Func<Vector2> _getCardSize;

    private CanvasLayer? _bounceLayer;
    private readonly SysCollections.List<(TextureRect ghost, Vector2 velocity)> _bounceState = new();
    private readonly SysCollections.List<GodotObject> _hiddenCards = new();
    private bool _bounceActive;
    private int _bounceTotal;

    private const float BounceMinSpeed = 120f;
    private const float BounceMaxSpeed = 340f;
    private const float BounceDealStep = 0.45f;
    private const float BounceDealSpeed = 800f;
    private const float BouncePitchMin = 0.8f;
    private const float BouncePitchRange = 0.4f;

    public bool BounceActive => _bounceActive;
    public int BounceCardCount => _bounceTotal;

    public ScoundrelBounceController(Node owner, int layerBounce, Func<Vector2> getCardSize)
    {
        _owner = owner;
        _layerBounce = layerBounce;
        _getCardSize = getCardSize;
    }

    public void Reset()
    {
        foreach (var card in _hiddenCards)
        {
            if (GodotObject.IsInstanceValid(card))
                card.Set("visible", true);
        }
        _hiddenCards.Clear();

        if (_bounceLayer != null)
        {
            _bounceLayer.QueueFree();
            _bounceLayer = null;
        }

        _bounceActive = false;
        _bounceState.Clear();
        _bounceTotal = 0;
    }

    public void StartBounceAnimation(bool isGameOver, Node deckPile, SysCollections.IEnumerable<GodotObject> cards, AudioManager audioManager)
    {
        Reset();

        _bounceLayer = new CanvasLayer
        {
            Layer = _layerBounce
        };
        _owner.AddChild(_bounceLayer);

        var rng = new System.Random();
        var vpSize = _owner.GetViewport().GetVisibleRect().Size;
        var deckPos = (Vector2)deckPile.Get("global_position");
        var deckPileId = deckPile.GetInstanceId();
        var cardSize = _getCardSize();
        var cardW = cardSize.X;
        var cardH = cardSize.Y;

        var candidates = cards.Where(c =>
        {
            var suit = c.Get("card_info").AsGodotDictionary()["suit"].AsString();
            return isGameOver
                ? suit == "clubs" || suit == "spades"
                : suit == "hearts" || suit == "diamonds";
        }).ToList();

        for (int i = 0; i < candidates.Count; i++)
        {
            var godotCard = candidates[i];

            // Hiding cards inside DeckPile creates uneven visible stack spacing
            // (hidden cards still consume pile offsets). Keep deck cards visible.
            var container = godotCard.Get("card_container").AsGodotObject();
            bool isDeckCard = container != null && container.GetInstanceId() == deckPileId;
            if (!isDeckCard)
            {
                godotCard.Set("visible", false);
                _hiddenCards.Add(godotCard);
            }

            var info = godotCard.Get("card_info").AsGodotDictionary();
            var texture = GD.Load<Texture2D>($"res://card_assets/{info["front_image"].AsString()}");
            if (texture == null) continue;

            var ghost = new TextureRect
            {
                Texture = texture,
                CustomMinimumSize = new Vector2(cardW, cardH),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Size = new Vector2(cardW, cardH),
                Position = deckPos,
                Visible = false
            };

            _bounceLayer.AddChild(ghost);
            _bounceTotal++;

            var targetPos = new Vector2(
                (float)(rng.NextDouble() * (vpSize.X - cardW)),
                (float)(rng.NextDouble() * (vpSize.Y - cardH)));
            float angle = (float)(rng.NextDouble() * System.Math.PI * 2);
            float speed = BounceMinSpeed + (float)(rng.NextDouble() * (BounceMaxSpeed - BounceMinSpeed));
            var vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            var timer = _owner.GetTree().CreateTimer(BounceDealStep * i);
            timer.Timeout += () =>
            {
                if (!GodotObject.IsInstanceValid(ghost)) return;
                ghost.Visible = true;
                audioManager.EndOfGame(BouncePitchMin, BouncePitchRange);
                var tween = _owner.CreateTween();
                tween.TweenProperty(ghost, "position", targetPos, deckPos.DistanceTo(targetPos) / BounceDealSpeed);
                tween.TweenCallback(Callable.From(() =>
                {
                    if (!GodotObject.IsInstanceValid(ghost)) return;
                    _bounceState.Add((ghost, vel));
                }));
            };
        }

        _bounceActive = true;
    }

    public void Process(double delta)
    {
        if (!_bounceActive) return;

        var vpSize = _owner.GetViewport().GetVisibleRect().Size;
        var cardSize = _getCardSize();
        var cardW = cardSize.X;
        var cardH = cardSize.Y;

        for (int i = 0; i < _bounceState.Count; i++)
        {
            var (ghost, vel) = _bounceState[i];
            var pos = ghost.Position + vel * (float)delta;

            if (pos.X < 0) { vel.X = Mathf.Abs(vel.X); pos.X = 0; }
            if (pos.X + cardW > vpSize.X) { vel.X = -Mathf.Abs(vel.X); pos.X = vpSize.X - cardW; }

            if (pos.Y < 0) { vel.Y = Mathf.Abs(vel.Y); pos.Y = 0; }
            if (pos.Y + cardH > vpSize.Y) { vel.Y = -Mathf.Abs(vel.Y); pos.Y = vpSize.Y - cardH; }

            ghost.Position = pos;
            _bounceState[i] = (ghost, vel);
        }
    }
}
