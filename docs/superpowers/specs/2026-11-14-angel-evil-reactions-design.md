# Angel & Evil: stung reaction, dizzy stars, power-ups

## What the user asked for (confirmed 2026-11-14)
1. **Getting slapped:** whoever gets slapped (Angel or Evil) plays the same stung animation as the main character, with **3 stars** spinning over their head to show they're dizzy. **Angel: golden stars. Evil: purple stars.**
2. **Collecting a power-up:** the **Angel** plays the same power-up animation as the main character (jump + full twirl). The **Evil** gets **more glow and more flames** to show its power.
3. Done properly and looking good.

## Why
Teammates' request: hits and power-ups must read clearly in the two-player game (P1 = Angel, WASD; P2 = Evil, arrows).

## Today (what exists)
- Angel/Evil animators: `Idle` (Fly loop) + a state named `Stung` that actually plays the **Slap** swing, on trigger `Stung` (Space).
- MainChar: `Stung` 0.8 s (body recoil/squash, head snap, arms fling) and `PowerUp` 1.0 s (jump + 360° twirl, arms up, squash/stretch).
- Slap: `P1Controller`/`P2Controller.SlapEvent()` raycast only logs a hit (4 s cooldown). Collectibles only add score; there is no separate power-up item yet.
- Rigs: Angel `body, head, wing.L/R, hand.L/R, tail`; Evil `body, hand.R, lantern, flames + flame.01-15, coat.*` (no head).

## Approaches (for the animations)
- **A. Keyframed clips generated in Unity by transferring MainChar's Stung/PowerUp motion bone-by-bone onto each rig, layered on its Fly loop (chosen).** Literally "the same animation", wings/flames keep moving, no FBX/Blender round trip, re-runnable.
- B. Re-key in Blender and re-export: same result, slower, touches the Evil's self-contained prefab import path and the shared Blender.
- C. Procedural motion at runtime: quickest, but not a real animation and fights the Rigidbody rotation.

## Calls made (user said "yes go"; redirect any)
- Existing `Stung` state/trigger on Angel/Evil is renamed **`Slap`** and plays on the **attacker**; new **`Stung`** = hit reaction on the victim.
- Dizzy stars visible **2.5 s**, pop in/out, orbit over the head (Evil: above the hood), glow via the existing selective-glow system.
- Angel power-up = MainChar twirl + her existing glow `Flare()`.
- Evil power-up: ramp in 0.25 s, hold **4 s**, fade 1.2 s: flames grow (×1.7) and brighten, fiery body glow (GlowSource), rising flame + ember particles, warm point light, a short swell at the start. No twirl.
- Hookups: slap hit → attacker `Slap()`, victim `Stung()`; collectible pickup → collector `PowerUp()` (until a real power-up item exists).
- Test keys: Angel `1`/`2`/`3`, Evil `8`/`9`/`0` = slap / stung / power-up. Space no longer triggers anything.
- Movement is not blocked while dizzy (gameplay call for the teammates); `IsDizzy` is exposed.
- **Slap is a button (user request, 2026-11-15):** P1/Angel **Left Shift**, P2/Evil **Right Shift** (replaces the automatic slap every frame). A press always swings; it lands only if the other player is in front and in range; 4 s cooldown after a hit, presses during the cooldown do nothing.

## Non-goals
New gameplay rules (stun, power-up effects on speed/score), a power-up item, MainChar changes.
