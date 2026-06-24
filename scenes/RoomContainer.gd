## 2×2 card grid for the Scoundrel dungeon room.
##
## Cards occupy fixed slots so positions don't shift when one is taken.
## Incoming drops are disabled (enable_drop_zone=false in inspector);
## cards are moved here programmatically by ScoundrelGame.cs.
## Emits card_selected(card) on click or drag-release so C# can handle game logic.
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


## Override the base release so card_selected fires on mouse-up, before the
## framework's return-to-slot tween has a chance to start. C# handles the
## card immediately and kills the return tween by redirecting the card to its
## real destination — all within the same frame, so no snap-back is visible.
func release_holding_cards() -> void:
	if _holding_cards.is_empty():
		return
	var card := _holding_cards[0] as Card
	super.release_holding_cards()  # starts return tween + fires _on_drag_dropped
	card_selected.emit(card)       # C# redirects card, killing the return tween


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
