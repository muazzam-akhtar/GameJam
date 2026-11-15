# Angel & Evil Reactions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Slapped Angel/Evil play MainChar's stung reaction with 3 orbiting dizzy stars (gold / purple); on power-up the Angel plays MainChar's jump-twirl and the Evil flares up (bigger, brighter flames, fiery glow, particles, light).

**Architecture:** An editor builder retargets MainChar's `Stung`/`PowerUp` clips bone-by-bone (deltas in character-root space, scaled by height) onto each rig, layered over its Fly loop, and rewires both animator controllers. Runtime components `DizzyStars` and `EvilPower` own the effects; `AngelController`/`EvilController` implement a shared `ICharacterActions` (`Slap/Stung/PowerUp`) that the teammates' slap and pickup code call.

**Tech Stack:** Unity 6000.3.23f1, URP 17.3, Input System, Unity Test Framework 1.6 (EditMode, `Assets/Tests/Editor`, compiled into Assembly-CSharp-Editor), live Editor driven by `unity command script eval` / `unity command test run`.

**Spec:** `docs/superpowers/specs/2026-11-14-angel-evil-reactions-design.md`

## Global Constraints

- The Unity Editor is the user's live, shared Editor: before any asset/prefab write, check `EditorApplication.isPlayingOrWillChangePlaymode == false`; never open, modify or save `DemoScene.unity`; previews use `EditorSceneManager.NewPreviewScene()`.
- `Evil.prefab` stays self-contained: new Evil clips are sub-assets of `Evil.prefab` (its controller already is). Angel clips are files next to `Angel.fbx`.
- Rig bone sets are fixed; add no bones (user preference: minimal rigs).
- Durations: Stung clip 0.8 s, PowerUp clip 1.0 s (MainChar's), dizzy stars 2.5 s, Evil power ramp 0.25 s / hold 4 s / fade 1.2 s, flame scale ×1.7.
- Colors: Angel stars gold (HDR ~ `(2.6, 1.9, 0.5)`), Evil stars purple (HDR ~ `(1.5, 0.45, 2.6)`), Evil power glow fiery (HDR ~ `(2.4, 0.9, 0.25)`).
- Test keys (Input System `Key`): Angel `Digit1/2/3`, Evil `Digit8/9/0` = slap / stung / power-up; nothing on Space. New serialized names `testSlapKey`, `testStungKey`, `testPowerUpKey` (so the scene's stale `stungKey` override stays inert).
- No commits/pushes until the user asks.

## Review Focus

- Slap while the victim is already dizzy → reaction restarts cleanly, stars timer resets, no duplicate stars.
- Power-up while the Evil is already powered → hold timer restarts without dropping to 0 (no flicker).
- Characters are scaled ×10 in DemoScene → stars/particles/light are children of the character so they scale with it.
- 360° twirl sampled into quaternion curves → no sign flips between keys (no backwards spin pop).
- Angel `PowerUp()` during `Stung` (or vice-versa) → AnyState trigger wins, returns to Idle afterwards, stars keep orbiting.

---

### Task 1: Retarget math

**Files:**
- Create: `Assets/Scripts/Characters/Editor/ReactionRetarget.cs`
- Test: `Assets/Tests/Editor/ReactionRetargetTests.cs`

**Interfaces:**
- Produces:
  - `public static Quaternion ReactionRetarget.TransferRotation(Quaternion srcRest, Quaternion srcNow, Quaternion srcParentRootRot, Quaternion dstParentRootRot, Quaternion dstBase)` — delta `srcNow·srcRest⁻¹` taken from src parent space to root space, into dst parent space, applied on top of `dstBase`.
  - `public static Vector3 ReactionRetarget.TransferOffset(Vector3 srcDeltaParentSpace, Quaternion srcParentRootRot, Vector3 srcParentRootScale, Quaternion dstParentRootRot, Vector3 dstParentRootScale, float heightRatio)`.
  - `public static void ReactionRetarget.MakeContinuous(Quaternion[] samples)` — flips sign so consecutive dot ≥ 0.

- [ ] **Step 1: Write failing tests** `ReactionRetargetTests`:
  - `ZeroDelta_ReturnsDstBase`: `srcNow == srcRest` → result ≈ `dstBase` (angle < 0.01°).
  - `WorldYawDelta_TransfersAcrossDifferentParents`: src parent `Euler(-90,0,0)`, dst parent `Euler(0,0,90)`, src delta = 90° about root Y → dst bone's root-space rotation changes by 90° about root Y.
  - `Offset_ScalesByHeightAndParent`: src parent space up 0.1 with parent rot `Euler(-90,0,0)`, heightRatio 1.5 → dst root-space offset (0, 0.15, 0).
  - `MakeContinuous_RemovesSignFlips`: alternating `q, -q` → all consecutive dots ≥ 0.
- [ ] **Step 2:** Run `unity command test run` filtered to `ReactionRetargetTests` → FAIL (type missing).
- [ ] **Step 3:** Implement `ReactionRetarget`.
- [ ] **Step 4:** Run tests → PASS.

### Task 2: Reaction clips + controller rewiring

**Files:**
- Create: `Assets/Scripts/Characters/Editor/ReactionClipBuilder.cs`
- Generated: `Assets/Prefabs/Characters/Angel/Angel_Stung.anim`, `Angel_PowerUp.anim`; `Stung` clip sub-asset in `Evil.prefab`
- Modify (generated): `Assets/Prefabs/Characters/Angel/Angel.controller`, Evil controller sub-asset
- Test: `Assets/Tests/Editor/ReactionClipTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces: menu `GameJam/Characters/Build Reaction Clips` → `public static void ReactionClipBuilder.BuildAll()`; animator triggers `Slap`, `Stung` (both), `PowerUp` (Angel only); states `Idle`, `Slap` (existing Slap clip), `Stung`, `PowerUp` (Angel).

Bone maps (MainChar → target): Angel `body→body, head→head, arm.L→hand.L, arm.R→hand.R`; Evil `body→body, arm.R→hand.R`. Source rest = MainChar `Idle` at t=0; target base = target `Fly` sampled at the same time (looped), so unmapped bones keep flying. Body position delta × (target height / MainChar height, from renderer bounds); body scale: vertical factor on the target axis most aligned with root up, horizontal on the other two. Evil secondary motion in `Stung`: `lantern` gets the body's root-space delta reversed ×0.8 delayed 0.08 s; `coat.*.1` 0.6× and `coat.*.2` 0.35× of the body delta delayed 0.12 s; flames untouched. Sample 60 fps, `MakeContinuous` per rotation curve. Transitions: AnyState→state on its trigger (no self-transition, 0.08 s), state→Idle at exit time 0.92, 0.12 s.

- [ ] **Step 1: Write failing tests** `ReactionClipTests`:
  - `Angel_Controller_HasSlapStungPowerUp`: triggers {Slap, Stung, PowerUp}; state `Slap`→clip `Slap`, `Stung`→`Angel_Stung` (0.8 s ±0.02), `PowerUp`→`Angel_PowerUp` (1.0 s ±0.02); no trigger named Stung drives the Slap state.
  - `Evil_Controller_HasSlapAndStung`: triggers {Slap, Stung}; `Stung` clip is a sub-asset of `Evil.prefab`, 0.8 s.
  - `Clip_Bindings_ResolveOnPrefab` (each new clip): every binding path exists under the prefab root.
  - `Angel_PowerUp_TurnsFullCircle`: body root-space yaw accumulated over the clip ≥ 350°.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement `ReactionClipBuilder`; run `BuildAll()` via `script eval` (after the play-mode check).
- [ ] **Step 4:** Run tests → PASS.
- [ ] **Step 5: Visual check:** render Angel and Evil at Stung t = 0, 0.15, 0.3, 0.5, 0.8 and Angel PowerUp t = 0, 0.25, 0.5, 0.75, 1.0 in a preview scene, next to MainChar at the same times; inspect PNGs; tune and rebuild until the motion matches MainChar's character.

### Task 3: Dizzy stars

**Files:**
- Create: `Assets/Scripts/Characters/DizzyStars.cs`
- Create: `Assets/Scripts/Characters/Editor/CharacterFxBuilder.cs` (stars part)
- Generated: `Assets/Prefabs/Characters/FX/DizzyStar.asset` (mesh), `MI_DizzyStar_Gold.mat`, `MI_DizzyStar_Purple.mat` (+ `_Glow` GlowSource variants); `DizzyStars` child on `Angel.prefab` (anchored to `head`) and `Evil.prefab` (anchored to `body`, above the hood)
- Test: `Assets/Tests/Editor/DizzyStarsTests.cs`

**Interfaces:**
- Produces: `public class DizzyStars : MonoBehaviour { public float duration = 2.5f; public bool Visible { get; } public void Show(); public void Hide(); public void Tick(float dt); }` — 3 child stars orbiting (≈220°/s), each spinning and bobbing; pop-in 0.18 s with overshoot, pop-out 0.2 s; `Update` calls `Tick(Time.deltaTime)`. Menu `GameJam/Characters/Build Character FX` → `CharacterFxBuilder.BuildAll()`.
- Star mesh: puffy rounded 5-point star (≈0.22 local units across before character scale), smooth normals.

- [ ] **Step 1: Write failing tests** `DizzyStarsTests`:
  - `Show_ActivatesThreeStars` and `Tick_PastDuration_HidesStars` (after 2.5 + 0.2 s none visible).
  - `Show_WhileVisible_ResetsTimer_NoExtraStars` (Review Focus 1).
  - `Prefabs_HaveStars_InTheirColor`: Angel stars material color is gold-ish (r>g>b, b<0.6·r), Evil purple-ish (b>r>g).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement `DizzyStars` + stars part of the builder; run it.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5: Visual check:** preview render of both characters with stars mid-orbit (incl. glow); tune size/height/radius.

### Task 4: Evil power-up FX

**Files:**
- Create: `Assets/Scripts/Characters/EvilPower.cs`
- Modify: `Assets/Scripts/Characters/Editor/CharacterFxBuilder.cs` (Evil power part)
- Generated: `MI_Evil_PowerGlow.mat` (GlowSource, 3rd material on the Evil renderer → renders the body submesh), `MI_Evil_PowerFlame.mat` / `MI_Evil_Ember.mat` (`GameJam/SoftGlowParticle`), children `PowerFlames`, `PowerEmbers` (particle systems), `PowerLight` on `Evil.prefab`
- Test: `Assets/Tests/Editor/EvilPowerTests.cs`

**Interfaces:**
- Produces: `public class EvilPower : MonoBehaviour { public float Power { get; } public bool IsPowered { get; } public void Activate(); public void Tick(float dt); }` — envelope 0.25 / 4 / 1.2 s; applies in `LateUpdate` (after the Animator): flame bones `flames`, `flame.01-15` local scale × `1 + 0.7·Power`, flame material colors × `1 + 0.8·Power` via MaterialPropertyBlock, GlowSource intensity `1.2·Power`, light intensity `3·Power`, particle emission ∝ Power, body swell `1 + 0.08·sin(πt/0.35)` during the first 0.35 s.

- [ ] **Step 1: Write failing tests** `EvilPowerTests`:
  - `Activate_RampsToFull_HoldsThenFades`: Power≈0 at start, 1 after 0.25 s, 1 at 4.0 s, 0 after 4.25+1.2 s.
  - `Activate_WhilePowered_RestartsHold_NoDip` (Review Focus 2): re-activate at 3 s → Power stays 1 until 7.25 s.
  - `Prefab_EvilPower_Wired`: references to renderer, 16 flame bones, light and both particle systems are set.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement `EvilPower` + builder part; run it.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5: Visual check:** preview render of the Evil at Power 0 and 1 (with bloom); tune.

### Task 5: Character API + test keys

**Files:**
- Create: `Assets/Scripts/Characters/ICharacterActions.cs`
- Modify: `Assets/Scripts/Characters/AngelController.cs`, `Assets/Scripts/Characters/EvilController.cs`
- Test: `Assets/Tests/Editor/CharacterActionsTests.cs`

**Interfaces:**
- Consumes: Tasks 2-4.
- Produces: `public interface ICharacterActions { void Slap(); void Stung(); void PowerUp(); bool IsDizzy { get; } }`. Angel: `Slap()` → trigger `Slap` + `AngelGlow.Flare()`; `Stung()` → trigger `Stung` + `DizzyStars.Show()`; `PowerUp()` → trigger `PowerUp` + `AngelGlow.Flare()`. Evil: `Slap()`, `Stung()` same; `PowerUp()` → `EvilPower.Activate()`. `IsDizzy` = stars visible. Test keys per Global Constraints, gated by `[SerializeField] bool testKeys = true`.

- [ ] **Step 1: Write failing tests** `CharacterActionsTests` (on instantiated prefabs in a preview scene):
  - `BothPrefabs_ImplementICharacterActions`.
  - `Stung_MakesDizzy` (both), `Evil_PowerUp_ActivatesEvilPower`.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run → PASS (all suites).

### Task 6: Gameplay hookups

**Files:**
- Modify: `Assets/Scripts/P1Controller.cs` (`SlapEvent` hit branch), `Assets/Scripts/P2Controller.cs` (same), `Assets/Scripts/CollectibleManager.cs` (`OnTriggerEnter`)
- Test: `Assets/Tests/Editor/HookupTests.cs`

**Interfaces:**
- Consumes: `ICharacterActions`.
- Changes: on a slap hit, `GetComponent<ICharacterActions>()?.Slap(); hit.collider.GetComponentInParent<ICharacterActions>()?.Stung();`. On pickup, `other.GetComponentInParent<ICharacterActions>()?.PowerUp();` (score logic unchanged).

- [ ] **Step 1: Write failing test** `Collectible_Pickup_CallsPowerUp`: fake `ICharacterActions` MonoBehaviour on a tagged `Player` named `Angel` with a collider; invoke `OnTriggerEnter` (reflection) → PowerUp called once, `AngelScore == 1`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run all suites → PASS.

### Task 7: Final verification

- [ ] Compile clean (`EditorUtility.scriptCompilationFailed == false`), console 0 errors.
- [ ] DemoScene instances (read-only check): Angel/Evil show `DizzyStars`, Evil shows `EvilPower`, no missing scripts/materials.
- [ ] Ask the user to play DemoScene: slap each other (stung + stars), pick up a collectible (power-ups), test keys `1/2/3`, `8/9/0`.
- [ ] Report and offer commit/push.
