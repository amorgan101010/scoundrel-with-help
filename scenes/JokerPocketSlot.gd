## Joker pocket slot (Potion/Weapon) — holds the joker's own card (always index 0,
## placed the moment the joker is taken and never removed while the joker lives) plus
## at most one stored item stacked on top of it (index 1, present only while something
## is pocketed). Lets the player drag the stored item back out to retrieve (drop on the
## top zone) or discard (drop on the right zone) — the joker card itself is never
## draggable, so it can't be dragged away from its slot by mistake.
##
## Mirrors RoomContainer.gd's drag-signal pattern almost exactly (shared with
## WeaponSlot.gd via DraggableCardSlot.gd); the differences are the underlying
## layout (single-column Pile stack here vs. RoomContainer's 2×2 grid) and the
## joker-card interaction guard in _update_card_states below.
##
## card_drag_started / card_drag_ended let C# reuse the exact same zone highlight logic
## already wired for room-card drags (ScoundrelGame.cs OnCardDragStarted/OnCardDragEnded
## switch on the card's suit, not its origin container).
##
## card_selected fires only when a drag actually lands the card in a different container
## (a real drop into a zone) — not when it snaps back to this slot via the framework's
## return_card() tween.
@tool
class_name JokerPocketSlot
extends DraggableCardSlot


## The joker card (always index 0 — placed once, the moment the joker is taken,
## and never re-added) must never be draggable, regardless of allow_card_movement/
## restrict_to_top_card. Only a genuinely stored item (index 1, added later via
## StorePotion/StoreWeapon's move_cards call) is interactive. Pile's own
## restrict_to_top_card=true alone doesn't distinguish these two cases: with only
## the joker present, it IS the "top" card and would incorrectly become draggable.
func _update_card_states() -> void:
	for i in range(_held_cards.size()):
		var card = _held_cards[i]
		card.show_front = card_face_up
		card.can_be_interacted_with = i > 0
