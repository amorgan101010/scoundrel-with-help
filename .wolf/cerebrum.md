# Cerebrum

> OpenWolf's learning memory. Updated automatically as the AI learns from interactions.
> Do not edit manually unless correcting an error.
> Last updated: 2026-06-24

## User Preferences

- **Always write tests with the feature.** Every implementation commit must include tests covering the new behavior. Tests are not a follow-up — they ship in the same commit.
- **Drop-zone-based card controls.** Cards must be dragged into LeftDropZone (left side, x:0-320) or RightDropZone (right side, x:820-1120) to be taken. Cards dropped in the room's safe gap (x:320-820) return to their slot. `ClickCard()` in tests still works (emits `card_selected` directly, bypasses zone routing entirely) for game-logic tests. Real-input tests must use `MouseDragCard` with a target position inside a zone (default: (850,300) is inside RightDropZone). Bare clicks never fire `card_selected` because the mouse never reaches a zone sensor.

- **Zone semantics (2026-06-25):** LEFT (green) = drink potion / equip weapon / fight with weapon. RIGHT (blue) = discard potion or weapon / fight bare-handed. `OnCardSelected` uses `activateCard = !((IsPotion || IsWeapon) && droppedRight)` to determine whether the card's effect fires. `GameEngine.TakeCard(card, useWeapon, activateCard)` — when `activateCard=false`, card goes straight to discard with no suit-specific effect. `ClickCard()` (no zone) always has `activateCard=true`.

## Key Learnings

- **Project:** scoundrel-with-help
- **Card state after move:** DeckPile's `_update_card_states()` (Pile.gd) enforces `show_front = card_face_up` and `can_be_interacted_with = false` (when allow_card_movement=false) for ALL cards on every `update_card_ui()` call — so C# `Set("show_front", false)` before `move_cards` is redundant but harmless.
- **No get_all_cards on Pile:** `Pile` (addon) has no `get_all_cards()`. Only `RoomContainer` defines it. Use `get_card_count()` + `get_top_cards(n)` for Pile queries. Cannot easily iterate all deck cards from C#.
- **Suit count tracking:** Tracked in C# via `_inPlayClubs/Spades/Hearts/Diamonds` — decremented only on discard (fought monster, used potion, old weapon replaced). Run cards return to deck and stay "in play."
- **Card modulate for visual feedback:** Setting `modulate` on a GodotObject card via C# `card.Set("modulate", new Color(...))` works for visual state changes (e.g., gray-out voided potions). Must reset to `(1,1,1,1)` before discarding/running cards back.

## Do-Not-Repeat

<!-- Mistakes made and corrected. Each entry prevents the same mistake recurring. -->
<!-- Format: [YYYY-MM-DD] Description of what went wrong and what to do instead. -->

- [2026-06-24] **gdUnit4 C# tests: always add `[RequireGodotRuntime]` on scene test classes.** Without it, `ISceneRunner.Load()` runs on a thread-pool thread and crashes with "AddChild only allowed from main thread." `[RequireGodotRuntime]` routes test hooks through Godot's SynchronizationContext.
- [2026-06-24] **After every `dotnet build`, run `godot --import` before running tests.** The Godot global script class cache (`.godot/global_script_class_cache.cfg`) is cleared on rebuild, causing `GdUnitTestCIRunner: type not found`. The `run_scene_tests.sh` script does this automatically.
- [2026-06-24] **NuGet deps don't auto-copy to Godot's output dir.** Add `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` to `ScoundrelWithHelp.csproj` — without it, `GdUnit4Api.dll`, `Mono.Cecil.dll`, `Newtonsoft.Json.dll` etc. must be manually copied each time.
- [2026-06-24] **gdUnit4 CLI arg parser is strict.** Do NOT use `--` separator before `-a`, and do NOT pass `--exit` (tool auto-exits). Correct: `-s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -a res://scene_tests/`.
- [2026-06-24] **Scene test assertions must not assume card type in random room.** If asserting a specific suit exists (diamond, heart, monster), use early `return` (not `AssertThat(...).IsNotNull()`) to skip gracefully when the random deal doesn't include it.
- [2026-06-24] **3 random monster cards can kill from full HP** (e.g. 9+5+6=20 damage). Tests clicking 3 cards must check `ParseHP(scene) <= 0` before asserting game-state conditions that require the player to be alive.
- [2026-06-24] **`SimulateMouseButtonPressed` is atomic — use separate press/release for card clicks.** gdUnit4's `SimulateMouseButtonPressed` fires both press and release in the same frame with no idle frame between. The card framework's DraggableObject needs at least one idle frame after the press to populate `_holding_cards` before `release_holding_cards()` runs. Use `SimulateMouseButtonPress` + `AwaitIdleFrame` + `SimulateMouseButtonRelease` instead. Also: cards in MOVING state (animating after deal) silently reject all input — wait ≥1200ms after a retry/re-deal before issuing mouse input in tests.

- [2026-06-24] **`card.move()` early-return causes stale-tween bug (bug-014, FIXED).** `draggable_object.gd`'s `move()` early-returns when `global_position == target_destination`. After `OnRunPressed()` moves old room cards to deck, their deck-bound tweens start but `_process` hasn't run yet — the cards still sit at their old room-slot positions. If `SyncRoomToGodot()` immediately re-deals a card back to the same room slot, `card.move(room_slot_pos)` early-returns, leaving the deck tween running. That tween carries the card (logically in the room, face-up/interactive) to the deck area. **Fix:** in `SyncRoomToGodot()`, set `godotCard.Set("global_position", deckAnchor)` before calling `move_cards` on the room container, so global_position ≠ room_slot_pos and the stale deck tween is always killed.

- [2026-06-24] **`ISceneRunner.AwaitMillis` takes `uint`, not `int`.** Pass uint literals (`1200u`) or cast explicitly — `int` arguments cause CS1503.

- [2026-06-24] **After `StartGameWithDeck()` mid-test, wait 1200ms before sending real mouse input.** `ISceneRunner.Load` + `AwaitMillis(200)` works because gdUnit4's loader processes frames during load; calling `StartGameWithDeck` inside a running test gives no such head-start and cards animate for up to 700ms. DraggableObject silently rejects all input while in MOVING state. Signal-based `ClickCard()` is unaffected; only `MouseClickCard` / `MouseDragCard` need the longer settle. In `SetupFixedDeck(uint settleMs)`, pass `1200u` from mouse-input tests.

- [2026-06-24] **Scene tests must use `ScoundrelGame.StartGameWithDeck()` for deterministic room composition.** Tests that depend on specific card suits (weapon, monster, potion) must inject a known deck at the start of the test via `((ScoundrelGame)_runner.Scene()).StartGameWithDeck(FixedDeck)`. Never use `if (card == null) return;` to silently skip when a required card isn't in the random initial room — that produces a green test that asserted nothing. Use `AssertThat(card).IsNotNull()` instead so a missing card is a clear failure.

- [2026-06-25] **gdUnit4 loads via add_child → card_manager=null → all drags silently fail (bug-041).** `get_tree().current_scene` is null in gdUnit4, so `CardContainer._find_and_register_card_manager()` always fails. With `card_manager=null`, `CardContainer.release_holding_cards()` skips the `_on_drag_dropped()` call. Cards always stay in room. Bounce tests pass coincidentally (expected=stayed, actual=stayed). Take tests fail (expected=moved, actual=stayed). **Fix:** Override `_ready()` in project-owned containers (RoomContainer, DropZoneContainer) with a `call_deferred` retry that searches `get_tree().get_root()` recursively for a CardManager node. Never diagnose drag failures without first checking that card_manager is non-null and containers are registered in card_container_dict.

- [2026-06-24] **Test assertions must be exact, not permissive.** Never write `Is.GreaterThanOrEqualTo(0)` when the exact value is `0` (or any known value). Permissive assertions pass even when the implementation returns a wrong-but-still-valid value. Write the specific value the code should return.

- [2026-06-24] **`ClickCard` (direct signal emit) vs `MouseClickCard` (real input).** `ClickCard` bypasses DraggableObject entirely and is appropriate for testing C# game-logic handlers in isolation. `MouseClickCard`/`MouseDragCard` test the real GDScript→C# input pathway and should be used when the interaction mechanism itself is under test. Don't use `ClickCard` for a test whose description says "clicking a card" if the signal routing is what you're verifying.

## Decision Log

<!-- Significant technical decisions with rationale. Why X was chosen over Y. -->

- [2026-06-24] **gdUnit4 v6.1.3 addon + gdUnit4.api NuGet v5.0.0** — the NuGet version numbering is decoupled from the addon version; v5 NuGet works with v6 addon.
- [2026-06-24] **`ISceneRunner.Load("res://scenes/Game.tscn", true)` in `[BeforeTest]`** — loads a fresh Game.tscn for each test case. gdUnit4 loads scenes via `add_child`, NOT `change_scene_to_*`, so `get_tree().current_scene` is null at `_ready()` time. CardContainer._ready()'s `current_scene` meta lookup fails AND `_find_card_manager_in_parents()` also fails (CardManager is a sibling of UI, not a parent of RoomContainer). This leaves `card_manager = null` on all containers, silently breaking `_on_drag_dropped`. **Fix:** RoomContainer.gd and DropZoneContainer.gd now override `_ready()` with a deferred `_find_card_manager_in_tree(get_tree().get_root())` retry that runs after all `_ready()` calls complete.
