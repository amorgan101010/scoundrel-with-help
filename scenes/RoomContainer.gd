## 2×2 card grid for the Scoundrel dungeon room.
##
## Cards occupy fixed slots so positions don't shift when one is taken.
## Incoming drops are disabled (enable_drop_zone=false in inspector);
## cards are moved here programmatically by ScoundrelGame.cs.
##
## Drag-only controls: card_selected fires only when a card lands in a registered
## drop zone (LeftDropZone or RightDropZone). Cards released elsewhere return to
## their room slot via the framework's return_card() tween. Bare clicks never fire
## the signal because the mouse never reaches a zone sensor.
##
## card_drag_started / card_drag_ended let C# show/hide zone highlights.
class_name RoomContainer
extends CardContainer

signal card_drag_started(card: Card)
signal card_drag_ended()
signal card_selected(card: Card)

const CARD_W: int = 225
const CARD_H: int = 315
const SLOT_GAP: int = 20

const SLOTS: Array[Vector2] = [
	Vector2(0, 0),
	Vector2(CARD_W + SLOT_GAP, 0),
	Vector2(0, CARD_H + SLOT_GAP),
	Vector2(CARD_W + SLOT_GAP, CARD_H + SLOT_GAP),
]

# Maps Card → slot index so positions stay stable as cards are removed.
var _slot_of: Dictionary = {}


func _ready() -> void:
	super._ready()
	# gdUnit4 loads scenes via add_child rather than change_scene_to_*, leaving
	# get_tree().current_scene null. CardContainer._ready() uses current_scene meta
	# to find CardManager, so it fails silently — card_manager stays null and
	# _on_drag_dropped is never called. Retry after the frame once all _ready()
	# calls are done so we can search the full tree for the CardManager.
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
##   - moved the card to a drop-zone container  (card.card_container != self), or
##   - called card.return_card() to tween it back to its room slot (card.card_container == self).
## Only emit card_selected for the first case so bare clicks and off-zone drags are ignored.
func release_holding_cards() -> void:
	if _holding_cards.is_empty():
		return
	var card := _holding_cards[0] as Card
	super.release_holding_cards()
	card_drag_ended.emit()
	if card.card_container != self:
		card_selected.emit(card)


func _update_target_positions() -> void:
	for card in _held_cards:
		var slot: int = _slot_of.get(card, 0)
		card.move(global_position + SLOTS[slot], 0.0)


func _update_card_states() -> void:
	for card in _held_cards:
		card.show_front = true
		card.can_be_interacted_with = true


## Returns all cards currently in the room (snapshot copy).
func get_all_cards() -> Array:
	return _held_cards.duplicate()


## Assign a slot before calling super so the first layout pass uses the right position.
func add_card(card: Card, index: int = -1) -> void:
	if not _slot_of.has(card):
		_slot_of[card] = _claim_next_slot()
	super.add_card(card, index)


## Release the slot when a card leaves the room.
func remove_card(card: Card) -> bool:
	_slot_of.erase(card)
	return super.remove_card(card)


## Reset slot tracking when the room is fully emptied (e.g. new game).
func clear_cards() -> void:
	_slot_of.clear()
	super.clear_cards()


func _claim_next_slot() -> int:
	var used: Array = _slot_of.values()
	for i in range(4):
		if not used.has(i):
			return i
	return 0
