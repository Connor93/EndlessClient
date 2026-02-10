#!/usr/bin/env python3
"""
Bitmap Font Generator for EndlessClient
Generates BMFont-compatible XML (.fnt) + PNG sprite sheet files
that are compatible with MonoGame.Extended's BitmapFont system.

Usage:
    python3 generate_fonts.py [--sizes 14 16 18 20] [--font /path/to/font.ttf]

If no font is specified, uses Arial (macOS) or Microsoft Sans Serif (Windows).
"""

import argparse
import math
import os
import sys
import xml.etree.ElementTree as ET
from xml.dom import minidom

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    print("Error: Pillow is required. Install with: pip3 install Pillow")
    sys.exit(1)


# Character ranges matching the original fontgen.bmfc config
CHAR_RANGES = [
    (32, 126),      # Basic Latin
    (160, 591),     # Latin Extended
    (7680, 7935),   # Latin Extended Additional
]

# Default font search paths by platform
FONT_SEARCH_PATHS_MAC = [
    "/System/Library/Fonts/Supplemental/Arial.ttf",
    "/System/Library/Fonts/Helvetica.ttc",
    "/Library/Fonts/Arial.ttf",
]

FONT_SEARCH_PATHS_WIN = [
    "C:/Windows/Fonts/micross.ttf",  # Microsoft Sans Serif
    "C:/Windows/Fonts/arial.ttf",
]


def find_system_font():
    """Find the best available system font."""
    import platform
    paths = FONT_SEARCH_PATHS_MAC if platform.system() == "Darwin" else FONT_SEARCH_PATHS_WIN
    for path in paths:
        if os.path.exists(path):
            return path
    raise FileNotFoundError(
        "Could not find a suitable system font. "
        "Please specify a font path with --font"
    )


def get_char_list():
    """Get the full list of character code points to include."""
    chars = []
    for start, end in CHAR_RANGES:
        chars.extend(range(start, end + 1))
    return chars


def measure_char(font, char_code):
    """Measure a single character's dimensions."""
    try:
        char = chr(char_code)

        # Get the advance width using getlength (works for all chars including space)
        advance = font.getlength(char)
        if advance <= 0 and char_code != 32:
            return None

        # Use getbbox for visible bounds
        bbox = font.getbbox(char)

        # Determine if this is a whitespace/invisible character
        is_whitespace = False
        if bbox is None:
            is_whitespace = True
        else:
            left, top, right, bottom = bbox
            width = right - left
            height = bottom - top
            if width <= 0 or height <= 0:
                is_whitespace = True

        if is_whitespace:
            # Whitespace character (space, nbsp, etc.) — no visible pixels
            # but still needs to exist in the font with an xadvance
            return {
                "char": char,
                "code": char_code,
                "width": max(1, int(advance)),  # minimal width for packing
                "height": 1,  # minimal height
                "left": 0,
                "top": 0,
                "advance": int(advance),
                "is_whitespace": True,
            }

        return {
            "char": char,
            "code": char_code,
            "width": width,
            "height": height,
            "left": left,
            "top": top,
            "advance": int(advance),
            "is_whitespace": False,
        }
    except Exception:
        return None


def pack_chars(char_metrics, texture_size, padding=1, spacing=1):
    """Pack characters into a texture atlas using simple row packing."""
    x = spacing
    y = spacing
    row_height = 0
    packed = []

    for m in char_metrics:
        w = m["width"] + padding * 2
        h = m["height"] + padding * 2

        # Check if we need a new row
        if x + w + spacing > texture_size:
            x = spacing
            y += row_height + spacing
            row_height = 0

        # Check if we've exceeded texture height
        if y + h + spacing > texture_size:
            return None  # Texture too small

        packed.append({
            **m,
            "x": x,
            "y": y,
            "packed_width": w,
            "packed_height": h,
        })

        row_height = max(row_height, h)
        x += w + spacing

    return packed


def find_texture_size(char_metrics, padding=1, spacing=1):
    """Find the smallest power-of-2 texture size that fits all characters."""
    # Start larger since we have 783 characters
    for size in [128, 256, 384, 512, 768, 1024, 1536, 2048]:
        result = pack_chars(char_metrics, size, padding, spacing)
        if result is not None:
            return size, result
    raise ValueError("Characters don't fit in any reasonable texture size")


def render_font(font_path, pixel_size, output_dir, padding_top=1, padding_bottom=1,
                padding_left=0, padding_right=1, spacing=1):
    """Generate a BMFont-compatible .fnt + .png for a given pixel size."""
    print(f"  Generating {pixel_size}px font...")

    # Load the font
    font = ImageFont.truetype(font_path, pixel_size)

    # Get font metrics
    ascent, descent = font.getmetrics()
    line_height = ascent + descent

    # Measure all characters
    char_list = get_char_list()
    char_metrics = []
    for code in char_list:
        m = measure_char(font, code)
        if m is not None:
            char_metrics.append(m)

    if not char_metrics:
        print(f"  ERROR: No renderable characters found for {pixel_size}px")
        return False

    # Use a fixed height for all characters (matching BMFont's useFixedHeight=1)
    fixed_height = line_height + padding_top + padding_bottom

    # Find texture size and pack characters
    # Override heights to use fixed height
    for m in char_metrics:
        m["packed_height_override"] = fixed_height

    texture_size, packed = find_texture_size_fixed_height(
        char_metrics, fixed_height, padding_left, padding_right, spacing
    )

    print(f"  Texture size: {texture_size}x{texture_size}, {len(packed)} characters")

    # Render the texture
    basename = f"sans_{pixel_size}px"
    img = Image.new("RGBA", (texture_size, texture_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    for p in packed:
        if p.get("is_whitespace"):
            continue  # No visible pixels to draw for whitespace
        char = chr(p["code"])
        # Draw the character at its origin within the fixed-height cell.
        # Pillow's draw.text uses the glyph's own bbox to position it —
        # descenders naturally extend below the baseline without extra offsets.
        draw_x = p["x"] + padding_left - p["left"]
        draw_y = p["y"] + padding_top
        draw.text((draw_x, draw_y), char, font=font, fill=(255, 255, 255, 255))

    # Save the PNG
    png_path = os.path.join(output_dir, f"{basename}_0.png")
    img.save(png_path)
    print(f"  Saved: {png_path}")

    # Generate the BMFont XML
    fnt_path = os.path.join(output_dir, f"{basename}.fnt")
    generate_fnt_xml(
        fnt_path, basename, font_path, pixel_size,
        line_height, ascent, texture_size,
        packed, fixed_height,
        padding_top, padding_bottom, padding_left, padding_right, spacing
    )
    print(f"  Saved: {fnt_path}")

    return True


def find_texture_size_fixed_height(char_metrics, fixed_height, padding_left, padding_right, spacing):
    """Find texture size using fixed height for all characters."""
    for size in [128, 256, 384, 512, 768, 1024, 1536, 2048]:
        result = pack_chars_fixed_height(
            char_metrics, size, fixed_height, padding_left, padding_right, spacing
        )
        if result is not None:
            return size, result
    raise ValueError(f"Characters don't fit in any reasonable texture size")


def pack_chars_fixed_height(char_metrics, texture_size, fixed_height, padding_left, padding_right, spacing):
    """Pack characters with fixed height into a texture."""
    x = spacing
    y = spacing
    packed = []

    for m in char_metrics:
        w = m["width"] + padding_left + padding_right
        h = fixed_height

        if x + w + spacing > texture_size:
            x = spacing
            y += h + spacing

        if y + h + spacing > texture_size:
            return None

        # Use the pre-measured advance width
        xadvance = m.get("advance", m["width"] + m["left"])

        packed.append({
            **m,
            "x": x,
            "y": y,
            "packed_width": w,
            "packed_height": h,
            "xadvance": xadvance,
        })

        x += w + spacing

    return packed


def generate_fnt_xml(fnt_path, basename, font_path, pixel_size,
                     line_height, base, texture_size,
                     packed_chars, fixed_height,
                     padding_top, padding_bottom, padding_left, padding_right, spacing):
    """Generate a BMFont XML (.fnt) file."""
    # Get font face name
    font_face = os.path.splitext(os.path.basename(font_path))[0]

    root = ET.Element("font")

    # Info element
    info = ET.SubElement(root, "info")
    info.set("face", font_face)
    info.set("size", str(-pixel_size))
    info.set("bold", "0")
    info.set("italic", "0")
    info.set("charset", "")
    info.set("unicode", "1")
    info.set("stretchH", "100")
    info.set("smooth", "0")
    info.set("aa", "1")
    info.set("padding", f"{padding_top},{padding_right},{padding_bottom},{padding_left}")
    info.set("spacing", f"{spacing},{spacing}")
    info.set("outline", "0")

    # Common element
    common = ET.SubElement(root, "common")
    common.set("lineHeight", str(fixed_height))
    common.set("base", str(base + padding_top))
    common.set("scaleW", str(texture_size))
    common.set("scaleH", str(texture_size))
    common.set("pages", "1")
    common.set("packed", "0")
    common.set("alphaChnl", "0")
    common.set("redChnl", "4")
    common.set("greenChnl", "4")
    common.set("blueChnl", "4")

    # Pages element
    pages = ET.SubElement(root, "pages")
    page = ET.SubElement(pages, "page")
    page.set("id", "0")
    page.set("file", f"{basename}_0.png")

    # Chars element
    chars = ET.SubElement(root, "chars")
    chars.set("count", str(len(packed_chars)))

    for p in packed_chars:
        char = ET.SubElement(chars, "char")
        char.set("id", str(p["code"]))
        char.set("x", str(p["x"]))
        char.set("y", str(p["y"]))
        char.set("width", str(p["packed_width"]))
        char.set("height", str(p["packed_height"]))
        char.set("xoffset", "0")
        char.set("yoffset", str(-padding_top))
        char.set("xadvance", str(p["xadvance"]))
        char.set("page", "0")
        char.set("chnl", "15")

    # Pretty-print the XML
    rough_string = ET.tostring(root, encoding="unicode")
    dom = minidom.parseString(rough_string)
    pretty = dom.toprettyxml(indent="  ")

    # Remove the XML declaration line that minidom adds, we'll add our own
    lines = pretty.split("\n")
    lines[0] = '<?xml version="1.0"?>'

    with open(fnt_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))


def main():
    parser = argparse.ArgumentParser(
        description="Generate BMFont-compatible bitmap fonts for EndlessClient"
    )
    parser.add_argument(
        "--sizes", nargs="+", type=int,
        default=[14, 16, 18, 20],
        help="Pixel sizes to generate (default: 14 16 18 20)"
    )
    parser.add_argument(
        "--font", type=str, default=None,
        help="Path to TTF font file (default: auto-detect system font)"
    )
    parser.add_argument(
        "--output", type=str, default=None,
        help="Output directory (default: same directory as this script)"
    )
    args = parser.parse_args()

    # Resolve font path
    if args.font:
        font_path = args.font
        if not os.path.exists(font_path):
            print(f"Error: Font file not found: {font_path}")
            sys.exit(1)
    else:
        font_path = find_system_font()

    # Resolve output directory
    output_dir = args.output or os.path.dirname(os.path.abspath(__file__))

    print(f"Font: {font_path}")
    print(f"Output: {output_dir}")
    print(f"Sizes: {args.sizes}")
    print()

    # Generate each size
    success_count = 0
    for size in args.sizes:
        if render_font(font_path, size, output_dir):
            success_count += 1
        print()

    print(f"Done! Generated {success_count}/{len(args.sizes)} font sizes.")

    if success_count > 0:
        print("\nNext steps:")
        print("1. Add new .fnt and .png entries to Content.mgcb")
        print("2. Add new FontSize constants to EOLib.Shared/Constants.cs")
        print("3. Load new fonts in ContentProvider.cs")


if __name__ == "__main__":
    main()
