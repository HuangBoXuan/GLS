"""Read atlas alpha bounds and write Unity sprite metadata; never edit bitmaps."""
import json
import re
import sys
from pathlib import Path

sys.dont_write_bytecode = True
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
THEME = ROOT / "Assets/LoopSort/Resources/LoopSort/Theme"


def sprite_regions(alpha, columns, rows):
    """Find substantial alpha islands without cutting art at nominal grid lines."""
    width, height = alpha.size
    mask = alpha.point(lambda a: 255 if a >= 64 else 0).tobytes()
    runs, parents, previous = [], [], []

    def find(index):
        while parents[index] != index:
            parents[index] = parents[parents[index]]
            index = parents[index]
        return index

    for y in range(height):
        current = []
        for match in re.finditer(b"\xff+", mask[y * width:(y + 1) * width]):
            left, right = match.span()
            index = len(runs)
            runs.append((left, right, y))
            parents.append(index)
            current.append(index)
            for old in previous:
                x0, x1, _ = runs[old]
                if x0 > right:
                    break
                if x1 >= left:
                    parents[find(old)] = find(index)
        previous = current

    islands = {}
    for index, (left, right, y) in enumerate(runs):
        key = find(index)
        box = islands.setdefault(key, [left, y, right, y + 1, 0])
        box[0], box[1] = min(box[0], left), min(box[1], y)
        box[2], box[3] = max(box[2], right), max(box[3], y + 1)
        box[4] += right - left

    regions = [None] * (columns * rows)
    minimum_area = max(24, width * height / (columns * rows) * .008)
    for left, top, right, bottom, area in islands.values():
        if area < minimum_area:
            continue
        column = min(columns - 1, int((left + right) * .5 * columns / width))
        row = min(rows - 1, int((top + bottom) * .5 * rows / height))
        index = row * columns + column
        prior = regions[index]
        regions[index] = [left, top, right, bottom] if prior is None else [
            min(prior[0], left), min(prior[1], top), max(prior[2], right), max(prior[3], bottom)]
    if any(box is None for box in regions):
        raise ValueError("Atlas has an empty sprite region; review its layout before importing")
    return [(max(0, left - 2), max(0, top - 2), min(width, right + 2), min(height, bottom + 2))
            for left, top, right, bottom in regions]


def atlas_bounds(name, columns, rows, transparent):
    with Image.open(THEME / f"{name}.png") as image:
        width, height = image.size
        alpha = image.getchannel("A") if transparent else None
        regions = sprite_regions(alpha, columns, rows) if alpha is not None else None
        bounds = []
        for index in range(columns * rows):
            x0, y0 = round(index % columns * width / columns), round(index // columns * height / rows)
            x1, y1 = round((index % columns + 1) * width / columns), round((index // columns + 1) * height / rows)
            if alpha is not None:
                # Merge disconnected icon parts by logical cell, but retain the
                # full actual bounds even when they cross a nominal cell edge.
                left, top, right, bottom = regions[index]
            else:
                left, top, right, bottom = x0 + 1, y0 + 1, x1 - 1, y1 - 1
            bounds.append([round(left / width, 8), round((height - bottom) / height, 8),
                           round((right - left) / width, 8), round((bottom - top) / height, 8)])
        return bounds


if __name__ == "__main__":
    # Preserve the current custom atlas metadata while refreshing core regions.
    data = json.loads((THEME / "atlas.json").read_text(encoding="utf-8"))
    for key in list(data):
        if key.split(".")[0] in {"panels", "worlds", "mine-key", "mine-lock", "tutorial-hand", "game-board"}:
            del data[key]
    data.update({"icons": atlas_bounds("icons", 6, 6, True),
            "belt-capacity": atlas_bounds("belt-capacity", 1, 1, True),
            "reference-ui": atlas_bounds("reference-ui", 4, 3, True)})
    # The reference skin has a short lower shadow. Anchor labels to the cream
    # face, not the full alpha bounds, so text stays visually centered.
    data["reference-ui.content"] = [[0,0,1,1] for _ in range(12)]
    data["reference-ui.content"][0] = [.075,.21,.85,.64]
    data["reference-ui.content"][1] = [.13,.19,.74,.69]
    data["reference-ui.content"][8] = [.10,.145,.80,.78]
    data["reference-ui.content"][10] = [.10,.14,.78,.76]
    data["reference-ui.content"][11] = [.15,.20,.70,.65]
    data["reference-ui.borders"] = [[0,0,0,0] for _ in range(12)]
    data["reference-ui.borders"][0] = [.18,.30,.18,.27]
    data["reference-ui.borders"][1] = [.24,.27,.24,.24]
    data["reference-ui.borders"][10] = [.21,.22,.21,.20]
    data["reference-ui.borders"][11] = [.24,.27,.24,.24]
    (THEME / "atlas.json").write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print("Wrote theme metadata including 12 reference UI sprites and the portrait game board; source images unchanged.")
    from build_ui_atlas import build
    build()
