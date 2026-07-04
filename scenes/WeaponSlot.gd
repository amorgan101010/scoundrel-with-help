## Weapon slot — holds the player's single equipped weapon card. Until now this was
## purely a display slot (the unmodified addon `Pile` script, allow_card_movement =
## false): the equipped weapon had no drag interaction of its own. Extended Rules
## chunk 13 lets the player drag the equipped weapon onto the Black Joker's pocket
## zone to store it there, so this slot needs the same card_drag_started/
## card_drag_ended/card_selected notification signals RoomContainer.gd and
## JokerPocketSlot.gd already emit for C# to react to a real drop — base Pile has no
## such signals at all, so config alone (flipping allow_card_movement) is not enough.
##
## Unlike JokerPocketSlot, there is no "index 0 is never draggable" special case here:
## the ONE card this slot ever holds (the equipped weapon) is exactly the card that
## should become draggable. Base Pile's own _update_card_states (restrict_to_top_card
## = true, allow_card_movement = true => can_be_interacted_with = (i ==
## held_cards.size() - 1), which is true for a lone card at index 0) already does the
## right thing, so it is intentionally NOT overridden here.
@tool
class_name WeaponSlot
extends Pile

signal card_drag_started(card: Card)
signal card_drag_ended()
signal card_selected(card: Card)


func _ready() -> void:
	super._ready()
	# Same fix as RoomContainer/JokerPocketSlot: gdUnit4 loads scenes via add_child
	# rather than change_scene_to_*, leaving get_tree().current_scene null, so the
	# base _ready()'s scene-root-meta CardManager lookup fails silently. Retry after
	# the frame once the full tree is searchable.
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
