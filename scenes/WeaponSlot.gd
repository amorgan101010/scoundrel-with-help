## Weapon slot — holds the player's single equipped weapon card. Until now this was
## purely a display slot (the unmodified addon `Pile` script, allow_card_movement =
## false): the equipped weapon had no drag interaction of its own. Extended Rules
## chunk 13 lets the player drag the equipped weapon onto the Black Joker's pocket
## zone to store it there, so this slot needs the same card_drag_started/
## card_drag_ended/card_selected notification signals DraggableCardSlot.gd provides
## for C# to react to a real drop — base Pile has no such signals at all, so config
## alone (flipping allow_card_movement) is not enough.
##
## Unlike JokerPocketSlot, there is no "index 0 is never draggable" special case here:
## the ONE card this slot ever holds (the equipped weapon) is exactly the card that
## should become draggable. Base Pile's own _update_card_states (restrict_to_top_card
## = true, allow_card_movement = true => can_be_interacted_with = (i ==
## held_cards.size() - 1), which is true for a lone card at index 0) already does the
## right thing, so it is intentionally NOT overridden here.
@tool
class_name WeaponSlot
extends DraggableCardSlot
