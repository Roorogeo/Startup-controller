#!/usr/bin/env python3
"""Generates src/StartupSelector/Resources/app.ico with no third-party dependencies.

The icon follows the Noir theme: a black rounded square with a soft white ring
(white 40% on black, like the border-strong token) and a white check mark, rendered at
16, 20, 24, 32, 40, 48, 64 and 256 px (4x4 supersampled). The 256 px frame is stored
as PNG; smaller frames use classic 32-bit DIB data so every icon API can read them.

Usage:  python tools/make_icon.py
"""
import math
import os
import struct
import zlib

SIZES = [16, 20, 24, 32, 40, 48, 64, 256]
SUPERSAMPLE = 4
FILL = (0x00, 0x00, 0x00)
RING = (0x66, 0x66, 0x66)  # white at 40% over black
CHECK_COLOR = (0xFF, 0xFF, 0xFF)
CHECK = [(0.27, 0.53), (0.44, 0.69), (0.75, 0.35)]


def inside_rounded_square(x, y, radius):
    # x, y in [0, 1]
    cx = min(max(x, radius), 1 - radius)
    cy = min(max(y, radius), 1 - radius)
    return (x - cx) ** 2 + (y - cy) ** 2 <= radius ** 2


def dist_to_segment(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    t = ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)
    t = max(0.0, min(1.0, t))
    qx, qy = ax + t * dx, ay + t * dy
    return math.hypot(px - qx, py - qy)


def on_check(x, y, half_width):
    for (ax, ay), (bx, by) in zip(CHECK, CHECK[1:]):
        if dist_to_segment(x, y, ax, ay, bx, by) <= half_width:
            return True
    return False


def render(size):
    """Returns a list of rows, each a list of (r, g, b, a) tuples, top to bottom."""
    margin = 0.0 if size <= 24 else 0.04
    radius = 0.22
    half_width = 0.075 if size <= 24 else 0.062
    ring = max(1.0 / size, 0.045)
    rows = []
    n = SUPERSAMPLE
    for py in range(size):
        row = []
        for px in range(size):
            r = g = b = a = 0.0
            for sy in range(n):
                for sx in range(n):
                    x = (px + (sx + 0.5) / n) / size
                    y = (py + (sy + 0.5) / n) / size
                    ux = (x - margin) / (1 - 2 * margin)
                    uy = (y - margin) / (1 - 2 * margin)
                    if not (0 <= ux <= 1 and 0 <= uy <= 1) or not inside_rounded_square(ux, uy, radius):
                        continue
                    if on_check(ux, uy, half_width):
                        cr, cg, cb = CHECK_COLOR
                    elif not inside_rounded_square((ux - ring) / (1 - 2 * ring), (uy - ring) / (1 - 2 * ring), radius):
                        cr, cg, cb = RING
                    else:
                        cr, cg, cb = FILL
                    r += cr
                    g += cg
                    b += cb
                    a += 1
            samples = n * n
            if a > 0:
                row.append((int(r / a), int(g / a), int(b / a), int(255 * a / samples)))
            else:
                row.append((0, 0, 0, 0))
        rows.append(row)
    return rows


def dib(size, rows):
    # BITMAPINFOHEADER with doubled height (XOR image + AND mask), 32 bpp BGRA, bottom-up.
    header = struct.pack("<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    pixels = bytearray()
    for row in reversed(rows):
        for r, g, b, a in row:
            pixels += bytes([b, g, r, a])
    mask_stride = ((size + 31) // 32) * 4
    mask = bytearray()
    for row in reversed(rows):
        bits = bytearray(mask_stride)
        for x, (_, _, _, a) in enumerate(row):
            if a == 0:
                bits[x // 8] |= 0x80 >> (x % 8)
        mask += bits
    return header + bytes(pixels) + bytes(mask)


def png(size, rows):
    raw = b"".join(b"\x00" + b"".join(bytes(px) for px in row) for row in rows)

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")


def main():
    frames = [(s, png(s, render(s)) if s >= 256 else dib(s, render(s))) for s in SIZES]
    out = bytearray(struct.pack("<HHH", 0, 1, len(frames)))
    offset = 6 + 16 * len(frames)
    for size, data in frames:
        dim = 0 if size >= 256 else size
        out += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    for _, data in frames:
        out += data
    target = os.path.join(os.path.dirname(__file__), "..", "src", "StartupSelector", "Resources", "app.ico")
    with open(target, "wb") as f:
        f.write(out)
    print(f"Wrote {os.path.normpath(target)} ({len(out)} bytes)")


if __name__ == "__main__":
    main()
