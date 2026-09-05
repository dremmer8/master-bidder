#!/usr/bin/env python3
"""
Strict painting texture upgrade.
- Only curated Wikimedia Commons / Met URLs (exact works from Painting_*.asset).
- Reject downloads whose aspect ratio differs from config by >0.35.
- Never invent substitutes; mark MISSING_SOURCE instead.
- Wikimedia: sequential with host gap (parallel only makes 429 worse).
"""

from __future__ import annotations

import codecs
import hashlib
import re
import ssl
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(r"d:\Unity3d\master-bidder\master-bidder")
TEX_DIR = ROOT / r"master-bidder-3d\Assets\content\paintings\tex"
CFG_DIR = ROOT / r"master-bidder-3d\Assets\content\paintings\configs"
BAK_DIR = ROOT / r"tools\painting_tex_backup"
STATUS = ROOT / r"tools\painting_source_status.tsv"
LOG = ROOT / r"tools\painting_upgrade_log.txt"
MAX_EDGE = 3840
AR_TOL = 0.35
FFMPEG = (
    r"C:\Users\Paul\AppData\Local\Microsoft\WinGet\Packages"
    r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
    r"\ffmpeg-8.1.2-full_build\bin\ffmpeg.exe"
)
UA = "MasterBidderPaintingUpgrade/12-strict-seq (Unity game; curated Commons only)"

# Wikimedia MUST stay sequential — parallel triggers 429 and is slower overall.
HOST_GAP = {
    "upload.wikimedia.org": 8.0,
    "images.metmuseum.org": 0.4,
    "en.wikipedia.org": 1.0,
    "default": 1.0,
}

# Hand-curated Commons filenames that depict the EXACT work in the config.
CURATED_COMMONS: dict[str, str] = {
    "school_of_athens.jpg": "Raphael_School_of_Athens.jpg",
    "primavera.jpg": "Botticelli-primavera.jpg",
    "portrait_of_a_man_in_a_red_turban.jpg": (
        "Jan_van_Eyck_-_Portrait_of_a_Man_(Self_Portrait?)_1433.jpg"
    ),
    "pinkie_sarah_barrett_moulton.jpg": "Pinkie_detailed.jpg",
    "the_night_watch.jpg": "The_Nightwatch_by_Rembrandt_-_Rijksmuseum.jpg",
    "sunflowers.jpg": "Vincent_Willem_van_Gogh_127.jpg",
    "the_hay_wain.jpg": "John_Constable_The_Hay_Wain.jpg",
    "the_swing.jpg": "Jean-Honoré_Fragonard_-_The_Swing.jpg",
    "wanderer_above_the_sea_of_fog.jpg": (
        "Caspar_David_Friedrich_-_Wanderer_above_the_sea_of_fog.jpg"
    ),
    "the_sea_of_ice.jpg": (
        "Caspar_David_Friedrich_-_Das_Eismeer_-_Hamburger_Kunsthalle_-_02.jpg"
    ),
    "third_of_may_1808.jpg": (
        "El_Tres_de_Mayo,_by_Francisco_de_Goya,_from_Prado_in_Google_Earth.jpg"
    ),
    "venus_and_mars.jpg": "Sandro_Botticelli_-_Venus_and_Mars_-_Google_Art_Project.jpg",
    "the_jewish_bride.jpg": (
        "Rembrandt_Harmensz._van_Rijn_-_Het_Joodse_bruidje_-_Google_Art_Project.jpg"
    ),
    "the_ninth_wave.jpg": "Ivan_Aivazovsky_-_The_Ninth_Wave_-_Google_Art_Project.jpg",
    "the_windmill_at_wijk_bij_duurstede.jpg": (
        "Jacob_Isaacksz._van_Ruisdael_-_De_molen_bij_Wijk_bij_Duurstede_-_Google_Art_Project.jpg"
    ),
    "the_feast_of_saint_nicholas.jpg": (
        "Jan_Havicksz._Steen_-_Het_Sint_Nicolaasfeest_-_Google_Art_Project.jpg"
    ),
    "the_gallant_conversation.jpg": (
        "Gerard_ter_Borch_(II)_-_Gallant_Conversation_-_Google_Art_Project.jpg"
    ),
    "the_proposition.jpg": (
        "Judith_Leyster_-_Man_offering_money_to_a_young_woman_(The_Proposition)"
        "_-_Google_Art_Project.jpg"
    ),
    "the_cradle.jpg": "Berthe_Morisot_-_Le_Berceau.jpg",
    "the_ballet_class.jpg": "Edgar_Degas_-_The_Dance_Class_-_Google_Art_Project.jpg",
    "supper_at_emmaus_caravaggio.jpg": (
        "1602-3_Caravaggio,_Supper_at_Emmaus_National_Gallery,_London.jpg"
    ),
    "still_life_with_a_nautilus_cup.jpg": (
        "Still_Life_with_Chinese_Bowl_and_Nautilus_1662_Willem_Kalf.jpg"
    ),
    "the_goldfinch.jpg": "Carel_Fabritius_-_The_Goldfinch_-_WGA07833.jpg",
    "the_laughing_cavalier.jpg": "The_Laughing_Cavalier.jpg",
    "regentesses_of_the_old_mens_almshouse.jpg": (
        "Frans_Hals_-_Regentessen_van_het_Oudemannenhuis_-_Google_Art_Project.jpg"
    ),
    "sistine_madonna.jpg": "Raphael_Sistine_Madonna.jpg",
    "portrait_of_innocent_x.jpg": "Portrait_of_Innocent_X.jpg",
    "the_sin.jpg": "Franz_von_Stuck_001.jpg",
    "water_lilies.jpg": "Claude_Monet_-_Water_Lilies_-_1906,_Ryerson.jpg",
    "tribute_money.jpg": "Masaccio,_cappella_brancacci,_san_pietro_paga_il_tributo.jpg",
    "cornelia_mother_of_the_gracchi.jpg": (
        "Angelica_kauffman_ra_cornelia_mother_of_the_gracchi060808).jpg"
    ),
    "three_beauties_of_the_present_day.jpg": (
        "Kitagawa_Utamaro_-_Toji_san_bijin_(Three_Beauties_of_the_Present_Day)"
        "From_Bijin-ga_(Pictures_of_Beautiful_Women),_published_by_Tsutaya_Juzaburo"
        "_-_Google_Art_Project.jpg"
    ),
    "tahitian_women_on_the_beach.jpg": "Paul_Gauguin_089.jpg",
    "the_basket_of_apples.jpg": "Paul_Cézanne,_The_Basket_of_Apples.jpg",
    "rubens_isabella_brant_honeysuckle_bower.jpg": (
        "Peter_Paul_Rubens_-_The_Artist_and_His_First_Wife,_Isabella_Brant,"
        "_in_the_Honeysuckle_Bower_-_Google_Art_Project.jpg"
    ),
    "sudden_shower_over_shin_ohashi_bridge.jpg": "Hiroshige_Atake_sous_une_averse_soudaine.jpg",
}

CURATED_URLS: dict[str, str] = {
    "sudden_shower_over_shin_ohashi_bridge.jpg": (
        "https://images.metmuseum.org/CRDImages/as/original/DP121525.jpg"
    ),
    "the_gulf_stream.jpg": (
        "https://images.metmuseum.org/CRDImages/ad/original/DP-20821-001.jpg"
    ),
}

ssl_ctx = ssl.create_default_context()
_log_lock = threading.Lock()
_log_fh = None


def log(msg: str) -> None:
    with _log_lock:
        print(msg, flush=True)
        if _log_fh:
            _log_fh.write(msg + "\n")
            _log_fh.flush()


def decode_unity(s: str) -> str:
    s = s.strip().strip('"')
    try:
        return codecs.decode(s, "unicode_escape")
    except Exception:
        return s


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
    for cfg in CFG_DIR.glob("Painting_*.asset"):
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
            title=decode_unity(title_m.group(1)),
            artist=decode_unity(artist_m.group(1)) if artist_m else "",
            cfg_w=float(w_m.group(1)),
            cfg_h=float(h_m.group(1)),
        )
    return out


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
            gap = HOST_GAP.get(host, HOST_GAP["default"])
            _gates[host] = HostGate(gap=gap)
        return _gates[host]


def commons_url(filename: str) -> str:
    h = hashlib.md5(filename.encode("utf-8")).hexdigest()
    q = urllib.parse.quote(filename, safe="")
    return f"https://upload.wikimedia.org/wikipedia/commons/{h[0]}/{h[0:2]}/{q}"


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


def source_url(fname: str) -> str | None:
    if fname in CURATED_URLS:
        return CURATED_URLS[fname]
    if fname in CURATED_COMMONS:
        return commons_url(CURATED_COMMONS[fname])
    return None


def restore_backup(fname: str) -> None:
    src = BAK_DIR / fname
    dst = TEX_DIR / fname
    if src.exists():
        dst.write_bytes(src.read_bytes())


def http_get(url: str) -> bytes:
    for attempt in range(5):
        gate(url).wait()
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA})
            with urllib.request.urlopen(req, context=ssl_ctx, timeout=240) as resp:
                data = resp.read()
            if len(data) < 25_000:
                raise RuntimeError(f"tiny ({len(data)})")
            return data
        except urllib.error.HTTPError as exc:
            if exc.code == 404:
                raise
            if exc.code == 429:
                wait = 60 + attempt * 45
                log(f"    429 — sleep {wait}s then retry")
                time.sleep(wait)
            else:
                time.sleep(5 + attempt * 3)
        except Exception as exc:
            if "tiny" in str(exc).lower():
                raise
            log(f"    retry after error: {exc}")
            time.sleep(5)
    raise RuntimeError(f"download failed: {url}")


def upgrade_one(p: Painting) -> str:
    path = TEX_DIR / p.file
    if not path.exists():
        return f"{p.file}\tMISSING_FILE\t{p.title}\t{p.artist}\t\t"

    w, h = probe(path)
    mx = max(w, h)
    url = source_url(p.file)

    if mx >= 1200:
        tex_ar = w / h if h else 0
        delta = abs(tex_ar - p.cfg_ar) if p.cfg_ar else 0
        if delta > AR_TOL:
            restore_backup(p.file)
            w2, h2 = probe(path)
            return (
                f"{p.file}\tRESTORED_AR_MISMATCH\t{p.title}\t{p.artist}\t"
                f"{w}x{h}->backup {w2}x{h2}\tdelta={delta:.3f}"
            )
        return f"{p.file}\tKEEP_HI\t{p.title}\t{p.artist}\t{w}x{h}\t"

    if not url:
        return (
            f"{p.file}\tMISSING_SOURCE\t{p.title}\t{p.artist}\t{w}x{h}\t"
            "no curated source"
        )

    log(f"GET {p.file} <- {url[:120]}")
    try:
        data = http_get(url)
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
            f"{p.file}\tREJECTED_AR\t{p.title}\t{p.artist}\t"
            f"got {nw}x{nh}\tcfgAR={p.cfg_ar:.3f} texAR={tex_ar:.3f} delta={delta:.3f}"
        )
    return f"{p.file}\tUPGRADED\t{p.title}\t{p.artist}\t{nw}x{nh}\t{url}"


def is_wiki_url(url: str) -> bool:
    return "wikimedia.org" in url or "wikipedia.org" in url


def main() -> int:
    global _log_fh
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    _log_fh = LOG.open("w", encoding="utf-8")

    paintings = parse_configs()

    todo: list[Painting] = []
    for fname, p in sorted(paintings.items()):
        path = TEX_DIR / fname
        if not path.exists():
            continue
        w, h = probe(path)
        if max(w, h) >= 1200:
            continue
        if not source_url(fname):
            log(f"{fname}\tMISSING_SOURCE\t{p.title}\t{p.artist}\t{w}x{h}\t")
            continue
        todo.append(p)

    fast = [p for p in todo if not is_wiki_url(source_url(p.file) or "")]
    wiki = [p for p in todo if is_wiki_url(source_url(p.file) or "")]

    # Short cool-down (not 90s) — sequential + 8s gap is enough politeness.
    cool = 20
    log(
        f"Cooling {cool}s… then {len(fast)} fast + {len(wiki)} Wikimedia "
        f"(sequential, ~{HOST_GAP['upload.wikimedia.org']:.0f}s gap)"
    )
    time.sleep(cool)

    lines = ["file\tstatus\ttitle\tartist\tsize\tnote"]

    for p in fast:
        row = upgrade_one(p)
        log(row)
        lines.append(row)

    for i, p in enumerate(wiki, 1):
        log(f"[{i}/{len(wiki)}] Wikimedia {p.file}")
        row = upgrade_one(p)
        log(row)
        lines.append(row)

    STATUS.write_text("\n".join(lines) + "\n", encoding="utf-8")
    c = Counter(l.split("\t")[1] for l in lines[1:])
    log(f"Summary: {dict(c)}")
    _log_fh.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
