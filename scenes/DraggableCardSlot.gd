## Shared base for single-purpose Pile-based slots that need drag notification
## signals for C# to react to (WeaponSlot.gd, JokerPocketSlot.gd). Base Pile has
## no such signals at all, so config alone (flipping allow_card_movement) is not
## enough — C# needs to know when a drag starts/ends and whether a drop actually
## landed the card somewhere else.
##
## Factors out what were three byte-for-byte-identical copies (this + the
## pre-existing RoomContainer.gd) of: the drag signal trio, the deferred
## CardManager-registration retry (gdUnit4 loads scenes via add_child rather
## than change_scene_to_*, leaving get_tree().current_scene null, so the base
## _ready()'s scene-root-meta CardManager lookup fails silently — retrying
## after the frame lets it search the full tree once everything is present),
## on_card_pressed, and release_holding_cards.
##
## RoomContainer.gd extends CardContainer (not Pile) — it needs a 2×2 fixed-grid
## layout with per-slot index tracking, none of which Pile provides, and it
## already fully overrides _update_target_positions/_update_card_states, so
## inheriting Pile's stacking behavior would only add unused
## pile_layout/pile_interaction inspector properties. There's no common
## ancestor below CardContainer itself that both Pile-based slots and
## RoomContainer could share without either forcing RoomContainer to extend
## Pile (adding irrelevant stacking config) or stripping WeaponSlot/
## JokerPocketSlot of the Pile stacking/positioning logic they rely on
## (_update_target_positions, _calculate_offset, get_target_pose_for). So
## RoomContainer.gd is intentionally left as a documented, not-yet-converted
## third copy of this pattern rather than forcing an awkward hierarchy.
@tool
class_name DraggableCardSlot
extends Pile

signal card_drag_started(card: Card)
signal card_drag_ended()
signal card_selected(card: Card)


func _ready() -> void:
	super._ready()
	# gdUnit4 loads scenes via add_child rather than change_scene_to_*, leaving
	# get_tree().current_scene null, so the base _ready()'s scene-root-meta
	# CardManager lookup fails silently. Retry after the frame once the full
	# tree is searchable.
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
