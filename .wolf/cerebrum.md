# Cerebrum

> OpenWolf's learning memory. Updated automatically as the AI learns from interactions.
> Do not edit manually unless correcting an error.
> Last updated: 2026-06-24

## User Preferences

- **Always write tests with the feature.** Every implementation commit must include tests covering the new behavior. Tests are not a follow-up — they ship in the same commit.

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

## Decision Log

<!-- Significant technical decisions with rationale. Why X was chosen over Y. -->

- [2026-06-24] **gdUnit4 v6.1.3 addon + gdUnit4.api NuGet v5.0.0** — the NuGet version numbering is decoupled from the addon version; v5 NuGet works with v6 addon.
- [2026-06-24] **`ISceneRunner.Load("res://scenes/Game.tscn", true)` in `[BeforeTest]`** — loads a fresh Game.tscn for each test case. CardManager's `current_scene` meta path fails (returns wrong node when run via GdUnitCmdTool), but `_find_card_manager_in_parents()` fallback succeeds because CardManager IS an ancestor in the gdUnit4-hosted tree. Error messages appear but are non-fatal.
