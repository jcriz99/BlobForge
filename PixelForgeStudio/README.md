# Pixel Forge Studio

A local, indexed-color pixel-art editor designed for shared use by a person and Codex. The browser UI and MCP server read and write the same `.pixelforge.json` projects.

## Start the editor

Double-click `start-studio.cmd`, or run:

```powershell
dotnet run --project PixelForgeStudio --configuration Release -- serve
```

Open <http://127.0.0.1:4876>. Projects are autosaved. The default data folder is `PixelForgeStudio/PixelForgeData`; set `PIXEL_FORGE_DATA` to change it.

The project picker groups artwork into **Tilesets**, **Objects**, and **Misc Art**. Use the search field to filter projects by name or section, and use the section selector beside the project picker to recategorize the open project. Older projects are categorized automatically when loaded.

## MCP

The `pixel_forge` MCP server is configured in Codex with the published executable in `mcp` mode. It exposes project creation, project resources, pixel batches, palettes, layers, animation frames, lines, rectangles, ellipses, flood fill, transforms, rendered previews, and engine exports.

Restart Codex after the initial installation so it discovers the new server. The visual editor does not have to be open for MCP operations.

## Exports

- Transparent current-frame PNG
- Horizontal spritesheet PNG with exact nearest-neighbor scaling
- Full project JSON
- Engine pack ZIP containing the spritesheet, atlas/timing JSON, and import notes for Godot, Unity, and MonoGame

Exports are also saved under `PixelForgeData/exports/<project-name>/`.
