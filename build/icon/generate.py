#!/usr/bin/env python3
"""
Draws AvrdudeUI.png at 1024x1024. The build script hands it to `sips` for the
per-size iconset variants and then to `iconutil` for the .icns package, so this
file only needs to produce the master image.

Design intent:
  • macOS-native rounded-square silhouette (22% corner radius)
  • Dark navy → charcoal gradient background that reads well over both
    light and dark wallpapers
  • Central DIP chip package with 4-pin sides, gold accents
  • "AVR" wordmark on the chip in a signal-green so it echoes the app's
    terminal log color
  • Tiny green LED near the chip corner to convey the "programming happening"
    idea from the parent AVRDUDESS icon without literally copying it
"""

from PIL import Image, ImageDraw, ImageFont
import os
import sys

SIZE = 1024
RADIUS = int(SIZE * 0.22)   # macOS Big Sur+ icon corner radius ~22.37%
OUT = os.path.join(os.path.dirname(__file__), "AvrdudeUI.png")

# Color palette
BG_TOP    = (34, 42, 58)      # dark navy
BG_BOTTOM = (18, 22, 32)      # near black
CHIP      = (28, 30, 34)      # chip body
CHIP_EDGE = (60, 66, 76)      # chip bevel
CHIP_TOP  = (46, 50, 58)      # chip specular hint
NOTCH     = (14, 16, 20)      # DIP notch
PIN       = (196, 168, 88)    # gold-ish
PIN_SHADOW= (120, 100, 40)
GREEN     = (110, 220, 130)   # signal green (matches AVRDUDESS "success" tone)
GREEN_GLOW= (110, 220, 130, 90)
TEXT      = (110, 220, 130)


def rounded_mask(size, radius):
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle([(0, 0), (size, size)], radius=radius, fill=255)
    return mask


def vertical_gradient(size, top, bottom):
    """Return an RGB image filled with a top→bottom linear gradient."""
    img = Image.new("RGB", (size, size), top)
    px = img.load()
    for y in range(size):
        t = y / (size - 1)
        r = round(top[0] * (1 - t) + bottom[0] * t)
        g = round(top[1] * (1 - t) + bottom[1] * t)
        b = round(top[2] * (1 - t) + bottom[2] * t)
        for x in range(size):
            px[x, y] = (r, g, b)
    return img


def find_font(preferred, size):
    candidates = [
        "/System/Library/Fonts/SFCompact.ttf",
        "/System/Library/Fonts/SFNSMono.ttf",
        "/System/Library/Fonts/Menlo.ttc",
        "/System/Library/Fonts/Supplemental/Courier New Bold.ttf",
        "/System/Library/Fonts/Helvetica.ttc",
    ]
    for path in [preferred] + candidates:
        if path and os.path.exists(path):
            try:
                return ImageFont.truetype(path, size=size)
            except Exception:
                continue
    return ImageFont.load_default()


def draw_pin(draw, x, y, w, h, orientation):
    """Draw a chip pin with a subtle two-tone body so it doesn't look flat."""
    # Base pin
    draw.rounded_rectangle([(x, y), (x + w, y + h)], radius=max(2, w // 5), fill=PIN)
    # Shadow band along the "leg" side
    if orientation == "left":
        draw.rectangle([(x, y + h - h // 3), (x + w - w // 3, y + h)], fill=PIN_SHADOW)
    else:
        draw.rectangle([(x + w // 3, y + h - h // 3), (x + w, y + h)], fill=PIN_SHADOW)


def main():
    # 1) Background: rounded gradient
    bg = vertical_gradient(SIZE, BG_TOP, BG_BOTTOM)
    mask = rounded_mask(SIZE, RADIUS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.paste(bg, (0, 0), mask=mask)

    draw = ImageDraw.Draw(canvas, "RGBA")

    # 2) Chip body — landscape DIP package silhouette
    chip_w = int(SIZE * 0.58)
    chip_h = int(SIZE * 0.42)
    cx0 = (SIZE - chip_w) // 2
    cy0 = (SIZE - chip_h) // 2
    cx1 = cx0 + chip_w
    cy1 = cy0 + chip_h
    chip_radius = int(chip_h * 0.08)

    # Subtle bevel: draw a slightly larger, brighter rounded rect behind
    draw.rounded_rectangle([(cx0 - 6, cy0 - 6), (cx1 + 6, cy1 + 6)],
                           radius=chip_radius + 6, fill=CHIP_EDGE)
    # Chip body
    draw.rounded_rectangle([(cx0, cy0), (cx1, cy1)], radius=chip_radius, fill=CHIP)
    # Top-face specular hint
    draw.rounded_rectangle([(cx0, cy0), (cx1, cy0 + chip_h // 3)],
                           radius=chip_radius, fill=CHIP_TOP)

    # 3) DIP notch — semicircle cut into the left edge (traditional pin-1 mark)
    notch_r = int(chip_h * 0.10)
    notch_cx = cx0
    notch_cy = (cy0 + cy1) // 2
    draw.pieslice(
        [(notch_cx - notch_r, notch_cy - notch_r), (notch_cx + notch_r, notch_cy + notch_r)],
        start=270, end=90, fill=NOTCH,
    )

    # 4) Pins — 5 per side, centered vertically along the chip
    pin_count = 5
    pin_w = int(SIZE * 0.055)
    pin_h = int(chip_h * 0.14)
    gutter = int(SIZE * 0.015)
    vertical_span = int(chip_h * 0.85)
    spacing = vertical_span // (pin_count - 1)
    top_y = (cy0 + cy1) // 2 - vertical_span // 2 - pin_h // 2
    for i in range(pin_count):
        py = top_y + i * spacing
        draw_pin(draw, cx0 - pin_w - gutter, py, pin_w, pin_h, "left")
        draw_pin(draw, cx1 + gutter,          py, pin_w, pin_h, "right")

    # 5) LED — small green pill in the top-right corner of the chip
    led_r = int(SIZE * 0.028)
    led_cx = cx1 - int(chip_w * 0.10)
    led_cy = cy0 + int(chip_h * 0.22)
    # Outer glow
    for radius, alpha in ((led_r + 22, 30), (led_r + 12, 60), (led_r + 4, 110)):
        draw.ellipse(
            [(led_cx - radius, led_cy - radius), (led_cx + radius, led_cy + radius)],
            fill=(GREEN[0], GREEN[1], GREEN[2], alpha),
        )
    # LED core
    draw.ellipse(
        [(led_cx - led_r, led_cy - led_r), (led_cx + led_r, led_cy + led_r)],
        fill=GREEN,
    )

    # 6) "AVR" wordmark centered on the chip
    font_size = int(chip_h * 0.55)
    font = find_font(None, font_size)
    text = "AVR"
    # PIL text metrics
    try:
        bbox = draw.textbbox((0, 0), text, font=font)
        tw = bbox[2] - bbox[0]
        th = bbox[3] - bbox[1]
        tx = (cx0 + cx1) // 2 - tw // 2 - bbox[0]
        ty = (cy0 + cy1) // 2 - th // 2 - bbox[1]
    except AttributeError:
        tw, th = draw.textsize(text, font=font)
        tx = (cx0 + cx1) // 2 - tw // 2
        ty = (cy0 + cy1) // 2 - th // 2
    # subtle drop shadow first
    draw.text((tx + 4, ty + 4), text, font=font, fill=(0, 0, 0, 160))
    draw.text((tx, ty), text, font=font, fill=TEXT)

    # 7) Re-mask so nothing bleeds past the rounded corners
    final = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    final.paste(canvas, (0, 0), mask=mask)

    final.save(OUT, "PNG")
    print(OUT)


if __name__ == "__main__":
    main()
