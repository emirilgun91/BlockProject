# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**RogueBlockBlast** — a Unity 2D rogue-like block-blast puzzle game. Players place Tetris-style pieces on an 8×8 grid, clear rows/columns for score, and earn cards + upgrades between runs.

- **Unity URP 2D**, render pipeline: `com.unity.render-pipelines.universal` 17.3.0
- **Input**: Unity Input System 1.18.0 (keyboard + mouse)
- **Animation**: DOTween
- **Scenes**: `Assets/Scenes/MainMenu.unity` (meta-loop / upgrades) and `Assets/Scenes/SampleScene.unity` (gameplay)

## Development Commands

This is a Unity project — all build/run actions go through the Unity Editor. There is no CLI build script.

- **Run game**: Open the project in Unity, open `SampleScene`, press Play
- **Run tests**: Window → General → Test Runner (Unity Test Framework `1.6.0` is in manifest)
- **Reset all save data** (PlayerPrefs): Call `UpgradeRegistry.Instance.ResetAll(...)` and `UnlockRegistry.Instance.ResetAll(...)` from a debug menu, or use Unity's `PlayerPrefs.DeleteAll()` in a one-off script

## Namespace & Folder Structure

| Namespace | Folder | Purpose |
|---|---|---|
| `RogueBlockBlast.Core` | `Assets/Scripts/Core/` | Pure C# game logic — no Unity/MonoBehaviour dependencies |
| `RogueBlockBlast.Game` | `Assets/Scripts/Game/` | Scene-level orchestration and MonoBehaviours |
| `RogueBlockBlast.Content` | `Assets/Scripts/Content/` | ScriptableObject data definitions |
| `RogueBlockBlast.UI` | `Assets/Scripts/UI/` | UI MonoBehaviours |

## Architecture

### Core Layer (pure C#, no MonoBehaviour)

These systems hold no Unity lifecycle and can be `new`'d freely:

- **`BoardModel`** — 8×8 grid storing `bool[,]` (filled), `Color[,]`, and `float[,]` (tile value per cell). Source of truth for board state.
- **`PlacementSystem`** (static) — `CanPlace` / `Place` against a `BoardModel`. Writes color + tile value from `PieceDefinition`.
- **`LineClearSystem`** (static) — Finds full rows/cols, collects `TileSnapshot[]` *before* clearing (for VFX), then clears. Returns `(totalCleared, tileValueSum, clearedRows[], clearedCols[], snapshots)`.
- **`ScoreSystem`** — `tileValueSum × comboMultiplier × globalMultiplier`, rounded to int.
- **`ComboSystem`** — Charge bar (default max 3). Line clear → charge up + multiplier increase; no-clear placement → charge down; charge hits 0 → multiplier resets to `BaseMultiplier`. Fires `OnStateChanged(ComboState)`, `OnComboReset`, `OnMaxCharge`. UI subscribes to `OnStateChanged`.
- **`MilestoneSystem`** — Dual-gating: score threshold AND piece count window. Each milestone reached fires `OnMilestoneReached(coins, MilestoneData)` and resets the piece counter. Pool exhausted without hitting threshold fires `OnPoolLimitExhausted`. UI subscribes to `OnProgressChanged(MilestoneProgressState)`.

### Persistent Singletons (`DontDestroyOnLoad` MonoBehaviours)

These live across scene loads and use PlayerPrefs for persistence. Always accessed via null-conditional (`Instance?.Method()`):

- **`CoinWallet`** — Balance, `Earn/Spend/CanAfford`. Fires `OnBalanceChanged`.
- **`UpgradeRegistry`** — Upgrade levels (buy/sell), PlayerPrefs key `Upgrade_Level_{id}`. `GetEffect(upgradeSO)` returns `level × EffectValuePerLevel`.
- **`UnlockRegistry`** — Shape/card/milestone unlock state, PlayerPrefs keys `Unlocked_Card_{id}`, `Unlocked_Shape_{id}`, `MilestoneFirstReach_{label}`.
- **`ShapeCardEffectRegistry`** — Run-scoped registry of active shape-card effects (score bonus, weight delta per shape). **Must be `Reset()` at `NewRun`** — `RunController` does this.
- **`AudioManager`** — `PlayMusic` / `PlaySFX`.

### Game Orchestration

- **`RunController`** (MonoBehaviour, game scene) — Central wiring point. Instantiates `BoardModel`, `ScoreSystem`, `ComboSystem`, `MilestoneSystem` each run. Handles the full frame loop: mouse → `PlacementSystem` → `LineClearSystem` → `ComboSystem` → `ScoreSystem` → `MilestoneSystem` → pool management → dead-pool → game-over.
- **`GameStateController`** (static) — Reference-counted input lock (`LockInput/UnlockInput`). `RunController.Update()` gates on `InputAllowed`. UI panels call Lock on open, Unlock on close.
- **`PieceFactory`** (static) — `Create(ShapeSO)` → `PieceDefinition`. Calls `ShapeSO.GetCurrentTileValue()` which reads `ShapeUpgradeRegistry` for the upgrade-adjusted tile value.
- **`CardEffectApplier`** (static) — `Apply(card, comboSystem, milestoneSystem, ref deadPoolRerolls, ref globalScoreMultiplier, ref coinBonusPerMilestone)`. Shape cards bypass the switch and go to `ShapeCardEffectRegistry.Register(card)` instead.

### Content (ScriptableObjects)

- **`ShapeSO`** — Shape definition: `List<Vector2Int> Cells` (R0 layout), `BaseWeight`, `BaseTileValue`, `BlockColorPreset`, lock/upgrade cost fields.
- **`ShapeLibrarySO`** — `List<ShapeSO>` — the full shape pool.
- **`CardSO`** — Card definition: `List<CardEffect>` (each has `CardEffectType` + `float Value`), `IsShapeCard` flag, `TargetShapeId`, `List<ShapeEffectEntry>`. `IsUnlocked` property checks `UnlockRegistry` and `RequiredShapeId` shape unlock.
- **`UpgradeSO`** — `UpgradeEffectType`, `MaxLevel`, `int[] CostPerLevel`, `float EffectValuePerLevel`. Effect IDs used in `RunController`: `upgrade_pool_capacity`, `upgrade_combo_bar`, `upgrade_starting_combo`, `upgrade_combo_gain`, `upgrade_pool_reroll`, `upgrade_card_reroll`, `upgrade_coin_gain`, `upgrade_dead_pool_revive`.
- **`MilestoneConfigSO`** — Array of `MilestoneData` (score threshold, coin reward, label) and `PoolLimit`.

### Shape Spawn Flow

```
ShapeSpawnService.GetRandomWeighted(library)
  → filters unlocked shapes
  → base weight from ShapeUpgradeRegistry (weight upgrades)
  → delta from ShapeCardEffectRegistry (card weight effects)
  → weighted random pick → PieceFactory.Create(shapeSO) → PieceDefinition
```

### Data Flow on Piece Placement

```
RunController.DoPlace(anchor)
  PlacementSystem.Place(board, piece, anchor, rot)
  LineClearSystem.ClearLines(board)
    → comboSystem.OnLineClear(cleared)
    → LineClearVFX.Play(snapshots, ...)
  comboSystem.OnPlacement(hadClear)
  scoreSystem.ResolveAfterPlacement(tileValueSum, combo, global)
  + ShapeCardEffectRegistry score bonus (per tile × combo × global)
  milestoneSystem.OnScoreChanged(score)
  pool.RemoveAt(selectedIndex) → GenerateNewPool or SpawnNextFromPool
  HasAnyValidMoveInPool() → HandleDeadPool or milestoneSystem.OnPiecePlaced()
```

### Milestone → Card Selection Flow

```
MilestoneSystem.OnMilestoneReached(coins, data)
  → RunController.HandleMilestoneReached
      CoinWallet.Earn(coins × multiplier)
      UnlockRegistry.IsFirstMilestoneReach → maybe unlock a locked card
      CardSelectionUI.Show(cardPool, OnCardPicked, newlyUnlockedCard, cardRerolls)
        player picks → CardEffectApplier.Apply(card, ...)
          IsShapeCard → ShapeCardEffectRegistry.Register(card)
          else → modifies comboSystem / globalScoreMultiplier / deadPoolRerolls / coinBonus
        CardInventoryUI.AddCard(card)
```

### Dead Pool Handling (priority order)

1. `upgrade_dead_pool_revive` upgrade charges (up to level times per run)
2. `_freeDeadPoolReroll` (1 free reroll per run, currently initialized to 0 in `NewRun`)
3. `_cardDeadPoolReroll` charges from `ExtraDeadPoolReroll` card effect (costs 300 score)
4. `OnGameOver(NoMoves)`

### UI Pattern

UI components are bound to systems, not polled. `ComboView.Bind(comboSystem)` subscribes to `comboSystem.OnStateChanged`; `MilestoneView.Bind(milestoneSystem)` subscribes to `milestoneSystem.OnProgressChanged`. State passed as readonly structs (`ComboState`, `MilestoneProgressState`) — UI never holds mutable system references.

`BoardView.Render(board, ghostCells, ghostTileValue)` is called every frame from `RunController.Update()`.

## Key Invariants

- `ShapeCardEffectRegistry.Reset()` must be called at `RunController.NewRun()` — effects don't persist across runs.
- `ComboSystem.Reset()` must be called after setting `BaseMultiplier` and `MaxCharge` (upgrade values), since `Reset()` sets `Multiplier = BaseMultiplier`.
- `LineClearSystem` takes `TileSnapshot`s *before* clearing — VFX reads pre-clear colors/values.
- Scores are only awarded when lines clear (`tileValueSum > 0`); shape card score bonus also only applies when `cleared > 0`.
- All upgrade effects are read once per `NewRun` from `UpgradeRegistry` and stored as local fields in `RunController` — they don't change mid-run.
