#!/usr/bin/env python3
"""Find exact open-access museum CDN URLs for LOW textures (no Wikimedia)."""
from __future__ import annotations

import json
import re
import ssl
import time
import urllib.parse
import urllib.request
from pathlib import Path

OUT = Path(r"d:\Unity3d\master-bidder\master-bidder\tools\museum_cdn_hits.tsv")  # regenerated on each run
UA = "MasterBidderSourceFinder/2 (Unity game asset upgrade; exact-title match)"
ctx = ssl.create_default_context()

TARGETS = [
    ("the_basket_of_apples.jpg", "basket of apples", "c[eé]zanne|cezanne", ["artic"]),
    ("water_lilies.jpg", "water lil", "monet", ["artic"]),
    ("the_ballet_class.jpg", "dance class|ballet class", "degas", ["artic", "met"]),
    ("three_beauties_of_the_present_day.jpg", "three beauties|toji san bijin", "utamaro", ["met"]),
    ("the_proposition.jpg", "proposition|man offering money", "leyster", ["met", "nga", "cleveland"]),
    ("the_goldfinch.jpg", "goldfinch|puttertje", "fabritius", ["met", "nga"]),
    ("the_night_watch.jpg", "night watch|nachtwacht", "rembrandt", ["rijks"]),
    ("the_jewish_bride.jpg", "jewish bride|joodse bruid|isaac and rebecca", "rembrandt", ["rijks"]),
    ("the_windmill_at_wijk_bij_duurstede.jpg", "wijk bij duurstede|windmill at wijk", "ruisdael|ruysdael", ["rijks"]),
    ("the_feast_of_saint_nicholas.jpg", "saint nicholas|sint nicolaas|feast of st", "steen", ["rijks"]),
    ("the_gallant_conversation.jpg", "gallant conversation|paternal admonition|parental admonition", "ter borch|borch", ["rijks"]),
    ("regentesses_of_the_old_mens_almshouse.jpg", "regentesses|oudemannenhuis", "hals", ["rijks"]),
    ("the_hay_wain.jpg", "hay wain", "constable", ["met", "cleveland"]),
    ("venus_and_mars.jpg", "venus and mars", "botticelli", ["met", "cleveland"]),
    ("supper_at_emmaus_caravaggio.jpg", "supper at emmaus", "caravaggio", ["met", "cleveland"]),
    ("the_swing.jpg", "the swing|les hasards", "fragonard", ["cleveland", "met", "nga"]),
    ("tahitian_women_on_the_beach.jpg", "tahitian women|femmes de tahiti|on the beach", "gauguin", ["met", "artic", "cleveland"]),
    ("rubens_isabella_brant_honeysuckle_bower.jpg", "isabella brant|honeysuckle", "rubens", ["met", "cleveland", "nga"]),
    ("portrait_of_innocent_x.jpg", "innocent x|innocenzo", "vel[aá]zquez|velazquez", ["met", "cleveland"]),
    ("sistine_madonna.jpg", "sistine madonna|sixtinische", "raphael|raffaello", ["met", "cleveland"]),
    ("the_cradle.jpg", "cradle|berceau", "morisot", ["met", "artic", "cleveland"]),
    ("the_laughing_cavalier.jpg", "laughing cavalier", "hals", ["met", "cleveland"]),
    ("the_sin.jpg", "die s[uü]nde|the sin|^sin$", "stuck", ["met", "artic", "cleveland"]),
    ("wanderer_above_the_sea_of_fog.jpg", "wanderer|sea of fog|nebelmeer", "friedrich", ["met", "artic", "cleveland"]),
    ("the_sea_of_ice.jpg", "sea of ice|eismeer|wreck of hope", "friedrich", ["met", "cleveland"]),
    ("third_of_may_1808.jpg", "third of may|tres de mayo", "goya", ["met", "artic", "cleveland"]),
    ("tribute_money.jpg", "tribute money", "masaccio", ["met", "cleveland"]),
    ("the_ninth_wave.jpg", "ninth wave", "aivazovsky|ayvazovsky", ["met", "artic", "cleveland"]),
    ("still_life_with_a_nautilus_cup.jpg", "nautilus", "kalf", ["met", "artic", "rijks", "cleveland"]),
    ("sunflowers.jpg", "sunflowers", "gogh", ["met", "artic", "cleveland"]),
    ("portrait_of_wally_neuzil.jpg", "wally", "schiele", ["met", "artic", "cleveland"]),
    ("saint_serapion.jpg", "serapion", "zurbar[aá]n|zurbaran", ["met", "cleveland", "nga"]),
    ("school_of_athens.jpg", "school of athens", "raphael|raffaello", ["met", "cleveland"]),
    ("pinkie_sarah_barrett_moulton.jpg", "pinkie|sarah barrett", "lawrence", ["met", "cleveland"]),
    ("cornelia_mother_of_the_gracchi.jpg", "cornelia", "kauffman|kauffmann", ["met", "cleveland"]),
]

def http_json(url):
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, context=ctx, timeout=90) as resp:
        return json.loads(resp.read().decode("utf-8", errors="replace"))

def ok_match(title, artist, tpat, apat):
    t = (title or "").lower()
    a = (artist or "").lower()
    return bool(re.search(tpat, t, re.I) and re.search(apat, a, re.I))

def artic(q, tpat, apat):
    url = "https://api.artic.edu/api/v1/artworks/search?" + urllib.parse.urlencode({
        "q": q, "limit": 8,
        "fields": "id,title,artist_title,image_id,is_public_domain,date_display",
    })
    data = http_json(url)
    hits = []
    for d in data.get("data", []):
        title = d.get("title") or ""
        artist = d.get("artist_title") or ""
        if not ok_match(title, artist, tpat, apat):
            continue
        iid = d.get("image_id")
        if not iid or not d.get("is_public_domain"):
            continue
        img = f"https://www.artic.edu/iiif/2/{iid}/full/!3840,3840/0/default.jpg"
        hits.append(("ARTIC", title, artist, img, str(d.get("id"))))
    return hits

def met(q, tpat, apat):
    search = http_json("https://collectionapi.metmuseum.org/public/collection/v1/search?" + urllib.parse.urlencode({"q": q, "hasImages": "true"}))
    ids = (search.get("objectIDs") or [])[:15]
    hits = []
    for oid in ids:
        try:
            o = http_json(f"https://collectionapi.metmuseum.org/public/collection/v1/objects/{oid}")
        except Exception:
            continue
        title = o.get("title") or ""
        artist = o.get("artistDisplayName") or ""
        if not ok_match(title, artist, tpat, apat):
            continue
        if not o.get("isPublicDomain"):
            continue
        img = o.get("primaryImage") or ""
        if not img:
            continue
        hits.append(("MET", title, artist, img, str(oid)))
        time.sleep(0.12)
    return hits

def cleveland(q, tpat, apat):
    url = "https://openaccess-api.clevelandart.org/api/artworks/?" + urllib.parse.urlencode({"q": q, "has_image": 1, "limit": 10})
    data = http_json(url)
    hits = []
    for d in data.get("data", []):
        title = d.get("title") or ""
        creators = d.get("creators") or []
        artist = ", ".join(c.get("description", "") for c in creators) if creators else ""
        if not ok_match(title, artist, tpat, apat):
            continue
        imgs = d.get("images") or {}
        img = (imgs.get("print") or {}).get("url") or (imgs.get("web") or {}).get("url") or ""
        if not img:
            continue
        hits.append(("CLEVELAND", title, artist[:100], img, str(d.get("id"))))
    return hits

RIJKS = {
    "the_night_watch.jpg": ("SK-C-5", "The Night Watch", "Rembrandt van Rijn"),
    "the_jewish_bride.jpg": ("SK-C-216", "Isaac and Rebecca, Known as The Jewish Bride", "Rembrandt van Rijn"),
    "the_windmill_at_wijk_bij_duurstede.jpg": ("SK-C-211", "The Windmill at Wijk bij Duurstede", "Jacob van Ruisdael"),
    "the_feast_of_saint_nicholas.jpg": ("SK-A-385", "The Feast of Saint Nicholas", "Jan Steen"),
    "the_gallant_conversation.jpg": ("SK-A-404", "Gallant Conversation (The Paternal Admonition)", "Gerard ter Borch"),
    "regentesses_of_the_old_mens_almshouse.jpg": ("SK-C-205", "Regentesses of the Old Men's Almshouse", "Frans Hals"),
    "still_life_with_a_nautilus_cup.jpg": ("SK-A-199", "Still Life with a Nautilus Cup", "Willem Kalf"),
}

def rijks(fname):
    if fname not in RIJKS:
        return []
    obj, title, artist = RIJKS[fname]
    # IIIF endpoint used by Rijksmuseum (no API key for image fetch)
    url = f"https://iiif.micr.io/{obj}/full/!3840,3840/0/default.jpg"
    return [("RIJKS", title, artist, url, obj)]

def main():
    lines = ["file\tmuseum\ttitle\tartist\turl\tid"]
    for fname, tpat, apat, museums in TARGETS:
        qbits = re.sub(r"[\\|]", " ", tpat).split()
        q = " ".join(qbits[:3]) + " " + re.split(r"[\\|]", apat)[0]
        q = re.sub(r"[\[\]\(\)\^\$]", "", q)
        print(f"\n## {fname} q={q!r}")
        found = []
        for m in museums:
            try:
                if m == "artic":
                    found.extend(artic(q, tpat, apat))
                elif m == "met":
                    found.extend(met(q, tpat, apat))
                elif m == "cleveland":
                    found.extend(cleveland(q, tpat, apat))
                elif m == "rijks":
                    found.extend(rijks(fname))
            except Exception as exc:
                print(f"  {m} ERR {exc}")
            time.sleep(0.2)
        if not found:
            print("  NO HIT")
            lines.append(f"{fname}\tNONE\t\t\t\t")
        else:
            seen = set()
            for museum, title, artist, url, oid in found:
                if url in seen:
                    continue
                seen.add(url)
                print(f"  {museum}: {title} / {artist} -> {url[:100]}")
                lines.append(f"{fname}\t{museum}\t{title}\t{artist}\t{url}\t{oid}")
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"\nWrote {OUT}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
