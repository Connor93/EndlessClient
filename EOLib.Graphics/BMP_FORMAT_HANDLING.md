# BMP Format Handling in EOLib.Graphics

## Overview

GFX resources (`.egf` files) are Windows PE (Portable Executable) files containing embedded bitmap resources. `PELoaderLib` extracts these as raw BMP byte arrays, which `NativeGraphicsManager` then loads via `StbImageSharp`.

## The BI_BITFIELDS Problem

### Background

BMP files can use different compression types:
- **BI_RGB (0)**: Uncompressed, channel layout implied by bit depth
- **BI_BITFIELDS (3)**: Uncompressed, but uses explicit color masks to define channel bit layout

When the EGF files were updated (commit `22c78689f037`), many resources changed from BI_RGB to **16-bit BI_BITFIELDS** format (RGB565: R=`0xF800`, G=`0x07E0`, B=`0x001F`).

### Two Issues Were Found

#### 1. Incorrect `bfOffBits` from PELoaderLib

For BI_BITFIELDS BMPs with a 40-byte `BITMAPINFOHEADER`, three DWORD color masks (12 bytes) sit between the info header and the pixel data:

```
Offset 0:  BMP File Header (14 bytes)
Offset 14: BITMAPINFOHEADER (40 bytes)
Offset 54: Color Masks (12 bytes) ← R, G, B masks
Offset 66: Pixel Data starts here
```

PELoaderLib calculates `bfOffBits = 14 + 40 = 54`, missing the 12 mask bytes. The correct value is `66`.

#### 2. StbImageSharp Does Not Support 16bpp BI_BITFIELDS

Even after fixing `bfOffBits`, StbImageSharp cannot decode 16-bit per-pixel BMP data with BI_BITFIELDS compression. It primarily supports 8bpp+ BI_RGB formats.

This caused pixel data to be misinterpreted, resulting in **visual offsets in every direction** — the symptom that prompted this investigation.

### The Fix in `FixBitmapData` / `Convert16bppBitfieldsTo32bppRgb`

The `FixBitmapData` method in `NativeGraphicsManager.cs` handles three cases:

| Case | BPP | Header Size | Action |
|------|-----|-------------|--------|
| 16bpp BI_BITFIELDS | 16 | 40 | **Full conversion** to 32bpp BI_RGB via `Convert16bppBitfieldsTo32bppRgb` |
| 32bpp BI_BITFIELDS | 32 | 40 | Fix `bfOffBits` from 54 → 66 |
| Any BI_BITFIELDS | any | ≥56 (V3+) | Change compression to BI_RGB (masks are embedded in header) |

The 16bpp conversion:
1. Reads the 3 DWORD color masks from offset 54
2. Expands each 16-bit pixel to 32-bit BGRA using bit shifting and scaling
3. Handles BMP row padding (rows are DWORD-aligned)
4. Rewrites headers for 32bpp BI_RGB format

### Diagnostic Tool

`tools/BmpHeaderDiag` is a standalone C# console app that reads EGF files and dumps BMP header information for each resource. Usage:

```bash
cd tools/BmpHeaderDiag
dotnet run -- ../../ClientAssets/gfx/gfx021.egf
```

Output columns: Resource ID, Width, Height, BPP, Header Size, Compression, bfOffBits, bfSize, Issue Flag.

### Real-World Data (gfx021.egf — NPC Sprites)

- 234 total resources
- 186 use BI_BITFIELDS (16bpp) — **all flagged as BAD_OFF**
- 48 use BI_RGB (16bpp) — loaded correctly without conversion
- All have 40-byte info headers
- Color masks: RGB565 (R=`0xF800`, G=`0x07E0`, B=`0x001F`)

### Affected GFX Files — Full Scan Results

The commit `22c78689f037` updated these EGF files. Diagnostic scan results:

| File | GFX Type | Resources | BI_BITFIELDS (16bpp) | BI_RGB | BPP | **Needs Fix** |
|------|----------|-----------|---------------------|--------|-----|--------------|
| gfx004.egf | MapObjects | 500 | 0 | 500 | 32 | No |
| gfx008.egf | SkinSprites | 9 | 0 | 9 | 32 (56-byte V3 header) | No |
| gfx013.egf | MaleArmor | 220 | 0 | 220 | 24 | No |
| gfx014.egf | FemaleArmor | 220 | 0 | 220 | 24 | No |
| gfx017.egf | MaleWeapons | 86 | **56** | 30 | 16 | **Yes** |
| gfx018.egf | FemaleWeapons | 86 | **56** | 30 | 16 | **Yes** |
| gfx021.egf | NPC | 234 | **186** | 48 | 16 | **Yes** |
| gfx023.egf | Items | 498 | 0 | 498 | 24 | No |

**3 of 8 files** contain 16bpp BI_BITFIELDS resources that require the `Convert16bppBitfieldsTo32bppRgb` fix.
All BI_BITFIELDS resources use the same RGB565 color masks: R=`0xF800`, G=`0x07E0`, B=`0x001F`.
