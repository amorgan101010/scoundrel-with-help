# Cerebrum

> OpenWolf's learning memory. Updated automatically as the AI learns from interactions.
> Do not edit manually unless correcting an error.
> Last updated: 2026-06-24

## User Preferences

<!-- How the user likes things done. Code style, tools, patterns, communication. -->

## Key Learnings

- **Project:** scoundrel-with-help
- **Card state after move:** DeckPile's `_update_card_states()` (Pile.gd) enforces `show_front = card_face_up` and `can_be_interacted_with = false` (when allow_card_movement=false) for ALL cards on every `update_card_ui()` call — so C# `Set("show_front", false)` before `move_cards` is redundant but harmless.
- **No get_all_cards on Pile:** `Pile` (addon) has no `get_all_cards()`. Only `RoomContainer` defines it. Use `get_card_count()` + `get_top_cards(n)` for Pile queries. Cannot easily iterate all deck cards from C#.
- **Suit count tracking:** Tracked in C# via `_inPlayClubs/Spades/Hearts/Diamonds` — decremented only on discard (fought monster, used potion, old weapon replaced). Run cards return to deck and stay "in play."
- **Card modulate for visual feedback:** Setting `modulate` on a GodotObject card via C# `card.Set("modulate", new Color(...))` works for visual state changes (e.g., gray-out voided potions). Must reset to `(1,1,1,1)` before discarding/running cards back.

## Do-Not-Repeat

<!-- Mistakes made and corrected. Each entry prevents the same mistake recurring. -->
<!-- Format: [YYYY-MM-DD] Description of what went wrong and what to do instead. -->

## Decision Log

<!-- Significant technical decisions with rationale. Why X was chosen over Y. -->
