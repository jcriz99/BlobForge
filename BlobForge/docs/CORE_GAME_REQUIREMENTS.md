# Core Game Requirements

This document intentionally removes implementation advice inherited from earlier Unity discussions. It describes what the game needs to make possible, not how the old project proposed doing it.

## Game identity

The game is a 2D physical sandbox/factory game about handling living blob-like matter.

The defining player experience is:

1. A blob has weight, softness, cohesion, and a readable internal physical state.
2. The player can pick it up at a point, drag it, shake it, throw it, crush it, and feed it through machinery.
3. The blob deforms continuously rather than switching between canned animations.
4. Damage changes the actual body. Cutting or tearing can separate it into independently simulated pieces.
5. Gravity, surfaces, other blobs, and machines produce understandable results that the player can learn and exploit.

No particular solver, object model, engine shell, quality-tier count, or rendering technique is part of the game identity.

## Required capabilities

### Physical blob matter

- Filled, deformable bodies rather than an animated sprite or hollow outline.
- Local stretch, compression, shear, and volume resistance.
- Configurable material character: gummy, fatty, watery, tough, brittle, or elastic.
- Stable rest with no permanent buzzing or shape pumping.
- Point grabbing that transfers force through the body instead of teleporting its center.
- Strong impacts, crushing, conveyors, blades, and moving machinery.

### Destruction and continuity

- Damage belongs to material or bonds, not to a single abstract hit-point bar.
- Cuts and tears can change topology.
- Disconnected material becomes separate physical components.
- Large pieces retain soft-body behavior; tiny pieces can become cheaper fragments, paste, or fluid.
- Matter should appear conserved unless a deliberate game rule consumes it.
- Destruction work happens when damage changes topology, not as a full graph scan every frame.

### Population and piles

- The world can contain a large population of blobs.
- A visible pile must contain individually addressable blobs that can be selected and removed.
- Only the locally important portion of a pile needs high-detail deformation at any instant.
- Representation changes must preserve identity, mass, approximate shape, and wake-up continuity.

### World interaction

- Static level geometry, moving platforms, conveyors, blades, crushers, funnels, and articulated machinery.
- Material properties such as friction, adhesion, cutting power, temperature, or corrosion can be added without rewriting the solver.
- Destructible terrain is useful if the game design calls for it, but full Noita-style simulation of every world pixel is not assumed to be mandatory.
- Blood, slime, and loose material use their own representation when they no longer need cohesive soft-body behavior.

### Production needs

- Fast creation of test rooms and machinery arrangements.
- Saveable scenes and material definitions.
- Repeatable physics regression tests.
- Live timing, allocation, contact, sleeping, topology, and population diagnostics.
- A simulation that can be profiled headlessly without the renderer.

## Explicitly not requirements

- Unity as the game shell.
- One GameObject, rigidbody, collider, or joint per particle.
- A fixed five-tier hierarchy.
- Every blob using the same particle count or solver settings.
- Exact real-world continuum mechanics.
- Pixel-perfect reproduction of Noita.
- Fully active, mutually detailed simulation for every blob in a large pile.
- A universal ECS or general-purpose editor before the core interaction is proven.

## Success tests

The engine direction is valid only if it can eventually pass these experience tests:

1. A blob can be grabbed by one edge, swung, released, and slammed into a wall with convincing force transfer.
2. A resting whole blob and a resting damaged blob become completely motionless.
3. A blade can cut a blob into two pieces that separate, collide, and settle independently.
4. Crushing changes the local shape without forcing the entire body to collapse or explode.
5. Pulling one blob from a large pile wakes a local region, not the entire population.
6. A low-detail blob can become fully deformable during interaction without an obvious pop, identity change, or mass jump.

