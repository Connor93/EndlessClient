#!/usr/bin/env python3
"""Generate 32x32 pixel art HUD icons for EndlessClient."""
from PIL import Image, ImageDraw
import os

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "HudIcons")
os.makedirs(OUTPUT_DIR, exist_ok=True)

# Color palette (RPG-ish)
TRANSPARENT = (0, 0, 0, 0)
BLACK = (20, 20, 20, 255)
DARK_GRAY = (50, 50, 55, 255)
MID_GRAY = (100, 100, 110, 255)
LIGHT_GRAY = (180, 180, 190, 255)
WHITE = (240, 240, 240, 255)

# Earthy
BROWN_DARK = (80, 50, 30, 255)
BROWN = (140, 90, 50, 255)
BROWN_LIGHT = (190, 140, 80, 255)
TAN = (220, 190, 130, 255)
GOLD = (220, 180, 50, 255)
GOLD_BRIGHT = (255, 220, 80, 255)

# Magical
PURPLE_DARK = (60, 30, 90, 255)
PURPLE = (120, 60, 180, 255)
PURPLE_LIGHT = (170, 120, 220, 255)
BLUE_DARK = (30, 50, 120, 255)
BLUE = (60, 100, 200, 255)
BLUE_LIGHT = (100, 160, 240, 255)
CYAN = (80, 200, 220, 255)

# Nature
GREEN_DARK = (30, 80, 40, 255)
GREEN = (60, 160, 60, 255)
GREEN_LIGHT = (120, 200, 100, 255)

# Red
RED_DARK = (120, 30, 30, 255)
RED = (200, 60, 60, 255)
RED_LIGHT = (240, 120, 100, 255)

# Steel
STEEL_DARK = (60, 65, 80, 255)
STEEL = (120, 130, 150, 255)
STEEL_LIGHT = (180, 190, 210, 255)


def new_icon():
    return Image.new("RGBA", (32, 32), TRANSPARENT)


def draw_map(img):
    """Folded map with X mark."""
    d = ImageDraw.Draw(img)
    # Paper background
    d.rectangle([4, 6, 27, 25], fill=TAN, outline=BROWN_DARK)
    # Fold line
    d.line([(16, 6), (16, 25)], fill=BROWN, width=1)
    # Map lines
    d.line([(7, 10), (14, 10)], fill=BROWN_LIGHT)
    d.line([(18, 12), (25, 12)], fill=BROWN_LIGHT)
    d.line([(7, 14), (14, 14)], fill=BROWN_LIGHT)
    d.line([(18, 16), (25, 16)], fill=BROWN_LIGHT)
    d.line([(7, 18), (14, 18)], fill=BROWN_LIGHT)
    # X mark
    d.line([(20, 19), (24, 23)], fill=RED, width=2)
    d.line([(24, 19), (20, 23)], fill=RED, width=2)
    # Rolled edges
    d.rectangle([2, 6, 4, 25], fill=BROWN)
    d.rectangle([27, 6, 29, 25], fill=BROWN)


def draw_inventory(img):
    """Backpack/bag."""
    d = ImageDraw.Draw(img)
    # Bag body
    d.rounded_rectangle([7, 10, 24, 27], radius=3, fill=BROWN, outline=BROWN_DARK)
    # Flap
    d.rounded_rectangle([8, 7, 23, 14], radius=2, fill=BROWN_LIGHT, outline=BROWN_DARK)
    # Buckle
    d.rectangle([14, 12, 17, 15], fill=GOLD)
    d.point((15, 13), fill=BROWN_DARK)
    d.point((16, 13), fill=BROWN_DARK)
    # Strap
    d.line([(10, 5), (10, 10)], fill=BROWN_DARK, width=2)
    d.line([(21, 5), (21, 10)], fill=BROWN_DARK, width=2)
    d.line([(10, 5), (21, 5)], fill=BROWN_DARK, width=2)


def draw_spells(img):
    """Magic wand with sparkles."""
    d = ImageDraw.Draw(img)
    # Wand shaft
    for i in range(14):
        x = 8 + i
        y = 24 - i
        d.point((x, y), fill=BROWN if i < 8 else PURPLE)
        d.point((x+1, y), fill=BROWN_DARK if i < 8 else PURPLE_DARK)
    # Wand tip glow
    d.rectangle([21, 9, 23, 11], fill=PURPLE_LIGHT)
    # Sparkles
    for (sx, sy) in [(24, 6), (26, 10), (19, 5), (27, 14), (17, 8)]:
        d.point((sx, sy), fill=CYAN)
    for (sx, sy) in [(25, 7), (20, 6), (26, 13)]:
        d.point((sx, sy), fill=WHITE)
    # Star sparkle at tip
    d.point((22, 7), fill=WHITE)
    d.point((21, 8), fill=CYAN)
    d.point((23, 8), fill=CYAN)
    d.point((22, 9), fill=PURPLE_LIGHT)


def draw_passive(img):
    """Open book with glow."""
    d = ImageDraw.Draw(img)
    # Glow behind
    d.ellipse([5, 7, 26, 26], fill=(80, 140, 220, 60))
    # Left page
    d.polygon([(5, 10), (15, 8), (15, 25), (5, 24)], fill=TAN, outline=BROWN_DARK)
    # Right page
    d.polygon([(16, 8), (26, 10), (26, 24), (16, 25)], fill=TAN, outline=BROWN_DARK)
    # Spine
    d.line([(15, 8), (16, 8)], fill=BROWN_DARK)
    d.line([(15, 25), (16, 25)], fill=BROWN_DARK)
    # Text lines on pages
    for y in [12, 15, 18, 21]:
        d.line([(7, y), (13, y)], fill=BLUE_LIGHT)
        d.line([(18, y), (24, y)], fill=BLUE_LIGHT)
    # Glow dots
    for (gx, gy) in [(8, 6), (23, 6), (15, 4)]:
        d.point((gx, gy), fill=BLUE_LIGHT)


def draw_stats(img):
    """Bar chart showing stats."""
    d = ImageDraw.Draw(img)
    # Bars (ascending)
    bars = [(7, 22, 10, 27), (12, 18, 15, 27), (17, 14, 20, 27), (22, 8, 25, 27)]
    colors = [RED, GOLD, GREEN, BLUE]
    for (x1, y1, x2, y2), color in zip(bars, colors):
        d.rectangle([x1, y1, x2, y2], fill=color, outline=DARK_GRAY)
    # Base line
    d.line([(5, 27), (27, 27)], fill=LIGHT_GRAY, width=1)
    # Arrow up
    d.line([(5, 27), (5, 5)], fill=LIGHT_GRAY, width=1)
    d.polygon([(3, 7), (5, 4), (7, 7)], fill=LIGHT_GRAY)


def draw_equip(img):
    """Sword and shield."""
    d = ImageDraw.Draw(img)
    # Shield
    d.polygon([(6, 8), (18, 6), (18, 22), (12, 26), (6, 22)], fill=STEEL, outline=STEEL_DARK)
    d.polygon([(8, 10), (16, 8), (16, 20), (12, 23), (8, 20)], fill=BLUE)
    # Shield emblem (cross)
    d.line([(12, 11), (12, 20)], fill=GOLD, width=2)
    d.line([(9, 15), (15, 15)], fill=GOLD, width=2)
    # Sword
    d.line([(20, 4), (20, 22)], fill=STEEL_LIGHT, width=2)
    d.line([(20, 4), (20, 6)], fill=WHITE, width=2)
    # Sword guard
    d.rectangle([17, 21, 23, 23], fill=GOLD, outline=BROWN_DARK)
    # Sword grip
    d.line([(20, 23), (20, 28)], fill=BROWN, width=2)
    # Pommel
    d.rectangle([19, 27, 21, 29], fill=GOLD)


def draw_macro(img):
    """Keyboard/hotkey."""
    d = ImageDraw.Draw(img)
    # Keyboard body
    d.rounded_rectangle([3, 12, 28, 26], radius=2, fill=DARK_GRAY, outline=MID_GRAY)
    # Keys row 1
    for x in [6, 11, 16, 21]:
        d.rectangle([x, 14, x+3, 17], fill=MID_GRAY, outline=LIGHT_GRAY)
    # Keys row 2
    for x in [7, 12, 17, 22]:
        d.rectangle([x, 19, x+3, 22], fill=MID_GRAY, outline=LIGHT_GRAY)
    # Space bar
    d.rectangle([10, 23, 21, 25], fill=MID_GRAY, outline=LIGHT_GRAY)
    # Lightning bolt (macro = fast action)
    d.polygon([(14, 3), (18, 3), (15, 8), (19, 8), (12, 14), (15, 9), (11, 9)], fill=GOLD_BRIGHT, outline=GOLD)


def draw_online(img):
    """Group of people."""
    d = ImageDraw.Draw(img)
    # Person 1 (center front)
    d.ellipse([12, 5, 19, 12], fill=TAN, outline=BROWN_DARK)  # head
    d.rounded_rectangle([10, 13, 21, 27], radius=2, fill=BLUE, outline=BLUE_DARK)  # body
    # Person 2 (left back)
    d.ellipse([3, 8, 10, 14], fill=TAN, outline=BROWN_DARK)
    d.rounded_rectangle([2, 15, 11, 27], radius=2, fill=GREEN, outline=GREEN_DARK)
    # Person 3 (right back)
    d.ellipse([21, 8, 28, 14], fill=TAN, outline=BROWN_DARK)
    d.rounded_rectangle([20, 15, 29, 27], radius=2, fill=RED, outline=RED_DARK)


def draw_party(img):
    """Two people together."""
    d = ImageDraw.Draw(img)
    # Person 1
    d.ellipse([5, 6, 13, 14], fill=TAN, outline=BROWN_DARK)  # head
    d.rounded_rectangle([4, 15, 14, 27], radius=2, fill=BLUE, outline=BLUE_DARK)
    # Person 2
    d.ellipse([18, 6, 26, 14], fill=TAN, outline=BROWN_DARK)
    d.rounded_rectangle([17, 15, 27, 27], radius=2, fill=GREEN, outline=GREEN_DARK)
    # Link (handshake/connection)
    d.line([(14, 20), (17, 20)], fill=GOLD, width=2)


def draw_config(img):
    """Gear/cog."""
    d = ImageDraw.Draw(img)
    # Outer gear teeth
    teeth_coords = [
        (13, 3, 18, 6), (13, 25, 18, 28),
        (3, 13, 6, 18), (25, 13, 28, 18),
        (6, 5, 9, 8), (22, 5, 25, 8),
        (6, 23, 9, 26), (22, 23, 25, 26),
    ]
    for t in teeth_coords:
        d.rectangle(t, fill=STEEL)
    # Gear body
    d.ellipse([7, 7, 24, 24], fill=STEEL, outline=STEEL_DARK)
    # Inner circle
    d.ellipse([11, 11, 20, 20], fill=DARK_GRAY, outline=STEEL_DARK)
    # Center dot
    d.ellipse([14, 14, 17, 17], fill=STEEL_LIGHT)


def draw_exp(img):
    """Rising bar chart / experience gain."""
    d = ImageDraw.Draw(img)
    # XP bar background
    d.rounded_rectangle([4, 20, 27, 27], radius=2, fill=DARK_GRAY, outline=MID_GRAY)
    # XP bar fill (partial)
    d.rounded_rectangle([5, 21, 20, 26], radius=1, fill=GREEN)
    # Star / level up sparkle
    star_pts = [(15, 3), (17, 9), (23, 10), (18, 14), (20, 20), (15, 17), (10, 20), (12, 14), (7, 10), (13, 9)]
    d.polygon(star_pts, fill=GOLD_BRIGHT, outline=GOLD)
    d.point((15, 10), fill=WHITE)


def draw_quests(img):
    """Scroll with exclamation mark."""
    d = ImageDraw.Draw(img)
    # Scroll body
    d.rectangle([8, 5, 23, 26], fill=TAN, outline=BROWN_DARK)
    # Top roll
    d.ellipse([6, 3, 25, 8], fill=BROWN_LIGHT, outline=BROWN_DARK)
    # Bottom roll
    d.ellipse([6, 23, 25, 28], fill=BROWN_LIGHT, outline=BROWN_DARK)
    # Exclamation mark
    d.rectangle([14, 10, 17, 18], fill=GOLD_BRIGHT)
    d.rectangle([14, 20, 17, 22], fill=GOLD_BRIGHT)


def draw_bounties(img):
    """Target/crosshair."""
    d = ImageDraw.Draw(img)
    # Outer ring
    d.ellipse([4, 4, 27, 27], fill=TRANSPARENT, outline=RED, width=2)
    # Middle ring
    d.ellipse([9, 9, 22, 22], fill=TRANSPARENT, outline=RED, width=2)
    # Inner dot
    d.ellipse([13, 13, 18, 18], fill=RED)
    # Crosshair lines
    d.line([(15, 2), (15, 6)], fill=RED_LIGHT, width=1)
    d.line([(15, 25), (15, 29)], fill=RED_LIGHT, width=1)
    d.line([(2, 15), (6, 15)], fill=RED_LIGHT, width=1)
    d.line([(25, 15), (29, 15)], fill=RED_LIGHT, width=1)


def draw_guild_info(img):
    """Shield with 'i'."""
    d = ImageDraw.Draw(img)
    # Shield shape
    d.polygon([(5, 4), (26, 4), (26, 18), (16, 28), (5, 18)], fill=BLUE, outline=BLUE_DARK)
    d.polygon([(8, 6), (23, 6), (23, 17), (16, 25), (8, 17)], fill=BLUE_LIGHT)
    # 'i' symbol
    d.rectangle([14, 10, 17, 12], fill=WHITE)
    d.rectangle([14, 14, 17, 22], fill=WHITE)


def draw_guild_panel(img):
    """Shield with people."""
    d = ImageDraw.Draw(img)
    # Shield shape
    d.polygon([(5, 4), (26, 4), (26, 18), (16, 28), (5, 18)], fill=PURPLE, outline=PURPLE_DARK)
    d.polygon([(8, 6), (23, 6), (23, 17), (16, 25), (8, 17)], fill=PURPLE_LIGHT)
    # Two people silhouettes
    d.ellipse([9, 8, 14, 13], fill=WHITE)  # head 1
    d.rectangle([9, 13, 14, 20], fill=WHITE)  # body 1
    d.ellipse([17, 8, 22, 13], fill=WHITE)  # head 2
    d.rectangle([17, 13, 22, 20], fill=WHITE)  # body 2


ICONS = {
    "icon_map": draw_map,
    "icon_inventory": draw_inventory,
    "icon_spells": draw_spells,
    "icon_passive": draw_passive,
    "icon_stats": draw_stats,
    "icon_equip": draw_equip,
    "icon_macro": draw_macro,
    "icon_online": draw_online,
    "icon_party": draw_party,
    "icon_config": draw_config,
    "icon_exp": draw_exp,
    "icon_quests": draw_quests,
    "icon_bounties": draw_bounties,
    "icon_guild_info": draw_guild_info,
    "icon_guild_panel": draw_guild_panel,
}

if __name__ == "__main__":
    for name, draw_fn in ICONS.items():
        img = new_icon()
        draw_fn(img)
        path = os.path.join(OUTPUT_DIR, f"{name}.png")
        img.save(path)
        print(f"Created {path}")
    print(f"\nAll {len(ICONS)} icons generated in {OUTPUT_DIR}")
