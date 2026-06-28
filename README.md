# Scoundrel

A Godot 4.7 / C# implementation of the solitaire card game [Scoundrel](https://www.youtube.com/watch?v=_nOSVELEI2Q). Built on top of the `addons/card-framework` GDScript addon (v1.4.0, MIT).

## Running the game

Open the project in the Godot editor and press **F5**, or from the command line:

```sh
godot --path /home/aileen/Repositories/godot/scoundrel-with-help
```

The main scene is `scenes/Game.tscn`.

### macOS

macOS quarantines downloaded binaries. Before running the exported app for the first time, strip the quarantine flag:

```sh
# Drag the app file onto the terminal to save keystrokes!
xattr -cr /path/to/Scoundrel.app
```

Then open it normally (double-click or `open /path/to/Scoundrel.app`).

## How to play

- Start at 20 HP. Die at 0.
- Each turn a room of 4 cards is dealt face-up. You must take at least 3 of them.
- **Monsters** (clubs / spades) deal damage equal to their value (A=14, 2–10=face, J=11, Q=12, K=13), reduced by your equipped weapon. Using a weapon degrades it — next use must be against a lower-value monster.
- **Weapons** (diamonds) equip on pickup, replacing the current weapon. Value = rank.
- **Potions** (hearts) restore HP equal to rank, capped at 20. Limit one per room.
- **Run**: skip the entire room and shuffle all 4 cards to the back of the deck. Cannot run two rooms in a row.

## Project layout

```
scenes/
  Game.tscn             main scene
  RoomContainer.gd      2×2 card grid; emits card_selected signal

scripts/
  ScoundrelGame.cs      Godot bridge — wires signals, drives visual card moves
  GameEngine.cs         pure C# game-state machine (no Godot dependency)
  ScoundrelRules.cs     pure game-logic functions (no Godot dependency)
  CardModel.cs          Godot-free card data record
  CardData.cs           CardData helper used by ScoundrelGame

card_data/              44 JSON card definitions (name, suit, rank)
card_assets/            44 card face PNGs + card_back.png

addons/card-framework/  GDScript visual/interaction framework (do not modify)
addons/gdUnit4/         gdUnit4 test runner addon
```

## Tests

There are two test suites.

### Unit tests (NUnit, no Godot required)

Tests for `GameEngine` and `ScoundrelRules` — pure C#, run with the standard .NET toolchain:

```sh
dotnet test tests/ScoundrelTests.csproj
```

### Scene integration tests (gdUnit4)

Tests that exercise the full Godot→C# signal chain (card clicks → `ScoundrelGame` → `GameEngine` → UI label updates). Requires a display and the Godot editor binary.

```sh
./run_scene_tests.sh
```

The script does three things in order:
1. `dotnet build` — compiles the C# project and copies NuGet DLLs to the output directory
2. `godot --import` — repopulates the GDScript class cache (required after every build)
3. Runs `GdUnitCmdTool.gd` against `res://scene_tests/`

Test file: `scene_tests/ScoundrelSceneTests.cs` (7 test cases).

You can also run a specific test directory:

```sh
./run_scene_tests.sh res://scene_tests/ScoundrelSceneTests.cs
```

The Godot binary path is hardcoded in `run_scene_tests.sh` — update it if yours differs:

```sh
GODOT=/usr/lib/godot-mono/godot.linuxbsd.editor.x86_64.mono
```
