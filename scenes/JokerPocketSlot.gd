## Joker pocket slot (Potion/Weapon) — holds the joker's own card (always index 0,
## placed the moment the joker is taken and never removed while the joker lives) plus
## at most one stored item stacked on top of it (index 1, present only while something
## is pocketed). Lets the player drag the stored item back out to retrieve (drop on the
## top zone) or discard (drop on the right zone) — the joker card itself is never
## draggable, so it can't be dragged away from its slot by mistake.
##
## Mirrors RoomContainer.gd's drag-signal pattern almost exactly; the differences are
## the underlying layout (single-column Pile stack here vs. RoomContainer's 2×2 grid)
## and the joker-card interaction guard in _update_card_states below.
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
extends Pile

signal card_drag_started(card: Card)
signal card_drag_ended()
signal card_selected(card: Card)


func _ready() -> void:
	super._ready()
	# Same fix as RoomContainer: gdUnit4 loads scenes via add_child rather
	# than change_scene_to_*, leaving get_tree().current_scene null, so the
	# base _ready()'s scene-root-meta CardManager lookup fails silently.
	# Retry after the frame once the full tree is searchable.
	if card_manager == null:
		_retry_card_manager_registration.call_deferred()


func _retry_card_manager_registration() -> void:
	if card_manager != null:
		return
	card_manager = _find_card_manager_in_tree(get_tree().get_root())
	if card_manager != null:
		card_manager._add_card_container(unique_id, self)


static func _find_card_manager_in_tree(node: Node) -> CardManager:
	if node is CardManager:
		return node
	for child in node.get_children():
		var found := _find_card_manager_in_tree(child)
		if found != null:
			return found
	return null


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


func on_card_pressed(card: Card) -> void:
	card_drag_started.emit(card)


## After super.release_holding_cards(), the framework has either:
##   - moved the card to a drop-zone container (card.card_container != self), or
##   - called card.return_card() to tween it back to this slot (card.card_container == self).
## Only emit card_selected for the first case.
func release_holding_cards() -> void:
	if _holding_cards.is_empty():
		return
	var card := _holding_cards[0] as Card
	super.release_holding_cards()
	card_drag_ended.emit()
	if card.card_container != self:
		card_selected.emit(card)
