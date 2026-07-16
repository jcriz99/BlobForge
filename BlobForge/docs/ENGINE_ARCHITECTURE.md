# BlobForge Production Architecture

## Decision

Build the game around a **bonded-particle tissue model solved with XPBD**, with GPU metaball/SDF rendering and a separate optional material grid for destructible terrain and loose fluids.

The former perimeter-ring demo has been replaced by the first Matter Lab implementation of this model. The remaining WinForms/GDI+ host is temporary.

## Why this model

| Candidate | Soft feel | Cutting and splitting | Large populations | Complexity | Decision |
|---|---:|---:|---:|---:|---|
| Perimeter spring ring | Good initially | Poor; body is hollow and topology is fragile | Medium | Low | Prototype only |
| Rigid bodies and joints | Crunchy at useful counts | Awkward | Poor | Medium | Reject |
| Dense per-pixel cellular blob | Fluid-like | Excellent | Poor for many cohesive blobs | Very high | Use only for loose matter |
| FEM/continuum mesh | Excellent | Possible but expensive and difficult | Poor | Very high | Reject for this game |
| MPM | Excellent deformation and material flow | Excellent | Expensive and research-heavy | Very high | Reconsider only if the game becomes primarily fluid simulation |
| Bonded particles + XPBD | Excellent and tunable | Natural bond breakage/components | Good with representation scaling | Medium | **Choose** |

XPBD gives controllable compliance that does not change drastically with frame rate or iteration count. A filled particle lattice gives damage an interior structure and avoids pretending that a hollow outline is solid matter.

## Blob representation

Each production blob owns compact handles into shared structure-of-arrays pools.

### Tissue particles

Each particle stores:

- position and previous position
- inverse mass and radius
- material identifier
- temperature/damage state when those systems exist
- blob/component identity
- sleep and contact state

Particles are laid out initially on a hexagonal lattice. A normal interactive blob should begin around 30-80 particles; counts are tuned from measurements rather than fixed as a design rule.

### Local constraints

- breakable distance bonds for stretch and cohesion
- triangle/local-area constraints for volume resistance
- optional overlapping shape-matching clusters for a fleshy memory of the resting form
- contact constraints generated only for nearby pairs
- a grab constraint attached to one or a weighted group of tissue particles

There is deliberately no single whole-blob area constraint. A global constraint fights valid cuts because it keeps trying to restore the area of the pre-cut body. Local triangles and clusters remain meaningful after topology changes.

### Damage and splitting

Impulses, blades, heat, corrosion, or scripted tools produce damage events. Events weaken or remove nearby bonds. Only a body whose connectivity changed enters the topology queue.

The topology job performs a connected-component pass over that body's remaining bonds:

- the largest connected sets become independent soft bodies
- small cohesive sets become cheap soft fragments
- isolated or liquefied particles transfer to the loose-material system

Mass, material, temperature, and momentum transfer with the particles. Rendering derives from the resulting components rather than holding a separate authoritative shape.

## Collision architecture

### Broad phase

A uniform spatial hash stores tissue particles, proxies, machinery shapes, and dirty terrain chunks. It produces capped local candidate lists without all-to-all body checks.

### Narrow phase

- particle-disc contacts for two high-detail blobs
- particle-versus-analytic-shape contacts for machines
- particle-versus-local-SDF contacts for terrain
- proxy contacts for low-detail or distant blobs

Conveyors contribute surface velocity through the contact constraint. Blades create both collision and bond-damage events. Crushers remain ordinary kinematic shapes with force/pressure telemetry.

## Terrain and loose matter

Do not force terrain, cohesive blobs, blood, and smoke into one solver.

### Terrain

Use static polygon/analytic collision for ordinary rooms and machinery. If destructible terrain becomes important, add a sparse chunked material grid. Only edited chunks rebuild their collision SDF and render data.

### Loose matter

Blood, slime, paste, and dust use capped particles or a coarse material grid. Tissue transfers into this system when cohesion is lost. Loose matter never pays for a full blob constraint graph.

This preserves Noita-inspired material consequences without committing the entire game world to expensive per-pixel simulation.

## Rendering

Render tissue particles as instanced influence discs into a low-resolution offscreen field. A threshold/normal pass produces a smooth metaball surface, then material, damage, eyes, decals, and outlines are composited on top.

Benefits:

- holes, cuts, merging edges, and separated components appear automatically
- particle count is decoupled from visible contour resolution
- low-detail and high-detail representations can share the same visual language
- the CPU does not rebuild a complex outline mesh every physics step

The production platform layer should move from WinForms/GDI+ to **SDL3 for windows/input/audio and SDL's GPU API for rendering/compute**. SDL's GPU API supplies cross-platform modern graphics and compute backends while the game retains a completely custom simulation and gameplay layer.

## Simulation scheduler

Replace hard-coded gameplay tiers with a priority-and-representation scheduler.

Every blob has persistent identity and authoritative coarse state. The scheduler chooses one of several representations for the next budget window:

- full tissue: all particles, bonds, local-area constraints, detailed contacts
- reduced tissue: sampled particles/clusters and capped contacts
- shape proxy: one or a few deformable lobes that preserve pose and compression
- asleep: cached state, no integration until an event wakes it
- loose fragment: handled by the fragment/material pool

Priority comes from direct manipulation, damage, machinery contact, motion, screen importance, pile exposure, and recent disturbance. Representation is an engine decision, not a game-visible category. Promotions and demotions transfer position, velocity, mass, compression, and visible shape over a short blend window.

## Frame pipeline

The target pipeline is:

1. Gather input and gameplay commands.
2. Select representations within CPU/contact budgets.
3. Integrate active particles at a fixed step.
4. Build the spatial hash and candidate contacts.
5. Solve grab, bonds, local volume, machinery, terrain, and blob contacts.
6. Update velocities, support, and deterministic sleep.
7. Accumulate damage events.
8. Process a bounded topology queue after the solve.
9. Publish an immutable render snapshot.
10. Render independently at the display rate.

The hero interaction may run at 120 Hz. Background materials and reduced representations may update less often. Catch-up remains capped; overload degrades simulation detail instead of attempting unlimited steps.

## Runtime and code organization

Continue with **C#/.NET 8 for the first production vertical slice**, because iteration speed matters more than theoretical peak throughput while the matter model is changing. Physics data must remain array-based and allocation-free during a frame; the runtime provides SIMD numeric types and low-latency GC modes, but those are aids rather than substitutes for profiling.

Suggested modules:

```text
BlobForge.Platform       SDL window, input, audio, timing
BlobForge.Graphics       GPU resources, material-field rendering, camera
BlobForge.Simulation     particles, XPBD constraints, contacts, sleeping
BlobForge.Topology       bonds, damage events, component splitting
BlobForge.Materials      tissue definitions, terrain chunks, loose matter
BlobForge.Gameplay       grabbing, machines, rules, scoring
BlobForge.Assets         scene/material serialization and hot reload
BlobForge.Diagnostics    headless tests, telemetry, capture/replay
BlobForge.Lab            focused test rooms; no full editor yet
```

If profiling later proves the custom simulation CPU-bound after data layout, scheduling, and parallelism are correct, `Simulation` and `Topology` can move behind a stable C ABI into native C++ without rewriting gameplay or assets. Starting in C++ now would slow iteration before the actual solver and game feel are known.

## What survives from the current demo

Keep the ideas, not necessarily the code:

- fixed-step simulation with capped catch-up
- point grabbing and release velocity sampling
- headless physics regression tests
- deterministic sleeping as a hard behavior guarantee
- an always-visible timing and count overlay
- small focused physics rooms

Completed replacements:

- the hollow perimeter-ring body
- the single global area constraint
- hard-coded hero/surface/normal tier semantics

Still replace:

- GDI+ contour rendering
- the tile wall as the primary proof of blob destruction

## Development milestones

### 1. Matter lab

Status: filled soft tissue, bounded weighted grabbing, multi-blob surface contacts, coarse anti-tunneling guards, visible-hull non-interpenetration, stacking support, and deterministic rest are implemented. GPU rendering remains.

- filled 30-80 particle blob
- XPBD bonds and local area constraints
- point grab, swing, throw, slam, crush, and stable rest
- GPU metaball rendering

Exit test: interaction feels better than the current ring prototype and both whole/deformed bodies sleep without visible motion.

### 2. Real destruction

Status: bond damage, linear-time bridge-pruned topology, event-driven splitting, stable main-component retention, and procedural physical chunk staging are implemented. A detached component rebases its bonds and local areas to its deformed cut-time state, while rendering traces the surviving tissue-cell boundary so the separated silhouette matches the actual damage. Chunks collide while airborne and only begin budgeted granular conversion after impact, landing/rest, or a long safety fallback. Persistent severity/clotting wounds and spatial-hashed material contact are also implemented. The current granular solver is capped and CPU-based; GPU material-field rendering remains.

- line blade and impact damage
- bond visualization
- event-driven connected-component split
- two large soft pieces plus small fragment conversion

Exit test: one continuous cut visibly and physically creates two independent bodies without an area explosion.

### 3. Machinery room

- conveyors, crusher, blade, funnel, moving platform
- material parameters and force telemetry
- saveable test scenes

Exit test: every machine uses the same contact/damage APIs; none contains blob-specific physics hacks.

### 4. Population test

- particle spatial hash and coarse proxy representation
- blob-blob contact
- sleeping/waking and identity-preserving representation transitions
- 10, 50, 100, and 200 blob benchmarks

Exit test: removing one blob from a pile causes local response and keeps the directly handled blob at full quality.

### 5. Game foundation

- scene flow, camera, audio, input mapping, assets, save data
- loose blood/slime/fragment system
- content authoring tools based on demonstrated needs

Do not build a broad general-purpose editor or generic ECS before these vertical slices establish what the game actually requires.
