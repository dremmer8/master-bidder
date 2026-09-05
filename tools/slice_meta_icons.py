#!/usr/bin/env python3
"""Slice ChatGPT meta-upgrade / booster sheets into named PNG sprites."""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "master-bidder-3d/Assets/content/ui/sprites/meta_sources"
OUT = ROOT / "master-bidder-3d/Assets/content/ui/Resources/MetaIcons"
SIZE = 256
# Sheet backdrop is near-pure black; icon fills are dark chocolate brown (~25-45 luma).
SHEET_BLACK = 14
FG_LUMA = 18


def luma(px: tuple[int, int, int, int] | tuple[int, int, int]) -> float:
    r, g, b = px[0], px[1], px[2]
    return 0.299 * r + 0.587 * g + 0.114 * b


def content_bbox(im: Image.Image, pad: int = 4, threshold: float = FG_LUMA) -> tuple[int, int, int, int]:
    rgba = im.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    min_x, min_y, max_x, max_y = w, h, 0, 0
    found = False
    for y in range(h):
        for x in range(w):
            p = px[x, y]
            if p[3] < 8:
                continue
            if luma(p) <= threshold:
                continue
            found = True
            if x < min_x:
                min_x = x
            if y < min_y:
                min_y = y
            if x > max_x:
                max_x = x
            if y > max_y:
                max_y = y
    if not found:
        return 0, 0, w, h
    return (
        max(0, min_x - pad),
        max(0, min_y - pad),
        min(w, max_x + 1 + pad),
        min(h, max_y + 1 + pad),
    )


def crop_content(im: Image.Image) -> Image.Image:
    box = content_bbox(im)
    return im.crop(box)


def punch_sheet_black(im: Image.Image) -> Image.Image:
    """Make pure sheet-black pixels transparent; keep dark-brown icon fills."""
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            p = px[x, y]
            if luma(p) <= SHEET_BLACK:
                px[x, y] = (0, 0, 0, 0)
    return im


def to_square_canvas(im: Image.Image, size: int = SIZE, circular: bool = False) -> Image.Image:
    im = punch_sheet_black(im.convert("RGBA"))
    box = content_bbox(im, pad=2, threshold=SHEET_BLACK + 1)
    im = im.crop(box)
    w, h = im.size
    side = max(w, h)
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(im, ((side - w) // 2, (side - h) // 2), im)
    out = canvas.resize((size, size), Image.Resampling.LANCZOS)

    if circular:
        mask = Image.new("L", (size, size), 0)
        ImageDraw.Draw(mask).ellipse((1, 1, size - 2, size - 2), fill=255)
        transparent = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        out = Image.composite(out, transparent, mask)
    return out


def find_blobs(im: Image.Image, min_area: int = 800) -> list[tuple[int, int, int, int]]:
    """Return bounding boxes of non-black connected components (4-connected)."""
    rgba = im.convert("RGBA")
    w, h = rgba.size
    px = rgba.load()
    visited = [[False] * w for _ in range(h)]
    boxes: list[tuple[int, int, int, int]] = []

    def is_fg(x: int, y: int) -> bool:
        p = px[x, y]
        return p[3] > 8 and luma(p) > FG_LUMA

    for y in range(h):
        for x in range(w):
            if visited[y][x] or not is_fg(x, y):
                continue
            stack = [(x, y)]
            visited[y][x] = True
            min_x = max_x = x
            min_y = max_y = y
            area = 0
            while stack:
                cx, cy = stack.pop()
                area += 1
                if cx < min_x:
                    min_x = cx
                if cx > max_x:
                    max_x = cx
                if cy < min_y:
                    min_y = cy
                if cy > max_y:
                    max_y = cy
                for nx, ny in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                    if nx < 0 or ny < 0 or nx >= w or ny >= h:
                        continue
                    if visited[ny][nx] or not is_fg(nx, ny):
                        continue
                    visited[ny][nx] = True
                    stack.append((nx, ny))
            if area >= min_area:
                boxes.append((min_x, min_y, max_x + 1, max_y + 1))
    # Largest first, then reading order
    boxes.sort(key=lambda b: ((b[2] - b[0]) * (b[3] - b[1])), reverse=True)
    return boxes


def reading_order(boxes: list[tuple[int, int, int, int]]) -> list[tuple[int, int, int, int]]:
    if not boxes:
        return []
    heights = [b[3] - b[1] for b in boxes]
    row_tol = max(24, int(sum(heights) / len(heights) * 0.45))
    rows: list[list[tuple[int, int, int, int]]] = []
    for b in sorted(boxes, key=lambda x: (x[1] + x[3]) / 2):
        cy = (b[1] + b[3]) / 2
        placed = False
        for row in rows:
            rcy = sum((r[1] + r[3]) / 2 for r in row) / len(row)
            if abs(cy - rcy) <= row_tol:
                row.append(b)
                placed = True
                break
        if not placed:
            rows.append([b])
    rows.sort(key=lambda row: sum((r[1] + r[3]) / 2 for r in row) / len(row))
    ordered: list[tuple[int, int, int, int]] = []
    for row in rows:
        ordered.extend(sorted(row, key=lambda b: b[0]))
    return ordered


def save(im: Image.Image, name: str, circular: bool = False) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    sprite = to_square_canvas(im, SIZE, circular=circular)
    path = OUT / f"{name}.png"
    sprite.save(path)
    print(f"  wrote {path.name} ({sprite.size[0]}x{sprite.size[1]})")


def export_single(src_name: str, id_name: str, circular: bool = False) -> None:
    im = Image.open(SRC / src_name)
    save(crop_content(im), id_name, circular=circular)


def export_blobs(src_name: str, ids: list[str], circular: bool, expect: int | None = None) -> None:
    im = Image.open(SRC / src_name)
    boxes = reading_order(find_blobs(im))
    if expect is not None and len(boxes) != expect:
        # Merge tiny fragments: keep the largest `expect`
        boxes = reading_order(find_blobs(im, min_area=2000))
        if len(boxes) > expect:
            boxes = boxes[:expect]
            boxes = reading_order(boxes)
    print(f"{src_name}: found {len(boxes)} blobs for {len(ids)} ids")
    if len(boxes) < len(ids):
        raise SystemExit(f"Not enough blobs in {src_name}: {len(boxes)} < {len(ids)}")
    for box, name in zip(boxes, ids):
        save(im.crop(box), name, circular=circular)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    print("Slicing meta icons ->", OUT)

    # Permanent upgrades (rounded-square sources)
    export_single("snowflake.jpg", "cool-nerves")
    export_single("coins_infinity.jpg", "investment-portfolio")
    export_single("magnifier.jpg", "fast-appraisal")
    export_single("medal.jpg", "lot-master")

    export_blobs(
        "upgrades_a.jpg",
        ["credit-line", "loyal-client", "personal-secretary", "expert-reputation"],
        circular=False,
        expect=4,
    )
    export_blobs(
        "upgrades_b.jpg",
        ["legal-counsel", "standing-advance", "calm-hall", "expanded-hall"],
        circular=False,
        expect=4,
    )

    # Temporary boosters (round)
    export_blobs(
        "boosters_5.jpg",
        [
            "auction-discount",
            "budget-advance",
            "commission-bonus",
            "lucky-lot",
            "marathon",
        ],
        circular=True,
        expect=5,
    )
    export_blobs(
        "boosters_4.jpg",
        ["insurance", "expert-appraiser", "quiet-start", "sleepy-rivals"],
        circular=True,
        expect=4,
    )

    expected = {
        # upgrades
        "fast-appraisal",
        "expert-reputation",
        "cool-nerves",
        "standing-advance",
        "legal-counsel",
        "credit-line",
        "calm-hall",
        "expanded-hall",
        "lot-master",
        "loyal-client",
        "personal-secretary",
        "investment-portfolio",
        # boosters
        "insurance",
        "expert-appraiser",
        "quiet-start",
        "sleepy-rivals",
        "auction-discount",
        "budget-advance",
        "commission-bonus",
        "lucky-lot",
        "marathon",
    }
    got = {p.stem for p in OUT.glob("*.png")}
    missing = sorted(expected - got)
    extra = sorted(got - expected)
    if missing:
        raise SystemExit(f"Missing sprites: {missing}")
    if extra:
        print("Extra files (ok):", extra)
    print(f"Done: {len(expected)} sprites.")


if __name__ == "__main__":
    main()
