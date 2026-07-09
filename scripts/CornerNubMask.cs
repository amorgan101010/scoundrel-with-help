using System.Collections.Generic;
using Godot;

/// <summary>
/// Fills the tiny "nub" a square-cornered StyleBoxTexture/StyleBoxFlat leaves
/// exposed just outside a true rounded corner's arc.
///
/// A StyleBoxFlat's own corner_radius can only produce shapes where the rounded
/// bulge is the majority of a corner box and the sliver is cut away (with NO
/// geometry there at all -- not even a transparent fill, the shape simply doesn't
/// extend there) -- there is no StyleBoxFlat configuration that fills only that
/// sliver, which is what a mask actually needs. A border-stroke mask (this file's
/// predecessor technique) only draws a thin line tracing the arc and leaves the
/// nub itself uncovered, so the square banner still shows through there -- this
/// was confirmed wrong via a real in-editor screenshot after the border-only
/// version shipped (widening the border did nothing, since the nub isn't part of
/// the border's own geometry either).
///
/// This draws the nub directly as a filled polygon: the literal corner point, an
/// arc traced from one adjacent edge to the other (centered at the point inset by
/// `radius` from the corner along both axes -- the same center a true StyleBoxFlat
/// rounded corner would use), filled solid. Used by BannerGradient (behind a
/// gradient banner, filled with the page background color -- both the banner AND
/// the card's own background are rounded away in that exact spot, so the true page
/// canvas is what should show there) and ButtonGradient (same reasoning, borderless
/// pill button).
/// </summary>
public partial class CornerNubMask : Control
{
    private Color _color;
    private float _radius;
    private bool _isTop;
    private bool _isLeft;

    public static CornerNubMask Create(Vector2 position, float radius, bool isTop, bool isLeft, Color color)
    {
        var mask = new CornerNubMask
        {
            _color      = color,
            _radius     = radius,
            _isTop      = isTop,
            _isLeft     = isLeft,
            MouseFilter = MouseFilterEnum.Ignore,
            Position    = position,
            Size        = new Vector2(radius, radius),
        };
        return mask;
    }

    public override void _Draw()
    {
        var corner = new Vector2(_isLeft ? 0f : _radius, _isTop ? 0f : _radius);
        var arcCenter = new Vector2(_isLeft ? _radius : 0f, _isTop ? _radius : 0f);

        float centerAngle = (corner - arcCenter).Angle();
        float startAngle = centerAngle - Mathf.Pi / 4f;
        float endAngle = centerAngle + Mathf.Pi / 4f;

        const int segments = 12;
        var points = new List<Vector2> { corner };
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            points.Add(arcCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _radius);
        }

        DrawColoredPolygon(points.ToArray(), _color);
    }
}
