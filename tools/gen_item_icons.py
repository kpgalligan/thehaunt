#!/usr/bin/env python3
"""Generate assets/sprites/icons/items.png — the inventory item-icon atlas.

One 12x12 icon per ItemDef, in ItemDefs.All insertion order (the same canonical
order the crops atlas uses), laid out as a single row: atlas width = 12 * item
count. src/UI/ItemIcons.cs holds the C# side of the order; IconTests pins the two
to each other by count and to this list by name. When an item is added: draw it
here, add it to BOTH order tables, and re-run from the repo root:

    python3 tools/gen_item_icons.py

Palette: the art-handoff constants (outline/wood/basic-tier iron from the tools
handoff, water from the can's pour) plus each item's own ItemDef.IconColor with
programmatic shade/highlight.

Drift guards: before writing, this script parses src/UI/ItemIcons.cs and
src/Core/ItemDefs.cs and REFUSES to run if its own ITEMS table disagrees with
ItemIcons.Order (ids, in order) or with ItemDefs' IconColor hexes — so a new item
added to one table but not the others, or appended out of order, fails here
instead of silently shifting every atlas column after it. (IconTests pins the C#
side to the registry and the atlas dimensions; THIS check is what pins the
generator.)
"""
import re, struct, sys, zlib

SIZE = 12

OUT = (0x2b, 0x24, 0x1d, 255)     # sprite outline (tools handoff)
WOOD = (0x6b, 0x4a, 0x2f, 255)    # handle base
WOOD_HI = (0xa5, 0x85, 0x5c, 255)
IRON = (0x7a, 0x6a, 0x5c, 255)    # basic-tier head
IRON_HI = (0xa8, 0x9a, 0x88, 255)
IRON_SH = (0x3a, 0x32, 0x2c, 255)
WATER = (0x47, 0x78, 0x8c, 255)
LEAF = (0x4a, 0x9a, 0x4a, 255)    # greenbean crop green, reused for leaf tops


def hex_rgb(s):
    return (int(s[1:3], 16), int(s[3:5], 16), int(s[5:7], 16), 255)


def shade(c, f=0.62):
    return (int(c[0] * f), int(c[1] * f), int(c[2] * f), 255)


def light(c, f=0.35):
    return (int(c[0] + (255 - c[0]) * f), int(c[1] + (255 - c[1]) * f),
            int(c[2] + (255 - c[2]) * f), 255)


class Icon:
    def __init__(self):
        self.p = [[(0, 0, 0, 0)] * SIZE for _ in range(SIZE)]

    def px(self, x, y, c):
        if 0 <= x < SIZE and 0 <= y < SIZE:
            self.p[y][x] = c

    def rect(self, x, y, w, h, c):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self.px(xx, yy, c)

    def outline(self):
        """1px OUT ring around every drawn pixel that borders transparency."""
        ring = []
        for y in range(SIZE):
            for x in range(SIZE):
                if self.p[y][x][3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < SIZE and 0 <= ny < SIZE and self.p[ny][nx][3] \
                            and self.p[ny][nx] != OUT:
                        ring.append((x, y))
                        break
        for x, y in ring:
            self.px(x, y, OUT)


def handle(icon, x0, y0, x1, y1):
    """Two-px-thick wooden shaft along a 45-degree diagonal, lit on its upper edge."""
    steps = max(abs(x1 - x0), abs(y1 - y0))
    for i in range(steps + 1):
        x = x0 + (x1 - x0) * i // steps
        y = y0 + (y1 - y0) * i // steps
        icon.px(x, y, WOOD_HI)
        icon.px(x, y + 1, WOOD)


def draw_hoe(icon, color):
    handle(icon, 1, 9, 7, 3)
    icon.rect(7, 1, 3, 2, IRON)          # shoulder plate at the shaft tip
    icon.px(7, 1, IRON_HI)
    icon.px(8, 1, IRON_HI)
    icon.rect(9, 3, 2, 3, IRON)          # blade hanging down to bite the soil
    icon.px(9, 3, IRON_HI)
    icon.px(10, 4, IRON_SH)
    icon.px(10, 5, IRON_SH)


def draw_watering_can(icon, color):
    body = hex_rgb(color)
    icon.rect(3, 4, 6, 6, body)
    icon.rect(3, 4, 6, 1, light(body))   # rim catch-light
    icon.rect(3, 8, 6, 2, shade(body))
    for i in range(3):                   # spout out the left, rising to its lip
        icon.px(2 - i, 6 - i, body)
        icon.px(2 - i, 7 - i, shade(body))
    icon.px(0, 5, light(body))
    icon.px(4, 2, body)                  # carry handle arc
    icon.px(5, 1, body)
    icon.px(6, 1, body)
    icon.px(7, 2, body)
    icon.px(0, 7, WATER)                 # one falling drop sells the purpose
    icon.px(0, 9, WATER)


def draw_scythe(icon, color):
    handle(icon, 2, 10, 5, 4)            # snath
    icon.px(5, 3, WOOD)
    icon.px(6, 2, IRON)                  # blade sweeps right off the top
    icon.px(7, 1, IRON)
    icon.px(8, 1, IRON_HI)
    icon.px(9, 1, IRON_HI)
    icon.px(10, 2, IRON)
    icon.px(11, 3, IRON)
    icon.px(11, 4, IRON_SH)
    icon.px(10, 3, IRON_SH)


def draw_axe(icon, color):
    handle(icon, 1, 9, 7, 3)
    icon.rect(7, 1, 2, 5, IRON)          # the head hangs BESIDE the shaft tip,
    icon.rect(9, 2, 1, 3, IRON)          # perpendicular — on the axis it reads
    icon.px(7, 1, IRON_HI)               # as a shovel
    icon.px(8, 1, IRON_HI)
    icon.rect(10, 2, 1, 3, IRON_HI)      # the bit's bright cutting edge
    icon.px(7, 5, IRON_SH)
    icon.px(8, 5, IRON_SH)
    icon.px(9, 4, IRON_SH)


def draw_pick(icon, color):
    handle(icon, 1, 10, 7, 4)
    arc = [(3, 3), (4, 2), (5, 1), (6, 1), (7, 1), (8, 1), (9, 2), (10, 3)]
    for i, (x, y) in enumerate(arc):     # the crescent head across the shaft tip
        icon.px(x, y, IRON)
        if 0 < i < len(arc) - 1:         # tips stay single-pixel sharp
            icon.px(x, y + 1, IRON_SH)
    icon.px(5, 1, IRON_HI)
    icon.px(6, 1, IRON_HI)
    icon.px(7, 1, IRON_HI)


def draw_seeds(icon, color):
    dot = hex_rgb(color)
    icon.rect(3, 4, 6, 6, WOOD_HI)       # burlap pouch
    icon.rect(3, 9, 6, 1, WOOD)
    icon.px(3, 4, WOOD)
    icon.px(8, 4, WOOD)
    icon.rect(4, 3, 4, 1, WOOD)          # cinched neck
    icon.px(3, 2, WOOD_HI)               # the tied mouth flaring open
    icon.px(8, 2, WOOD_HI)
    icon.px(5, 6, dot)                   # this variety's seeds, on the cloth
    icon.px(7, 7, dot)
    icon.px(4, 8, dot)
    icon.px(10, 10, dot)                 # and one spilled
    icon.px(9, 11, dot)


def draw_turnip(icon, color):
    bulb = hex_rgb(color)
    purple = (0x8a, 0x5a, 0x9a, 255)     # the purple crown a turnip actually has
    icon.rect(3, 5, 6, 5, bulb)
    icon.px(3, 5, purple)
    icon.rect(4, 4, 4, 2, purple)
    icon.px(8, 5, purple)
    icon.rect(3, 9, 6, 1, shade(bulb, 0.8))
    icon.px(5, 10, bulb)
    icon.px(6, 10, bulb)
    icon.px(5, 11, shade(bulb, 0.8))     # taproot
    icon.px(5, 3, LEAF)                  # greens
    icon.px(6, 2, LEAF)
    icon.px(6, 3, light(LEAF))
    icon.px(7, 1, LEAF)


def draw_greenbean(icon, color):
    pod = hex_rgb(color)
    for ox, oy in ((0, 0), (3, 2), (-3, 3)):   # three pods fanned out
        for i in range(5):
            icon.px(5 + ox + i, 3 + oy + i, pod)
            icon.px(5 + ox + i, 4 + oy + i, shade(pod, 0.75))
        icon.px(4 + ox, 2 + oy, shade(pod, 0.55))   # stem tip


def draw_potato(icon, color):
    skin = hex_rgb(color)
    icon.rect(3, 5, 7, 4, skin)
    icon.rect(4, 4, 5, 6, skin)
    icon.px(4, 4, light(skin, 0.25))
    icon.px(5, 4, light(skin, 0.25))
    icon.rect(4, 9, 5, 1, shade(skin, 0.78))
    icon.px(9, 8, shade(skin, 0.78))
    icon.px(5, 6, shade(skin, 0.55))     # eyes
    icon.px(8, 5, shade(skin, 0.55))
    icon.px(6, 8, shade(skin, 0.55))


def draw_cauliflower(icon, color):
    curd = hex_rgb(color)
    wrap = (0x7a, 0xb0, 0x60, 255)       # the leaf wrap, cauliflower_seeds' green
    icon.rect(3, 3, 6, 6, curd)
    icon.px(3, 3, wrap)
    icon.px(8, 3, wrap)
    for x, y in ((4, 4), (6, 3), (7, 5), (5, 6), (8, 7)):   # curd bumps
        icon.px(x, y, light(curd, 0.5))
    for x, y in ((5, 4), (7, 6), (4, 7)):
        icon.px(x, y, shade(curd, 0.85))
    icon.rect(2, 8, 8, 2, wrap)          # leaves cupping the head
    icon.px(1, 7, wrap)
    icon.px(10, 7, wrap)
    icon.rect(3, 10, 6, 1, shade(wrap, 0.7))


def draw_lumber(icon, color):
    plank = hex_rgb(color)
    for i, (x, y) in enumerate(((2, 2), (1, 5), (3, 8))):   # three stacked planks
        icon.rect(x, y, 8, 3, plank)
        icon.rect(x, y, 8, 1, light(plank, 0.3))
        icon.rect(x + 8, y, 1, 3, WOOD_HI)                  # end grain
        icon.px(x + 8, y + 1, WOOD)
        icon.px(x + 3 + i, y + 2, shade(plank, 0.7))        # grain flecks


def draw_stone(icon, color):
    rock = hex_rgb(color)
    icon.rect(3, 4, 7, 6, rock)
    icon.rect(4, 3, 4, 1, rock)
    icon.px(2, 6, rock)
    icon.px(2, 7, rock)
    icon.rect(4, 3, 2, 1, light(rock, 0.35))    # top facet
    icon.px(4, 4, light(rock, 0.35))
    icon.px(5, 4, light(rock, 0.5))
    icon.rect(4, 9, 6, 1, shade(rock, 0.62))    # under-shadow facet
    icon.px(9, 7, shade(rock, 0.62))
    icon.px(9, 8, shade(rock, 0.62))
    icon.px(6, 6, shade(rock, 0.8))             # fracture line
    icon.px(7, 7, shade(rock, 0.8))


# (id, IconColor from ItemDefs, draw fn) — ItemDefs.All insertion order, exactly.
ITEMS = [
    ('hoe', '#8a5a3a', draw_hoe),
    ('watering_can', '#6a8ab0', draw_watering_can),
    ('scythe', '#9a9a9a', draw_scythe),
    ('axe', '#7a6a5c', draw_axe),
    ('pick', '#575a58', draw_pick),
    ('turnip_seeds', '#c8b060', draw_seeds),
    ('greenbean_seeds', '#7ab060', draw_seeds),
    ('potato_seeds', '#b08d57', draw_seeds),
    ('cauliflower_seeds', '#c8d0a8', draw_seeds),
    ('turnip', '#d8c8e8', draw_turnip),
    ('greenbean', '#4a9a4a', draw_greenbean),
    ('potato', '#c9a86a', draw_potato),
    ('cauliflower', '#e8e8d8', draw_cauliflower),
    ('lumber', '#8a6a42', draw_lumber),
    ('stone', '#8d8f8a', draw_stone),
]

ATLAS = 'assets/sprites/icons/items.png'


def write_png(path, w, h, rows):
    def chunk(t, b):
        c = struct.pack('>I', len(b)) + t + b
        return c + struct.pack('>I', zlib.crc32(t + b) & 0xffffffff)
    raw = b''.join(
        b'\x00' + b''.join(bytes(c) for c in rows[y]) for y in range(h))
    open(path, 'wb').write(
        b'\x89PNG\r\n\x1a\n'
        + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
        + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def preview(icon):
    ramp = ' .:-=+*#%@'
    for row in icon.p:
        line = ''
        for r, g, b, a in row:
            if not a:
                line += ' .'[0] * 2
            else:
                v = (r + g + b) // 3
                line += ramp[min(9, 1 + v * 9 // 256)] * 2
        print(line)


def check_against_csharp():
    order_src = open('src/UI/ItemIcons.cs').read()
    body = order_src.split('Order = new[]')[1].split('};')[0]
    csharp_order = re.findall(r'"([a-z_]+)"', body)
    ids = [item_id for item_id, _, _ in ITEMS]
    if ids != csharp_order:
        sys.exit(f'ITEMS disagrees with ItemIcons.Order — fix BOTH, in the same order.\n'
                 f'  generator: {ids}\n  ItemIcons: {csharp_order}')
    defs_src = open('src/Core/ItemDefs.cs').read()
    colors = dict(re.findall(r'new ItemDef\("([a-z_]+)",[^)]*?"(#[0-9a-f]{6})"', defs_src))
    for item_id, color, _ in ITEMS:
        if colors.get(item_id) != color:
            sys.exit(f"ITEMS color for '{item_id}' is {color} but ItemDefs.cs says "
                     f"{colors.get(item_id)} — the registry is the source; copy it here.")


def main():
    check_against_csharp()
    icons = []
    for item_id, color, fn in ITEMS:
        icon = Icon()
        fn(icon, color)
        icon.outline()
        icons.append(icon)
        if '--preview' in sys.argv:
            print(f'--- {item_id} ---')
            preview(icon)
    rows = [[(0, 0, 0, 0)] * (SIZE * len(ITEMS)) for _ in range(SIZE)]
    for i, icon in enumerate(icons):
        for y in range(SIZE):
            for x in range(SIZE):
                rows[y][i * SIZE + x] = icon.p[y][x]
    if '--preview' not in sys.argv:
        write_png(ATLAS, SIZE * len(ITEMS), SIZE, rows)
        print(f'wrote {ATLAS}: {SIZE * len(ITEMS)}x{SIZE}, {len(ITEMS)} icons')


if __name__ == '__main__':
    main()
