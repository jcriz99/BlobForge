# Pixel Forge Studio

A local, indexed-color pixel-art editor designed for shared use by a person and Codex. The browser UI and MCP server read and write the same `.pixelforge.json` projects.

## Start the editor

Double-click `start-studio.cmd`, or run:

```powershell
dotnet run --project PixelForgeStudio --configuration Release -- serve
```

Open <http://127.0.0.1:4876>. Projects are autosaved. The default data folder is `PixelForgeStudio/PixelForgeData`; set `PIXEL_FORGE_DATA` to change it.

The project picker groups artwork into **Tilesets**, **Objects**, and **Misc Art**. Use the search field to filter projects by name or section, and use the section selector beside the project picker to recategorize the open project. Older projects are categorized automatically when loaded.

The **Art Direction** inspector provides approved-reference overlays, palette locking, rectangular selection/mask copy-paste and transforms, contact sheets, side-by-side comparison, tile/loop validation, named attachment points, production reports, and direct BlobForge asset preview/export. Plain mouse-wheel input zooms the canvas; `Shift` + mouse wheel scrolls vertically through oversized artwork.

For Codex-authored work, MCP also supports atomic mixed-operation batches with dry-run pixel-diff reports and optimistic revision checks. Layers may be marked frame-invariant, and cross-frame validation reports occupancy, bounds, silhouette-component, and supposedly static-layer drift. Animation operations add named clips, forward/reverse/ping-pong playback, loop modes, atomic timing ranges, clip-specific contact sheets, transition cadence, duplicate holds, and keyed attachment-motion diagnostics. These safeguards make automated edits reviewable and prevent partial, stale, or mechanically inconsistent animation work.

The browser timeline supports named clip playback plus configurable red previous-frame and blue next-frame onion skins. Attachment points can be global or keyed on the current frame to form a validated machinery motion track.

## MCP

The `pixel_forge` MCP server is configured in Codex with the published executable in `mcp` mode. It exposes project creation, project resources, pixel batches, palettes and palette locks, layers and frame-invariant layers, animation frames and named clips, range timing, playback-order contact sheets, animation and attachment-motion reports, atomic dry-run/commit operation batches, drawing primitives, fills, whole-canvas and region transforms, rectangular selection data, approved references, attachment points, production and cross-frame reports, comparisons, validation, BlobForge preview bridging, and engine exports.

Restart Codex after the initial installation so it discovers the new server. The visual editor does not have to be open for MCP operations.

## Exports

- Transparent current-frame PNG
- Horizontal spritesheet PNG with exact nearest-neighbor scaling
- Full project JSON
- Engine pack ZIP containing the spritesheet, atlas/timing JSON, and import notes for Godot, Unity, and MonoGame

Exports are also saved under `PixelForgeData/exports/<project-name>/`.
