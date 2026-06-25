extends Pile

## Large-area drop zone whose hit-detection is a plain global_rect check.
##
## The standard Pile/CardContainer flow creates a DropZone sensor node via
## _initialize_drop_zone(), which requires CardManager to be registered before
## the container's _ready() fires. In some test-runner contexts that registration
## may not happen in time, leaving drop_zone == null and silently rejecting every
## drop. This subclass bypasses the sensor node entirely — detection is just
## get_global_rect().has_point(mouse) — so the zones work unconditionally.
##
## It also must be registered in card_manager.card_container_dict for _on_drag_dropped
## to check it. Same current_scene-null issue prevents super._ready() from doing that,
## so we retry registration with a deferred tree search (see _retry_card_manager_registration).

func _ready() -> void:
	super._ready()
	# Same fix as RoomContainer: retry CardManager registration after the frame
	# so the full tree is searchable (current_scene is null in gdUnit4 tests).
	if card_manager == null:
		_retry_card_manager_registration.call_deferred()

func _retry_card_manager_registration() -> void:
	if card_manager != null:
		return
	card_manager = _find_card_manager_in_tree(get_tree().get_root())
	if card_manager != null:
		card_manager._add_card_container(unique_id, self)
		# Don't call _initialize_drop_zone — check_card_can_be_dropped is overridden
		# to use get_global_rect() directly; no DropZone sensor node is needed.

static func _find_card_manager_in_tree(node: Node) -> CardManager:
	if node is CardManager:
		return node
	for child in node.get_children():
		var found := _find_card_manager_in_tree(child)
		if found != null:
			return found
	return null

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
