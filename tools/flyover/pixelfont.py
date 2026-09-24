"""The game's one typeface as geometry: the motel handoff's 3x5 pixel alphabet
(src/World/PixelFont.cs, glyph rows MSB left), so a lettered sign in the film reads
exactly like the game's. Letters are raised blocks, one per horizontal run of lit
pixels. Canon lettering only: the caller passes text from the dump / places files.
"""

import archkit

GLYPHS = {
    "A": "010,101,111,101,101", "B": "110,101,110,101,110", "C": "011,100,100,100,011",
    "D": "110,101,101,101,110", "E": "111,100,110,100,111", "F": "111,100,110,100,100",
    "G": "011,100,101,101,011", "H": "101,101,111,101,101", "I": "111,010,010,010,111",
    "J": "001,001,001,101,010", "K": "101,101,110,101,101", "L": "100,100,100,100,111",
    "M": "101,111,111,101,101", "N": "101,111,111,111,101", "O": "010,101,101,101,010",
    "P": "110,101,110,100,100", "Q": "010,101,101,110,011", "R": "110,101,110,101,101",
    "S": "011,100,010,001,110", "T": "111,010,010,010,010", "U": "101,101,101,101,011",
    "V": "101,101,101,101,010", "W": "101,101,111,111,101", "X": "101,101,010,101,101",
    "Y": "101,101,010,010,010", "Z": "111,001,010,100,111",
    "0": "111,101,101,101,111", "1": "010,110,010,010,111", "2": "110,001,010,100,111",
    "3": "110,001,010,001,110", "4": "101,101,111,001,001", "5": "111,100,110,001,110",
    "6": "011,100,110,101,010", "7": "111,001,010,010,010", "8": "010,101,010,101,010",
    "9": "010,101,011,001,110", "-": "000,000,111,000,000", ".": "000,000,000,000,010",
    "'": "010,010,000,000,000", " ": "000,000,000,000,000",
}
GLYPH_H = 5


def measure(text, px):
    """Width in metres: 4 px advance per character, the last gap dropped."""
    return (len(text) * 4 - 1) * px if text else 0.0


def runs(text):
    """(x0, x1, row) pixel runs, x in pixels from the text's left, row 0 = top."""
    out = []
    for i, ch in enumerate(text.upper()):
        if ch not in GLYPHS:
            raise ValueError(f"no 3x5 glyph for {ch!r}")
        for r, bits in enumerate(GLYPHS[ch].split(",")):
            c = 0
            while c < 3:
                if bits[c] == "1":
                    c0 = c
                    while c < 3 and bits[c] == "1":
                        c += 1
                    out.append((i * 4 + c0, i * 4 + c, r))
                else:
                    c += 1
    return out


def text(part, s, cx, z_top, px, depth, mat, y=0.0):
    """Raised lettering on a wall-local plane y (outside -Y), centred on cx, its top at
    z_top, `px` metres per pixel, standing `depth` proud."""
    x0 = cx - measure(s, px) / 2
    for a, b, r in runs(s):
        z1 = z_top - r * px
        part.box(x0 + a * px, y - depth, z1 - px, x0 + b * px, y, z1, mat, skip=("back",))
    return part


def text_part(s, px, depth, mat):
    """Lettering centred on x = 0 with its top at z = 0, as its own Part."""
    return text(archkit.Part(), s, 0.0, 0.0, px, depth, mat)
