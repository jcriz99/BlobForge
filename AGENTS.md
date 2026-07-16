# PROCESS Project Guide

This file applies to the entire `PROCESS` workspace. It is the durable handoff for future Codex chats after older project conversations are archived.

## Start here

Before changing the project:

1. Read this file.
2. Read `BlobForge/README.md` for the current playable implementation.
3. Read `BlobForge/docs/CORE_GAME_REQUIREMENTS.md` when making gameplay or physics decisions.
4. Read `BlobForge/docs/ENGINE_ARCHITECTURE.md` only for longer-term engine direction.
5. Read `PixelForgeStudio/README.md` before art-tool or asset work.
6. Inspect the current source and tests. Source code plus passing regressions are more authoritative than an old chat or a stale paragraph in documentation.

Do not restart the project from an archived design. Continue from the working vertical slice and preserve behavior that the user did not ask to change.

## Workspace map

- `BlobForge/`: the game, custom soft-body engine, factory gameplay, rendering, audio, diagnostics, and current playable build.
- `PixelForgeStudio/`: the self-made indexed pixel-art editor and its `pixel_forge` MCP server.
- `PixelForgeStudio/PixelForgeData/`: editable `.pixelforge.json` source projects and exported art.
- `BlobForge/Assets/`: runtime PNG/spritesheet exports consumed by the game.
- `BlobForge/bin/CurrentBuild/BlobForge.exe`: the build the user expects to launch when work is handed off.
- `BlobForge/artifacts/`: diagnostic screenshots and other temporary visual verification output.
- `BlobForge.slnx`: currently contains the BlobForge project. Pixel Forge Studio is built separately.

This workspace is a Git repository with private remote `https://github.com/jcriz99/BlobForge` and `main` as its primary branch. Treat all existing files and unrelated edits as user-owned. Do not delete, reset, or broadly rewrite work to obtain a clean state.

## Version control and disaster recovery

- Git tracks the game/editor source, runtime art, editable `.pixelforge.json` projects, documentation, and project configuration.
- `.gitignore` intentionally excludes `bin/`, `obj/`, `.publish/`, diagnostic artifacts, logs, and generated Pixel Forge exports. Do not casually force-add those changing outputs to normal source history.
- `BlobForge/bin/CurrentBuild/` is nevertheless an important local deliverable. A source commit can rebuild the project, but it is not a substitute for preserving the exact known-good executable.
- At meaningful playable milestones, zip the complete `CurrentBuild` folder and attach it to a **private GitHub Release** whose tag points at the matching source commit. This is the downloadable disaster-recovery/playable snapshot.
- Do not delete redundant builds or other only-known-good output until the corresponding source commit is pushed and, when an exact executable matters, the release archive has been uploaded and verified. Keep `CurrentBuild` locally instead of accumulating many ad-hoc build directories.
- Never commit or print GitHub credentials, tokens, local secrets, or user-specific authentication files.

## Project purpose

BlobForge is a 2D physical sandbox/factory game about handling living, destructible blob matter through an industrial processing line.

The core experience is that blobs have understandable weight, softness, cohesion, damage, and momentum. The player can grab, throw, crush, drill, tumble, vacuum, cut, and convey them. Machinery should act on the actual simulated body. Damage changes real material and can produce separated soft pieces, tissue, and blood rather than playing canned visual effects.

The current executable is a focused Matter Lab/factory vertical slice built on a custom C#/.NET 8 soft-body engine. WinForms/GDI+ is the current working platform and test harness. SDL3/GPU material-field rendering is a future production direction, not an automatic rewrite task.

Historic Unity chats are useful evidence of the intended feel, not implementation authority. The custom engine superseded the Unity implementation. Do not restore old Unity scripts, solver layering, or scene architecture merely because an archived conversation mentions them.

## Blob feel and damage contract

These requirements recur throughout the project history and are more important than preserving a particular solver technique:

- Blobs should be soft, heavy, cohesive, and able to compress dramatically, including close to flat, while retaining area/matter continuity. They must not respond to pressure by teleporting, exploding, wrapping completely around an obstacle, tunneling through it, or becoming rigid.
- Visual contour, contact points, collision, and material occupancy must agree at all times. No invisible protruding contacts, stale collision at an old wound position, oscillating alternate silhouettes, dangling orphan points, or lines extending beyond the body.
- All blobs of an archetype should spawn with the same organic silhouette and physical feel; spawned copies must not silently become generic polygons or use a cheaper feel without an intentional representation transition.
- Clicking anywhere on a pickable blob should produce a stable center/whole-body grab rather than stretching one edge particle. The grab should track strongly without desynchronizing, preserve believable throwing momentum and recoil, and remain constrained by walls and other matter even if the cursor moves outside the room.
- Blob-on-blob support should come from deformation, friction, mass, and a lower blob forming a cradle. Do not fake stacking with glue/sticky constraints, and do not make piles weightless. Blobs may mush deeply against each other but may not permanently intertwine or occupy the same material space.
- Damage is local. A tool or cursor damages only material it actually touches; unaffected perimeter regions should not globally dent or reshape without a physical reason.
- A single right-click/gouge and a held right-drag slice are distinct gestures. A slice previews along the traversed body path and commits when it exits another edge or when the button is released; a click removes a localized chunk. Preserve these semantics unless the user explicitly redesigns the controls.
- Removed/detached matter should match the actual excised silhouette, remain visible, collide as a real deformable piece, and then shed/dissipate progressively into pixels after impact when appropriate. Do not replace it with a generic circle, delete it instantly, or stagger its disappearance as an obvious canned sequence.
- Wounds emit blood according to location and severity, then taper naturally. Blood and tissue must not remain trapped inside intact blob matter, and damage must not manufacture or silently erase large amounts of matter.

## Current factory loop

- The game begins in a true blackout. Only the dim yellow lamp above the main breaker illuminates its immediate area.
- The player must grab and pull the breaker handle downward. Power then brings up lights, conveyors, machine ambience, and the first blob shortly afterward.
- The breaker box and blob counter are movable authoring fixtures and persist their saved positions.
- A holding chamber releases one blob into a passive receiving tub. The tub itself must not add hidden conveyor force.
- Blobs remain pickable through the receiving area and conveyors until entering Bay 1. From Bay 1 onward they cannot be picked up.
- Bay 1 is a five-tooth spike crusher operated by its held button.
- Bay 2 is a spike drill operated by its held lever.
- Bay 3 is a physical tumbler. A visible foreground lift loads the blob before it moves behind the drum glass. The side handwheel responds quickly to circular mouse movement, the body tumbles under gravity and moving-wall friction, and the cycle currently requires six complete rotations before aligning, opening, and safely discharging to the outgoing belt.
- Bay 4 is a hand-operated vacuum nozzle and hose. The hose connects to the rear of the nozzle; hose bulges use the hose color. Completion releases the blob promptly and must not leave an indefinite blood drain or a blocked queue.
- Bay 5 is a one-pass laser/filter interaction. One completed right-to-left pass processes a blob and resets promptly; the same blob must not require repeated passes.
- Later bays make extracted blood count progressively more toward the basin.
- Processing should leave a visible remnant of the blob/tissue to reach the output cart rather than annihilating every blob before the end by default.
- Every bay has a readable status light: green when available and red when busy, blocked, or locked. Prefer these in-world signals over explanatory labels floating above machinery.
- Loose blood/tissue travels through visible machine drains into the glass basin. Suspended drops can float briefly and dissolve into the conserved cellular fluid rather than disappearing on contact.
- The basin contains its blood: no stains should appear beneath it. Interior glass, walls, submerged pipe portions, and pipe-mouth edges may become wet or stained in physically plausible ways.
- Basin bubbles begin only around 35% fill. Diego is currently disabled/dormant; preserve his art and code. If re-enabled, he is a basin inhabitant that may consume stored blood, walks in the correct direction, renders in front of the blood but behind the front glass, and must not destabilize the fluid.
- At 100% basin capacity, machinery turns red and locks safely. It must never crash or overfill. The in-world blood-exchange/shop foundation can spend basin value and is intended to host later upgrades.
- The final conveyor transfers blobs and loose matter into a physically containing output cart. The cart and doorway can receive blood stains without matter clipping through the cart walls.
- Pause Settings currently contains fullscreen, debug, gravity, and persistent Master/SFX/Music volume sliders. Existing game and machinery cues use the SFX bus; the reserved future music channel uses the Music bus. Per-asset sound-file selection does not belong in the in-game Settings screen.
- New major machinery/gameplay components should expose an authoring/configuration cue path and per-cue gain routed through the SFX bus. Player Settings should expose bus volumes, not development-time file pickers.
- The blood-exchange/shop UI is part of the physical factory wall. It may cover only background negative space, must conform to the receiving-tub underside and adjacent architecture, must not overlap gameplay fixtures, and should close gaps rather than creating a floating overlay.
- User-created conveyors and hanging lanterns are authoring fixtures. Preserve conveyor move/resize/speed/reverse/delete behavior and lantern placement/cable-length/delete behavior when changing layout or input routing.

## Product and visual goals

- Keep the game simple, readable, tactile, gruesome, and playful.
- Maintain a consistent sterile industrial indie pixel-art language: dark neutral factory structure, restrained amber/cyan machinery accents, bright red blob/blood contrast, hard pixel edges, and nearest-neighbor scaling.
- Background tiles should have structural variation without becoming noisy or competing with gameplay.
- Machinery art, collision, and animation must agree. Connected components must stay connected; tools may not detach from housings, pass through their own supports, or teleport blobs behind art.
- Prefer visible mechanical staging—lifts, hatches, guides, hoses, rollers, and moving walls—over unexplained position changes.
- Prefer physics-driven motion where it materially improves understanding. Scripted staging is acceptable for a mechanical carrier or door, but do not pin a blob to canned positions once it is inside a physical container.
- Preserve matter and fluid continuity unless an explicit gameplay rule consumes it.
- The basin is Noita-inspired, not a mandate to reproduce Noita exactly. Favor conserved, contained, natural-looking cellular liquid with rough/sloshing surfaces over sand piles, vertical columns, sawtooth equalization, or unconstrained per-pixel cost.

## Blood and stain contract

- Blood stains the exact surface region it touches, not an entire terrain tile or machinery component because one pixel made contact.
- Stains are persistent because a future cleaning mechanic is planned. They may dry, darken, and lose wet shine, but must not automatically disappear. Fresh blood should be able to widen an old path or find a neighboring path instead of merely replaying one identical streak.
- Splashes, streaks, and 2.5D drips need natural variation in width, length, branching, and occasional falling droplets. Dense wet areas may travel across multiple adjacent tiles/surfaces under gravity; avoid uniform straight one-tile lines.
- Stains must remain attached to real visible surfaces, layer in front of the face they coat, and never float in empty space. Left/right walls, conveyors, machinery, the doorway, and other stainable surfaces should behave consistently.
- Moving surfaces such as conveyor loops should carry blood around their actual path. Containers such as the basin and output cart must prevent exterior floor stains or side clipping unless matter physically spills over an opening.
- Preserve the current staining system carefully. Visual cleanup must not erase persistence, wall dripping, surface-local placement, or bounded performance.

## Mandatory art workflow: Pixel Forge MCP

Whenever new raster art, sprites, animations, tiles, or revisions to existing pixel art are needed, use the self-made `pixel_forge` MCP connection to Pixel Forge Studio.

This is a hard project rule:

- Do not create final game art with ad-hoc image scripts, procedural drawing added directly to `GameRenderer`, generic image generation, Paint, or hand-edited exported PNGs when Pixel Forge MCP can perform the work.
- Start with `pixel_forge` project listing/get/preview operations and reuse the existing `blobforge_*` source project when one exists.
- Use MCP palette, pixel batch, line, rectangle, ellipse, fill, layer, frame, duration, and transform operations to author the asset.
- Preview every affected frame through MCP and verify the animation as a sequence, not as isolated stills.
- Export through MCP as a transparent PNG or horizontal spritesheet at exact nearest-neighbor scale.
- Keep the editable `.pixelforge.json` project in `PixelForgeStudio/PixelForgeData/`. The source project and runtime export must not diverge.
- Copy/update the approved export in `BlobForge/Assets/` and ensure `BlobForge.csproj` copies it to the output directory.
- Place projects in the editor's appropriate **Tilesets**, **Objects**, or **Misc Art** section so the dropdown remains searchable.
- For machinery animation, design attachment points, travel limits, and frame timing around the actual gameplay geometry before integrating the export.

The MCP namespace is `pixel_forge` and currently supports project creation/list/get, rendered previews, indexed pixel batches, palettes, layers, animation frames and durations, drawing primitives, fills, transforms, and asset/engine-pack export.

If the `pixel_forge` MCP tools are missing or unavailable, do not silently bypass this rule. Report that the art portion is blocked, verify the Pixel Forge MCP configuration/published server, and restart discovery as needed. Pixel Forge Studio does not need its browser UI open for MCP edits.

If the editor lacks a capability required for the requested asset, extend `PixelForgeStudio` first, test it, republish it, and then perform the art work through MCP. The user has explicitly authorized improvements to the sprite editor when needed.

## Engineering invariants

- Fixed simulation: 120 Hz with no more than four catch-up steps per rendered frame.
- Logical world: fixed 1280×720 with correct letterboxing and pointer mapping.
- Performance is gameplay-critical. Machinery activation must not collapse the frame rate.
- The station render benchmark has a hard 13.5 ms average-frame regression ceiling. Treat a regression near or above this as a release blocker.
- Long term, the game should support tens to hundreds of blobs/objects through sleeping, spatial partitioning, representation tiers, pile-specific simulation, and bounded contact work. This does **not** mean hundreds of full-quality awake soft bodies at once. Important, grabbed, exposed, or actively processed blobs keep premium physics; distant/buried/resting matter may use cheaper but still physical representations.
- Avoid allocations, image resampling, topology rebuilds, full-screen blends, file I/O, or media-player state churn in per-frame/fixed-update paths.
- Static factory/machinery structure should remain cached. Dynamic matter and moving machine parts should be bounded and layered deliberately.
- Physics, rendered contours, and collision should derive from authoritative material state rather than independent approximations that can visibly disagree.
- Preserve deterministic sleep, containment, back-pressure, one-blob-per-machine ownership, and safe release behavior.
- Never fix a visual problem by changing current gameplay functionality unless the user asks for that change.
- Do not hide transitions behind foreground sprites. Validate layer order at the beginning, middle, and end of every machine animation.
- At basin capacity, clamp safely and lock the line; never pass invalid negative/overflow dimensions or values into GDI+.
- Audio state updates must remain transition-based/idempotent. Do not restart looping media every 120 Hz update.

## Source guide

- `BlobForge/Physics/`: soft bodies, constraints, world stepping, contact resolution, granular material, and simulation modes.
- `BlobForge/World/`: processing line, machines, basin, holding chamber, conveyors, terrain, lighting, and saved fixture layout.
- `BlobForge/Rendering/GameRenderer.cs`: cached factory rendering, matter layers, machinery animation, basin/glass composition, lighting, UI, and diagnostics.
- `BlobForge/Audio/SoundEffectMixer.cs`: cue paths, per-cue levels, persistent Master/SFX/Music buses, and async playback.
- `BlobForge/GameWindow.cs`: WinForms host, input routing, pause/settings UI, audio state integration, and main loop.
- `BlobForge/Diagnostics/SelfTests.cs`: regression suite, benchmarks, and diagnostic scene/snapshot writers.
- `PixelForgeStudio/Core/`: indexed project model, operations, storage, PNG rendering, and exports.
- `PixelForgeStudio/Mcp/`: `pixel_forge` protocol server.
- `PixelForgeStudio/wwwroot/`: browser editor UI.

## Tools and preferred use

- Use `rg`/`rg --files` first for source and asset discovery.
- Use `apply_patch` for deliberate source/document edits.
- Use the `pixel_forge` MCP tools for all required pixel-art creation and editing.
- Use local image inspection for diagnostic screenshots and exported sprites. Check full-resolution pixel edges and important crops.
- Use Windows computer control when a WinForms interaction must be reproduced and verified in the actual app, especially menus, sliders, focus, fullscreen, and mouse gestures.
- Before taking interactive control of the user's PC, give a clear pre-control confirmation/notice so the user does not press keys or fight the mouse. Do not send Escape or otherwise interrupt the running game before that notice. When control is released, give a simple, clear completion signal. Prefer headless diagnostics when they can prove the same behavior.
- Use the browser UI at `http://127.0.0.1:4876` for human inspection of Pixel Forge projects when helpful; MCP remains the required automation/editing path.
- Use web research only when the request calls for it or a technical claim needs current primary documentation. External inspiration does not override this project's measured behavior or art pipeline.

## Build, test, and publish

Run these commands from the `PROCESS` root unless noted otherwise.

Build and run BlobForge:

```powershell
dotnet build .\BlobForge\BlobForge.csproj -c Release
dotnet run --project .\BlobForge\BlobForge.csproj -c Release
```

Core validation:

```powershell
dotnet run --project .\BlobForge\BlobForge.csproj -c Release --no-build -- --self-test
dotnet run --project .\BlobForge\BlobForge.csproj -c Release --no-build -- --station-render-benchmark
dotnet run --project .\BlobForge\BlobForge.csproj -c Release --no-build -- --audio-loop-benchmark
```

Additional diagnostics are exposed in `BlobForge/Program.cs`, including contour, paint, general render, granular render/simulation benchmarks and station/drum/drum-loading/pipe/blood/runoff snapshots.

Publish the build the user should launch:

```powershell
dotnet publish .\BlobForge\BlobForge.csproj -c Release -o .\BlobForge\bin\CurrentBuild
.\BlobForge\bin\CurrentBuild\BlobForge.exe --window-smoke-test
```

For a milestone/disaster-recovery release, archive the entire verified folder (executable, runtime files, and `Assets/`) and attach it to a private GitHub Release whose tag targets the matching commit. Verify the release is private and the archive is downloadable before deleting older known-good builds.

Pixel Forge Studio:

```powershell
dotnet build .\PixelForgeStudio\PixelForgeStudio.csproj -c Release
dotnet run --project .\PixelForgeStudio\PixelForgeStudio.csproj -c Release -- self-test
dotnet run --project .\PixelForgeStudio\PixelForgeStudio.csproj -c Release -- serve
dotnet publish .\PixelForgeStudio\PixelForgeStudio.csproj -c Release -o .\PixelForgeStudio\.publish
```

The editor UI normally runs at `http://127.0.0.1:4876`. `PixelForgeStudio/start-studio.cmd` launches the published editor.

## Verification expectations

For a gameplay, physics, rendering, UI, audio, or art change:

1. Reproduce or identify the actual cause before editing.
2. Make the narrowest coherent fix while preserving existing functionality.
3. Add or update a regression when the bug can be expressed headlessly.
4. Build Release with zero errors and preferably zero warnings.
5. Run the full self-test suite.
6. Run the station render benchmark for anything touching rendering, machinery, background art, lighting, fluid, or per-frame logic.
7. Run the audio-loop benchmark for audio state or mixer changes.
8. Generate and inspect diagnostic snapshots for visual/layering work.
9. Exercise UI-only behavior in the actual Windows build when relevant.
10. Publish and smoke-test `BlobForge/bin/CurrentBuild` before reporting completion.

Do not call work complete merely because it compiles. The user should always be able to find the latest verified playable executable in `BlobForge/bin/CurrentBuild/`.

## Current development direction

Near-term work should strengthen the factory game rather than replace it:

- physically legible machine loading, processing, and unloading;
- stable multi-blob queue/back-pressure behavior;
- better contained liquid motion and basin interaction without performance collapse;
- the in-world blood-spending shop and later upgrades;
- additional sound/music content routed through existing buses;
- continued sterile factory art and tileset variation authored through Pixel Forge;
- regression coverage for every previously recurring jam, containment, layering, crash, or performance failure.

Longer term, the architecture documents target SDL3, GPU material-field/metaball rendering, scalable blob representations, saveable scenes/material definitions, and larger population benchmarks. Begin those migrations only when explicitly requested and with milestone/benchmark exit tests; do not destabilize the current playable factory slice speculatively.
