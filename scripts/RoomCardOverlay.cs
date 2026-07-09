using Godot;

/// <summary>
/// Visual overlay for a room card: banner header + icon + description + divider +
/// footer, matching the room-card treatment in ui_overhaul.png. Built entirely from
/// native Control/Panel/Label/ColorRect nodes plus a custom-drawn RoomCardIcon — no
/// new image assets, per the coordinator's steer that new UI chrome for this redesign
/// should come from StyleBoxFlat/ColorRect/_Draw() primitives rather than SVG/PNG art
/// (the same reasoning chunk 1's ScoundrelPalette used: no Theme .tres convention
/// here, so this follows the existing inline-Color/C#-first styling pattern).
///
/// Added as a plain child Control directly on the addon's Card GodotObject — the same
/// technique ScoundrelGame's AddSlainBadge/RemoveSlainBadges/ClearSlainBadges already
/// use to put weapon slain-monster badges on top of a Card without touching
/// addons/card-framework/ (see ScoundrelGame.cs ~1143, "AddSlainBadge"). Added as the
/// last child so it paints over the addon's own front_image art (the generic per-suit
/// SVG), with MouseFilter Ignore throughout so clicks/drags still reach the Card
/// control beneath for the existing drag-drop machinery.
///
/// Built once per card — banner color/name/icon/footer never change, since a card's
/// Suit/Rank are fixed for its lifetime — and reused for as long as that card stays in
/// the room. Only UpdateDescription is called again on every
/// ScoundrelGame.SyncRoomCardOverlays() tick, since the tooltip text it mirrors
/// depends on live state (equipped weapon, weapon floor, potion-used flag, health,
/// attack bonuses).
/// </summary>
public partial class RoomCardOverlay : Control
{
    public const string GroupName = "room_card_overlay";

    private const float Padding         = 14f;
    private const float BannerHeight    = 46f;
    private const float IconHeight      = 80f;
    private const float FooterHeight    = 28f;
    private const float FooterBottomGap = 12f;
    private const float DividerGap      = 10f;
    private const float DescGap         = 8f;

    // Every card_assets/*.svg shares the same template: a ~5-6px border, the
    // illustration, then a fixed text banner starting at y=162 of a 150x210
    // viewBox (verified across monster/weapon/potion/face/ace/joker art). Cropping
    // "border to just above that banner" — rather than tightly bounding each
    // illustration, which varies in size per card kind — is robust across all of
    // them. Expressed as fractions of the actual runtime texture (not raw viewBox
    // pixels), since these SVGs import at svg/scale=4.0 (600x840), not 150x210.
    private const float ArtCropLeft   = 8f / 150f;
    private const float ArtCropRight  = 142f / 150f;
    private const float ArtCropTop    = 8f / 210f;
    private const float ArtCropBottom = 152f / 210f;

    private Label _descriptionLabel = null!;

    public static RoomCardOverlay Create(CardModel card, Vector2 cardSize, Texture2D frontImage)
    {
        var overlay = new RoomCardOverlay
        {
            Name        = "RoomCardOverlay",
            Position    = Vector2.Zero,
            Size        = cardSize,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex      = 1,
        };
        overlay.AddToGroup(GroupName);
        overlay.Build(card, cardSize, frontImage);
        return overlay;
    }

    /// <summary>Refreshes the state-dependent description body text in place.</summary>
    public void UpdateDescription(string text) => _descriptionLabel.Text = text;

    private void Build(CardModel card, Vector2 cardSize, Texture2D frontImage)
    {
        float w = cardSize.X, h = cardSize.Y;
        var family = RoomCardContent.BannerFamily(card.Kind);
        var (bannerBg, bannerFg) = BannerColors(card, family);

        // Opaque background + thin border, covering the addon's generic front_image
        // art beneath (a StyleBoxFlat panel, not a background image/9-patch).
        var backgroundStyle = new StyleBoxFlat { BgColor = ScoundrelPalette.CardBackMaroon, BorderColor = ScoundrelPalette.DividerGoldBrown };
        backgroundStyle.SetBorderWidthAll(2);
        var background = new Panel { OffsetRight = w, OffsetBottom = h, MouseFilter = MouseFilterEnum.Ignore };
        background.AddThemeStyleboxOverride("panel", backgroundStyle);
        AddChild(background);

        // Banner header bar.
        var banner = new Panel { OffsetRight = w, OffsetBottom = BannerHeight, MouseFilter = MouseFilterEnum.Ignore };
        banner.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = bannerBg });
        AddChild(banner);

        var bannerLabel = new Label
        {
            Text                = RoomCardContent.DisplayName(card),
            OffsetLeft          = Padding * 0.5f,
            OffsetRight         = w - Padding * 0.5f,
            OffsetBottom        = BannerHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.Word,
            ClipText            = true,
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        bannerLabel.AddThemeFontOverride("font", ScoundrelPalette.DisplayBold);
        bannerLabel.AddThemeFontSizeOverride("font_size", 17);
        bannerLabel.AddThemeColorOverride("font_color", bannerFg);
        AddChild(bannerLabel);

        // Icon area — the real illustration baked into this card's own front_image
        // art (card_assets/*.svg already has thematic per-card pictures: a monster
        // face, a weapon shape, etc. — not just plain suit pips), cropped to just
        // that illustration via an AtlasTexture region so the source image's own
        // border/corner-marks/rank-text banner don't show (see ArtCrop* consts).
        // Chunk 6 replaced chunk 2's hand-drawn RoomCardIcon here after the user
        // saw the invented line art next to real card art and wanted the real
        // picture instead — native-shape chrome (banner/border/divider/footer)
        // stays, this is specifically about the illustration.
        var texSize = frontImage.GetSize();
        var atlas = new AtlasTexture
        {
            Atlas = frontImage,
            Region = new Rect2(
                texSize.X * ArtCropLeft,
                texSize.Y * ArtCropTop,
                texSize.X * (ArtCropRight - ArtCropLeft),
                texSize.Y * (ArtCropBottom - ArtCropTop)),
        };
        var icon = new TextureRect
        {
            Texture      = atlas,
            ExpandMode   = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode  = TextureRect.StretchModeEnum.KeepAspectCentered,
            OffsetTop    = BannerHeight,
            OffsetRight  = w,
            OffsetBottom = BannerHeight + IconHeight,
            MouseFilter  = MouseFilterEnum.Ignore,
        };
        AddChild(icon);

        // Footer/divider geometry, computed first so the description area fills
        // whatever space remains above it.
        float footerY    = h - FooterHeight - FooterBottomGap;
        float dividerY   = footerY - DividerGap;
        float descTop    = BannerHeight + IconHeight + DescGap;
        float descBottom = dividerY - DescGap;

        _descriptionLabel = new Label
        {
            OffsetLeft        = Padding,
            OffsetTop         = descTop,
            OffsetRight       = w - Padding,
            OffsetBottom      = descBottom,
            AutowrapMode      = TextServer.AutowrapMode.Word,
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter       = MouseFilterEnum.Ignore,
        };
        _descriptionLabel.AddThemeFontOverride("font", ScoundrelPalette.SerifRegular);
        _descriptionLabel.AddThemeFontSizeOverride("font_size", 11);
        _descriptionLabel.AddThemeColorOverride("font_color", ScoundrelPalette.DescriptionGray);
        AddChild(_descriptionLabel);

        // Divider — a plain thin ColorRect, not an image.
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

        // Footer row: suit glyph + value for monster/weapon/potion, or a role tag +
        // sparkle for the friendly kinds (Blacksmith/Merchant/PotionJoker/WeaponJoker).
        int? footerValue = RoomCardContent.FooterValue(card);
        bool isFriendly  = family == RoomCardBannerFamily.Friendly;

        var footerLeft = new Label
        {
            Text              = RoomCardContent.FooterLeftText(card),
            OffsetLeft        = Padding,
            OffsetTop         = footerY,
            OffsetRight       = w * 0.6f,
            OffsetBottom      = footerY + FooterHeight,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter       = MouseFilterEnum.Ignore,
        };
        footerLeft.AddThemeFontOverride("font", ScoundrelPalette.SerifRegular);
        footerLeft.AddThemeFontSizeOverride("font_size", 13);
        footerLeft.AddThemeColorOverride("font_color", isFriendly ? ScoundrelPalette.BrightGold : ScoundrelPalette.MutedGoldGray);
        AddChild(footerLeft);

        var footerRight = new Label
        {
            Text                = footerValue.HasValue ? footerValue.Value.ToString() : "✦", // sparkle glyph
            OffsetLeft          = w * 0.6f,
            OffsetTop           = footerY,
            OffsetRight         = w - Padding,
            OffsetBottom        = footerY + FooterHeight,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Center,
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        footerRight.AddThemeFontOverride("font", ScoundrelPalette.DisplayBold);
        footerRight.AddThemeFontSizeOverride("font_size", footerValue.HasValue ? 24 : 18);
        // Monster damage reads in a brighter red than the monster banner itself (for
        // contrast against the dark card background); every other footer value/sparkle
        // uses the same bright gold as the rest of the UI's numeric readouts.
        footerRight.AddThemeColorOverride("font_color", card.Kind == CardKind.Monster ? ScoundrelPalette.MonsterRedValue : ScoundrelPalette.BrightGold);
        AddChild(footerRight);
    }

    // Friendly cards (Blacksmith/Merchant/PotionJoker/WeaponJoker) share one gold
    // banner regardless of suit, matching the mockup's Blacksmith example. Monster/
    // Weapon/Potion cards use their card's actual Suit instead of a fixed per-KIND
    // color (chunk 6): the real card art now shown in the icon slot is itself
    // per-suit colored (green clubs, purple spades, gold diamonds, red hearts), so
    // the banner matches that rather than clashing with a fixed "all monsters red"
    // treatment.
    private static (Color bg, Color fg) BannerColors(CardModel card, RoomCardBannerFamily family)
    {
        if (family == RoomCardBannerFamily.Friendly)
            return (ScoundrelPalette.BannerGoldFriendly, ScoundrelPalette.BackgroundNearBlack);

        return card.Suit switch
        {
            Suit.Clubs    => (ScoundrelPalette.BannerClubsGreen, Colors.White),
            Suit.Spades   => (ScoundrelPalette.BannerSpadesPurple, Colors.White),
            Suit.Diamonds => (ScoundrelPalette.BannerDiamondsGold, Colors.White),
            Suit.Hearts   => (ScoundrelPalette.BannerHeartsRed, Colors.White),
            _ => throw new System.InvalidOperationException($"Suit {card.Suit} has no room-card banner color."),
        };
    }
}
