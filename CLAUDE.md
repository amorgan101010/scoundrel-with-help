# OpenWolf

@.wolf/OPENWOLF.md

This project uses OpenWolf for context management. Read and follow .wolf/OPENWOLF.md every session. Check .wolf/cerebrum.md before generating code. Check .wolf/anatomy.md before reading files.


# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Scoundrel** — a Godot 4.7 C# solitaire card game built on top of the `addons/card-framework` GDScript addon (v1.4.0, MIT).

## Running the project

Open the project in the Godot editor and press F5, or from the command line:

```sh
godot --path /home/aileen/Repositories/godot/scoundrel-with-help
```

There are no automated tests. Verification is done by running the game.

## Architecture

Two distinct layers communicate exclusively through Godot signals and property access — never through shared state or direct coupling.

### GDScript layer (addon — do not modify)

`addons/card-framework/` contains the visual/interaction framework. Key classes:

- **`CardManager`** (`Control`) — scene root orchestrator. Must appear above all `CardContainer` nodes in the scene tree. Holds the `card_factory` reference and routes drag-drop events. Registers itself on `scene_root` meta so containers anywhere in the tree can find it.
- **`CardContainer`** (`Control`) — abstract base for all card slots. Subclasses override:
  - `_card_can_be_added(cards)` — accept/reject rule
  - `_update_target_positions()` — layout (positions + drop zone geometry only; must not touch `show_front` or `can_be_interacted_with`)
  - `_update_card_states()` — face direction and interaction flags
  - `on_card_pressed(card)` — fired when a card is clicked
- **`Pile`** (`CardContainer`) — stacked layout with `PileDirection` enum, `restrict_to_top_card`, `card_face_up`. Use for deck, discard, weapon slot.
- **`Hand`** (`CardContainer`) — fan layout with Curve-based rotation/vertical offset. Not used in Scoundrel.
- **`Card`** (`DraggableObject → Control`) — a single card node. Has `card_info: Dictionary` (set from JSON), `card_name: String`, `show_front: bool`, `can_be_interacted_with: bool`, `card_container: CardContainer`.
- **`JsonCardFactory`** (`CardFactory`) — reads `card_info_dir/*.json` and `card_asset_dir/*.png`, caches them in `preloaded_cards`, creates `Card` instances via `create_card(card_name, target_container)`.
- **`DraggableObject`** — state machine: `IDLE → HOVERING → HOLDING → MOVING`. Cards transition through these states automatically on mouse events.
- **`DropZone`** — collision sensor attached to each `CardContainer`. Supports vertical/horizontal partitioning for insert-position targeting.

### C# layer (game logic — all new code goes here)

Planned files under `scripts/`:

- **`CardData.cs`** — immutable data model parsed from a card's `card_info` Dictionary: `Suit` (enum), `Rank` (int), `Value` (int). Static factory `CardData.FromGodotCard(GodotObject)` reads the GDScript property via `.Get("card_info")`.
- **`ScoundrelGame.cs`** — sole game controller. Holds all mutable game state (health, equipped weapon, weapon floor, room flags). Wires up signals in `_Ready()` and drives all game logic.

Planned GDScript file (project-specific, not addon):

- **`scenes/RoomContainer.gd`** — extends `CardContainer`. 2×2 fixed-grid layout. Emits `card_selected(card: Card)` from `on_card_pressed`. Overrides `_card_can_be_added` to return `false` (room is populated programmatically only).

## C# / GDScript interop patterns

**C# calling GDScript methods:**
```csharp
var factory = (GodotObject)_cardManager.Get("card_factory");
factory.Call("create_card", "2_clubs", _roomContainer);
_deckPile.Call("shuffle");
var cards = new Godot.Collections.Array { card };
_discardPile.Call("move_cards", cards, -1, false);
```

**C# connecting to GDScript signals:**
```csharp
_roomContainer.Connect("card_selected", Callable.From<GodotObject>(OnCardSelected));
_runButton.Connect("pressed", Callable.From(OnRunPressed));
```

**Reading card data from C#:**
```csharp
var info = card.Get("card_info").AsGodotDictionary();
var suit = info["suit"].AsString();
var rank  = info["rank"].AsInt32();
```

## Card data format

Each of the 44 Scoundrel cards needs a JSON file in `card_data/` and a matching PNG in `card_assets/`:

```json
{
  "name": "2_clubs",
  "front_image": "2_clubs.png",
  "suit": "clubs",
  "rank": 2
}
```

**Deck composition** (44 cards — standard 52 minus red J/Q/K and red aces):
- Clubs + Spades: A, 2–10, J, Q, K (13 × 2 = 26 cards) — **monsters**
- Hearts: 2–10 (9 cards) — **potions**
- Diamonds: 2–10 (9 cards) — **weapons**

## Scoundrel rules reference

- Start at 20 HP; die at 0
- Deal 4 cards into a 2×2 room each turn; must take ≥3 per room; may skip exactly 1
- **Monsters** (clubs/spades): deal damage equal to card value (A=14, 2–10=face, J=11, Q=12, K=13); reduced by equipped weapon value, but weapon degrades — next use must be vs. a lower-value monster
- **Weapons** (diamonds): equip by taking; replaces current weapon; value = rank (A=1, 2–10=face)
- **Potions** (hearts): restore HP equal to rank, capped at 20; limit one per room
- **Run**: skip entire room (shuffle all 4 cards back into the deck); cannot run two rooms in a row

## Scene setup requirements

- `CardManager` must appear **above** all `CardContainer` nodes in the scene tree (or anywhere — it registers itself via scene root meta, so flexible layouts are supported)
- `card_factory_scene` export on `CardManager` must point to a configured `JsonCardFactory` scene
- `CardContainer` nodes register themselves with `CardManager` automatically in `_ready()`
- `enable_drop_zone = false` on containers that should not accept drops (deck, room slots)
