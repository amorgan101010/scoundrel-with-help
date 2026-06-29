public static class SlainBadgeLayout
{
    private const float BaseCardHeight = 315f;
    private const float BadgeLayoutWidth = 45f;
    private const float BadgeLayoutHeight = 66f;
    private const float BadgeVisualWidth = 30f;
    private const float BadgeVisualHeight = 44f;
    private const float BadgeNaturalStep = 52.5f;

    public static float BadgeWidth(float cardHeight)
    {
        return BadgeVisualWidth * CardScale(cardHeight);
    }

    public static float BadgeHeight(float cardHeight)
        => BadgeVisualHeight * CardScale(cardHeight);

    public static float BadgeStep(float cardWidth, float cardHeight, int count)
    {
        float scale = CardScale(cardHeight);
        float badgeNaturalStep = BadgeNaturalStep * scale;
        float badgeLayoutWidth = BadgeLayoutWidth * scale;

        return count <= 1
            ? badgeNaturalStep
            : System.Math.Min(badgeNaturalStep, (cardWidth - badgeLayoutWidth) / (count - 1));
    }

    public static float BadgeStartX(float cardWidth, float cardHeight, int count)
    {
        float scale = CardScale(cardHeight);
        float badgeLayoutWidth = BadgeLayoutWidth * scale;
        float step = BadgeStep(cardWidth, cardHeight, count);
        return (cardWidth - ((count - 1) * step + badgeLayoutWidth)) / 2f;
    }

    public static float BadgeTopY(float cardHeight)
    {
        return cardHeight - (BadgeHeight(cardHeight) / 3f);
    }

    private static float CardScale(float cardHeight)
    {
        float scale = cardHeight / BaseCardHeight;
        return scale <= 0f ? 1f : scale;
    }
}