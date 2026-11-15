# Split Courtyard Environment — build plan (branch `enviromentexpr`)

User: replace the environment with layout **C (split courtyard)** from the concept art (`image.png`, "Broken Aqueduct" + "Fractured Sanctuary"), 3D, environment only, "don't ask, just do".

## Layout (world units; characters ≈ 32 tall, 22 wide, scaled x10; camera (54,100,-34) 53° down sees z -22..200)
- Arena interior x -34..146, z -8..168, chamfered corners; border wall of chunky blocks (≈14 high, colliders 60 high).
- Left half: aqueduct ruin across the back-left (x -34..62, z 134..176, top 30) with an arch tunnel at x≈0; stream flows out of it to the front, under a culvert in the front wall, small waterfall; slab bridge (z≈92) + stepping stones (z≈32); teal/gold banner on its face.
- Right half: paved sanctuary courtyard (slabs in grass), raised terrace back-right (x 92..146, z 128..176, top 26) with side stairs (x 74..92), glowing pink cracks, pillar + pink banner; freestanding ruined arch at (110,100) players pass through.
- Cover block clusters outside the spawn area; Silver_Cats trees ring the arena outside; ferns/plantain/dandelion/mushrooms inside along walls; moss on stones.
- Spawn area x -24..132, z 0..88 (open ground only).

## Build
- Shaders: `GameJam/StylizedEnv` (vertex colour + painterly mottling + world-XZ detail + wrap light, shadows, SSAO, extra lights), `GameJam/StylizedWater` (flowing stripes + foam). Cracks = emissive + GlowSource.
- `Assets/Scripts/Environment/Editor/EnvMeshKit.cs` (rounded irregular blocks, mesh builder, ribbon/banner), `CourtyardBuilder.cs` (layout → meshes/materials/prefab `Assets/Environment/Courtyard/Courtyard.prefab`, install into DemoScene: remove Floor/Wall/Trees/Terrain/blockout cubes, add map, spawn area, lighting).
- Project render fixes: URP shadow distance 50 → 420 (camera is 125-250 away), camera SMAA.
- Tests `Assets/Tests/Editor/CourtyardTests.cs`: no broken materials, arena enclosed, spawn area open, start positions free.
- Iterate with renders from the game camera; play check; commit on `enviromentexpr`.
