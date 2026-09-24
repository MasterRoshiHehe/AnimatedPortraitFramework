# Animated Portrait Framework (APF)

A SMAPI framework for **animated NPC portraits** in Stardew Valley 1.6+.

Version 1.3.0 adds native conditional variant rules for both animated portraits and overworld sprites. APF evaluates rules, selects weighted variants, and applies late sprite overlays internally; SVO is no longer required. Modders create simple **Content Packs** — just a folder with `manifest.json`, `content.json`, and PNG spritesheets. No C# coding required.

---

## Features

- **Unlimited expressions** — not limited to the vanilla 6. Define as many as you need.
- **Per-expression spritesheets** — each expression gets its own small PNG file. Memory-efficient, easy to manage.
- **Single-sheet mode** — alternatively, put all expressions in one combined spritesheet.
- **4 animation modes** — `loop`, `pingpong`, `once`, `once-pingpong`.
- **Auto DDF integration** — portrait position, scale and offset are injected into Dialogue Display Framework automatically.
- **Performance-safe texture caching** — variant strings are normalized and cached to avoid redundant texture reload loops during dialogue.
- **Native conditional variants** — select portrait and overworld sub-variants from weather, season, time, location, hearts, day of week, mail/event flags, or loaded mods.
- **Weighted variant selection** — highest-priority eligible variants compete using configurable relative weights, with stable daily rolls.
- **Overworld sprite overlays** — winning variants are patched at late asset priority while preserving extended animation frames from other edits.
- **Dynamic GMCM toggles** — enable or disable each discovered variant without editing content files.
- **Content Patcher compatibility** — APF respects portrait suffix overrides and applies sprite overlays late in the asset pipeline.
- **Standard SMAPI content packs** — drop in `Mods/`, done.

---

## Recent 1.3.0 Changes

This release merges Sprite Variant Orchestrator functionality into APF:

- Added native `Rules` loading alongside `Portraits`, including recursive `Include` files.
- Added conditional evaluation for hearts, weather, season, time, location, day of week, mail/event flags, and loaded mods.
- Added priority and weighted selection for competing variants, with a stable roll per NPC, root variant, and in-game day.
- Applied selected variants to APF portrait resolution and cached the winning NPC/root pair.
- Added late-priority overworld sprite patching so `Characters/{NPC}_{Root}` can receive `Characters/{NPC}_{Root_Suffix}` overlays.
- Added dynamic GMCM boolean toggles for every discovered variant ID.
- Removed the structural dependency on SVO; APF now owns the complete variant workflow.

---

## Requirements

| Mod | Required | Link |
|-----|----------|------|
| SMAPI 4.0+ | Yes | [smapi.io](https://smapi.io/) |
| Dialogue Display Framework Continued | Yes | [Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/7569) |
| Content Patcher | Only for Option B (external textures) | [Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/1915) |

## Installation

1. Install **SMAPI** and **Dialogue Display Framework Continued**.
2. Download this mod and extract the `AnimatedPortraitFramework` folder into your `Stardew Valley/Mods/` directory.
3. Download or create any APF content packs and place them in `Mods/` as well.
4. Launch the game via SMAPI.

---

## Creating a Content Pack

You can create content packs entirely by hand — no special tools needed. You just need:
- An image editor (Photoshop, GIMP, Paint.NET, etc.) or any way to generate PNG sprite grids
- A text editor for JSON files

### Per-Expression Mode (Recommended)

Each expression is a **separate PNG spritesheet**. This is the easiest approach and uses less memory.

#### Folder Structure

```
[APF] My NPC Pack/
├── manifest.json
├── content.json
└── Portraits/
    ├── Haley_0.png     ← Expression 0 spritesheet (e.g. 10 frames in a 5×2 grid)
    ├── Haley_1.png     ← Expression 1 spritesheet
    ├── Haley_2.png     ← Expression 2 spritesheet
    └── ...
```

#### manifest.json

```json
{
    "Name": "[APF] My NPC Pack",
    "Author": "YourName",
    "Version": "1.0.0",
    "Description": "Animated portraits for Haley.",
    "UniqueID": "YourName.APFMyNpcPack",
    "ContentPackFor": {
        "UniqueID": "tyr4ntx.AnimatedPortraitFramework"
    }
}
```

> **Note:** The `UniqueID` must only contain letters, numbers, dots, hyphens, and underscores. No spaces or brackets.

#### content.json (Per-Expression Mode)

```json
{
    "Format": "1.0.0",
    "Portraits": [
        {
            "Target": "Haley",
            "FrameSize": 1024,
            "Columns": 5,
            "Ddf": {
                "XOffset": -950,
                "YOffset": -950,
                "Scale": 1,
                "Position": "bottom"
            },
            "Expressions": {
                "0": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Haley_0.png" },
                "1": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Haley_1.png" },
                "2": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Haley_2.png" },
                "3": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Haley_3.png" },
                "4": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Haley_4.png" },
                "5": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Haley_5.png" }
            }
        }
    ]
}
```

When each expression defines its own `Sprite` path, the framework enters **per-expression mode**: only the current expression's texture is loaded into VRAM, keeping memory usage low.

---

### Single-Sheet Mode (Alternative)

All expressions go into one combined PNG. The framework computes row offsets automatically.

#### content.json (Single-Sheet Mode)

```json
{
    "Format": "1.0.0",
    "Portraits": [
        {
            "Target": "Haley",
            "Sprite": "Portraits/Haley.png",
            "FrameSize": 1024,
            "Columns": 5,
            "Ddf": {
                "XOffset": -950,
                "YOffset": -950,
                "Scale": 1,
                "Position": "bottom"
            },
            "Expressions": {
                "0": { "Frames": 10, "Fps": 10, "Mode": "pingpong" },
                "1": { "Frames": 10, "Fps": 10, "Mode": "pingpong" },
                "2": { "Frames": 10, "Fps": 10, "Mode": "loop" }
            }
        }
    ]
}
```

Note the top-level `Sprite` and no per-expression `Sprite` paths. All frames are in one big PNG, arranged in a grid. Expression 0 starts at the top-left, expressions are stacked vertically.

---

### Spritesheet Layout

Each spritesheet is a PNG grid of square frames:

```
Frame layout for 10 frames, 5 columns:

┌──────┬──────┬──────┬──────┬──────┐
│  0   │  1   │  2   │  3   │  4   │
├──────┼──────┼──────┼──────┼──────┤
│  5   │  6   │  7   │  8   │  9   │
└──────┴──────┴──────┴──────┴──────┘

Image size = 5 × 1024 = 5120px wide, 2 × 1024 = 2048px tall
```

You can use any frame size (e.g. 512, 1024). Just make sure `FrameSize` matches.

---

## Field Reference

### Portrait Definition

| Field | Default | Description |
|-------|---------|-------------|
| `Target` | *(required)* | NPC internal name (e.g. `"Haley"`, `"Leah"`, `"Abigail"`) |
| `Sprite` | `null` | Path to combined spritesheet (single-sheet mode). Omit for per-expression mode. |
| `FrameSize` | `1024` | Width & height of each frame in pixels (square). |
| `Columns` | `5` | Number of columns in the spritesheet grid. |
| `Ddf` | `null` | DDF display settings (see below). If omitted, no DDF config is injected. |
| `OverrideDdf` | `true` | When `false`, APF does not forcibly overwrite DDF portrait sizing/offsets so another mod can own framing and layout. |

### DDF Settings (optional)

| Field | Default | Description |
|-------|---------|-------------|
| `XOffset` | `-950` | Horizontal portrait offset in the dialogue box. |
| `YOffset` | `-950` | Vertical portrait offset. |
| `Scale` | `1` | Portrait scale factor. |
| `Position` | `"bottom"` | Portrait anchor position. |
| `BoxWidth` | *(none)* | Override dialogue box width. |
| `BoxHeight` | *(none)* | Override dialogue box height. |

### Expression Definition

| Field | Default | Description |
|-------|---------|-------------|
| `Frames` | `10` | Number of unique animation frames (before pingpong expansion). |
| `Fps` | `8` | Frames per second. |
| `Mode` | `"loop"` | Animation mode (see below). |
| `Sprite` | *(none)* | Per-expression spritesheet path. If any expression has this, per-expression mode activates. |

### Animation Modes

| Mode | Behavior |
|------|----------|
| `loop` | Plays frames 0 → N, restarts at 0. Repeats forever. |
| `pingpong` | Plays 0 → N → 1 → 0 → N → ... Bounces back and forth. |
| `once` | Plays 0 → N once, then holds the last frame. |
| `once-pingpong` | Plays 0 → N → 0 once, then holds frame 0. |

### Expression Keys

Expression keys correspond to the vanilla portrait index:

| Key | Vanilla Meaning |
|-----|----------------|
| `"0"` | Neutral / Default |
| `"1"` | Happy |
| `"2"` | Sad |
| `"3"` | Unique (varies per NPC) |
| `"4"` | Love / Blush |
| `"5"` | Angry |
| `"6"+` | Custom (mod-defined, no upper limit) |

Any expression NOT listed in the config passes through with no animation (vanilla behavior).

---

## Multiple NPCs in One Pack

You can define multiple NPCs in a single content pack:

```json
{
    "Format": "1.0.0",
    "Portraits": [
        {
            "Target": "Haley",
            "FrameSize": 1024,
            "Columns": 5,
            "Ddf": { "XOffset": -950, "YOffset": -950, "Scale": 1, "Position": "bottom" },
            "Expressions": {
                "0": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Haley_0.png" },
                "1": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Haley_1.png" }
            }
        },
        {
            "Target": "Leah",
            "FrameSize": 1024,
            "Columns": 5,
            "Ddf": { "XOffset": -950, "YOffset": -950, "Scale": 1, "Position": "bottom" },
            "Expressions": {
                "0": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Leah_0.png" },
                "1": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Leah_1.png" }
            }
        }
    ]
}
```

---

## Splitting content.json into Multiple Files

Large packs can split their portrait definitions across several JSON files using the optional top-level `Include` field. Each included file uses the **same format** as `content.json` and its portraits are appended to the pack's portrait list:

```json
{
    "Format": "1.0.0",
    "Include": [
        "definitions/Haley.json",
        "definitions/Abigail.json",
        "definitions/Emily.json"
    ]
}
```

And e.g. `definitions/Haley.json`:

```json
{
    "Format": "1.0.0",
    "Portraits": [
        {
            "Target": "Haley",
            "FrameSize": 1024,
            "Columns": 5,
            "Expressions": {
                "0": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Haley_0.png" }
            }
        }
    ]
}
```

Notes:

- Paths are relative to the **content pack folder** (not to the including file).
- `Sprite` paths inside included files are also relative to the pack folder, exactly as in `content.json` — splitting files does not change any sprite paths.
- You can mix both: `content.json` may contain its own `Portraits` **and** an `Include` list.
- Included files may themselves include further files. Each file is loaded at most once; duplicates and missing files log a warning instead of breaking the pack.
- If the same `Target` is defined twice within one pack, the last definition wins (a warning is logged).
- `Include` is fully optional — existing packs with a single `content.json` work unchanged.

---

## Conditional Portrait and Sprite Variants

APF can select a sub-variant after resolving a portrait's normal root variant. Rules can be placed in the same `content.json` as the portrait definitions or in included files. The selected ID is used for both the portrait variant and the matching overworld character sprite.

### Unified `content.json` Schema

```json
{
    "Format": "1.0.0",
    "Portraits": [
        {
            "Target": "Haley",
            "FrameSize": 1024,
            "Columns": 5,
            "Variants": [
                "Beach",
                "Beach_PinkSuit",
                "Beach_BlackTube"
            ],
            "Expressions": {
                "0": { "Frames": 10, "Fps": 10, "Mode": "pingpong", "Sprite": "Portraits/Haley_0.png" }
            }
        }
    ],
    "Rules": [
        {
            "NPC": "Haley",
            "Root": "Beach",
            "Variants": [
                {
                    "Id": "Beach_PinkSuit",
                    "Priority": 20,
                    "Weight": 1.0,
                    "Conditions": [
                        { "Type": "Hearts", "Min": 8 }
                    ]
                },
                {
                    "Id": "Beach_BlackTube",
                    "Priority": 10,
                    "Conditions": [
                        { "Type": "Hearts", "Min": 4 }
                    ]
                }
            ]
        }
    ]
}
```

For the example above, provide these assets through the content pipeline:

```
Characters/Haley_Beach.png
Characters/Haley_Beach_PinkSuit.png
Characters/Haley_Beach_BlackTube.png
```

`Root` must match a variant that APF resolves for the NPC. `Id` should follow the `Root_Suffix` convention. Among eligible variants, the highest `Priority` wins; variants at that priority are selected by their relative `Weight`. A roll is stable for the current in-game day, so it does not change every frame.

## Overworld Sprite Integration (Content Patcher)

APF natively synchronizes animated portraits with overworld sprites. When the game requests a base sprite, APF patches the winning sub-variant onto the existing image with `AssetEditPriority.Late`. This preserves `Action: EditImage` changes made by other mods, including extended animation frames.

For this to work, your companion Content Patcher pack must provide three things:

1. **The Appearance Hook:** Create an NPC Appearance entry in `Data/Characters` with a `Sprite` field pointing to your root, such as `Characters/Haley_Beach`.
2. **The Fallback Asset:** Load the base `Characters/Haley_Beach` asset into the pipeline so the game does not error before variants are evaluated.
3. **The Sub-Variant Assets:** Load every sub-variant referenced in your APF rules as a named asset, such as `Characters/Haley_Beach_PinkSuit`.

## Condition Reference

All conditions use AND logic: every condition in a variant's list must pass for that variant to be eligible.

| Type | Required fields | Optional fields | Passes when |
|------|-----------------|-----------------|-------------|
| `Hearts` | - | `Min`, `Max` | Player's friendship hearts with the NPC are within the given range. |
| `Weather` | `Value` | - | Current weather matches: `Sun`, `Rain`, `Storm`, `Snow`, or `Wind`. |
| `Season` | `Value` | - | Current season matches: `Spring`, `Summer`, `Fall`, or `Winter`. |
| `Time` | - | `Min`, `Max` | Current in-game time, from 600 to 2600, is within range. |
| `Location` | `Name` | - | Current location name equals or starts with `Name`. |
| `DayOfWeek` | `Value` | - | Current day matches `Monday` through `Sunday`. |
| `HasFlag` | `Flag` | - | `mailReceived` or `eventsSeen` contains the flag. |
| `Mod` | `ModId` | - | SMAPI's mod registry contains the given mod UniqueID. |

All conditions on a candidate must pass. Unknown condition types fail evaluation. Use the optional `Rules` list with `Include` files to keep large rule sets separate from portrait definitions.

---

## How to Create Spritesheets

You don't need any special tools. Any method that produces a PNG grid of square frames works:

1. **From video clips** — Use ffmpeg to extract frames from a short video, then tile them into a grid with any image editor or script.
2. **From individual images** — Arrange your hand-drawn or AI-generated frames in a grid using Photoshop, GIMP, or ImageMagick.
3. **From animated GIFs** — Split the GIF into frames, resize to your target frame size, then tile into a grid.

### Example with ffmpeg + ImageMagick

```bash
# Extract 10 frames from a 3-second clip, resize to 1024x1024
ffmpeg -i input.mp4 -vf "fps=10/3,scale=1024:1024" -frames:v 10 frame_%04d.png

# Tile into a 5-column grid
magick montage frame_*.png -tile 5x -geometry 1024x1024+0+0 spritesheet.png
```

### Tips

- Use **transparent backgrounds** (PNG with alpha) for best results with DDF.
- Keep frame counts reasonable (8–20 frames per expression is typical).
- `pingpong` mode doubles your visible frames: 10 source frames → 18 animated frames.
- All frames must be the **same square size** (e.g. 1024×1024).

---

## One-Time Variant Trigger Dialogues

Heart variants can show a localized reaction the first time the player talks to an NPC after reaching a configured threshold. The trigger is shown before the normal NPC dialogue, and the normal dialogue resumes afterwards.

```json
"VariantTriggers": {
    "Hearts3": {
        "Mode": "once",
        "DialogueKey": "trigger.haley.hearts3",
        "Expression": 8
    }
}
```

Put the English fallback text in the content pack's `i18n/default.json`:

```json
{
    "trigger.haley.hearts3": "Wait—has this dress always fit me like this?"
}
```

- `DialogueKey` is resolved through the owning content pack's SMAPI translations.
- `Dialogue` remains available as a backwards-compatible raw-text fallback.
- `Expression` is an optional numeric portrait index and is kept out of translated text.
- `Mode: "once"` is persisted per save. When several thresholds were skipped, only the highest current threshold is shown.
- Triggers are skipped during events and when heart-based growth is disabled.

## Compatibility

- **Stardew Valley** 1.6+
- **SMAPI** 4.0+
- **Content Patcher**: compatible with portrait suffix overrides and other texture edits. APF applies overworld variant overlays at late asset priority.
- **Sprite Variant Orchestrator (SVO)**: no longer required. Remove SVO after updating to APF 1.3.0 to avoid duplicate variant logic.
- Compatible with other portrait mods — APF only replaces portraits for NPCs defined in its content packs.
- Multiple APF content packs can coexist (last loaded wins if targeting the same NPC).

## Troubleshooting

- **Portraits don't animate**: Check the SMAPI log for errors. Make sure DDF Continued is installed. Verify your `content.json` expression keys match the NPC's portrait indices.
- **Wrong portrait position/size**: Adjust `XOffset`, `YOffset`, and `Scale` in the `Ddf` section. Common values for 1024px frames: `XOffset: -950, YOffset: -950, Scale: 1`.
- **Content Patcher overwrites vanish**: Set `"OverrideDdf": false` on the portrait definition if another mod is meant to own portrait layout and placement. APF's overworld variant overlays are applied at late asset priority.
- **A sprite variant does not appear**: Confirm that `Root` matches an APF portrait variant, `Id` uses the `Root_Suffix` convention, and the matching `Characters/{NPC}_{Id}.png` asset is available through the content pipeline.
- **A variant never wins**: Check its GMCM toggle, `Priority`, `Weight`, and every condition. A disabled variant or a candidate with a failed condition is excluded.
- **Missing expression**: If an expression key (e.g. `"3"`) isn't defined, the game shows the static vanilla portrait for that expression. This is intentional.
- **SMAPI says "content pack not loaded"**: Check that `ContentPackFor.UniqueID` is exactly `tyr4ntx.AnimatedPortraitFramework`.

---

## For Modders

APF is designed to be easy to create content for. The full workflow is:

1. Create portrait frames (any method — video, drawing, AI generation, etc.)
2. Arrange frames into PNG sprite grids (one per expression, or one combined sheet)
3. Write `manifest.json` and `content.json`
4. Drop the folder into `Mods/` and test

No special tools or software required beyond an image editor and a text editor.

---

## Priority, Tiebreaking, and GMCM

When multiple variants pass their conditions simultaneously, APF picks the winner by:

1. **Highest `Priority` wins.** Give rare or more specific variants a higher number.
2. **`Weight` selects within a priority tier.** Eligible variants sharing the highest `Priority` are selected with a deterministic daily roll. A weight of `2.0` is twice as likely as a weight of `1.0`.

**GMCM Configuration**

If Generic Mod Config Menu is installed, APF automatically registers a configuration page. Alongside the existing portrait and content-pack settings, APF generates a boolean toggle for every discovered `Variant.Id`. Disabling a toggle excludes that outfit from evaluation. Variants default to enabled when no saved toggle exists.

---

## Credits

- **Framework**: tyr4ntx
- **Dialogue Display Framework**: aedenthorn (original), Mangupix (Continued)
- **SMAPI**: Pathoschild

## License

MIT — free to use, modify, and redistribute. Content pack authors: you may distribute your APF content packs freely.
