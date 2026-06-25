extends Pile

## Large-area drop zone whose hit-detection is a plain global_rect check.
##
## The standard Pile/CardContainer flow creates a DropZone sensor node via
## _initialize_drop_zone(), which requires CardManager to be registered before
## the container's _ready() fires. In some test-runner contexts that registration
## may not happen in time, leaving drop_zone == null and silently rejecting every
## drop. This subclass bypasses the sensor node entirely — detection is just
## get_global_rect().has_point(mouse) — so the zones work unconditionally.

func _update_target_positions() -> void:
	pass  # zone is always empty; nothing to position

func _update_card_states() -> void:
	pass  # C# manages card appearance before and after a zone accept

func check_card_can_be_dropped(cards: Array) -> bool:
	if not enable_drop_zone:
		return false
	return get_global_rect().has_point(get_global_mouse_position()) \
		and _card_can_be_added(cards)

func get_partition_index() -> int:
	return -1  # always append; zones hold at most one transient card
