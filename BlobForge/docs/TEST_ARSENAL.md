# Test Arsenal Design Contract

This document defines the first developer-test arsenal. Each item must earn its place through a distinct physical interaction, not merely a different damage number or sprite.

## Current playable implementation

The palette contains the cleaver plus twenty-two arsenal variants, all dispatched from the one centered rack. The original thirteen cover saber cutting, physical firearms and saws, vacuum shredding, heavy slams, mounted slingshot/pike interactions, piston punches, grenades, and the battleaxe. The expanded pass adds a mini-black-hole launcher, chewing rats, an enlarger, flamethrower, freeze ray, lightning coil, acid lobber, water doll, and the paired baseball bat and ball. Firearms use capped physical projectile entities with weapon-specific penetration, force, and recoil rather than hitscan damage lines.

## Test harness and lifecycle

- `I` opens or closes the developer-only arsenal palette without pausing the simulation. Arrow keys or the mouse select an entry; `Enter` or left click confirms; `Escape` closes it.
- Confirming an entry safely swaps the authoritative physical tool into the one existing centered cleaver rack. No additional holster is spawned. The selected item supports the same left-drag and `E` equip/drop lifecycle as the cleaver.
- The palette displays the item silhouette, name, primary-action reminder, and deployment type: handheld, thrown, conveyor-mounted, or wall-mounted.
- Handheld items use the existing physical tool contract: hover the centered item and press `E` to equip it, press `E` again to release it, or left-drag the loose item without attacking. The cleaver is the first inventory entry so the player can swap back without resetting.
- Equipped items follow the simulated grip, inherit physical release velocity, collide with the room, stain only where blood touches them, return magnetically when released near their holster, and recover if lost offscreen.
- Right-dragging most equipped items changes their persistent base rotation, including during melee recovery. It cancels an active attack/throw and blocks activation until RMB is released. The sledge is the deliberate exception: RMB toggles its committed floor slam between the left and right side, with no free-rotation guide. Barrel tools inherit their left-facing rack pose; other tools default upward.
- Equip the slingshot or pike and move the cursor to obtain an alpha placement ghost. Left click confirms a valid ghost; `E` on the deployed device picks it back up. The slingshot snaps to a conveyor and selects one of three visible heights from cursor height. The pike accepts only background-wall positions above and clear of conveyor geometry, becomes immovable after placement, and pins punctured blob matter until the player grabs it free or destroys it.
- The existing butcher's cleaver remains the reference tool and is not replaced by this arsenal.

## Shared art contract

- Native tools sheet: `96x64` per frame, twenty-two frames, transparent background.
- Native holsters sheet: `72x72` per frame, thirteen frames, transparent background.
- Pixel Forge source projects are `blobforge_test_arsenal_tools`, `blobforge_test_arsenal_holsters`, and the three-frame `blobforge_deployed_slingshot` in the Objects category.
- Runtime exports are horizontal spritesheets rendered with nearest-neighbor sampling only. The edge-on projectile source is `blobforge_saw_projectile`, exported as `SawProjectile.png`.
- Tool frames share a locked factory palette: near-black recess, three neutral steel values, brown grip, amber mechanism, restrained cyan energy, danger red, and green armed/status light.
- Every handheld frame defines `grip_pivot`. Ranged items define `muzzle`. Cutting tools define `edge_start` and `edge_end`. Mounted devices define `mount_socket` and their physical interaction point.
- Holsters are visibly tool-specific. Their socket and supports must agree with the item's grip or mount geometry; a generic recolored rack is not acceptable.

## Weapon and item contracts

### 1. Lightsaber

- **Silhouette:** compact black-steel hilt with cyan emitter collar and a thin, intensely bright cyan blade. The holster is a narrow insulated charging clamp with a cyan readiness strip.
- **Primary action:** the first left click ignites without swinging. Later left clicks use the common orientation-relative cleaver swing. `Z` is the dedicated de-ignition control; right-drag remains the shared base-rotation control.
- **Feel:** nearly weightless blade but a tangible hilt. Any ignited blade contact cuts locally, including a conveyor-driven blob moving into a stationary blade; this does not require blunt impulse. Swings widen the swept cut. Blood stains only the hilt. Individual physical blood pixels that touch the hot blade fizzle with a brief sizzle spark and disappear one at a time, without globally deleting blood produced by a stabbed blob.
- **Identity:** continuous positional cutting plus the common LMB-driven, orientation-relative melee arc.

### 2. Nail gun

- **Silhouette:** squat industrial cyan-and-steel body, brown grip, amber magazine rail, blunt safety nose. The holster is a shallow construction-tool cradle with a magazine shelf.
- **Primary action:** each click fires one large physical nail; holding does not auto-fire and the slower pneumatic cadence prevents rapid spam.
- **Feel:** sharp pneumatic kick, strong lift/knockback, and a large surgical puncture. A struck blob is carried along the nail's travel; when the nail reaches a solid wall it establishes a bounded physical pin. A nail that penetrates two blobs establishes an equal-and-opposite spring pin between their actual impact points, fastening them together until the connection is overstretched.
- **Identity:** positional fastening and persistent foreign objects, not general-purpose damage spam.

### 3. Shotgun

- **Silhouette:** short, broad twin-tone receiver, thick brown stock, wide muzzle, amber shell window. The holster has two deep hooks and a shell ledge.
- **Primary action:** click fires a single close-range pellet cone, followed by a mandatory physical pump/recovery driven by cursor motion or a short automatic rack.
- **Feel:** huge recoil into the grip, strong nearby deformation, scattered shallow pellet wounds at range, and catastrophic chunking only at very close range. The muzzle must not damage behind itself.
- **Identity:** one deliberate, room-shaking close-range event with meaningful recovery.

### 4. Magnum

- **Silhouette:** long heavy barrel, oversized cylinder, dark brown grip, tiny amber rear sight. The holster is a reinforced angled leather-and-steel bucket.
- **Primary action:** hold left mouse to steady and cock; release fires one heavy round. A quick click still fires but with more cursor-driven spread and less penetration.
- **Feel:** slow cadence, high recoil and muzzle rise, one deep local wound line, strong knockback without shotgun-like spread.
- **Identity:** timing and precision; the release commits the shot.

### 5. SMG

- **Silhouette:** compact stamped-steel receiver, cyan sight, downward magazine, small folding stock. The holster is a wide two-point clamp that captures receiver and magazine.
- **Primary action:** hold left mouse for automatic fire. Recoil climbs in the current aim direction; releasing for a short beat resets the climb.
- **Feel:** quick light impulses, low individual damage, accumulating peppered wounds, visible heat cadence, and a strict projectile/contact budget so sustained fire cannot collapse performance.
- **Identity:** continuous recoil management and local suppression.

### 6. Spinning blade shooter

- **Silhouette:** an edge-on horizontally loaded saw disc with visible teeth, steel launcher housing, amber rail, cyan bearing, and brown grip. The holster is a round keyed dock with a lower rail support.
- **Primary action:** hold left mouse to spin and charge a single saw disc; release launches it along the current aim line.
- **Feel:** rising angular whine and hand vibration during charge. The edge-on physical disc keeps meaningful speed through several contacts, cuts only along its actual swept path, and stops spinning when it embeds in tissue or a solid surface.
- **Identity:** charge, release, and follow the persistent moving cutter through the room.

### 7. Wood-chipper vacuum

- **Silhouette:** heavy cyan intake horn, dark motor housing, amber warning teeth visible inside, short rear hose coupling. Its wall station is a deep motor cradle with a hose reel and red throat guard.
- **Primary action:** hold left mouse to create a directional suction cone. Loose matter moves first; cohesive blobs resist until the intake physically seals against them. Matter at the throat is then shredded progressively.
- **Feel:** heavy, slow aim; suction ramps rather than snapping. Chipper load drags the hand toward the target, coughs on large chunks, and ejects blood/tissue through a bounded outlet stream.
- **Identity:** continuous matter transport and progressive consumption, governed by distance and seal quality.

### 8. Sledgehammer

- **Silhouette:** very wide dark-steel head with bright edge highlights, long brown wrapped handle, amber end cap. The holster is a floor-braced vertical pair of clamps.
- **Primary action:** hold left mouse to raise and charge; release commits the head overhead and accelerates it through a fixed downward arc. RMB toggles the left/right slam side instead of rotating the weapon. The attack finds the nearest conveyor/terrain support below the hand and carries the grip down far enough that the hammer face lands completely flat against it.
- **Feel:** slowest handheld swing, a short impact hold, light screen shake, and a deliberate but brief lifting recovery. Material beneath the hammer face is driven into the support and a bounded ground-side band is destroyed. A fully charged hit adds a broad outward/upward impulse to nearby blobs.
- **Heavy-impact flavor:** heavy-designated weapons create only a few thick, short-lived liquid blood bridges between the crushing face and the impact surface. The strands showcase the initial lift, then snap quickly.
- **Identity:** localized side destruction and grounded crushing rather than slicing, whole-body flattening, or generic knockback.

### 9. Conveyor slingshot

- **Silhouette:** bolted steel base, tall fork arms, amber clamps, dark elastic bands, cyan aim pin. The holster frame is actually a folded deployment rack because the device mounts on the conveyor.
- **Primary action:** after placement, drop or drag one blob into the cradle. Hold left mouse on that blob and pull opposite the desired launch direction. Charge is the real band extension and is capped visibly; release transfers stored band impulse into the whole blob.
- **Feel:** the blob remains physical during loading and stretching. Aim is readable from the fork-to-cradle line. A high-speed collision may splatter because of actual impact speed, never because launch itself applies arbitrary damage.
- **Identity:** uses a blob as the projectile and turns physical placement, aim, and impact into the attack.

### 10. Wall pike

- **Silhouette:** long tapered steel spike, squared wall plate, amber retaining bolts, dark blood gutter. The holster/deployment plate is the mounted base itself.
- **Primary action:** placement uses left click; once mounted it has no attack button. The normal blob grab remains active, and the player must physically slam a blob onto the tip.
- **Feel:** the first high-speed tip contact punctures locally and can pin material around the shaft. Gravity and player force can deepen or tear the wound. Bleeding follows wound depth and motion.
- **Identity:** a passive environmental tool whose damage is entirely earned through blob manipulation.

### 11. Boxing gloves

- **Silhouette:** one oversized red leather piston glove with a dark cuff and amber wrist plate. The holster is one reinforced hanging peg with a low drip tray.
- **Primary action:** hold to charge and release a physical straight thrust along the selected base direction. A full charge changes the stroke into an uppercut.
- **Feel:** short reach, strong local soft-body denting, springy rebound, and no beam or line attack. A quick punch has useful squish/knockback; the uppercut launches the contacted body and can break it apart in flight or on impact.
- **Identity:** one readable forward blunt piston whose power and trajectory are controlled by commitment.

### 12. Grenades

- **Silhouette:** compact segmented dark-steel body, amber safety lever, cyan armed pin light. The holster is a three-cell locked grenade rack; only one live grenade may be held at a time.
- **Primary action:** hold left mouse to freeze the grenade at its starting position while cursor movement rotates and charges the displayed collision-aware throw arc. Release throws and starts a fixed fuse. RMB cancels aiming and restores normal held-tool movement/rotation. Holding never cooks away fuse time.
- **Feel:** bouncy heavy casing, radial impulse at detonation, local line-of-sight damage, and bounded debris/blood emission.
- **Identity:** throwing skill and spatial risk without an in-hand cooking timer.

### 13. Whirlwind battleaxe

- **Silhouette:** broad crescent steel head, opposing rear hook, long wrapped brown haft, restrained cyan bearing at the grip. The holster is a tall forked shrine-like rack with amber Diablo-inspired diamond accents, without copying protected iconography.
- **Primary action:** currently uses the shared melee contract: hold to charge and release the same orientation-relative physical arc as the cleaver.
- **Feel:** a broad crescent head and long straight haft make the arc heavier and wider than the cleaver, with actual swept blade contact and recoil when it catches dense matter.
- **Identity:** a readable heavy battleaxe for the current common-melee pass; a later dedicated Whirlwind stance can build on this without compromising the shared rotation controls.

### 14–22. Expanded experimental set

- **Black hole:** launches a bounded miniature singularity that pulls nearby blob matter, excises material at its core, and ejects blood/tissue rather than deleting bodies invisibly.
- **Rat gun:** launches a physical animated rat that seeks the nearest blob after landing, attaches to the exact material particle it reaches, follows that deforming point, and periodically chews off small bloody pieces until its target is gone or leaves the room.
- **Enlarger:** auto-aims repeated physical growth pulses at the nearest blob. Matter, particle spacing, and collision size grow together up to roughly three times scale before a radial burst throws tissue and blood outward from the center.
- **Flamethrower:** emits a short physical flame stream, leaves bounded animated flame trails, and ignites contacted blobs so local burn damage continues briefly after impact.
- **Freeze ray:** fires physical ice bolts that lock a blob into a visible rigid ice block. The frozen body remains pickable and throwable; a sufficiently hard impact shatters it, and separated child chunks inherit the frozen state so they can shatter again.
- **Lightning coil:** launches an arc seed that damages its first physical contact and chains through a bounded number of nearby blobs with visible branching arcs.
- **Acid lobber:** charges and lobs a slightly inaccurate gravity-driven glob. It splatters on impact, coats and creeps down blob material, or rides a conveyor as a bounded persistent surface pool while progressively melting contacted matter.
- **Water doll:** the held doll cries one large, slow physical tear per click. Each impact flashes the struck blob red and shoves it; the fifth tear on one material lineage triggers an explosive physical payoff.
- **Bat + ball:** the first click makes a short physical ball lob. Later bat swings can re-hit the ball toward a nearby blob at extreme speed; `E` retrieves a reachable ball and an unreachable ball returns automatically.

## Performance and physics limits

- Bullets, nails, pellets, discs, explosion rays, sparks, and ejected tissue use fixed-capacity pools; no per-shot heap allocation is allowed in fixed update or rendering.
- A projectile queries only spatially relevant bodies. Pellet and explosion work is capped per trigger event and never scans dormant distant populations every simulation step.
- Persistent nails and discs have explicit active limits and deterministic oldest-first retirement. Cosmetic sparks do not become full granular physics particles.
- Automatic weapons schedule shots from fixed time and consume at most a bounded number per update, preventing catch-up bursts after a slow frame.
- Mounted devices sleep when empty. Holsters and static deployed structure belong in cached factory layers; only status lights, elastic bands, projectiles, and moving parts are dynamic.
- Every item requires a headless interaction regression and the station render benchmark must remain below the 13.5 ms average-frame ceiling.

## Implementation order

1. `I` palette, selection lifecycle, common physical handheld base, holster swapping, pooled projectile framework.
2. Sledgehammer and lightsaber to prove blunt versus continuous-edge melee.
3. Nail gun, magnum, shotgun, and SMG on the pooled projectile/contact system.
4. Spinning blade shooter and grenades for persistent projectile and timed radial interactions.
5. Boxing gloves and battleaxe for alternating/composed cursor gestures.
6. Slingshot and pike for placement, loading, pinning, and passive environment interaction.
7. Wood-chipper vacuum last because it combines suction, progressive topology damage, matter routing, and the strictest performance risks.
