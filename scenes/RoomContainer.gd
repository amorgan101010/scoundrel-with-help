## 2×2 card grid for the Scoundrel dungeon room.
##
## Cards occupy fixed slots so positions don't shift when one is taken.
## Incoming drops are disabled (enable_drop_zone=false in inspector);
## cards are moved here programmatically by ScoundrelGame.cs.
## Emits card_selected(card) after a click or drag-release so C# can handle game logic.
class_name RoomContainer
extends CardContainer

signal card_selected(card: Card)

const SLOT_GAP_X := 170
const SLOT_GAP_Y := 230
const SLOTS: Array[Vector2] = [
	Vector2(0, 0),
	Vector2(170, 0),
	Vector2(0, 230),
	Vector2(170, 230),
]

# Maps Card → slot index so positions stay stable as cards are removed.
var _slot_of: Dictionary = {}

# Card currently being dragged; set on press, cleared on move_done.
var _dragged_card: Card = null


## Record which card was pressed. The actual card_selected signal fires in
## on_card_move_done so that dragging also works: the card follows the mouse,
## then snaps back to its slot on release, and the signal fires on arrival.
func on_card_pressed(card: Card) -> void:
	_dragged_card = card


## Fires card_selected after a drag-return (or a quick click-snap-back).
## Only responds to the card that was pressed; ignores layout/deal moves.
func on_card_move_done(card: Card) -> void:
	if card == _dragged_card:
		_dragged_card = null
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
	_dragged_card = null
	super.clear_cards()


func _claim_next_slot() -> int:
	var used: Array = _slot_of.values()
	for i in range(4):
		if not used.has(i):
			return i
	return 0
