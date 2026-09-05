#!/usr/bin/env python3
"""Install LOW textures from curated Google Arts asset pages (lh3 =s0)."""
from __future__ import annotations

import re
import ssl
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

ROOT = Path(r"d:\Unity3d\master-bidder\master-bidder")
TEX = ROOT / r"master-bidder-3d\Assets\content\paintings\tex"
BAK = ROOT / r"tools\painting_tex_backup"
CFG = ROOT / r"master-bidder-3d\Assets\content\paintings\configs"
TMP = ROOT / r"tools\_dl_tmp"
LOG = ROOT / r"tools\google_arts_upgrade_log.txt"
TMP.mkdir(exist_ok=True)

FFMPEG = (
    r"C:\Users\Paul\AppData\Local\Microsoft\WinGet\Packages"
    r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
    r"\ffmpeg-8.1.2-full_build\bin\ffmpeg.exe"
)
MAX_EDGE = 3840
AR_TOL = 0.35
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/124.0.0.0 Safari/537.36"
ssl_ctx = ssl.create_default_context()

# Loaded from google_arts_discovered.tsv (add new asset pages there).
ASSETS: dict[str, str] = {}
DISC = ROOT / r"tools\google_arts_discovered.tsv"
if DISC.exists():
    for line in DISC.read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.startswith("#") or "\t" not in line:
            continue
        fname, page = line.split("\t", 1)
        ASSETS[fname.strip()] = page.strip()


def log(msg: str) -> None:
    print(msg, flush=True)
    _log_fh.write(msg + "\n")
    _log_fh.flush()


def http_get(url: str, binary: bool = False):
    req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept-Language": "en"})
    with urllib.request.urlopen(req, context=ssl_ctx, timeout=120) as resp:
        data = resp.read()
    return data if binary else data.decode("utf-8", "replace")


def camel(n: str) -> str:
    n = n.removeprefix("Painting_")
    s1 = re.sub(r"(.)([A-Z][a-z]+)", r"\1_\2", n)
    return re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", s1).lower()


def cfg_ar() -> dict[str, float]:
    out = {}
    for c in CFG.glob("Painting_*.asset"):
        t = c.read_text(encoding="utf-8", errors="replace")
        w = re.search(r"(?m)^\s*width:\s*([0-9.]+)", t)
        h = re.search(r"(?m)^\s*height:\s*([0-9.]+)", t)
        if not w or not h:
            continue
        fname = camel(c.stem) + ".jpg"
        if fname == "third_of_may1808.jpg":
            fname = "third_of_may_1808.jpg"
        out[fname] = float(w.group(1)) / float(h.group(1))
    return out


def probe(path: Path) -> tuple[int, int]:
    p = subprocess.run([FFMPEG, "-i", str(path)], capture_output=True, text=True, encoding="utf-8", errors="replace")
    m = re.search(r"(\d{2,5})x(\d{2,5})", p.stderr)
    return (int(m.group(1)), int(m.group(2))) if m else (0, 0)


def encode(src: Path, dest: Path) -> tuple[int, int]:
    vf = (
        f"scale="
        f"'if(gt(max(iw,ih),{MAX_EDGE}),if(gt(iw,ih),{MAX_EDGE},-2),iw)':"
        f"'if(gt(max(iw,ih),{MAX_EDGE}),if(gt(ih,iw),{MAX_EDGE},-2),ih)'"
    )
    tmp = dest.with_suffix(".tmp.jpg")
    subprocess.run([FFMPEG, "-y", "-i", str(src), "-vf", vf, "-q:v", "2", str(tmp)], check=True, capture_output=True)
    tmp.replace(dest)
    return probe(dest)


def og_image(html: str) -> str | None:
    ogs = re.findall(r'property="og:image"\s+content="([^"]+)"', html)
    ogs += re.findall(r'content="([^"]+)"\s+property="og:image"', html)
    if ogs:
        return ogs[0]
    cis = re.findall(r"https://lh3\.googleusercontent\.com/ci/[A-Za-z0-9_\-]+", html)
    return cis[0] if cis else None


def download_max(og: str) -> tuple[bytes, str]:
    base = og.split("=")[0]
    best = None
    for cand in [base + "=s0", base + "=w2400", base + "=s2400", og]:
        try:
            data = http_get(cand, binary=True)
        except Exception:
            continue
        if not isinstance(data, (bytes, bytearray)) or len(data) < 30000:
            continue
        if best is None or len(data) > len(best[0]):
            best = (bytes(data), cand)
    if not best:
        raise RuntimeError("no downloadable lh3 variant")
    return best


def main() -> int:
    global _log_fh
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    _log_fh = LOG.open("w", encoding="utf-8")
    ars = cfg_ar()
    rows = ["file\tstatus\tsize\tnote"]
    for fname, page in sorted(ASSETS.items()):
        path = TEX / fname
        if not path.exists():
            rows.append(f"{fname}\tMISSING_FILE\t\t")
            continue
        w, h = probe(path)
        if max(w, h) >= 1200:
            row = f"{fname}\tKEEP_HI\t{w}x{h}\t"
            log(row)
            rows.append(row)
            continue
        log(f"GET {fname}")
        try:
            html = http_get(page)
            if "Error 404" in html:
                raise RuntimeError("404")
            og = og_image(html)
            if not og:
                raise RuntimeError("no og:image")
            data, used = download_max(og)
            raw = TMP / f"{fname}.raw"
            raw.write_bytes(data)
            nw, nh = encode(raw, path)
            raw.unlink(missing_ok=True)
            tex_ar = nw / nh if nh else 0
            delta = abs(tex_ar - ars.get(fname, 0)) if ars.get(fname) else 0
            if delta > AR_TOL:
                path.write_bytes((BAK / fname).read_bytes())
                row = f"{fname}\tREJECTED_AR\t{nw}x{nh}\tdelta={delta:.3f} {used}"
            else:
                row = f"{fname}\tUPGRADED\t{nw}x{nh}\t{used}"
            log(row)
            rows.append(row)
        except Exception as exc:
            row = f"{fname}\tFAIL\t\t{exc}"
            log(row)
            rows.append(row)
        time.sleep(0.4)
    (ROOT / r"tools\google_arts_status.tsv").write_text("\n".join(rows) + "\n", encoding="utf-8")
    _log_fh.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
