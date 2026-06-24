# Memory

> Chronological action log. Hooks and AI append to this file automatically.
> Old sessions are consolidated by the daemon weekly.

## Session: 2026-06-24 00:28

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| 00:53 | Edited scripts/ScoundrelGame.cs | expanded (+12 lines) | ~318 |
| 00:53 | Edited scripts/ScoundrelGame.cs | expanded (+6 lines) | ~304 |
| 00:53 | Edited scripts/ScoundrelGame.cs | 10→15 lines | ~121 |
| 00:53 | Edited scripts/ScoundrelGame.cs | modified DealRoom() | ~47 |
| 00:53 | Edited scripts/ScoundrelGame.cs | modified switch() | ~224 |
| 00:54 | Edited scripts/ScoundrelGame.cs | modified EquipWeapon() | ~115 |
| 00:54 | Edited scripts/ScoundrelGame.cs | modified UpdateUI() | ~245 |
| 00:54 | Edited scripts/ScoundrelGame.cs | added 1 condition(s) | ~287 |
| 00:55 | Edited scenes/Game.tscn | expanded (+41 lines) | ~362 |
| 00:55 | Edited scripts/ScoundrelGame.cs | modified MoveToDiscard() | ~58 |
| 00:55 | Edited scripts/ScoundrelGame.cs | modified foreach() | ~205 |
| 00:57 | Session end: 11 writes across 2 files (ScoundrelGame.cs, Game.tscn) | 11 reads | ~21235 tok |

## Session: 2026-06-24 03:20 — resume

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| 03:20 | Added suit-count tracking (_inPlayClubs/Spades/Hearts/Diamonds) | ScoundrelGame.cs | Decremented on discard (fight, potion, old weapon replaced); initialized in StartGame | ~500 |
| 03:20 | Added deck/discard count labels to UI | ScoundrelGame.cs, Game.tscn | DeckLabel/DiscardLabel now show live "(N)" counts; 4 suit-count labels added below weapon slot | ~400 |
| 03:22 | Added potion visual feedback | ScoundrelGame.cs | TintRemainingPotions() grays out remaining ♥ cards after first use; resets on new room and on discard/run | ~300 |
| 03:25 | Updated .wolf/anatomy.md + cerebrum.md | .wolf/ | anatomy: scenes/ scripts/ entries updated; cerebrum: key learnings on Pile API, modulate trick, suit tracking | ~200 |
| 00:57 | Session end: 11 writes across 2 files (ScoundrelGame.cs, Game.tscn) | 11 reads | ~21235 tok |
| 01:16 | Created scripts/ScoundrelRules.cs | — | ~446 |
| 01:16 | Edited scripts/CardData.cs | modified MonsterValue() | ~124 |
| 01:17 | Edited scripts/ScoundrelGame.cs | modified ApplyMonsterDamage() | ~122 |
| 01:17 | Edited scripts/ScoundrelGame.cs | Min() → Heal() | ~68 |
| 01:18 | Edited scripts/ScoundrelGame.cs | 3→2 lines | ~35 |
| 01:18 | Edited scripts/ScoundrelGame.cs | inline fix | ~7 |
| 01:18 | Created tests/ScoundrelTests.csproj | — | ~193 |
| 01:19 | Created tests/ScoundrelRulesTests.cs | — | ~1244 |
| 01:20 | Edited ScoundrelWithHelp.sln | 3→5 lines | ~84 |
| 01:20 | Edited ScoundrelWithHelp.sln | 3→7 lines | ~146 |
| 01:21 | Unit tests: 30/30 passed (dotnet test) | tests/ | ScoundrelRules.cs extracted as Godot-free core; NUnit test project wired into sln |
| 01:21 | Session end: 21 writes across 7 files (ScoundrelGame.cs, Game.tscn, ScoundrelRules.cs, CardData.cs, ScoundrelTests.csproj) | 14 reads | ~24324 tok |

## Session: 2026-06-24 01:21

| Time | Action | File(s) | Outcome | ~Tokens |
|------|--------|---------|---------|--------|
| 01:22 | Edited .gitignore | 5→9 lines | ~24 |
| 01:22 | Session end: 1 writes across 1 files (.gitignore) | 1 reads | ~39 tok |
| 01:22 | Session end: 1 writes across 1 files (.gitignore) | 1 reads | ~39 tok |
| 01:27 | Created scripts/CardModel.cs | — | ~126 |
| 01:27 | Created scripts/CardData.cs | — | ~208 |
| 01:27 | Edited scripts/ScoundrelGame.cs | inline fix | ~11 |
| 01:28 | Edited scripts/ScoundrelGame.cs | inline fix | ~17 |
| 01:28 | Edited scripts/ScoundrelGame.cs | inline fix | ~14 |
| 01:28 | Edited scripts/ScoundrelGame.cs | 2→2 lines | ~45 |
| 01:28 | Edited scripts/ScoundrelGame.cs | inline fix | ~13 |
| 01:29 | Created scripts/GameEngine.cs | — | ~1348 |
| 01:29 | Edited tests/ScoundrelTests.csproj | 5→6 lines | ~73 |
| 01:32 | Created tests/GameEngineTests.cs | — | ~4864 |
| 01:33 | Edited ScoundrelWithHelp.csproj | 2→5 lines | ~26 |
| 01:33 | Session end: 12 writes across 8 files (.gitignore, CardModel.cs, CardData.cs, ScoundrelGame.cs, GameEngine.cs) | 3 reads | ~7674 tok |
