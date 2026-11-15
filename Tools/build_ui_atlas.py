"""Pack the shared HUD artwork while retaining the existing source sprite metadata."""

import json
import math
from pathlib import Path

from PIL import Image


THEME = Path(__file__).resolve().parents[1] / "Assets/LoopSort/Resources/LoopSort/Theme"
NAME = "ui-batch"
SIZE = 2048
GUTTER = 4
CELLS = {
    "candy-buttons": range(8),
    "candy-v2-panels": (0, 1, 4, 5, 8, 9, 10, 11, 12, 13, 14),
    "candy-mechanisms": range(12),
    "candy-home-icons": range(3),
    "icons": (*range(6, 24), 35),
    "reference-ui": (7, 8),
    "menu-details": (3,),
    "approved-menu": (0,),
    **dict.fromkeys(("belt", "close", "helpButton", "haptic", "language", "music", "redButton", "shuffle", "sound",
                    "toggleOn", "tutorialHand", "undo", "belt-capacity", "advert-clapper", "advert-yellow"), (0,)),
}
# These full-size illustrations are displayed as small icons. The cap is at
# least the original imported size for the belt and clapper.
LIMITS = {"belt-capacity": 256, "advert-clapper": 256, "advert-yellow": 320}


def read_tiles(metadata):
    for name, indices in CELLS.items():
        with Image.open(THEME / f"{name}.png") as original:
            source = original.convert("RGBA")
        cap = LIMITS.get(name)
        for index in indices:
            x, y, w, h = metadata[name][index]
            width, height = source.size
            left, top = x * width, (1 - y - h) * height
            right, bottom = (x + w) * width, (1 - y) * height
            box = (math.floor(left + 1e-4), math.floor(top + 1e-4),
                   math.ceil(right - 1e-4), math.ceil(bottom - 1e-4))
            tile = source.crop(box)
            logical = (left - box[0], top - box[1], w * width, h * height)
            if cap and max(tile.size) > cap:
                old_width, old_height = tile.size
                tile.thumbnail((cap, cap), Image.Resampling.LANCZOS)
                sx, sy = tile.width / old_width, tile.height / old_height
                logical = (logical[0] * sx, logical[1] * sy, logical[2] * sx, logical[3] * sy)
            yield name, index, tile, logical
    yield "white", 0, Image.new("RGBA", (8, 8), "white"), (0, 0, 8, 8)


def subtract(space, occupied):
    x, y, right, bottom = space
    left, top, edge, foot = occupied
    if left >= right or top >= bottom or edge <= x or foot <= y:
        return [space]
    return [r for r in ((x, y, left, bottom), (edge, y, right, bottom), (x, y, right, top), (x, foot, right, bottom))
            if r[0] < r[2] and r[1] < r[3]]


def place(free, width, height):
    fits = [(min(r - x - width, b - y - height), max(r - x - width, b - y - height), y, x)
            for x, y, r, b in free if r - x >= width and b - y >= height]
    if not fits:
        raise ValueError(f"Shared UI atlas is full ({width} x {height})")
    _, _, y, x = min(fits)
    occupied = (x, y, x + width, y + height)
    spaces = sorted({remaining for space in free for remaining in subtract(space, occupied)})
    free[:] = [a for a in spaces if not any(a != b and b[0] <= a[0] and b[1] <= a[1] and b[2] >= a[2] and b[3] >= a[3]
                                                for b in spaces)]
    return x + GUTTER, y + GUTTER


def paste(atlas, tile, x, y):
    # Duplicate the outside texels so filtering never samples a neighbor.
    width, height = tile.size
    for bounds, left, top, size in (
        ((0, 0, 1, height), x - GUTTER, y, (GUTTER, height)),
        ((width - 1, 0, width, height), x + width, y, (GUTTER, height)),
        ((0, 0, width, 1), x, y - GUTTER, (width, GUTTER)),
        ((0, height - 1, width, height), x, y + height, (width, GUTTER)),
    ):
        atlas.paste(tile.crop(bounds).resize(size, Image.Resampling.NEAREST), (left, top))
    for dx, tx in ((-GUTTER, 0), (width, width - 1)):
        for dy, ty in ((-GUTTER, 0), (height, height - 1)):
            atlas.paste(tile.getpixel((tx, ty)), (x + dx, y + dy, x + dx + GUTTER, y + dy + GUTTER))
    atlas.paste(tile, (x, y))


def build():
    path = THEME / "atlas.json"
    metadata = json.loads(path.read_text(encoding="utf-8"))
    tiles = sorted(read_tiles(metadata), key=lambda t: (-max(t[2].size), -t[2].width * t[2].height, t[0], t[1]))
    atlas = Image.new("RGBA", (SIZE, SIZE))
    free = [(0, 0, SIZE, SIZE)]
    packed = {name: [None] * len(metadata[name]) for name in CELLS}
    packed["white"] = [None]
    for name, index, tile, (left, top, width, height) in tiles:
        x, y = place(free, tile.width + GUTTER * 2, tile.height + GUTTER * 2)
        paste(atlas, tile, x, y)
        packed[name][index] = [round(n / SIZE, 10) for n in (x + left, SIZE - y - top - height, width, height)]
    atlas.save(THEME / f"{NAME}.png", optimize=True)
    metadata = {name: data for name, data in metadata.items() if not name.startswith(NAME + ".")}
    metadata.update({f"{NAME}.{name}": entries for name, entries in packed.items()})
    path.write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Packed {len(tiles)} shared UI regions into {SIZE} x {SIZE}")


if __name__ == "__main__":
    build()
