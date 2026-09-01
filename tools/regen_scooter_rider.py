#!/usr/bin/env python3
"""Recomposite assets/sprites/scooter_rider.png from assets/sprites/character.png.

The scooter handoff (docs/designs/design_handoff_scooter) defines the riding sheet
as DERIVED art: "the rider is the unmodified character.png art, drawn 6px higher in
the cell so the feet land on the deck", over a scooter drawn from the handoff's
pixel-exact recipe tables. Whenever character.png changes (the cast-sprites handoff
replaced it; a future repaint will again), rerun this from the repo root:

    python3 tools/regen_scooter_rider.py

This script reproduces the originally shipped sheet byte-for-byte (modulo the ±1
per-channel export noise the original canvas export carried) when run against the
pre-cast-handoff character.png, with two deliberate divergences, both from the
2026-08-27 cast integration:

  * Row 1 (profile) mirrors the character's left-facing row so the rider faces the
    direction of travel — the original sheet composited it unmirrored, which read
    fine under the old character's hat but puts the new Jane's face against the
    scooter's direction.
  * The forearm reaching to the handlebar is COAT, Jane's chore-shirt green
    (green-mid #457539) — the original used her old plum coat.
  * The rider composites in two parts with a slight knee-bend. The old art was
    27px tall, so one 6px lift put its feet on the deck and clipped 1px of hat;
    the cast-handoff Jane is 29px in a cell with only 26 rows above the deck, so
    a single lift cannot fit her. Her legs lift so her feet land on the deck,
    her head and torso lift only as far as the cell top allows, and the torso
    overlaps the top of the thighs — the riding crouch hides the difference.
    Both lifts are measured from the sheet, so a repaint changes nothing here.

The wheels (the per-column spoke rotation) are authored art with no recipe in the
handoff; they are carried over pixel-for-pixel from the previous scooter_rider.png,
which the script therefore reads BEFORE overwriting it.
"""
import struct, sys, zlib

INK9 = (0x17, 0x13, 0x10, 255)   # tire, grips, bobbed ground shadow
INK7 = (0x2b, 0x24, 0x1d, 255)   # bar, forearm shadow, grounded shadow
STONE_BASE = (0x9a, 0x9a, 0x8a, 255)
STONE_PALE = (0xb8, 0xb5, 0xa5, 255)
DECK_DK = (0x2d, 0x8c, 0x46, 255)
DECK = (0x45, 0xbf, 0x62, 255)
DECK_HI = (0x74, 0xd9, 0x8a, 255)
CREAM = (0xed, 0xe3, 0xcb, 255)
COAT = (0x45, 0x75, 0x39, 255)   # Jane's chore-shirt green (cast handoff)

BOB = [0, 0, 1, 1, 1, 0]         # handoff bob pattern, applied to every row
WHEEL_COLORS = (INK9, STONE_BASE, STONE_PALE)

RIDER = 'assets/sprites/scooter_rider.png'
CHARACTER = 'assets/sprites/character.png'


def read_png(path):
    d = open(path, 'rb').read()
    assert d[:8] == b'\x89PNG\r\n\x1a\n', path
    pos, idat, plte, trns = 8, b'', None, None
    while pos < len(d):
        ln, = struct.unpack('>I', d[pos:pos + 4])
        typ, body = d[pos + 4:pos + 8], d[pos + 8:pos + 8 + ln]
        if typ == b'IHDR':
            w, h, bd, ct = struct.unpack('>IIBB', body[:10])
        elif typ == b'IDAT':
            idat += body
        elif typ == b'PLTE':
            plte = body
        elif typ == b'tRNS':
            trns = body
        pos += 12 + ln
    raw = zlib.decompress(idat)
    ch = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ct]
    stride = w * ch
    px = bytearray(w * h * 4)
    prev = bytearray(stride)
    pos = 0
    for y in range(h):
        f = raw[pos]; pos += 1
        line = bytearray(raw[pos:pos + stride]); pos += stride
        for i in range(stride):
            a = line[i - ch] if i >= ch else 0
            b = prev[i]
            c = prev[i - ch] if i >= ch else 0
            if f == 1: line[i] = (line[i] + a) & 255
            elif f == 2: line[i] = (line[i] + b) & 255
            elif f == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        prev = line
        for x in range(w):
            o = (y * w + x) * 4
            if ct == 6:
                px[o:o + 4] = line[x * 4:x * 4 + 4]
            elif ct == 2:
                px[o:o + 3] = line[x * 3:x * 3 + 3]; px[o + 3] = 255
            elif ct == 3:
                idx = line[x]
                px[o:o + 3] = plte[idx * 3:idx * 3 + 3]
                px[o + 3] = trns[idx] if trns and idx < len(trns) else 255
    return w, h, px


def write_png(path, w, h, px):
    def chunk(t, b):
        c = struct.pack('>I', len(b)) + t + b
        return c + struct.pack('>I', zlib.crc32(t + b) & 0xffffffff)
    raw = b''.join(b'\x00' + bytes(px[y * w * 4:(y + 1) * w * 4]) for y in range(h))
    open(path, 'wb').write(
        b'\x89PNG\r\n\x1a\n'
        + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
        + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


class Cell:
    def __init__(self):
        self.p = [[(0, 0, 0, 0)] * 16 for _ in range(32)]

    def set(self, x, y, c):
        if 0 <= x < 16 and 0 <= y < 32:
            self.p[y][x] = c

    def rect(self, x, y, w, h, c):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self.set(xx, yy, c)


def cellget(px, w, cx, cy, x, y):
    if x < 0 or x > 15 or y < 0 or y > 31:
        return (0, 0, 0, 0)
    o = ((cy * 32 + y) * w + cx * 16 + x) * 4
    return tuple(px[o:o + 4])


LEGS_SPLIT = 20   # gen_cast.js draws legs at yL=20; everything above is head/torso


def measure(char):
    # Vertical extent of idle frame A (col 0) across all three rows: the lifts are
    # derived from the art, not hardcoded to one paint of it.
    ys = [y for r in (0, 1, 2) for y in range(32) for x in range(16)
          if cellget(char, 96, 0, r, x, y)[3]]
    return min(ys), max(ys)


def rider_lifts(char, b):
    # Feet land on the deck's top row (25); bob frames sink the rider 1px while
    # the scooter rises 1px, per the original sheet's convention. The upper body
    # lifts only as far as keeps the head inside the cell; the difference is the
    # knee-bend (0 when the art is short enough to fit whole).
    top, bottom = measure(char)
    legs = bottom - 25 - b
    upper = max(0, min(legs, top - b))
    return upper, legs


def draw_rider(cell, char, row, upper_lift, legs_lift, mirror):
    # Idle frame A (col 0) on every column; only the bob moves the rider. Legs
    # first, upper body over them — the overlap rows are the bent knee.
    for part_lift, y0, y1 in ((legs_lift, LEGS_SPLIT, 31), (upper_lift, 0, LEGS_SPLIT - 1)):
        for sy in range(y0, y1 + 1):
            y = sy - part_lift
            if y < 0 or y > 31:
                continue
            for x in range(16):
                cp = cellget(char, 96, 0, row, 15 - x if mirror else x, sy)
                if cp[3]:
                    cell.set(x, y, cp)


def handlebar(cell, x, y, w):
    cell.rect(x, y, w, 2, INK7)
    cell.rect(x + 2, y, w - 4, 1, STONE_PALE)
    cell.rect(x, y, 2, 2, INK9)
    cell.rect(x + w - 2, y, 2, 2, INK9)


def scooter_front_back(cell, b):
    barY, deckY = 16 - b, 25 - b
    cell.rect(8, barY + 2, 1, deckY - barY - 2, STONE_BASE)   # stem
    cell.rect(3, deckY + 2, 10, 1, DECK_DK)                   # deck underside
    cell.rect(3, deckY, 10, 2, DECK)                          # deck
    cell.rect(4, deckY, 8, 1, DECK_HI)                        # highlight
    cell.rect(7, deckY + 3, 2, 4, INK9)                       # wheel edge-on
    cell.rect(7, deckY + 4, 2, 1, STONE_BASE)                 # hub pixels


def shadow(cell, r, b):
    # From the shipped sheet: bobbed frames keep the spec's 4-wide ink-900 at
    # deckY+7 (rows 0/2); grounded frames widen to 6 of ink-700 on rows 0/1
    # and drop it on row 2.
    if b == 1 and r in (0, 2):
        cell.rect(6, 31, 4, 1, INK9)
    if b == 0 and r in (0, 1):
        cell.rect(5, 31, 6, 1, INK7)


def main():
    _, _, old_rider = read_png(RIDER)
    cw, chh, char = read_png(CHARACTER)
    assert (cw, chh) == (96, 96), 'character.png must be the 96x96 walk sheet'

    out = bytearray(96 * 96 * 4)
    for c in range(6):
        b = BOB[c]
        barY, deckY, wheelY = 16 - b, 25 - b, 29 - b
        upper_lift, legs_lift = rider_lifts(char, b)
        for r in (0, 1, 2):
            cell = Cell()
            if r == 1:
                for cx0, cx1 in ((1, 5), (10, 14)):           # wheels: carried-over art
                    for y in range(wheelY - 2, wheelY + 3):
                        for x in range(cx0, cx1 + 1):
                            p = cellget(old_rider, 96, c, 1, x, y)
                            if p[3] and any(all(abs(p[i] - wc[i]) <= 2 for i in range(3))
                                            for wc in WHEEL_COLORS):
                                cell.set(x, y, p)
                cell.rect(3, deckY + 2, 10, 1, DECK_DK)
                cell.rect(3, deckY, 10, 2, DECK)
                cell.rect(4, deckY, 8, 1, DECK_HI)
                cell.rect(11, barY + 2, 2, deckY - barY - 2, DECK_DK)   # stem behind
                draw_rider(cell, char, 1, upper_lift, legs_lift, mirror=True)
                cell.rect(11, barY + 2, 2, 4, DECK_DK)                  # stem in front
                handlebar(cell, 9, barY, 7)
                cell.rect(7, barY + 2, 4, 2, COAT)                      # forearm
                cell.rect(7, barY + 4, 4, 1, INK7)                      # forearm shadow
                cell.set(14, barY + 3, CREAM)                           # headlamp lens
                cell.set(14, barY + 4, STONE_BASE)                      # housing
                shadow(cell, 1, b)
            elif r == 0:
                draw_rider(cell, char, 0, upper_lift, legs_lift, mirror=False)
                scooter_front_back(cell, b)
                shadow(cell, 0, b)
                handlebar(cell, 2, barY, 12)
            else:
                handlebar(cell, 2, barY, 12)
                draw_rider(cell, char, 2, upper_lift, legs_lift, mirror=False)
                scooter_front_back(cell, b)
                shadow(cell, 2, b)
            for y in range(32):
                for x in range(16):
                    o = ((r * 32 + y) * 96 + c * 16 + x) * 4
                    out[o:o + 4] = bytes(cell.p[y][x])

    write_png(RIDER, 96, 96, out)
    print(f'recomposited {RIDER} from {CHARACTER}')


if __name__ == '__main__':
    main()
