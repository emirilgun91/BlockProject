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
- **`PlacementSystem`** (static) — `CanPlace` / `Place` against a `BoardModel`. Writes color + tile value from `PieceDefinition`, or from the optional `tileValueOverride` argument when runtime bonuses apply (see **Scoring rule**).
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

### Portrait (mobile) version

`Assets/Scenes/GamePortrait.unity` is **generated**, never hand-edited: **Tools ▸ RogueBlockBlast ▸ Scene Setup ▸ Build Portrait Scene** (`PortraitSceneBuilder`) copies `SampleScene` and converts it to a portrait layout. Rerunning it rebuilds the scene from the current `SampleScene`, so manual edits to the portrait scene are lost. Put permanent layout changes in the builder. The builder:

- sets the canvas to 1080×1920, matched to width;
- puts Combo, Score and Milestone side by side at the top;
- lays the pool out at the bottom with explicit positions. The pool's `HorizontalLayoutGroup` is removed because it overwrote those positions;
- moves the pieces-left counter (normally a child of `MilestoneBoard`) next to the pool;
- turns the card inventory into a single horizontal strip (`CardInventoryUI.HorizontalStrip`);
- adds `MobileControlsView` (rotate and pause buttons, icons drawn in code, no text);
- sets `TutorialController.TouchText` (uses `*.Touch` localisation keys) and `RunController.DragToPlace`;
- adds `PortraitCameraFit`, which fits the board to the screen width;
- disables physical-card hover lift and camera parallax;
- hides Quit;
- also adds `PortraitMenuLayout` to the MainMenu scene.

**Drag-to-place** (`RunController`): the ghost is shown only while the finger is down, `DragLift` (1.8 cells plus `GameSettings.DragOffsetY`) above the finger, and the piece is placed when the finger lifts. `TrackDragPress` runs *before* the input-lock early return. A touch "arms" placement only if it started while input was allowed and not on a `Selectable` or `ScrollRect` (pool slots are the exception). Without this, the touch that picked a card or pressed the rotate button placed a piece when it was released. Pool slots select on pointer **down**, so a drag can start from the slot.

**Main menu**: there is no separate scene, because it is build index 0 and a phone opens it first. `PortraitMenuLayout` re-lays out the same scene while `Screen.height > Screen.width` and restores the original layout when the screen is landscape again. `MainMenuController.ResolveGameplayScene` loads `GamePortrait` when the screen is portrait. `GameOverUI` retry reloads the *active* scene.

### Shape Shop from the Game Over screen

`GameOverUI` has an **Upgrade Now!** button (`BottomSection/UpgradeButton`, key `GameOver.UpgradeNow` in `MainMenu.csv`) that opens the Shape Shop on top of the Game Over panel, so the player can spend coins without leaving for the main menu. It is shown only while `ShapeShopOverlay.HasAffordableUpgrade` is true (a locked shape's unlock cost, a score upgrade, or a weight increase fits the balance; weight *decrease* does not count). It pulses gently (`DOScale`, `SetUpdate(true)`) and is re-evaluated every time the overlay closes, so it disappears once nothing is affordable.

- The panel is the prefab `Assets/Prefabs/ShapeShopPanel.prefab`, used by MainMenu (opened by `PanelManager`) and by `SampleScene` / `GamePortrait` (opened by `ShapeShopOverlay`, a component on the `Gameplay Canvas`; the panel instance starts inactive). Edit the prefab, not the instances. A prefab cannot hold a reference to a scene object, so the back button calls `ShapeShopController.OnBackButton`, which raises `BackRequested` if someone listens (the overlay) and otherwise falls back to `PanelManager.CloseCurrentPanel()`.
- `ShapeShopOverlay.Close` (back button or ESC) fades the panel out and raises `Closed`, which makes `GameOverUI` refresh the button; the Game Over screen is still there, so the player can press New Run or Main Menu.
- Game Over runs with `Time.timeScale = 0`, so every fade is `SetUpdate(true)`.
- `ShapeShopHeaderFit` (on the prefab's `Header`) resizes the title block to the real panel width. Its width was baked at 1920, so on the 1080-wide portrait canvas the coin balance was pushed off screen.
- Coins reach `CoinWallet` as soon as a milestone is reached, so the balance on the Game Over screen is current. Upgrades bought here apply from the next run (the scene reloads on New Run).
- `GamePortrait` is generated from `SampleScene`, so re-run **Build Portrait Scene** after changing the Game Over layout.

### Audio settings persistence

`GameSettings` is the only owner of persisted volumes (`MasterVolume`, `MusicVolume`, `SFXVolume`, `Set_Muted`). `AudioManager.SetMasterVolume/SetMusicVolume/SetSFXVolume` only drive the mixer and **must not write PlayerPrefs**. They used to, under the same keys: when the game window loses focus on quit, `SettingsBootstrap` ducks master to 0 (Mute When Minimized), and that 0 was saved, so the game started silent next time (muting did the same). `AudioManager.Start` now just calls `GameSettings.ApplyAudio()`.

### Mouse → placement anchor

`RunController.ResolvePlacementAnchor()` puts the centre of the piece's bounding box under the cursor. Before this, the piece's `(0,0)` cell sat under the cursor, so pieces landed up and to the right of where the player aimed, and rotating around `(0,0)` made them jump away. It works from `BoardView.TryGetMouseCellPoint`, which gives the cursor position in continuous cell units. The ray hits the *visible* tile surface (`SurfaceZ`, which includes the TileSkirt lift), not `z = 0`. The anchor is cell-based: the piece box's middle cell (`floor((min+max+1)/2)` per axis) sits on the cell under the cursor, so the piece steps exactly when the cursor crosses a cell border. An earlier version rounded the continuous box centre plus a 0.12 hysteresis, so pieces with an even size jumped mid-cell or near the cell's far edge. If that spot is blocked, the anchor snaps to the nearest valid anchor within `_placementSnapRadius` (default 1.05 cells). `BoardView.TryGetClampedCellUnderMouse` is no longer used by gameplay.

### Ghost preview & line-clear hint

The ghost is drawn in the piece's own colour, opaque and blended toward the empty-cell colour (`BoardView._ghostStrength`). It is flat: `TileView.IsGhostPreview` tells `TileSkirt` not to draw a side face, so it never reads as a placed block. An invalid placement turns the empty ghost cells grey and makes the overlapping cells blink red. Grey is used because a red warning would be indistinguishable from a red piece.

`RunController.UpdateLinePreview` highlights the rows and columns the current ghost would complete, but only after `_linePreviewDelay` seconds (default 4) have passed since the last placement. `LineHintOutline` (`UI/FX/`, a self-creating singleton, shader `Resources/FX/LineHintOutline`) draws a thin neon frame in the piece's colour around each of those lines. The frame settles into place with a small pop, and a light runs along it. Placed blocks are never recoloured: an earlier version tinted them toward white, and players read that as failure, not success. The frame draws nothing inside the line. Keep its HDR intensity moderate, because bloom from the frame spilling onto warm-coloured blocks greys them out. If `Show` is not called in a frame (pause, card pick), the frames hide themselves. The delay is deliberate: the hint should help a player who hesitates, not play the game for them. It never suggests a spot. It only reports what happens at the spot the cursor is on. It uses the same rule as `LineClearSystem` (`IsFilled` or covered by the ghost), and `DoPlace` changes nothing about fill state between `Place` and `ClearLines`, so the preview always matches the real clear. The timer runs on `Time.time`, so it stops while the game is paused or a card is being picked.

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

### Line Clear VFX

`LineClearVFX.Play` drives the per-tile flash and score popups, and hands the particle/shader layer to **`LineClearParticles`** (`UI/FX/`). That component needs no scene setup: `Instance` creates it on first use, loads its shaders from `Resources/FX/` (`LineClearParticle`, `LineClearBeam`, `LineClearRing`) and generates its textures in code. It holds five pre-built `ParticleSystem`s (shards, glow, embers, sparks, flares) fed through `Emit`, so a clear never instantiates anything. Beams and shockwave rings are pooled quads driven by `MaterialPropertyBlock`. Everything draws on the `Board` sorting layer at order ~400 and `z = -0.35` (in front of the TileSkirt-lifted tiles). Particle vertex colour is 8-bit, so the HDR brightness that feeds bloom comes from each material's `_Intensity`. Particle counts scale with `GameSettings.VfxIntensity`. Camera shake goes through `BoardCameraRig.AddTrauma`, because the rig rewrites the camera transform every frame; it scales with `GameSettings.ScreenShake`. `LineClearVFX._useParticleLayer = false` brings back the old `_tileBurstPrefab`.

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

## Settings, Localization & Meta Systems

### Settings (`RogueBlockBlast.Core.Settings`)

- **`GameSettings`** (static) — single source of truth for all settings, PlayerPrefs-backed, with `OnChanged` / `OnAudioChanged` / `OnDisplayChanged` / `OnAccessibilityChanged` events. Covers audio, display (fullscreen/resolution/vsync/fps), gameplay toggles, and accessibility. `ReduceMotion` overrides `ScreenShake` (returns 0) and caps `VfxIntensity`; the raw values stay readable via `ScreenShakeRaw` / `VfxIntensityRaw` so sliders keep their position.
- **`SettingsBootstrap`** — `[RuntimeInitializeOnLoadMethod]`, self-instantiating. Applies display settings before the first scene, applies audio once `AudioManager` exists, and ducks audio on focus loss.
- **`SaveDataService.ResetProgress()`** — wipes PlayerPrefs then re-persists settings (PlayerPrefs has no key enumeration, so "delete all, write settings back" is the only complete reset). Also clears the runtime caches of `CoinWallet` / `UpgradeRegistry` / `UnlockRegistry` / `ShapeUpgradeRegistry`.
- **`SettingsController`** — every serialized field is optional; unbound controls are silently skipped. Takes a snapshot on open so Cancel can restore.

### Localization

The game uses the **SimpleLocalization** asset at `Assets/SimpleLocalization/` (not a custom system, and not Unity Localization). CSVs live at `Assets/SimpleLocalization/Resources/Localization/{Game,MainMenu,Settings}.csv`.

- **`Loc`** — the facade over SimpleLocalization: language persistence (`GameSettings.Language`), system-language detection, next/previous cycling, `Get` / `GetOr` that return the key instead of throwing on a miss, and culture-correct `ToUpper` (Turkish `i → İ`).
- **`ContentLocalization`** — translations for ScriptableObject content via `Card.<id>.Name` / `Card.<id>.Desc`, `Upgrade.<id>.*`, `Shape.<id>.Name`. Falls back to the asset's own text when a key is missing, so content can be translated incrementally.
- **`ValueHighlighter`**: makes the numeric values in card text (`+6`, `20%`, `%20`, `2.5x`, `×1.5`, `1.5 倍`, `0,02`) bold and coloured through TMP rich text. Values with an explicit minus get the penalty colour, everything else the value colour. It runs when the text is displayed (`Cardview`, `CardtTooltip`), not in the CSVs, so every language and every future card is covered with no markup. Digits attached to letters are left alone because they are shape names (`I3`, `4Gen`), and the dash in a range (`2-3`) is not treated as a minus. Always apply it to the raw text; applying it twice wraps the numbers twice.
- **`LocFiller`** (editor) — bulk-writes CSV cells; preserves existing rows and never overwrites a filled cell unless `overwrite: true`.

**Rule: every new player-facing string ships translated in all 15 supported languages.** Key naming is `Section.Element` PascalCase.

Arabic and Traditional Chinese were removed from the CSVs. Arabic needs contextual glyph shaping, the Unicode bidi algorithm and a mirrored UI — TMP's `isRightToLeftText` alone does not deliver that, so shipping the column meant offering a broken option. Traditional Chinese was dropped as a product decision; those systems fall back to Simplified in `Loc.DetectSystemLanguage`.

Non-Latin scripts render through a fallback chain (Exo 2 → Inter → Noto JP/KR/SC) set in TMP Settings. `CjkFontFallback` reorders the CJK part of that chain when the language changes, because TMP's fallback is global and first-match-wins while Japanese and Chinese need different regional glyph variants for shared Han characters.

### Tutorial & HUD affordances

- **`TutorialEvents`** (static) — one-way hub. `RunController` reports what happened (`PiecePlaced`, `Rotated`, `ComboChanged`, `MilestoneReached`, `CardPicked`); it never references the tutorial. No subscriber → no cost.
- **`TutorialController`** (Gameplay Canvas) — scenario-driven first-run tutorial. A single top-centre line states the next goal and advances when the real game event fires; nothing is modal and nothing pauses. Steps live in the `Scenario` array (place → rotate → clear → combo → milestone → card → dead pool). Builds its own UI, so adding the component is the whole setup. Gated by PlayerPrefs `Tutorial_Completed`; `SaveDataService.ResetProgress()` clears it with everything else.
- **`RotateHintView`** (Gameplay Canvas) — bottom-left "Q / E · Döndür" chip; the key cap flashes on the real key press. Builds its own UI.
- **`TutorialIntroTour`** (`Game/Tutorial/`) — one-time UI tour shown before the banner scenario. It dims the screen, cuts a spotlight hole around the real panel, draws a pulsing frame and puts a card next to it. Four stops: shape pool (slots + reroll), target score + pieces-left counter (two holes), combo multiplier, rotate keys (the `RotateHint` chip, or `RotateButton` in portrait with the key caps hidden). `TutorialController.Start` creates it at runtime 0.8 s after the scene opens, so no scene setup is needed and `GamePortrait` is covered automatically. PlayerPrefs `Tutorial_Intro_Completed`, cleared by `TutorialController.ResetProgress()`. Players who finished the old banner tutorial still see the tour once. Input is locked (`GameStateController`) while it runs and unlocked in `Finish` / `OnDestroy`. Targets are resolved by component or name at the moment a stop is shown (`PoolView` slots, `MilestoneView` + its `PiecesRect`, `ComboView`, `RotateHint`, `RotateButton`). Do not use `PoolPanel`'s own rect: it covers the whole canvas. The dim is a custom `Graphic` (`HoleGraphic`) that draws horizontal strips around the holes. It needs a `CanvasRenderer`, or nothing is drawn. The card goes next to the largest hole first, never over a hole, and above it when the target is near the bottom edge. Strings are `Tutorial.Intro.*` in `Game.csv` (the rotate stop also has `.Touch`).
- **`BoardClearanceFitter`** (CardInventoryUI) — keeps a left-anchored HUD panel out of the board's screen rect. Needed because the board is world-space (moves with `1/aspect`) while the canvas scales with `1/sqrt(aspect)`: at 16:9 the inventory ended a few pixels short of the board, so any aspect change put it on top. The panel is scaled from the board's *actual* screen edge instead of a fixed margin. Expects the panel's anchor and pivot to be left-aligned so its left edge stays put and the scale can't feed back.

### Meta / Debug

- **`PauseMenuController`** (game scene) — ESC opens it: Resume / Settings / Main Menu / Quit. Sets `timeScale = 0` and locks input. **The controller must live on a GameObject that stays active** and toggle a child container — putting it on the object it disables kills its own `Update()`.
- **`DebugPanel`** — F1, IMGUI, `#if UNITY_EDITOR || DEVELOPMENT_BUILD` only, self-instantiating. Injects any card through the real `OnCardPicked` path, plus board/pool/score/coin actions and a live card-state readout.
- **`ExternalLinkButton`** — opens http/https links only; disables itself when the URL is empty or invalid.
- **`UpgradeCurve`** — per-level value curve (`Steps` + `Multiplier`/`Increment` + `MaxValue`/`MaxLevel`) used by `ShapeSO.ScoreGain` / `ScoreCost`. Score gain is **cumulative**: level 3 with steps `3,5,7` adds +15, not +7. Empty curve → the old flat formula.
- **Editor tools** live under `Tools ▸ RogueBlockBlast ▸` (save data, localization validation, shape upgrade curves, settings panel builder, pause menu builder, card inventory layout). The panel/layout builders regenerate UI from code, so hand edits to those objects are lost on the next run.

## Scoring rule

**Points are only awarded when lines clear.** Every bonus that scales with tiles is therefore written into the tile's own value at placement time, never added to `gainedScore` separately:

- Shape-card bonus → `effectiveTileValue = piece.TileValue + GetScoreBonus(pieceId)`, passed to `PlacementSystem.Place`.
- Corner Stone / Center Base → `ApplyPositionBonusToPlacedCells` rewrites the affected cells after placement (the bonus is per-cell, not per-piece).

This keeps four displays in sync from one source: the empty-cell `+N` hint, the ghost preview, the value stored on the board, and the number that flies to the scoreboard on a clear. Adding a bonus to `gainedScore` as well would double-count it.

## Soft-lock protection

`RunController.CheckForSoftLock()` runs every 0.4 s while input is allowed and calls `HandleDeadPool()` if no valid move exists. It exists because anything that mutates the board outside `DoPlace` (Decaying Rift closing a cell, future cards) can strand the player with no legal move and no game over. `HasAnyValidMoveInPool()` tries only rotation R0 while `IsRotationLocked` (First Picks), since the player cannot rotate then. `OnGameOver` is guarded by `_gameOverFired` so it can only fire once per run.

## Key Invariants

- `ShapeCardEffectRegistry.Reset()` must be called at `RunController.NewRun()` — effects don't persist across runs.
- `ComboSystem.Reset()` must be called after setting `BaseMultiplier` and `MaxCharge` (upgrade values), since `Reset()` sets `Multiplier = BaseMultiplier`.
- `LineClearSystem` takes `TileSnapshot`s *before* clearing — VFX reads pre-clear colors/values.
- Scores are only awarded when lines clear. Tile-scaled bonuses (shape card, Corner Stone, Center Base) are written into the tile value at placement and must NOT also be added to `gainedScore` — see **Scoring rule**.
- All upgrade effects are read once per `NewRun` from `UpgradeRegistry` and stored as local fields in `RunController` — they don't change mid-run.
- Cards whose text says "During this Milestone" (First Picks, Diet Plan) must be turned **off** in `RunCardState.OnMilestoneReached()`, not merely reset. That method runs *before* card selection, so a card picked now survives exactly one milestone. Decaying Rift's dead zones are cleared at the same point.
- `OnGameOver` re-checks `HasAnyValidMoveInPool()` before it fires, for every reason except `PoolExhausted`. "No moves" is decided at several points in the placement chain, and cards that change the board *after* placement (Selective Blindness removing random blocks, Neon Cable exploding an area, Decaying Rift) can open room in between — the re-check is what stops a game over on a board that still has a legal move.
- `PauseMenuController` must not sit on the GameObject it hides — its `Update()` would stop and ESC would die.
- Every new player-facing string goes through `Loc` / `ContentLocalization` and ships translated in all 15 languages. Every language also needs a `Language.<Name>` row in `Settings.csv` — without it the picker shows the raw key.
