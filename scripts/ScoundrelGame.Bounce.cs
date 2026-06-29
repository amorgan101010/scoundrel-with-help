using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SysCollections = System.Collections.Generic;

/// <summary>
/// Partial class: Game-over/win bounce animation with physics.
/// Handles the end-game visual spectacle where cards bounce around the viewport
/// and gradually settle. The animation is responsive to viewport size changes.
/// </summary>
public partial class ScoundrelGame : Node
{
    // ── Bounce animation state ────────────────────────────────────────────
    // Cards bounce within the visible viewport and respect its bounds dynamically.
    private CanvasLayer? _bounceLayer;
    private readonly SysCollections.List<(TextureRect ghost, Vector2 velocity)> _bounceState = new();
    private bool _bounceActive;
    private int _bounceTotal;

    public bool BounceActive    => _bounceActive;
    public int  BounceCardCount => _bounceTotal;

    // Bounce animation tuning constants
    private const float BounceMinSpeed   = 120f;      // Minimum card velocity (px/s)
    private const float BounceMaxSpeed   = 340f;      // Maximum card velocity (px/s)
    private const float BounceDealStep   = 0.45f;     // Delay between card reveals (s)
    private const float BounceDealSpeed  = 800f;      // Speed of initial deal slide (px/s)
    private const float BouncePitchMin   = 0.8f;      // Min pitch for end-game SFX
    private const float BouncePitchRange = 0.4f;      // Pitch variation range

    /// <summary>
    /// Initiates the game-over or win bounce animation.
    /// Selects appropriate cards (monsters for game-over, potions/weapons for win),
    /// creates ghost TextureRects, and staggered-deals them into the scene.
    /// </summary>
    private void StartBounceAnimation(bool isGameOver)
    {
        _bounceLayer = new CanvasLayer();
        _bounceLayer.Layer = LayerBounce;
        AddChild(_bounceLayer);

        var rng = new System.Random();
        var vpSize  = GetViewport().GetVisibleRect().Size;
        var deckPos = (Vector2)_deckPile.Get("global_position");

        // Select cards to animate: monsters (game over) or potions/weapons (win)
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

            // Create a ghost TextureRect that bounces around the viewport
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

            // Random target position and velocity
            var targetPos = new Vector2(
                (float)(rng.NextDouble() * (vpSize.X - CardW)),
                (float)(rng.NextDouble() * (vpSize.Y - CardH)));
            float angle = (float)(rng.NextDouble() * System.Math.PI * 2);
            float speed = BounceMinSpeed + (float)(rng.NextDouble() * (BounceMaxSpeed - BounceMinSpeed));
            var vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            // Staggered deal: show ghost after delay, slide to random spot, then bounce
            var timer = GetTree().CreateTimer(BounceDealStep * i);
            timer.Timeout += () => {
                if (!GodotObject.IsInstanceValid(ghost)) return;
                ghost.Visible = true;
                AudioManager.EndOfGame(BouncePitchMin, BouncePitchRange);
                var tween = CreateTween();
                tween.TweenProperty(ghost, "position", targetPos, deckPos.DistanceTo(targetPos) / BounceDealSpeed);
                tween.TweenCallback(Callable.From(() => {
                    if (!GodotObject.IsInstanceValid(ghost)) return;
                    _bounceState.Add((ghost, vel));
                }));
            };
        }

        _bounceActive = true;
    }

    /// <summary>
    /// Bounce physics simulation. Cards bounce off viewport edges and slide
    /// within the visible area. Called every frame while _bounceActive is true.
    /// Viewport size is queried dynamically so bouncing adapts to resizes.
    /// </summary>
    public override void _Process(double delta)
    {
        if (!_bounceActive) return;

        var vpSize = GetViewport().GetVisibleRect().Size;

        for (int i = 0; i < _bounceState.Count; i++)
        {
            var (ghost, vel) = _bounceState[i];
            var pos = ghost.Position + vel * (float)delta;

            // Bounce off left/right edges
            if (pos.X < 0)                      { vel.X =  Mathf.Abs(vel.X); pos.X = 0; }
            if (pos.X + CardW > vpSize.X)       { vel.X = -Mathf.Abs(vel.X); pos.X = vpSize.X - CardW; }

            // Bounce off top/bottom edges
            if (pos.Y < 0)                      { vel.Y =  Mathf.Abs(vel.Y); pos.Y = 0; }
            if (pos.Y + CardH > vpSize.Y)       { vel.Y = -Mathf.Abs(vel.Y); pos.Y = vpSize.Y - CardH; }

            ghost.Position  = pos;
            _bounceState[i] = (ghost, vel);
        }
    }
}
