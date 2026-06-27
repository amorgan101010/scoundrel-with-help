# Product Requirements Document — Scoundrel HD

**Version:** 0.8.7 (current as of 2026-06-27)  
**Platform:** Desktop (Linux, Windows); exported via Godot 4.7 C#  
**Engine:** Godot 4.7 + `addons/card-framework` GDScript v1.4.0  
**Game logic:** Pure C# (`GameEngine.cs`, `ScoundrelRules.cs`) — Godot-free, fully unit-testable

---

## 1. Product Overview

Scoundrel HD is a single-player solitaire dungeon-crawl card game based on the original Scoundrel rules. The player fights through a shuffled 44-card deck one 2×2 room at a time, managing HP, weapons, and potions to survive as long as possible.

The project exists because the creator wanted a polished digital version of a physical game they enjoy, with room to extend the rules in ways that feel fun rather than faithful.

---

## 2. Core Rules (Canonical)

- Start at **20 HP**. Die at 0.
- Each turn deals **4 cards** face-up into a 2×2 room.
- Player must take **at least 3** cards per room. May skip exactly 1.
- **Monsters** (clubs / spades): deal damage = card value (A=14, 2–10 face, J=11, Q=12, K=13). Damage reduced by equipped weapon value. Using a weapon degrades it — next fight must be against a *lower-value* monster.
- **Weapons** (diamonds): equip on pickup, replacing the current weapon. Value = rank (A=1, 2–10 face).
- **Potions** (hearts): restore HP equal to rank, capped at 20. **Limit one per room** — additional potions are void (draggable but have no effect).
- **Run**: shuffle all 4 room cards to the back of the deck. Cannot run two rooms in a row.
- Deck composition: 44 cards — clubs/spades full 13 each (monsters), hearts 2–10 (potions), diamonds 2–10 (weapons). No red face cards, no red aces, no jokers.

---

## 3. Interaction Model

Cards are controlled by drag-and-drop. Two drop zones are visible **only while a card is being held**:

| Zone | Screen position | Action |
|------|----------------|--------|
| **Left (green)** | Left ~⅓ of viewport | Drink potion / Equip weapon / Fight with weapon |
| **Right (blue)** | Right ~⅓ of viewport | Discard potion or weapon / Fight bare-handed |

Cards released in the center dead-zone (the room area) snap back to their slot.

Bare clicks on a card never consume it — only a successful zone drop does.

---

## 4. Features: Done

The following features are fully implemented, tested, and merged to main.

### 4.1 Gameplay
- [x] Full 44-card deck with JSON definitions and SVG art
- [x] Deal 4-card room; take 3, skip 1
- [x] Monster combat (weapon-modified damage, weapon degradation)
- [x] Weapon equip / replace / discard
- [x] Potion healing (capped at 20, one per room)
- [x] Void potion visual feedback (gray modulate on second potion in room)
- [x] Run mechanic (shuffle room back, no consecutive runs)
- [x] Game over at 0 HP
- [x] Win condition (deck exhausted alive)
- [x] Weapon choice: drag left to use weapon, drag right to fight bare-handed

### 4.2 UI / Visual
- [x] 2×2 room card grid (RoomContainer.gd)
- [x] Deck and discard piles (Pile nodes)
- [x] Equipped weapon display slot
- [x] HP display (D20 die; green → yellow → red as HP drops)
- [x] Zone highlights appear/disappear while a card is dragged
- [x] Zone labels ("Fight (Weapon)" / "Fight (Fists)" etc.)
- [x] Tooltips on card hover (damage preview, heal preview, weapon info)
- [x] Slain monster badges on weapon card
- [x] Bounce animation on game over
- [x] 1080p (1920×1080) base viewport with `expand` stretch mode
- [x] Cards scale with viewport resize
- [x] Consistent card gap in room on resize

### 4.3 Audio
- [x] Card deal sound
- [x] Potion drink sound (bubbling choir)
- [x] Weapon combat (punch)
- [x] Bare-fist combat (punch)
- [x] Weapon equip (sword unsheathe)
- [x] Potion discard (breaking glass)
- [x] Weapon discard (clattering metal)

### 4.4 CI/CD & Engineering
- [x] Unit tests (NUnit, no Godot) — `GameEngine`, `ScoundrelRules`
- [x] Scene integration tests (gdUnit4) — full Godot → C# signal chain
- [x] GitHub Actions: unit tests on every push, scene tests on main/PRs
- [x] Build + release pipeline (Godot export → GitHub Release artifact)
- [x] Version bump required check on every PR
- [x] Custom app icon (potion) and executable name (Scoundrel HD)
- [x] Linux and Windows export targets

---

## 5. Features: To Build

Ordered roughly by priority / dependency.

### 5.1 Deck / Room Navigation (High Priority)

**Click deck to advance rooms**  
Players intuitively click the deck expecting it to deal. Move that intent here — clicking the deck starts the next room. The "Run" button moves below the deck and becomes red with an exclamation mark to signal danger/cost.

### 5.2 Stats Tracking (High Priority)

Track per-session and lifetime stats including:
- Games played / wins / losses
- Turns survived per game
- Total damage dealt / taken
- Potions consumed
- Weapons used

Persist to a local file. Surface stats in a Balatro-style popup.

### 5.3 Discard / Debug Popup (Medium Priority)

Replace the always-visible debug counters with a popup accessible via:
- Clicking the discard pile (which shows a tombstone placeholder — see §5.4)
- Or a dedicated info button

Popup shows:
- Cards remaining in deck
- Cards in discard (list or counts by suit)
- Current room number / turn count
- Cards by suit remaining

### 5.4 Tombstone Discard Pile (Medium Priority)

Replace the discard pile visual with a tombstone image or similar thematic art.  
Clicking it opens the popup from §5.3.

### 5.5 Main Menu (Medium Priority)

Minimal main menu before game start:
- "Play" — starts Classic ruleset
- "Play (Extra Rules)" — starts Extended ruleset (§6)
- "Stats" — opens lifetime stats
- "Settings" — run seed input, audio toggles (future)

### 5.6 Seeded Runs (Medium Priority)

Allow the player to enter a run seed before starting. Display the active seed on the HUD. Same seed + same ruleset = reproducible game. Enables:
- Sharing interesting runs with friends
- The future tutorial (fixed seed)

### 5.7 Win / Lose Card Animations (Low Priority)

- **Lose:** All monster cards (clubs/spades) eject from the discard/room and bounce chaotically around the screen.
- **Win:** All potions and weapons bounce around instead.

Purely cosmetic; follows same pattern as the existing bounce animation system.

### 5.8 Multi-Resolution Support (Deferred — own PR)

Two independent problems:
1. **Test coordinate mismatch** at non-1080p: `GlobalPosition` (canvas space) ≠ `SimulateMouseMove` coords (screen space). Fix: `zone.GetViewport().GetFinalTransform() * canvasPos`.
2. **Layout behavior** at non-16:9: design decision needed on min/max panel widths and room scaling at ultrawide or narrow sizes.

### 5.9 Tutorial (Low Priority — depends on §5.5 + §5.6)

Guided walkthrough of one seeded run with annotations. Requires main menu entry point and seeded run support.

### 5.10 Endless Mode (Stretch)

When the deck runs out but the player is alive, reshuffle the discard into a new deck and continue. Track generation count. No win condition — play until death.

### 5.11 Multiplayer Co-op (Stretch)

Two players share one or two decks:
- Roll to decide who picks first each room
- Players alternate selecting cards
- Potentially larger room size (2×3 or 3×3)

---

## 6. Extended Rules (Extra Ruleset)

A toggleable alternative ruleset that adds new card types. All new cards need art ("pillow art" style matching existing deck). Implement incrementally, one card type at a time, with a toggle between Classic and Extended on the main menu.

### Implementation order

1. **Joker (Potion Pocket)** — Red Joker holds one potion card in a pocket slot. Player can store a potion here instead of drinking it, and retrieve it later.
2. **Joker (Weapon Pocket)** — Black Joker holds one weapon card. Can hold a weapon for later use and engage in combat only when the fight would deal 0 damage.
3. **Blacksmith (Diamond Face Cards)** — Removes slain monster cards from the equipped weapon, extending its usefulness:
   - Jack: remove 1 slain monster
   - Queen: remove 2
   - King: remove 3
   - Ace: remove all (and if weapon already has 0 attached, grants a rank-based attack bonus: J+1, Q+2, K+3, A+4)
4. **Merchant (Heart Face Cards)** — Sell your equipped weapon for HP:
   - HP gained = weapon value − attached monster count (min 1)
   - Bonus by face card: Jack +0, Queen +1, King +3
   - Ace of Hearts: sell for max value + 5 bonus
   - If unused, recycle to a random position in the deck (not the back)

**Designer notes:**
- Ace of Diamonds is treated as Excalibur — single-use (consumed on first fight) to prevent it being too OP.
- Red/Black Joker "pocket" framing: think of them as companions carrying extra inventory, not as cards in the room.
- If a blacksmith/merchant cannot or will not be used, they recycle into a random deck position (unlike run cards which go to the back).

---

## 7. Known Deferred Issues

| Issue | Status | Notes |
|-------|--------|-------|
| Multi-resolution layout | Deferred | Own PR; see §5.8 |
| Test coord mismatch at non-1080p | Deferred | Fix in §5.8 PR |
| Blur effect on zone highlights | Deferred | Requires shader work |

---

## 8. Technical Constraints

- **Architecture boundary:** GDScript handles all visuals/interaction (addon). C# handles all game logic. They communicate exclusively via Godot signals and `.Get()`/`.Call()`. No shared state, no direct coupling.
- **No addon modifications:** `addons/card-framework/` is read-only. All game-specific behavior lives in `scenes/` (GDScript) and `scripts/` (C#).
- **Tests ship with features:** Every implementation commit includes tests covering the new behavior. Unit tests (NUnit) for pure logic; scene tests (gdUnit4) for Godot integration.
- **Branch workflow:** All work on `feature/<topic>` or `fix/<topic>` branches. Never commit directly to main. Version bump required on every merge (except config-only updates).
- **Wolf runtime files are gitignored:** `memory.md`, `buglog.json`, `anatomy.md`, `cerebrum.md` are local-only and not committed.

---

## 9. Open Design Questions

1. **Potion-in-pocket (Joker) interaction with the one-potion-per-room rule:** Does retrieving a stored potion count as the room's one potion? (Claude Suggested: yes — the pocket is a convenience, not a loophole.) (Aileen: I'm not sure, in the past I allowed drinking one potion and storing one potion per room, but I'm not committed to that.)
2. **Merchant + weapon with 0 monsters:** Should you be able to sell an unused weapon? (Suggested: yes, gain the weapon value directly.)
3. **Room size for multiplayer:** 2×3 or 3×3? Or dynamically chosen at lobby start?
4. **Stats persistence format:** Local JSON file? SQLite? (Suggested: JSON for simplicity, migrate later if needed.)
5. **Run button placement after §5.1:** Below the deck, or adjacent to it? Red background + `!` icon confirmed — exact layout TBD.
