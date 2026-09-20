#!/usr/bin/env python3

# Copyright 2026 luginf
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

"""Draws examples/sdl2_tiles.png, the tile sheet of sdl2_roguelike.fs.

This is the Python version of examples/make_sdl2_tiles.fs, which does the
same in ueforth; both write the same pixels. Here zlib compresses the image
(352 bytes), the Forth version writes the PNG by hand and does not compress it.

The tiles are drawn here as text, 8 characters by 8 lines each, and the
letters are looked up in PALETTE ('.' is transparent). Edit them and run
    python3 examples/make_sdl2_tiles.py
to write the sheet again: a 64x8 PNG holding 8 tiles side by side, in the
order of TILES. Only the Python standard library is needed.
"""

import os
import struct
import zlib

PALETTE = {
    '.': None,
    'a': (0x2a, 0x2a, 0x48), 'b': (0x4a, 0x4a, 0x78), 'c': (0x6a, 0x6a, 0xa8),
    'F': (0x18, 0x1c, 0x30), 'f': (0x2c, 0x30, 0x50),
    'C': (0x60, 0xe0, 0xff), 'D': (0x2a, 0x90, 0xb0),
    'G': (0x40, 0xff, 0x80), 'g': (0x20, 0xa0, 0x50), 'H': (0xc0, 0xff, 0xd0),
    'w': (0xa0, 0x80, 0x60),
    'y': (0xff, 0xe0, 0x60), 'Y': (0xc0, 0x90, 0x30), 'k': (0x30, 0x20, 0x10),
    'r': (0xff, 0x50, 0x50), 'R': (0xa0, 0x20, 0x20), 'W': (0xff, 0xff, 0xff),
    's': (0xe0, 0xe0, 0xe0), 'S': (0x90, 0x90, 0xa0),
}

TILES = [
    # 0 wall
    ["bbbbabbb", "bcbbabcb", "aaaaaaaa", "bbabbbbb",
     "bcbabbcb", "aaaaaaaa", "bbbbabbb", "bcbbabcb"],
    # 1 floor
    ["FFFFFFFF", "FFFFFFFF", "FFFfFFFF", "FFFFFFFF",
     "FFFFFFFF", "FFFFFfFF", "FFFFFFFF", "FfFFFFFF"],
    # 2 stairs (on a floor)
    ["CCCCFFFF", "CCCCFFFF", "DDCCCCFF", "DDCCCCFF",
     "FFDDCCCC", "FFDDCCCC", "FFFFDDCC", "FFFFDDCC"],
    # 3 potion
    ["...ww...", "...ww...", "..gGGg..", ".gGGGGg.",
     ".gGHGGg.", ".gGGGGg.", "..gGGg..", "........"],
    # 4 player
    ["..yyyy..", "..ykky..", "...YY...", ".yyyyyy.",
     "y.yyyy.y", "..yYYy..", "..y..y..", ".YY..YY."],
    # 5 monster
    ["..rrrr..", ".rrrrrr.", "rWRrrRWr", "rrrrrrrr",
     "rrrrrrrr", ".rrrrrr.", ".rRrrRr.", "r......r"],
    # 6 skull
    ["..ssss..", ".ssssss.", "sskssksS", "sskssksS",
     ".ssssss.", "..skks..", "..s.s.s.", "........"],
    # 7 heart
    [".rr.rr..", "rrrrrrR.", "rrrrrrR.", "rrrrrrR.",
     ".rrrrR..", "..rrR...", "...R....", "........"],
]


def chunk(kind, data):
    body = kind + data
    return (struct.pack('>I', len(data)) + body +
            struct.pack('>I', zlib.crc32(body) & 0xffffffff))


def main():
    width, height = 8 * len(TILES), 8
    raw = bytearray()
    for y in range(height):
        raw.append(0)  # PNG filter type: none
        for tile in TILES:
            for x in range(8):
                color = PALETTE[tile[y][x]]
                raw += bytes(color + (255,)) if color else bytes(4)
    png = (b'\x89PNG\r\n\x1a\n' +
           chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) +
           chunk(b'IDAT', zlib.compress(bytes(raw), 9)) +
           chunk(b'IEND', b''))
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'sdl2_tiles.png')
    with open(out, 'wb') as f:
        f.write(png)
    print('wrote', out, width, 'x', height)


if __name__ == '__main__':
    main()
