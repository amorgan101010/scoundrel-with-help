using Godot;

/// <summary>
/// Weapon panel overlay for the ui_overhaul.png "WEAPON" column: an opaque card-style
/// background, a big equipped-weapon value in the top-left, a muted "next: &lt; N"
/// constraint hint below it, a centered picture of the equipped weapon's real art
/// (card_assets/art/{name}.svg — background-free, see RoomCardOverlay.cs's class doc
/// for how that art is generated), and a bold serif name strip near the bottom
/// (reusing RoomCardContent.DisplayName — see RoomCardContent.cs). Frame chrome
/// (background/border/divider/name-strip) is native Label/Panel/ColorRect, per the
/// coordinator's steer that new UI CHROME should avoid image assets; the icon itself
/// is the deliberate exception (chunk 6) since weapons have real art. The icon's
/// texture is swapped in UpdateWeapon() rather than fixed at Build() time, since each
/// equipped weapon rank has a different picture (unlike chunk 2's RoomCardIcon, which
/// drew one fixed generic icon regardless of rank).
///
/// Added once as a child of WeaponSlot (the addon Pile control that hosts the equipped
/// weapon's actual Card node) at _Ready(), sized to the initial card size, and then only
/// refreshed in place via UpdateWeapon() every UpdateUI() tick — unlike RoomCardOverlay
/// (built fresh per room card, since a room card's Suit/Rank never change while it's in
/// the room), the equipped weapon can be swapped freely, so this overlay's text/
/// visibility need to track live state rather than being fixed at construction.
///
/// WeaponSlot's own children today are the addon's "Cards" control (the actual draggable
/// Card node lives under there) and its DropZone — CardContainer.gd never enumerates
/// get_children() for card bookkeeping (see card_container.gd), so adding this as a
/// plain sibling Control is safe and paints over the addon's generic front_image art
/// exactly like RoomCardOverlay does for room cards. MouseFilter.Ignore throughout so
/// drag-out-to-give-to-Joker still reaches the Card control beneath.
///
/// The opaque background stops short of the card's bottom edge (see BadgeSafeTopReserve)
/// rather than covering the full slot: ScoundrelGame's slain-monster badges are added as
/// children of the equipped weapon's own Card node and deliberately hang partially below
/// the visual card (RelayoutBadges positions them at CardH - BadgeLayoutHeight/3, see
/// ScoundrelGame.cs), and this overlay — being a later sibling in the WeaponSlot tree —
/// paints on top of the Card and would otherwise hide them. Leaving that strip
/// uncovered lets the real card art + badges show through underneath, which also happens
/// to mirror the mockup's own "badges as a separate row below the weapon card" layout.
///
/// WeaponLabel (the pre-existing plain-text summary) is kept fully intact and still
/// computed every UpdateUI() call — gdUnit4 scene tests assert its exact string in
/// several places — just hidden, since this overlay now shows the same information
/// visually. See ScoundrelGame.UpdateUI()/_Ready().
/// </summary>
public partial class WeaponPanelOverlay : Control
{
    private const float Padding          = 16f;
    private const float NumberTop        = 8f;
    private const float NumberHeight     = 52f;
    private const float ConstraintHeight = 22f;
    private const float ConstraintGap    = 2f;
    private const float IconTopGap       = 14f;
    private const float IconBottomGap    = 10f;
    private const float NameStripHeight  = 30f;
    private const float DividerGap       = 8f;

    // Mirrors ScoundrelGame's BadgeLayoutHeight/3 (the slain-badge row's vertical
    // offset from the card's bottom edge) — keep the two in sync if either changes,
    // same cross-file convention ScoundrelPalette's header comment documents for
    // colors shared between C# and .tscn.
    private const float BadgeSafeTopReserve = 22f;

    private Label _numberLabel      = null!;
    private Label _constraintLabel  = null!;
    private TextureRect _icon       = null!;
    private Label _nameLabel        = null!;

    public static WeaponPanelOverlay Create(Vector2 slotSize)
    {
        var overlay = new WeaponPanelOverlay
        {
            Name        = "WeaponPanelOverlay",
            Position    = Vector2.Zero,
            Size        = slotSize,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex      = 1,
        };
        overlay.Build(slotSize);
        return overlay;
    }

    /// <summary>Refreshes all state-dependent visuals; hides the whole panel when no
    /// weapon is equipped (the addon's WeaponSlot is then just an empty Pile).</summary>
    public void UpdateWeapon(CardModel? weapon, int weaponFloor, int totalAttackBonus)
    {
        Visible = weapon != null;
        if (weapon == null) return;

        _numberLabel.Text = weapon.WeaponValue.ToString();

        string floor = weaponFloor == int.MaxValue ? "any" : $"< {weaponFloor}";
        string bonusSuffix = totalAttackBonus > 0 ? $"   +{totalAttackBonus} atk" : "";
        _constraintLabel.Text = $"♦ next: {floor}{bonusSuffix}";

        _nameLabel.Text = RoomCardContent.DisplayName(weapon);
        _icon.Texture   = GD.Load<Texture2D>($"res://card_assets/art/{weapon.Name}.svg");
    }

    private void Build(Vector2 size)
    {
        float w = size.X, h = size.Y;
        float badgeSafeTop = h - BadgeSafeTopReserve;

        // Opaque background + thin border, same treatment as RoomCardOverlay's card
        // background — but stopping at badgeSafeTop rather than the full slot height
        // (see class doc comment on BadgeSafeTopReserve).
        var backgroundStyle = new StyleBoxFlat { BgColor = ScoundrelPalette.CardBackMaroon, BorderColor = ScoundrelPalette.DividerGoldBrown };
        backgroundStyle.SetBorderWidthAll(2);
        var background = new Panel { OffsetRight = w, OffsetBottom = badgeSafeTop, MouseFilter = MouseFilterEnum.Ignore };
        background.AddThemeStyleboxOverride("panel", backgroundStyle);
        AddChild(background);

        _numberLabel = new Label
        {
            OffsetLeft   = Padding,
            OffsetTop    = NumberTop,
            OffsetRight  = w - Padding,
            OffsetBottom = NumberTop + NumberHeight,
            MouseFilter  = MouseFilterEnum.Ignore,
        };
        _numberLabel.AddThemeFontOverride("font", ScoundrelPalette.DisplayBold);
        _numberLabel.AddThemeFontSizeOverride("font_size", 42);
        _numberLabel.AddThemeColorOverride("font_color", ScoundrelPalette.TitleCream);
        AddChild(_numberLabel);

        float constraintTop = NumberTop + NumberHeight + ConstraintGap;
        _constraintLabel = new Label
        {
            OffsetLeft   = Padding,
            OffsetTop    = constraintTop,
            OffsetRight  = w - Padding,
            OffsetBottom = constraintTop + ConstraintHeight,
            AutowrapMode = TextServer.AutowrapMode.Off,
            ClipText     = true,
            MouseFilter  = MouseFilterEnum.Ignore,
        };
        _constraintLabel.AddThemeFontOverride("font", ScoundrelPalette.SerifRegular);
        _constraintLabel.AddThemeFontSizeOverride("font_size", 13);
        _constraintLabel.AddThemeColorOverride("font_color", ScoundrelPalette.ConstraintRose);
        AddChild(_constraintLabel);

        // Name strip + divider anchored to badgeSafeTop (not the full slot height —
        // see BadgeSafeTopReserve); the icon then fills whatever space remains between
        // the constraint hint and the name strip.
        float dividerY = badgeSafeTop - NameStripHeight - DividerGap;
        var divider = new ColorRect
        {
            Color        = ScoundrelPalette.DividerGoldBrown,
            OffsetLeft   = Padding,
            OffsetTop    = dividerY,
            OffsetRight  = w - Padding,
            OffsetBottom = dividerY + 1f,
            MouseFilter  = MouseFilterEnum.Ignore,
        };
        AddChild(divider);

        float iconTop    = constraintTop + ConstraintHeight + IconTopGap;
        float iconBottom = dividerY - IconBottomGap;
        float iconSize   = Mathf.Max(24f, Mathf.Min(iconBottom - iconTop, w - Padding * 2f));
        float iconCenterY = (iconTop + iconBottom) / 2f;
        _icon = new TextureRect
        {
            ExpandMode   = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode  = TextureRect.StretchModeEnum.KeepAspectCentered,
            OffsetLeft   = (w - iconSize) / 2f,
            OffsetTop    = iconCenterY - iconSize / 2f,
            OffsetRight  = (w + iconSize) / 2f,
            OffsetBottom = iconCenterY + iconSize / 2f,
            MouseFilter  = MouseFilterEnum.Ignore,
        };
        AddChild(_icon);

        _nameLabel = new Label
        {
            OffsetLeft          = Padding,
            OffsetTop           = dividerY + DividerGap,
            OffsetRight         = w - Padding,
            OffsetBottom        = badgeSafeTop,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.Word,
            ClipText            = true,
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        _nameLabel.AddThemeFontOverride("font", ScoundrelPalette.DisplayBold);
        _nameLabel.AddThemeFontSizeOverride("font_size", 14);
        _nameLabel.AddThemeColorOverride("font_color", ScoundrelPalette.TitleCream);
        AddChild(_nameLabel);
    }
}
