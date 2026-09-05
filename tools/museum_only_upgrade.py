#!/usr/bin/env python3
"""Upgrade LOW textures from museum CDNs only (no Wikimedia)."""
from __future__ import annotations

import re
import ssl
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(r"d:\Unity3d\master-bidder\master-bidder")
TEX = ROOT / r"master-bidder-3d\Assets\content\paintings\tex"
CFG = ROOT / r"master-bidder-3d\Assets\content\paintings\configs"
BAK = ROOT / r"tools\painting_tex_backup"
LOG = ROOT / r"tools\painting_upgrade_log.txt"
STATUS = ROOT / r"tools\painting_source_status.tsv"
MAX_EDGE = 3840
AR_TOL = 0.35
FFMPEG = (
    r"C:\Users\Paul\AppData\Local\Microsoft\WinGet\Packages"
    r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
    r"\ffmpeg-8.1.2-full_build\bin\ffmpeg.exe"
)
UA = "MasterBidderMuseumOnly/1 (Unity game; museum open-access CDN)"

# Exact works only — loaded from museum_cdn_proven.tsv (add new rows there).
CURATED_URLS: dict[str, str] = {}
PROVEN = ROOT / r"tools\museum_cdn_proven.tsv"
if PROVEN.exists():
    for line in PROVEN.read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.startswith("#") or "\t" not in line:
            continue
        parts = line.split("\t")
        if len(parts) < 3:
            continue
        CURATED_URLS[parts[0].strip()] = parts[2].strip()


HOST_GAP = {
    "images.metmuseum.org": 0.3,
    "iiif.micr.io": 0.4,
    "media.ng-london.org.uk": 0.4,
    "fotothek.slub-dresden.de": 0.5,
    "www.artic.edu": 1.0,
    "openaccess-cdn.clevelandart.org": 0.3,
    "default": 0.5,
}

ssl_ctx = ssl.create_default_context()
ssl_ctx_insecure = ssl._create_unverified_context()
_log_lock = threading.Lock()
_log_fh = None


def log(msg: str) -> None:
    with _log_lock:
        print(msg, flush=True)
        if _log_fh:
            _log_fh.write(msg + "\n")
            _log_fh.flush()


@dataclass
class HostGate:
    gap: float
    lock: threading.Lock = field(default_factory=threading.Lock)
    next_ok: float = 0.0

    def wait(self) -> None:
        with self.lock:
            now = time.monotonic()
            d = self.next_ok - now
            if d > 0:
                time.sleep(d)
            self.next_ok = time.monotonic() + self.gap


_gates: dict[str, HostGate] = {}
_gates_lock = threading.Lock()


def gate(url: str) -> HostGate:
    host = urllib.parse.urlparse(url).hostname or "default"
    with _gates_lock:
        if host not in _gates:
            _gates[host] = HostGate(HOST_GAP.get(host, HOST_GAP["default"]))
        return _gates[host]


def camel_to_snake(name: str) -> str:
    name = name.removeprefix("Painting_")
    s1 = re.sub(r"(.)([A-Z][a-z]+)", r"\1_\2", name)
    return re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", s1).lower()


@dataclass
class Painting:
    file: str
    title: str
    artist: str
    cfg_w: float
    cfg_h: float

    @property
    def cfg_ar(self) -> float:
        return self.cfg_w / self.cfg_h if self.cfg_h else 0.0


def parse_configs() -> dict[str, Painting]:
    out: dict[str, Painting] = {}
    for cfg in CFG.glob("Painting_*.asset"):
        text = cfg.read_text(encoding="utf-8", errors="replace")
        title_m = re.search(r"paintingTitle:\s*(.+)", text)
        artist_m = re.search(r"(?m)^\s*artist:\s*(.+)$", text)
        w_m = re.search(r"(?m)^\s*width:\s*([0-9.]+)", text)
        h_m = re.search(r"(?m)^\s*height:\s*([0-9.]+)", text)
        if not title_m or not w_m or not h_m:
            continue
        fname = camel_to_snake(cfg.stem) + ".jpg"
        if fname == "third_of_may1808.jpg":
            fname = "third_of_may_1808.jpg"
        out[fname] = Painting(
            file=fname,
            title=title_m.group(1).strip().strip('"'),
            artist=(artist_m.group(1).strip().strip('"') if artist_m else ""),
            cfg_w=float(w_m.group(1)),
            cfg_h=float(h_m.group(1)),
        )
    return out


def probe(path: Path) -> tuple[int, int]:
    proc = subprocess.run(
        [FFMPEG, "-i", str(path)],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    m = re.search(r"(\d{2,5})x(\d{2,5})", proc.stderr)
    return (int(m.group(1)), int(m.group(2))) if m else (0, 0)


def to_jpeg_max4k(src: Path, dest: Path) -> tuple[int, int]:
    vf = (
        f"scale="
        f"'if(gt(max(iw,ih),{MAX_EDGE}),if(gt(iw,ih),{MAX_EDGE},-2),iw)':"
        f"'if(gt(max(iw,ih),{MAX_EDGE}),if(gt(ih,iw),{MAX_EDGE},-2),ih)'"
    )
    tmp = dest.with_suffix(f".{threading.get_ident()}.enc.jpg")
    subprocess.run(
        [FFMPEG, "-y", "-i", str(src), "-vf", vf, "-q:v", "2", str(tmp)],
        check=True,
        capture_output=True,
    )
    tmp.replace(dest)
    return probe(dest)


def restore_backup(fname: str) -> None:
    src = BAK / fname
    dst = TEX / fname
    if src.exists():
        dst.write_bytes(src.read_bytes())


def http_get(url: str) -> bytes:
    ctx = ssl_ctx_insecure if url.startswith("http://") else ssl_ctx
    last = None
    for attempt in range(4):
        gate(url).wait()
        try:
            req = urllib.request.Request(
                url,
                headers={
                    "User-Agent": UA,
                    "Accept": "image/avif,image/webp,image/*,*/*;q=0.8",
                },
            )
            with urllib.request.urlopen(req, context=ctx, timeout=240) as resp:
                data = resp.read()
            if len(data) < 20_000:
                raise RuntimeError(f"tiny ({len(data)})")
            return data
        except Exception as exc:  # noqa: BLE001
            last = exc
            time.sleep(2 + attempt * 2)
    raise RuntimeError(f"download failed: {url} ({last})")


def playwright_get(url: str) -> bytes:
    from playwright.sync_api import sync_playwright

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        context = browser.new_context(user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36")
        page = context.new_page()
        # warm up domain
        host = urllib.parse.urlparse(url).scheme + "://" + urllib.parse.urlparse(url).hostname
        try:
            page.goto(host, wait_until="domcontentloaded", timeout=60000)
            page.wait_for_timeout(3000)
        except Exception:
            pass
        resp = page.request.get(url, timeout=120000)
        data = resp.body()
        browser.close()
        if len(data) < 20_000:
            raise RuntimeError(f"playwright tiny ({len(data)}) status={resp.status}")
        return data


def upgrade_one(p: Painting) -> str:
    path = TEX / p.file
    if not path.exists():
        return f"{p.file}\tMISSING_FILE\t{p.title}\t{p.artist}\t\t"
    w, h = probe(path)
    if max(w, h) >= 1200:
        return f"{p.file}\tKEEP_HI\t{p.title}\t{p.artist}\t{w}x{h}\t"
    url = CURATED_URLS.get(p.file)
    if not url:
        return f"{p.file}\tMISSING_SOURCE\t{p.title}\t{p.artist}\t{w}x{h}\tno museum CDN yet"

    log(f"GET {p.file} <- {url[:110]}")
    try:
        if "artic.edu" in url:
            data = playwright_get(url)
        else:
            try:
                data = http_get(url)
            except Exception:
                # fallback browser for stubborn hosts
                data = playwright_get(url)
    except Exception as exc:  # noqa: BLE001
        return f"{p.file}\tFAIL_DOWNLOAD\t{p.title}\t{p.artist}\t{w}x{h}\t{exc}"

    raw = path.with_suffix(f".{threading.get_ident()}.raw")
    raw.write_bytes(data)
    try:
        nw, nh = to_jpeg_max4k(raw, path)
    finally:
        raw.unlink(missing_ok=True)

    tex_ar = nw / nh if nh else 0
    delta = abs(tex_ar - p.cfg_ar) if p.cfg_ar else 0
    if delta > AR_TOL:
        restore_backup(p.file)
        return (
            f"{p.file}\tREJECTED_AR\t{p.title}\t{p.artist}\tgot {nw}x{nh}\t"
            f"cfgAR={p.cfg_ar:.3f} texAR={tex_ar:.3f} delta={delta:.3f}"
        )
    return f"{p.file}\tUPGRADED\t{p.title}\t{p.artist}\t{nw}x{nh}\t{url}"


def main() -> int:
    global _log_fh
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    _log_fh = LOG.open("w", encoding="utf-8")
    paintings = parse_configs()

    todo = []
    for fname, p in sorted(paintings.items()):
        path = TEX / fname
        if not path.exists():
            continue
        w, h = probe(path)
        if max(w, h) >= 1200:
            continue
        todo.append(p)

    log(f"Museum-only upgrade for {len(todo)} LOW textures ({len(CURATED_URLS)} curated URLs)")
    lines = ["file\tstatus\ttitle\tartist\tsize\tnote"]

    # Parallel across hosts; gates serialize same host.
    with ThreadPoolExecutor(max_workers=6) as pool:
        futs = [pool.submit(upgrade_one, p) for p in todo]
        for fut in as_completed(futs):
            row = fut.result()
            log(row)
            lines.append(row)

    STATUS.write_text("\n".join(lines) + "\n", encoding="utf-8")
    from collections import Counter

    c = Counter(l.split("\t")[1] for l in lines[1:])
    log(f"Summary: {dict(c)}")
    _log_fh.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
