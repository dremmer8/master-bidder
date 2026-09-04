#!/usr/bin/env python3
"""Resolve Commons files in batches via Wikipedia, download CDN thumbs at 3840px."""

from __future__ import annotations

import json
import re
import ssl
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(r"d:\Unity3d\master-bidder\master-bidder")
TEX_DIR = ROOT / r"master-bidder-3d\Assets\content\paintings\tex"
REPORT = ROOT / r"tools\painting_upgrade_report.txt"
MAX_EDGE = 3840
FFMPEG = r"C:\Users\Paul\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.1.2-full_build\bin\ffmpeg.exe"
UA = "MasterBidderPaintingUpgrade/3.0 (Unity educational game; local asset refresh)"
WIKI = "https://en.wikipedia.org/w/api.php"

FORCE = {
    "bonaparte_crossing_the_alps.jpg",
    "bathers_at_asnieres.jpg",
    "cornelia_mother_of_the_gracchi.jpg",
    "five_eldest_children_of_charles_i.jpg",
    "creation_of_adam.jpg",
    "impression_sunrise.jpg",
    "judith_slaying_holofernes.jpg",
    "pinkie_sarah_barrett_moulton.jpg",
}

# Ordered candidates (first existing / largest wins after imageinfo).
CANDIDATES: dict[str, list[str]] = {
    "starry_night.jpg": ["Van_Gogh_-_Starry_Night_-_Google_Art_Project.jpg"],
    "the_night_watch.jpg": ["The_Nightwatch_by_Rembrandt_-_Rijksmuseum.jpg", "Nachtwacht-2.jpg"],
    "school_of_athens.jpg": ["Sanzio_01.jpg", "Raphael_School_of_Athens.jpg"],
    "the_kiss.jpg": ["The_Kiss_-_Gustav_Klimt_-_Google_Cultural_Institute.jpg", "Klimt_The_Kiss.jpg"],
    "primavera.jpg": ["Botticelli-primavera.jpg", "La_Primavera_(Botticelli).jpg"],
    "the_milkmaid.jpg": ["Johannes_Vermeer_-_Het_melkmeisje_-_Google_Art_Project.jpg"],
    "the_hay_wain.jpg": ["John_Constable_The_Hay_Wain.jpg", "Constable_-_The_Hay_Wain.jpg"],
    "the_fighting_temeraire.jpg": [
        "Turner_-_The_Fighting_Temeraire.jpg",
        "The_Fighting_Temeraire,_JMW_Turner,_National_Gallery.jpg",
    ],
    "sunday_afternoon_la_grande_jatte.jpg": [
        "A_Sunday_on_La_Grande_Jatte,_Georges_Seurat,_1884.jpg",
        "Georges_Seurat_-_A_Sunday_on_La_Grande_Jatte_--_1884_-_Google_Art_Project.jpg",
    ],
    "the_gleaners.jpg": [
        "Jean-François_Millet_-_Gleaners_-_Google_Art_Project_2.jpg",
        "Millet_Gleaners.jpg",
    ],
    "water_lilies.jpg": [
        "Claude_Monet_-_Water_Lilies_-_1906,_Ryerson.jpg",
        "Monet_-_Waterlilies_-_Google_Art_Project.jpg",
        "Claude_Monet_038.jpg",
    ],
    "third_of_may_1808.jpg": [
        "El_Tres_de_Mayo,_by_Francisco_de_Goya,_from_Prado_in_Google_Earth.jpg",
        "Francisco_de_Goya_y_Lucientes_-_Los_fusilamientos_del_3_de_mayo_-_1814.jpg",
    ],
    "wanderer_above_the_sea_of_fog.jpg": [
        "Caspar_David_Friedrich_-_Wanderer_above_the_sea_of_fog.jpg"
    ],
    "saturn_devouring_his_son.jpg": [
        "Francisco_de_Goya,_Saturno_devorando_a_su_hijo_(1819-1823).jpg",
        "Saturno_devorando_a_su_hijo.jpg",
    ],
    "sunflowers.jpg": [
        "Vincent_van_Gogh_-_Sunflowers_-_Vase_with_fifteen_sunflowers.jpg",
        "Vincent_Willem_van_Gogh_127.jpg",
        "Van_Gogh_Vase_with_Fifteen_Sunflowers.jpg",
    ],
    "red_fuji.jpg": [
        "Katsushika_Hokusai,_1830-32,_South_Wind,_Clear_Sky.jpg",
        "Red_Fuji_southern_wind_clear_morning.jpg",
        "Hokusai_Fuji_red.jpg",
    ],
    "venus_and_mars.jpg": ["Sandro_Botticelli_-_Venus_and_Mars_-_Google_Art_Project.jpg"],
    "tribute_money.jpg": [
        "Masaccio,_cappella_brancacci,_san_pietro_paga_il_tributo.jpg",
        "Masaccio,_The_Tribute_Money.jpg",
        "Tribute_Money_Brancacci_Chapel.jpg",
    ],
    "the_ninth_wave.jpg": [
        "Ivan_Aivazovsky_-_The_Ninth_Wave_-_Google_Art_Project.jpg",
        "Aivazovsky,_Ivan_-_The_Ninth_Wave.jpg",
    ],
    "the_jewish_bride.jpg": [
        "Rembrandt_Harmensz._van_Rijn_-_Het_Joodse_bruidje_-_Google_Art_Project.jpg"
    ],
    "the_last_day_of_pompeii.jpg": [
        "Karl_Briullov_-_The_Last_Day_of_Pompeii_-_Google_Art_Project.jpg",
        "Karl_Brullov_-_The_Last_Day_of_Pompeii_-_Google_Art_Project.jpg",
    ],
    "the_sea_of_ice.jpg": [
        "Caspar_David_Friedrich_-_Das_Eismeer_-_Hamburger_Kunsthalle_-_02.jpg"
    ],
    "the_gulf_stream.jpg": [
        "Winslow_Homer_-_The_Gulf_Stream_-_Metropolitan_Museum_of_Art.jpg",
        "Winslow_Homer_-_The_Gulf_Stream_-_Google_Art_Project.jpg",
        "Homer_Gulf_Stream.jpg",
    ],
    "the_anatomy_lesson_of_dr_nicolaes_tulp.jpg": [
        "Rembrandt_-_The_Anatomy_Lesson_of_Dr_Nicolaes_Tulp.jpg",
        "The_Anatomy_Lesson.jpg",
        "Anatomy_Lesson_(Mauritshuis).jpg",
    ],
    "portrait_of_adele_bloch_bauer_i.jpg": [
        "Gustav_Klimt_-_Portrait_of_Adele_Bloch-Bauer_I.jpg",
        "Gustav_Klimt_046.jpg",
        "Adele_Bloch-Bauer_I.jpg",
    ],
    "portrait_of_wally_neuzil.jpg": [
        "Egon_Schiele_-_Wally.jpg",
        "Egon_Schiele_-_Portrait_of_Wally.jpg",
        "Schiele_-_Wally_Neuzil.jpg",
    ],
    "tahitian_women_on_the_beach.jpg": [
        "Paul_Gauguin_089.jpg",
        "Paul_Gauguin_-_Tahitian_Women_on_the_Beach.jpg",
        "Gauguin_Femmes_de_Tahiti.jpg",
    ],
    "the_basket_of_apples.jpg": [
        "Paul_Cézanne,_The_Basket_of_Apples.jpg",
        "Paul_Cézanne_109.jpg",
        "Cezanne_-_The_Basket_of_Apples.jpg",
    ],
    "supper_at_emmaus_caravaggio.jpg": [
        "1602-3_Caravaggio,_Supper_at_Emmaus_National_Gallery,_London.jpg",
        "Caravaggio_-_Cena_in_Emmaus.jpg",
        "The_Supper_at_Emmaus_Caravaggio.jpg",
    ],
    "the_gallant_conversation.jpg": [
        "Gerard_ter_Borch_(II)_-_Gallant_Conversation_-_Google_Art_Project.jpg",
        "Gerard_ter_Borch_-_The_Gallant_Conversation.jpg",
        "Gerard_ter_Borch_the_Younger_-_The_Gallant_Conversation.jpg",
    ],
    "regentesses_of_the_old_mens_almshouse.jpg": [
        "Frans_Hals_-_Regentessen_van_het_Oudemannenhuis_-_Google_Art_Project.jpg",
        "Frans_Hals_-_Regentesses_of_the_Old_Men's_Alms_House.jpg",
    ],
    "the_windmill_at_wijk_bij_duurstede.jpg": [
        "Jacob_Isaacksz._van_Ruisdael_-_De_molen_bij_Wijk_bij_Duurstede_-_Google_Art_Project.jpg",
        "Jacob_van_Ruisdael_-_The_Windmill_at_Wijk_bij_Duurstede.jpg",
    ],
    "saint_serapion.jpg": [
        "Francisco_de_Zurbarán_063.jpg",
        "Francisco_de_Zurbarán_-_Saint_Serapion.jpg",
        "Zurbaran_Saint_Serapion.jpg",
    ],
    "the_ballet_class.jpg": [
        "Edgar_Degas_-_The_Dance_Class_-_Google_Art_Project.jpg",
        "Edgar_Degas_-_The_Ballet_Class_-_Google_Art_Project.jpg",
        "Degas_The_Ballet_Class.jpg",
    ],
    "portrait_of_innocent_x.jpg": [
        "Portrait_of_Innocent_X.jpg",
        "Diego_Velázquez_-_Portrait_of_Innocent_X_-_WGA24431.jpg",
        "Velazquez_-_Innocent_X.jpg",
    ],
    "the_feast_of_saint_nicholas.jpg": [
        "Jan_Havicksz._Steen_-_Het_Sint_Nicolaasfeest_-_Google_Art_Project.jpg",
        "Jan_Steen_-_The_Feast_of_Saint_Nicholas.jpg",
    ],
    "still_life_with_a_nautilus_cup.jpg": [
        "Still_Life_with_Chinese_Bowl_and_Nautilus_1662_Willem_Kalf.jpg",
        "Willem_Kalf_-_Still-Life_with_a_Nautilus_Cup.jpg",
    ],
    "the_swing.jpg": [
        "Jean-Honoré_Fragonard_-_The_Swing.jpg",
        "Fragonard,_The_Swing.jpg",
        "The_Swing_Fragonard.jpg",
    ],
    "the_courtyard_of_a_house_in_delft.jpg": [
        "Pieter_de_Hooch_-_The_Courtyard_of_a_House_in_Delft.jpg",
        "Pieter_de_Hooch_-_Courtyard_of_a_House_in_Delft_-_WGA11685.jpg",
    ],
    "rembrandt_self_portrait.jpg": [
        "Rembrandt_-_Self-Portrait_with_Two_Circles_-_Kenwood_House.jpg",
        "Rembrandt_Self-portrait_(Kenwood).jpg",
        "Rembrandt_van_Rijn_-_Self-Portrait_-_Google_Art_Project.jpg",
    ],
    "judith_slaying_holofernes.jpg": [
        "Artemisia_Gentileschi_-_Judith_Beheading_Holofernes_-_Naples.jpg",
        "Artemisia_Gentileschi_-_Judith_Beheading_Holofernes_-_WGA8563.jpg",
        "Judith_Slaying_Holofernes_Artemisia_Gentileschi.jpg",
    ],
    "the_laughing_cavalier.jpg": [
        "Frans_Hals_-_De_lachende_cavalier.jpg",
        "The_Laughing_Cavalier.jpg",
        "Frans_Hals_-_The_Laughing_Cavalier.jpg",
    ],
    "the_cradle.jpg": [
        "Berthe_Morisot_-_Le_Berceau.jpg",
        "Berthe_Morisot_-_The_Cradle_-_Google_Art_Project.jpg",
    ],
    "the_proposition.jpg": [
        "Judith_Leyster_-_Man_offering_money_to_a_young_woman_(The_Proposition)_-_Google_Art_Project.jpg",
        "Judith_Leyster_The_Proposition.jpg",
    ],
    "the_death_of_marat.jpg": [
        "Jacques-Louis_David_-_Marat_assassinated_-_Google_Art_Project_2.jpg",
        "Death_of_Marat_by_David.jpg",
    ],
    "the_sin.jpg": ["Franz_von_Stuck_001.jpg", "Franz_von_Stuck_-_Die_Sünde_1893.jpg"],
    "self_portrait_allegory_of_painting.jpg": [
        "Self-portrait_as_the_Allegory_of_Painting_(La_Pittura)_-_Artemisia_Gentileschi.jpg"
    ],
    "rubens_isabella_brant_honeysuckle_bower.jpg": [
        "Peter_Paul_Rubens_018.jpg",
        "Rubens_and_Isabella_Brant.jpg",
    ],
    "sistine_madonna.jpg": [
        "Raphael_-_The_Sistine_Madonna_-_Google_Art_Project.jpg",
        "Raphael_Sistine_Madonna.jpg",
        "Sixtinische_Madonna.jpg",
    ],
    "portrait_of_a_man_in_a_red_turban.jpg": [
        "Jan_van_Eyck_-_Portrait_of_a_Man_in_a_Red_Turban_(Self-Portrait?)_-_National_Gallery_London.jpg",
        "Portrait_of_a_Man_in_a_Turban.jpg",
        "Van_Eyck_-_Portrait_of_a_Man_in_a_Red_Turban.jpg",
    ],
    "portrait_of_erasmus.jpg": [
        "Holbein-erasmus.jpg",
        "Hans_Holbein_d._J._-_Erasmus_-_Louvre.jpg",
        "Holbein_Erasmus_Louvre.jpg",
    ],
    "three_beauties_of_the_present_day.jpg": [
        "Utamaro_Three_Beauties.jpg",
        "Kitagawa_Utamaro_-_Three_Beauties_of_the_Present_Day.jpg",
        "Toji_san_bijin.jpg",
    ],
    "pinkie_sarah_barrett_moulton.jpg": [
        "Thomas_Lawrence_Pinkie.jpg",
        "Pinkie_by_Thomas_Lawrence.jpg",
        "Thomas_Lawrence_-_Pinkie.jpg",
    ],
    "sudden_shower_over_shin_ohashi_bridge.jpg": [
        "Hiroshige_Atake_sous_une_averse_soudaine.jpg",
        "Hiroshige,_Sudden_shower_over_Shin-Ōhashi_bridge_and_Atake,_1857.jpg",
    ],
    "the_goldfinch.jpg": [
        "Carel_Fabritius_-_The_Goldfinch_-_WGA07833.jpg",
        "Carel_Fabritius_-_The_Goldfinch.jpg",
        "Het_puttertje.jpg",
    ],
    "bonaparte_crossing_the_alps.jpg": [
        "Jacques-Louis_David_-_Bonaparte_franchissant_le_Grand_Saint-Bernard,_20_mai_1800_-_Google_Art_Project.jpg",
        "David_-_Napoleon_crossing_the_Alps_-_Malmaison2.jpg",
        "Napoleon_Crossing_the_Alps.jpg",
    ],
    "bathers_at_asnieres.jpg": [
        "Georges_Seurat_-_Bathers_at_Asnières_-_Google_Art_Project.jpg",
        "Baigneurs_a_Asnieres.jpg",
        "Seurat_Bathers_at_Asnieres.jpg",
    ],
    "cornelia_mother_of_the_gracchi.jpg": [
        "Angelica_Kauffmann_-_Cornelia,_Mother_of_the_Gracchi.jpg",
        "Angelica_Kauffman_-_Cornelia_Pointing_to_her_Children_as_her_Treasures.jpg",
        "Kauffmann_Cornelia.jpg",
        "Cornelia_Mother_of_the_Gracchi_Kauffman.jpg",
    ],
    "five_eldest_children_of_charles_i.jpg": [
        "Anthony_van_Dyck_(1599-1641)_-_The_Five_Eldest_Children_of_Charles_I_-_267_-_Royal_Collection.jpg",
        "The_Five_Eldest_Children_of_Charles_I_-_Van_Dyck_&_Studio_1637.jpg",
        "The_children_of_Charles_I_of_England-painting_by_Sir_Anthony_van_Dyck_in_1637.jpg",
    ],
    "creation_of_adam.jpg": [
        "Michelangelo_-_Creation_of_Adam_(cropped).jpg",
        "Creación_de_Adán.jpg",
        "'Adam's_Creation_Sistine_Chapel_ceiling'_by_Michelangelo_JBU33cut.jpg",
    ],
    "impression_sunrise.jpg": [
        "Monet_-_Impression,_Sunrise.jpg",
        "Claude_Monet,_Impression,_soleil_levant.jpg",
        "Impression,_soleil_levant.jpg",
    ],
}

ssl_ctx = ssl.create_default_context()


def http_get(url: str, retries: int = 5) -> bytes:
    last: Exception | None = None
    for i in range(retries):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA})
            with urllib.request.urlopen(req, context=ssl_ctx, timeout=180) as resp:
                return resp.read()
        except urllib.error.HTTPError as exc:
            last = exc
            if exc.code == 404:
                raise
            wait = 15 * (1.8**i)
            print(f"    HTTP {exc.code}, wait {wait:.0f}s", flush=True)
            time.sleep(wait)
        except Exception as exc:  # noqa: BLE001
            last = exc
            wait = 12 * (1.5**i)
            print(f"    {type(exc).__name__}: {exc}; wait {wait:.0f}s", flush=True)
            time.sleep(wait)
    assert last is not None
    raise last


def wiki_api(params: dict) -> dict:
    url = WIKI + "?" + urllib.parse.urlencode({**params, "format": "json", "formatversion": "2"})
    return json.loads(http_get(url).decode("utf-8"))


def batched_imageinfo(filenames: list[str]) -> dict[str, dict]:
    """filename -> {url, width, height, orig_w, orig_h, mime}"""
    found: dict[str, dict] = {}
    chunk = 40
    for i in range(0, len(filenames), chunk):
        part = filenames[i : i + chunk]
        titles = "|".join("File:" + n for n in part)
        data = wiki_api(
            {
                "action": "query",
                "titles": titles,
                "prop": "imageinfo",
                "iiprop": "url|size|mime",
                "iiurlwidth": str(MAX_EDGE),
            }
        )
        for page in data.get("query", {}).get("pages", []):
            title = page.get("title", "")
            name = title.removeprefix("File:").replace(" ", "_")
            if "missing" in page or "imageinfo" not in page:
                continue
            info = page["imageinfo"][0]
            found[name] = {
                "url": info.get("thumburl") or info.get("url"),
                "width": int(info.get("thumbwidth") or info.get("width") or 0),
                "height": int(info.get("thumbheight") or info.get("height") or 0),
                "orig_w": int(info.get("width") or 0),
                "orig_h": int(info.get("height") or 0),
                "mime": info.get("mime") or "",
                "title": title,
            }
        time.sleep(2.0)
    return found


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
    tmp = dest.with_suffix(".enc.jpg")
    subprocess.run(
        [FFMPEG, "-y", "-i", str(src), "-vf", vf, "-q:v", "2", str(tmp)],
        check=True,
        capture_output=True,
    )
    tmp.replace(dest)
    return probe(dest)


def pick_source(tex: str, info_map: dict[str, dict]) -> tuple[str, dict] | None:
    best = None
    best_area = -1
    for cand in CANDIDATES.get(tex, []):
        info = info_map.get(cand)
        if not info or not info.get("url"):
            # MediaWiki may normalize underscores/spaces; try space version
            info = info_map.get(cand.replace("_", " "))
        if not info:
            continue
        area = info["orig_w"] * info["orig_h"]
        if area > best_area:
            best_area = area
            best = (cand, info)
    return best


def main() -> int:
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

    files = sorted(TEX_DIR.glob("*.jpg"))
    todo: list[Path] = []
    for path in files:
        w, h = probe(path)
        if path.name in FORCE or max(w, h) < 1200:
            todo.append(path)

    print(f"{len(todo)} textures to upgrade (of {len(files)})", flush=True)

    all_names: list[str] = []
    seen: set[str] = set()
    for path in todo:
        for n in CANDIDATES.get(path.name, []):
            if n not in seen:
                seen.add(n)
                all_names.append(n)

    print(f"Resolving {len(all_names)} Commons files via Wikipedia (batched)...", flush=True)
    print("Cooling 75s...", flush=True)
    time.sleep(75)
    info_map = batched_imageinfo(all_names)
    print(f"Resolved {len(info_map)} files", flush=True)

    ok = fail = 0
    lines: list[str] = []
    for i, path in enumerate(todo, 1):
        name = path.name
        picked = pick_source(name, info_map)
        print(f"[{i}/{len(todo)}] {name} ...", flush=True)
        if not picked:
            msg = f"FAIL\t{name}\tno candidate resolved"
            print("  " + msg, flush=True)
            lines.append(msg)
            fail += 1
            continue
        cand, info = picked
        try:
            raw = path.with_suffix(".raw.bin")
            raw.write_bytes(http_get(info["url"]))
            if raw.stat().st_size < 20_000:
                raw.unlink(missing_ok=True)
                raise RuntimeError("download too small")
            w, h = to_jpeg_max4k(raw, path)
            raw.unlink(missing_ok=True)
            msg = f"OK\t{name}\t{w}x{h}\tsrc={info['orig_w']}x{info['orig_h']}\t{cand}"
            print("  " + msg, flush=True)
            lines.append(msg)
            ok += 1
        except Exception as exc:  # noqa: BLE001
            msg = f"FAIL\t{name}\t{type(exc).__name__}: {exc}"
            print("  " + msg, flush=True)
            lines.append(msg)
            fail += 1
        time.sleep(1.2)

    summary = f"\nDone: {ok} ok, {fail} failed, {len(todo)} attempted\n"
    print(summary, flush=True)
    REPORT.write_text("\n".join(lines) + summary, encoding="utf-8")
    return 0 if fail == 0 else 2


if __name__ == "__main__":
    raise SystemExit(main())
