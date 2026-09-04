"""Create panel_body (flat top) + panel_ornament for Intro; leave panel.png unchanged."""
from pathlib import Path
from PIL import Image

ROOT = Path(r"d:\Unity3d\master-bidder\master-bidder\master-bidder-3d")
SLICED = ROOT / "Assets/content/ui/sprites/sliced"
RES = ROOT / "Assets/content/ui/Resources/UiSprites"
SRC = SLICED / "panel.png"

img = Image.open(SRC).convert("RGBA")
w, h = img.size
cx = w // 2
fill = (226, 216, 204)

orn_xs, orn_ys = [], []
for y in range(18):
    for x in range(w):
        r, g, b, a = img.getpixel((x, y))
        if a < 40:
            continue
        if abs(r - fill[0]) + abs(g - fill[1]) + abs(b - fill[2]) < 25 and y >= 12:
            continue
        if abs(x - cx) <= 55:
            orn_xs.append(x)
            orn_ys.append(y)

pad = 2
ox0, ox1 = max(0, min(orn_xs) - pad), min(w, max(orn_xs) + pad + 1)
oy0, oy1 = 0, max(orn_ys) + pad + 1
ornament = img.crop((ox0, oy0, ox1, oy1))

for dest in (SLICED / "panel_ornament.png", RES / "panel_ornament.png"):
    ornament.save(dest)
print("ornament", ornament.size, "box", ox0, oy0, ox1, oy1)

sample_x = 70
body = img.copy()
for y in range(0, 18):
    sample = img.getpixel((sample_x, y))
    for x in range(ox0, ox1):
        r, g, b, a = sample
        cur = img.getpixel((x, y))
        if a < 20:
            cr, cg, cb, ca = cur
            if abs(cr - fill[0]) + abs(cg - fill[1]) + abs(cb - fill[2]) < 30:
                body.putpixel((x, y), (*fill, 253 if ca > 200 else ca))
            else:
                body.putpixel((x, y), (0, 0, 0, 0))
        else:
            body.putpixel((x, y), sample)

for dest in (SLICED / "panel_body.png", RES / "panel_body.png"):
    body.save(dest)
print("panel_body saved")
