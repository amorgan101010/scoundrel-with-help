using Godot;

/// <summary>
/// Simple flat line-art icon for a room card overlay, drawn natively via _Draw()
/// rather than pulling in new SVG/PNG image assets (per the coordinator's steer:
/// new UI chrome for this redesign should come from StyleBoxFlat/ColorRect/_Draw()
/// primitives, not additional image files). One abstract silhouette per CardKind —
/// monsters/weapons/potions/Blacksmith/Merchant/Jokers carry no individual art in
/// this codebase (CardModel only has Suit+Rank), so these read as a generic
/// class-of-thing icon rather than a specific illustration, matching the mockup's
/// "simple line-art icon, centered, generous whitespace" treatment. Color is passed
/// in by RoomCardOverlay (the same accent used for that card's banner), so the icon
/// always matches its banner family.
/// </summary>
public partial class RoomCardIcon : Control
{
    public CardKind Kind;
    public Color LineColor = Colors.White;

    private const float LineWidth = 3f;

    public override void _Draw()
    {
        var c = Size / 2f;
        float s = Mathf.Min(Size.X, Size.Y) * 0.42f; // icon half-extent

        switch (Kind)
        {
            case CardKind.Monster:
                DrawClawMarks(c, s);
                break;
            case CardKind.Weapon:
                DrawSword(c, s);
                break;
            case CardKind.Potion:
                DrawPotionBottle(c, s);
                break;
            case CardKind.Blacksmith:
                DrawDagger(c, s);
                break;
            case CardKind.Merchant:
                DrawCoin(c, s);
                break;
            case CardKind.PotionJoker:
            case CardKind.WeaponJoker:
                DrawJesterMask(c, s);
                break;
        }
    }

    // Three parallel diagonal slashes — a generic "monster attack" shorthand, since
    // individual monster cards carry no unique identity beyond Suit+Rank to draw.
    private void DrawClawMarks(Vector2 c, float s)
    {
        for (int i = -1; i <= 1; i++)
        {
            var offset = new Vector2(0, i * s * 0.5f);
            DrawLine(c + new Vector2(-s * 0.7f, -s * 0.35f) + offset,
                     c + new Vector2(s * 0.7f, s * 0.35f) + offset,
                     LineColor, LineWidth, true);
        }
    }

    private void DrawSword(Vector2 c, float s)
    {
        DrawLine(c + new Vector2(0, -s), c + new Vector2(0, s * 0.35f), LineColor, LineWidth, true); // blade
        DrawLine(c + new Vector2(-s * 0.45f, s * 0.35f), c + new Vector2(s * 0.45f, s * 0.35f), LineColor, LineWidth, true); // crossguard
        DrawLine(c + new Vector2(0, s * 0.35f), c + new Vector2(0, s * 0.75f), LineColor, LineWidth, true); // grip
        DrawCircle(c + new Vector2(0, s * 0.85f), s * 0.1f, LineColor); // pommel
    }

    private void DrawPotionBottle(Vector2 c, float s)
    {
        var neckRect = new Rect2(c + new Vector2(-s * 0.15f, -s * 0.9f), new Vector2(s * 0.3f, s * 0.35f));
        DrawRect(neckRect, LineColor, false, LineWidth, true); // neck
        DrawLine(c + new Vector2(-s * 0.22f, -s * 0.95f), c + new Vector2(s * 0.22f, -s * 0.95f), LineColor, LineWidth, true); // cork
        DrawArc(c + new Vector2(0, s * 0.15f), s * 0.6f, 0f, Mathf.Tau, 40, LineColor, LineWidth, true); // bulb
    }

    // Diagonal blade + small perpendicular guard + circular pommel — matches the
    // mockup's Blacksmith icon (a dagger silhouette angled bottom-left to top-right).
    private void DrawDagger(Vector2 c, float s)
    {
        var tip = c + new Vector2(s * 0.7f, -s * 0.7f);
        var hilt = c + new Vector2(-s * 0.5f, s * 0.5f);
        DrawLine(tip, hilt, LineColor, LineWidth, true);

        var dir = (tip - hilt).Normalized();
        var perp = new Vector2(-dir.Y, dir.X) * s * 0.18f;
        var guardCenter = hilt + dir * s * 0.28f;
        DrawLine(guardCenter - perp, guardCenter + perp, LineColor, LineWidth, true); // guard
        DrawCircle(hilt, s * 0.1f, LineColor); // pommel
    }

    private void DrawCoin(Vector2 c, float s)
    {
        DrawArc(c, s * 0.6f, 0f, Mathf.Tau, 40, LineColor, LineWidth, true); // rim
        DrawArc(c, s * 0.38f, 0f, Mathf.Tau, 32, LineColor, LineWidth * 0.7f, true); // inner ring
    }

    // Rounded jester mask: head outline, two eyes, a curved smile, and three small
    // cap points on top — shared by both PotionJoker (Red Joker) and WeaponJoker
    // (Black Joker); the two are told apart by their banner color, not the icon.
    private void DrawJesterMask(Vector2 c, float s)
    {
        var head = c + new Vector2(0, s * 0.1f);
        DrawArc(head, s * 0.55f, 0f, Mathf.Tau, 40, LineColor, LineWidth, true);
        DrawCircle(head + new Vector2(-s * 0.2f, -s * 0.05f), s * 0.06f, LineColor);
        DrawCircle(head + new Vector2(s * 0.2f, -s * 0.05f), s * 0.06f, LineColor);

        // Smile, built as a sampled curve rather than DrawArc so the "corners up,
        // middle down" orientation is unambiguous regardless of arc-angle winding.
        const int steps = 8;
        var mouth = new Vector2[steps + 1];
        float mouthY = head.Y + s * 0.22f;
        float bump = s * 0.12f;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            mouth[i] = new Vector2(head.X + Mathf.Lerp(-0.28f, 0.28f, t) * s, mouthY + bump * Mathf.Sin(Mathf.Pi * t));
        }
        DrawPolyline(mouth, LineColor, LineWidth, true);

        // Cap points.
        float capY = head.Y - s * 0.95f;
        DrawLine(head + new Vector2(-s * 0.4f, -s * 0.4f), new Vector2(head.X - s * 0.4f, capY), LineColor, LineWidth, true);
        DrawLine(head + new Vector2(0, -s * 0.55f), new Vector2(head.X, capY - s * 0.15f), LineColor, LineWidth, true);
        DrawLine(head + new Vector2(s * 0.4f, -s * 0.4f), new Vector2(head.X + s * 0.4f, capY), LineColor, LineWidth, true);
    }
}
