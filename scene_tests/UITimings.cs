/// <summary>
/// Timing constants for UI animations and scene-test input waits.
/// All times are in milliseconds.
/// </summary>
public static class UITimings
{
    /// <summary>
    /// Brief delay for animations to visually settle after initial scene load.
    /// Used in scene test SetupFixedDeck() defaults and assertions following card moves.
    /// </summary>
    public const uint AnimationSettleMs = 200;

    /// <summary>
    /// Wait time for card animation to complete and drag state to reset.
    /// Used before sending real mouse input in scene tests.
    /// Required because DraggableObject silently rejects clicks while in MOVING state.
    /// </summary>
    public const uint DragAnimationMs = 1200;

    /// <summary>
    /// Delay after mouse movement to allow hover state to register in DraggableObject.
    /// Used between SimulateMouseMove and button press in drag/click sequence.
    /// </summary>
    public const uint MouseHoverDelayMs = 100;

    /// <summary>
    /// Delay between pressing and releasing mouse button during a drag.
    /// Allows the card state machine to transition through HOLDING before release fires.
    /// </summary>
    public const uint MouseDragDelayMs = 80;

    /// <summary>
    /// Wait time after all input is complete for game logic to run and animations to settle.
    /// Used at the end of drag/click sequences before assertions.
    /// </summary>
    public const uint PostInputSettleMs = 500;

    /// <summary>
    /// Delay between consecutive card interactions in the same room.
    /// Used when clicking/dragging multiple cards in sequence.
    /// </summary>
    public const uint InteractionDelayMs = 50;
}
