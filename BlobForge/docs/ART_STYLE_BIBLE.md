# BlobForge Art Style Bible

This is the visual authority for BlobForge raster art. Gameplay geometry and current source remain authoritative when an asset must fit machinery or collision.

## Visual thesis

BlobForge is a sterile, old industrial facility invaded by vivid living matter. Structure is dark, heavy, restrained, and readable. Blob tissue and blood provide the strongest color contrast. The result should feel tactile, gruesome, playful, and mechanically legible rather than decorative or generically futuristic.

## Non-negotiable rules

- Author final raster art through Pixel Forge MCP and retain the `.pixelforge.json` source.
- Use hard indexed pixels, transparent backgrounds, integer coordinates, and nearest-neighbor scaling.
- Do not use soft gradients, subpixel detail, antialiasing, bloom baked into sprites, or photographic texture.
- Establish the silhouette before internal detail. At gameplay scale, the outline must explain the part's mass and function.
- Make art match physical geometry, attachment points, travel limits, collision, and layer order.
- Keep machinery mostly neutral. Reserve bright cyan and amber for controls, energy, glass edges, and limited guidance.
- Reserve vivid red primarily for blobs, fresh blood, danger, failure, and locked states.
- Prefer visible fasteners, brackets, housings, hinges, pipes, guides, rollers, glass rims, and service seams over unexplained shapes.
- Preserve volume across animation frames unless the part physically compresses, opens, rotates, or changes perspective.

## Canonical Pixel Forge references

Inspect these projects before related work:

| Purpose | Canonical project |
| --- | --- |
| Foreground factory materials and tile density | `blobforge_factory_tileset` |
| Dark structural background and restrained accents | `blobforge_factory_backdrop_tileset_v2` |
| General bay proportions and machinery language | `blobforge_machine_bay` |
| Glass, clamps, and containment | `blobforge_holding_chamber` |
| Animated mechanical assembly and shared palette | `blobforge_drum_housing`, `blobforge_drum_rotor`, `blobforge_drum_handwheel` |
| Collision-aligned receiving geometry | `blobforge_receiving_tub` |
| Rust and drain materials | `blobforge_rusty_drain_pipe_v2` |
| Compact readable state lights | `blobforge_machine_status_v2` |

Do not copy a canonical asset blindly. Match its palette discipline, pixel density, material rendering, and functional clarity.

## Palette families

### Factory structure

Use the foreground steel ramp from `#252c33` through `#7f8b94`. Large structural regions should stay in the darker half. Light values are edge accents, worn faces, or readable mechanical separations—not broad white surfaces.

### Deep background

Use `#080d11`, `#0d141a`, `#111b22`, `#17242c`, `#20313a`, `#2b414a`, `#3b5660`, and `#55717a`. Background variation must remain quieter than playable matter and machine controls.

### Machinery accents

- Cyan: `#65e6df` for powered controls, glass/energy cues, and rare guidance.
- Amber: `#b6812f`, `#e6b53a`, or `#f0c34b` for handles, caution, service points, and warm lamps.
- Danger red: `#7b1d26`, `#c64135`, `#d43c36`, or `#eb3e44` only where danger/state readability warrants it.
- Green status: `#246b4f` and `#4ee09d` for available state.

Keep each asset's palette small. Reuse the closest canonical palette and lock it before detail work. Add a color only when it represents a distinct material or required readability level.

## Shape language

### Machinery

- Use squared housings, chamfered corners, thick brackets, inset panels, and obvious load paths.
- Avoid thin floating shapes, clean consumer electronics, arbitrary neon strips, and ornamental sci-fi greebling.
- Give moving parts visible clearance and a believable pivot, rail, socket, hose, or bearing.
- Connect hoses at real sockets. Connect tools to housings throughout their full animation.

### Glass and liquid containers

- Define glass with dark frames, sparse cyan-gray edge highlights, controlled transparency, and clear front/back layering.
- Do not fill glass with uniform cyan. Interior matter must remain visually dominant.
- Make container rims and walls agree with physical containment.

### Organic matter

- Keep blob and blood reds as the visual focus against neutral machinery.
- Make tissue irregular and material-driven, not decorated with repetitive noise.
- Keep detached pieces related to the silhouette and color ancestry of the removed matter.

## Pixel construction

- Use one-pixel structural highlights at native resolution unless the sprite's scale clearly requires two.
- Cluster pixels into purposeful planes and edges. Avoid isolated noise pixels unless they communicate wear, wetness, or a small fastener.
- Use stepped diagonals consistently. Do not alternate diagonal cadence without a shape reason.
- Keep important controls at least 3×3 native pixels and distinguish them by both shape and value.
- Check transparency bounds so no accidental pixel expands the runtime footprint.

## Animation

- Define frame count, duration, pivot, attachment points, and travel limits before drawing frames.
- Organize multiple motions as named clips with explicit ranges, forward/reverse/ping-pong direction, and loop or one-shot behavior.
- Block key silhouettes first: anticipation/start, readable extreme or impact, and recovery/end. Add breakdowns and in-betweens only after those poses communicate the action at gameplay scale.
- Keep static housing pixels identical across frames when only one component moves.
- Mark stationary housing and background layers frame-invariant so Pixel Forge rejects accidental cross-frame drift.
- Key moving machinery attachment points on each applicable frame and validate their travel; no socket, tool tip, hinge, or hose connection may jump or go missing without a mechanical reason.
- Use explicit frame durations for holds and impacts instead of duplicating identical pictures. Inspect transition pixel counts for accidental dead frames or abrupt cadence changes.
- Use previous-and-next onion skins to judge spacing. Preview every named clip in its actual playback order as a contact sheet and as timed playback.
- For loops, inspect the last-to-first transition and preserve rotational or mechanical cadence.
- For one-shots, make the final pose readable at the duration the game actually holds it.
- Do not hide loading/unloading transitions behind foreground art. Inspect beginning, middle, and end states.

## Asset brief requirements

Before authoring, establish:

- gameplay purpose and viewing distance;
- exact canvas and runtime dimensions;
- canonical reference project;
- palette and whether it is locked;
- named attachment points, pivot, and collision-aligned edges;
- frame count and timing;
- runtime asset filename;
- forbidden colors, shapes, or visual competition.

## Verification checklist

1. Preview the silhouette at 1× and gameplay scale.
2. Run the Pixel Forge production report.
3. Resolve configured tile-seam and loop warnings.
4. Inspect named-clip contact sheets and the animation report for timing cadence, duplicate holds, loop closure, attachment motion, volume, bounds, component-count, and static-layer drift.
5. Compare beside the approved reference.
6. Confirm every attachment point and physical edge.
7. Export through Pixel Forge to `BlobForge/Assets`.
8. Preview in the current BlobForge build and inspect real layer order.
9. Run the relevant game snapshot and station render benchmark for integrated visual changes.

## Failure patterns

Reject or revise art that is noisy, over-accented, softly rendered, generically sci-fi, physically disconnected, misaligned with collision, inconsistent between frames, palette-bloated, or more visually dominant than blob matter without a gameplay reason.
