using Godot;

/// <summary>
/// Companion panel overlay for the ui_overhaul.png "COMPANIONS" column: a banner
/// header (name in bold serif caps, colored per Joker identity), a centered line-art
/// icon (reusing RoomCardIcon's jester mask — see RoomCardIcon.cs), a fixed
/// description paragraph (reusing ScoundrelRules.TooltipFor + RoomCardContent.
/// Description, same "strip the redundant 'Kind — ' prefix" pattern chunk 2
/// established for room cards), a native ProgressBar HP bar, and a role/HP label row
/// ("POTION JOKER   4 / 8"). Built entirely from native Panel/Label/ColorRect/
/// ProgressBar/RoomCardIcon primitives, no new image assets, per this redesign's
/// native-primitives-over-images steer.
///
/// Once a Joker becomes a companion it gets its own identity color (red for the
/// Potion/Red Joker, near-black for the Weapon/Black Joker — see
/// ScoundrelPalette.CompanionRedBanner/CompanionBlackBanner) rather than the generic
/// gold "friendly" room-card banner Blacksmith/Merchant/undrawn-Jokers share — that's
/// the mockup's intent, not an inconsistency with RoomCardOverlay's banner family.
///
/// Added once as a child of PotionJokerSlot/WeaponJokerSlot (the addon Pile controls
/// that host each Joker's actual Card node once taken) at _Ready(), sized to the
/// slot's card size, and refreshed in place via UpdateCompanion() every UpdateUI()
/// tick — following WeaponPanelOverlay's "build once, refresh live state" model
/// rather than RoomCardOverlay's "build once per card instance" model, since there's
/// exactly one overlay per slot for the lifetime of the game (the Joker's identity
/// never changes once assigned to PotionJokerSlot vs WeaponJokerSlot; only its
/// presence and HP do). The banner/icon/description content is itself entirely fixed
/// per slot (ScoundrelRules.TooltipFor's Joker branches return a fixed string
/// regardless of game state), so only Visible/HP-bar-value/HP-label need refreshing.
///
/// PotionJokerSlot/WeaponJokerSlot's own children today are the addon's "Cards"
/// control and its DropZone — same as WeaponSlot — so adding this as a plain sibling
/// Control is safe, and MouseFilter.Ignore throughout keeps the existing
/// drag-to-retrieve-pocketed-item interaction reaching the Card control beneath.
///
/// PotionJokerHpLabel/WeaponJokerHpLabel (the pre-existing plain-text summaries) are
/// kept fully intact and still computed every UpdateUI() call — gdUnit4 scene tests
/// assert their exact text in several places — just hidden, since this overlay now
/// shows the same information visually. See ScoundrelGame.UpdateUI()/_Ready().
/// </summary>
public partial class CompanionPanelOverlay : Control
{
    // Mirrors GameEngine's private JokerStartingHealth (8) — that constant isn't
    // exposed publicly today (ScoundrelGame's own hp-label text hardcodes "/8" the
    // same way), so this hardcodes the same value rather than plumbing a new public
    // constant through GameEngine for a single display use.
    private const int MaxHealth = 8;

    private const float Padding       = 14f;
    private const float BannerHeight  = 36f;
    private const float IconTopGap    = 8f;
    private const float IconHeight    = 46f;
    private const float DescGap       = 8f;
    private const float DividerGap    = 8f;
    private const float HpBarHeight   = 10f;
    private const float HpBarGap      = 10f;
    private const float RoleRowHeight = 22f;
    private const float BottomGap     = 12f;

    private ProgressBar _hpBar         = null!;
    private Label _hpNumberLabel       = null!;
    private CardKind _kind;

    public static CompanionPanelOverlay Create(CardKind kind, Vector2 slotSize)
    {
        var overlay = new CompanionPanelOverlay
        {
            Name        = "CompanionPanelOverlay",
            Position    = Vector2.Zero,
            Size        = slotSize,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex      = 1,
        };
        overlay.Build(kind, slotSize);
        return overlay;
    }

    /// <summary>Rebuilds this overlay at a new slot size -- called when
    /// ScoundrelLayoutController recomputes card size on viewport resize (see
    /// WeaponPanelOverlay.Resize for why a fresh Build() beats in-place re-layout).
    /// Caller must re-call UpdateCompanion() with current state afterward.</summary>
    public void Resize(Vector2 newSize)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        Size = newSize;
        Build(_kind, newSize);
    }

    /// <summary>Refreshes the state-dependent bits: hides the whole panel when this
    /// Joker hasn't been taken yet (present == false), otherwise updates the HP bar
    /// fill and the "N / 8" numeral. Banner/icon/description never change once built
    /// (see class doc comment), so only this much needs to run every UpdateUI() tick.</summary>
    public void UpdateCompanion(bool present, int health)
    {
        Visible = present;
        if (!present) return;

        _hpBar.Value = health;
        _hpNumberLabel.Text = $"{health} / {MaxHealth}";
    }

    private void Build(CardKind kind, Vector2 size)
    {
        _kind = kind;
        float w = size.X, h = size.Y;
        Color bannerColor = kind == CardKind.PotionJoker
            ? ScoundrelPalette.CompanionRedBanner
            : ScoundrelPalette.CompanionBlackBanner;
        // Bright icon accent per companion, distinct from the banner fill itself —
        // same reasoning as RoomCardOverlay.IconColor: the banner fills are tuned to
        // host white banner text and read low-contrast as line art on the near-black
        // card background. MonsterRedValue/BrightGold are existing bright accents
        // reused here rather than adding two more near-duplicate palette colors.
        Color iconColor = kind == CardKind.PotionJoker
            ? ScoundrelPalette.MonsterRedValue
            : ScoundrelPalette.BrightGold;

        var placeholderCard = PlaceholderCard(kind);
        string bannerName = RoomCardContent.DisplayName(placeholderCard);
        string roleName   = kind == CardKind.PotionJoker ? "POTION JOKER" : "WEAPON JOKER";
        // TooltipFor's Joker branches ignore every param but `card` (fixed text
        // regardless of equipped weapon/potion-used/health), so the rest are
        // harmless placeholders — see ScoundrelRules.TooltipFor.
        string description = RoomCardContent.Description(ScoundrelRules.TooltipFor(
            placeholderCard, null, int.MaxValue, false, ScoundrelRules.StartHealth));

        // Opaque background + thin border, same treatment as RoomCardOverlay/
        // WeaponPanelOverlay's card background.
        var backgroundStyle = new StyleBoxFlat { BgColor = ScoundrelPalette.CardBackMaroon, BorderColor = ScoundrelPalette.DividerGoldBrown };
        backgroundStyle.SetBorderWidthAll(2);
        var background = new Panel { OffsetRight = w, OffsetBottom = h, MouseFilter = MouseFilterEnum.Ignore };
        background.AddThemeStyleboxOverride("panel", backgroundStyle);
        AddChild(background);

        // Banner header bar.
        var banner = new Panel { OffsetRight = w, OffsetBottom = BannerHeight, MouseFilter = MouseFilterEnum.Ignore };
        banner.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = bannerColor });
        AddChild(banner);

        var bannerLabel = new Label
        {
            Text                = bannerName,
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
        bannerLabel.AddThemeColorOverride("font_color", Colors.White);
        AddChild(bannerLabel);

        float iconTop = BannerHeight + IconTopGap;
        var icon = new RoomCardIcon
        {
            Kind         = kind,
            LineColor    = iconColor,
            OffsetTop    = iconTop,
            OffsetRight  = w,
            OffsetBottom = iconTop + IconHeight,
            MouseFilter  = MouseFilterEnum.Ignore,
        };
        AddChild(icon);

        // Bottom-up geometry for the role row / HP bar / divider, mirroring
        // RoomCardOverlay's footer/divider calc — the description area then fills
        // whatever space remains above it.
        float roleRowBottom = h - BottomGap;
        float roleRowTop    = roleRowBottom - RoleRowHeight;
        float hpBarBottom   = roleRowTop - HpBarGap;
        float hpBarTop      = hpBarBottom - HpBarHeight;
        float dividerY      = hpBarTop - DividerGap;
        float descTop       = iconTop + IconHeight + DescGap;
        float descBottom    = dividerY - DescGap;

        var descriptionLabel = new Label
        {
            Text              = description,
            OffsetLeft        = Padding,
            OffsetTop         = descTop,
            OffsetRight       = w - Padding,
            OffsetBottom      = descBottom,
            AutowrapMode      = TextServer.AutowrapMode.Word,
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter       = MouseFilterEnum.Ignore,
        };
        descriptionLabel.AddThemeFontOverride("font", ScoundrelPalette.SerifRegular);
        descriptionLabel.AddThemeFontSizeOverride("font_size", 11);
        descriptionLabel.AddThemeColorOverride("font_color", ScoundrelPalette.DescriptionGray);
        AddChild(descriptionLabel);

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

        // HP bar — native ProgressBar with a custom red fill + dark track StyleBox,
        // not an image. MaxValue is fixed for the overlay's lifetime (companions
        // always cap at 8); only Value changes, in UpdateCompanion().
        _hpBar = new ProgressBar
        {
            MinValue        = 0,
            MaxValue        = MaxHealth,
            ShowPercentage  = false,
            OffsetLeft      = Padding,
            OffsetTop       = hpBarTop,
            OffsetRight     = w - Padding,
            OffsetBottom    = hpBarBottom,
            MouseFilter     = MouseFilterEnum.Ignore,
        };
        var trackStyle = new StyleBoxFlat { BgColor = ScoundrelPalette.BackgroundNearBlack };
        trackStyle.SetCornerRadiusAll(3);
        var fillStyle = new StyleBoxFlat { BgColor = ScoundrelPalette.CompanionRedBanner };
        fillStyle.SetCornerRadiusAll(3);
        _hpBar.AddThemeStyleboxOverride("background", trackStyle);
        _hpBar.AddThemeStyleboxOverride("fill", fillStyle);
        AddChild(_hpBar);

        // Role/HP label row: role tag left, "N / 8" numeral right — same two-label
        // footer-row shape RoomCardOverlay uses for its suit/value footer.
        var roleLabel = new Label
        {
            Text              = roleName,
            OffsetLeft        = Padding,
            OffsetTop         = roleRowTop,
            OffsetRight       = w * 0.6f,
            OffsetBottom      = roleRowTop + RoleRowHeight,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter       = MouseFilterEnum.Ignore,
        };
        roleLabel.AddThemeFontOverride("font", ScoundrelPalette.SerifRegular);
        roleLabel.AddThemeFontSizeOverride("font_size", 13);
        roleLabel.AddThemeColorOverride("font_color", ScoundrelPalette.MutedGoldGray);
        AddChild(roleLabel);

        _hpNumberLabel = new Label
        {
            OffsetLeft          = w * 0.6f,
            OffsetTop           = roleRowTop,
            OffsetRight         = w - Padding,
            OffsetBottom        = roleRowTop + RoleRowHeight,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Center,
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        _hpNumberLabel.AddThemeFontOverride("font", ScoundrelPalette.SerifRegular);
        _hpNumberLabel.AddThemeFontSizeOverride("font_size", 13);
        _hpNumberLabel.AddThemeColorOverride("font_color", ScoundrelPalette.MutedGoldGray);
        AddChild(_hpNumberLabel);
    }

    // A minimal CardModel whose Kind resolves to PotionJoker/WeaponJoker, just to
    // satisfy RoomCardContent.DisplayName/ScoundrelRules.TooltipFor's CardModel
    // parameter — Rank/Name are irrelevant to both for these two kinds (see
    // CardModel.Kind and TooltipFor's Joker branches).
    private static CardModel PlaceholderCard(CardKind kind) => kind == CardKind.PotionJoker
        ? new CardModel(Suit.RedJoker, 0)
        : new CardModel(Suit.BlackJoker, 0);
}
