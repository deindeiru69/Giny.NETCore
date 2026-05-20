#!/usr/bin/env python3
"""
NPC position scraper — Doflex primary, wiki-dofus.eu fallback.

Strategy :
  1. Build an in-memory index of ALL Doflex NPCs (their /encyclopedia/npcs
     listing, ~115 pages of 50). Index keyed by accent-stripped lowercase name.
  2. For each Incarnam NPC, look up the Doflex ID, fetch its detail page,
     parse the FIRST "Positions" entry : sub-area label + [X, Y].
  3. Map (X, Y) -> MapId via maps.json, preferring entries whose SubAreaName
     matches the Doflex sub-area label (case-insensitive substring).
  4. For NPCs not on Doflex, try wiki-dofus.eu (MediaWiki at .../w/{Name})
     and parse the first {{Localisation|x|y}} or [X,Y] pattern.
  5. Fallback : SPAWN_MAP_ID=154010883 with explicit source label.

Hard skip : "Portail vers Astrub" (4398) — it's a teleporter object,
not a dialogue NPC ; output source="skipped-teleporter".

No invention : every coord comes from a third-party page, and the map
lookup must hit a real Incarnam map otherwise it's a fallback.
"""
import html
import json
import re
import time
import unicodedata
from pathlib import Path
import requests

UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36")
HEADERS = {"User-Agent": UA}
RATE_LIMIT_S = 0.5
TIMEOUT = 15
RETRIES = 3

PROJECT_DIR = Path(__file__).resolve().parent / "IncarnamScan"
OUTPUT_DIR = PROJECT_DIR / "output"
NPC_INPUT = OUTPUT_DIR / "npcs.json"
MAPS_INPUT = OUTPUT_DIR / "maps.json"
OUTPUT_FILE = OUTPUT_DIR / "npc-positions-wiki.json"

SPAWN_MAP_ID = 154010883
DEFAULT_CELL = 280
DEFAULT_DIRECTION = 2

SKIP_TELEPORTER_IDS = {4398}  # Portail vers Astrub

DOFLEX_INDEX_URL = "https://doflex.fr/fr/encyclopedia/npcs"
WIKI_DOFUS_EU = "https://www.wiki-dofus.eu"


def normalize(s: str) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", s) if unicodedata.category(c) != "Mn").lower()


def fetch_with_retry(url, params=None):
    for attempt in range(RETRIES):
        try:
            r = requests.get(url, params=params, headers=HEADERS, timeout=TIMEOUT)
            if r.status_code == 200:
                return r
            if r.status_code in (404, 500):
                return r  # don't retry definitive errors
            print(f"    [retry {attempt+1}] HTTP {r.status_code}")
        except Exception as e:
            print(f"    [retry {attempt+1}] exception: {e}")
        time.sleep(1)
    return None


# ---------------- Doflex ----------------

def build_doflex_index():
    """Walk all Doflex NPC listing pages and build { normalized_name: [(id, url, raw_name)] }."""
    index = {}
    page = 1
    while True:
        print(f"  Doflex index page {page}...", end=" ", flush=True)
        r = fetch_with_retry(DOFLEX_INDEX_URL, {"page": page})
        time.sleep(RATE_LIMIT_S)
        if r is None or r.status_code == 500:
            print("(end)")
            break
        if r.status_code != 200:
            print(f"unexpected {r.status_code}, stopping")
            break
        matches = re.findall(
            r'href="(/fr/encyclopedia/npcs/(\d+)-[a-z0-9-]+)"[\s\S]{0,800}?class="card-a-name[^"]*">([^<]+?)</div>',
            r.text)
        if not matches:
            print("(no NPC cards)")
            break
        for url, nid, name_raw in matches:
            name = html.unescape(name_raw).strip()
            key = normalize(name)
            index.setdefault(key, []).append((int(nid), f"https://doflex.fr{url}", name))
        print(f"{len(matches)} entries")
        page += 1
        if page > 200:  # safety
            break
    print(f"  Doflex index built : {sum(len(v) for v in index.values())} NPCs across {len(index)} normalized names")
    return index


# Match: <div class="card-a-name ...">SUBAREA_LABEL [X, Y]</div>... <div class="text-muted ...">SUBAREA_PARENT</div>
DOFLEX_POSITION_RE = re.compile(
    r'class="card-a-name[^"]*">([^<\[]+?)\s*\[(-?\d+),\s*(-?\d+)\]\s*</div>'
    r'[\s\S]{0,500}?class="text-muted[^"]*">([^<]+)</div>',
    re.IGNORECASE)


def fetch_doflex_positions(url):
    """Return list of (subAreaLabel, parentLabel, x, y) parsed from the Positions section. First entry is canonical."""
    r = fetch_with_retry(url)
    time.sleep(RATE_LIMIT_S)
    if r is None or r.status_code != 200:
        return []
    body = r.text
    # Restrict to the Positions section if found.
    m = re.search(r'<h2>\s*Positions\s*</h2>', body, re.IGNORECASE)
    section = body[m.end():m.end() + 50000] if m else body
    positions = []
    for sa, x, y, parent in DOFLEX_POSITION_RE.findall(section):
        positions.append((html.unescape(sa).strip(), html.unescape(parent).strip(), int(x), int(y)))
    return positions


# ---------------- wiki-dofus.eu ----------------

def fetch_wiki_dofus_eu(name):
    """
    Try wiki-dofus.eu MediaWiki API to find a page and parse its coords.
    Returns (url, x, y, subAreaLabel|None) or None.
    """
    api = f"{WIKI_DOFUS_EU}/api.php"
    # opensearch
    r = fetch_with_retry(api, {"action": "opensearch", "search": name, "limit": 5, "format": "json"})
    time.sleep(RATE_LIMIT_S)
    if r is None or r.status_code != 200:
        return None
    try:
        data = r.json()
    except Exception:
        return None
    if len(data) < 4 or not data[3]:
        return None
    norm = normalize(name)
    chosen = None
    for title, url in zip(data[1], data[3]):
        if normalize(title) == norm:
            chosen = (title, url)
            break
    if chosen is None:
        return None
    title, url = chosen
    r2 = fetch_with_retry(api, {"action": "parse", "page": title, "format": "json", "prop": "wikitext"})
    time.sleep(RATE_LIMIT_S)
    if r2 is None or r2.status_code != 200:
        return None
    try:
        wt = r2.json().get("parse", {}).get("wikitext", {}).get("*", "")
    except Exception:
        return None
    # Match {{Localisation|x|y}} or |coords=[x,y] or [x,y] near "Position"
    m = re.search(r"\{\{\s*Localisation\s*\|\s*(-?\d+)\s*\|\s*(-?\d+)\s*\}\}", wt, re.IGNORECASE)
    if not m:
        m = re.search(
            r"\|\s*(?:position|coordonn[eé]es?|coords?)\s*=\s*[\(\[]?\s*(-?\d+)\s*[,;]\s*(-?\d+)\s*[\)\]]?",
            wt, re.IGNORECASE)
    if not m:
        m = re.search(r"[\(\[]\s*(-?\d+)\s*,\s*(-?\d+)\s*[\)\]]", wt)
    if not m:
        return None
    return url, int(m.group(1)), int(m.group(2)), None


# ---------------- Map resolution ----------------

def resolve_mapid(x, y, sub_area_hint, maps_by_coord):
    candidates = maps_by_coord.get((x, y), [])
    if not candidates:
        return None, None
    if sub_area_hint:
        hint = normalize(sub_area_hint)
        for m in candidates:
            sn = normalize(m["SubAreaName"])
            if hint in sn or sn in hint:
                return m["MapId"], m["SubAreaName"]
    # Else prefer Outdoor
    for m in candidates:
        if m["Outdoor"]:
            return m["MapId"], m["SubAreaName"]
    return candidates[0]["MapId"], candidates[0]["SubAreaName"]


def build_maps_by_coord(maps):
    table = {}
    for m in maps:
        table.setdefault((m["PosX"], m["PosY"]), []).append(m)
    return table


# ---------------- Main ----------------

def main():
    npcs = json.loads(NPC_INPUT.read_text(encoding="utf-8"))
    maps = json.loads(MAPS_INPUT.read_text(encoding="utf-8"))
    maps_by_coord = build_maps_by_coord(maps)
    print(f"Loaded {len(npcs)} NPCs, {len(maps)} maps")
    print()
    print("=== Building Doflex index ===")
    doflex_index = build_doflex_index()
    print()
    print("=== Mapping NPCs ===")

    results = []
    counts = {"doflex": 0, "wiki-dofus-eu": 0, "fallback-spawn-map": 0, "skipped-teleporter": 0, "coords-off-map": 0}

    for i, npc in enumerate(npcs, 1):
        name = npc["Name"]
        tid = npc["TemplateId"]
        print(f"[{i:2d}/{len(npcs)}] {tid}: {name!r}")

        entry = {
            "templateId": tid,
            "name": name,
            "mapId": SPAWN_MAP_ID,
            "cellId": DEFAULT_CELL,
            "direction": DEFAULT_DIRECTION,
            "source": "fallback-spawn-map",
            "url": None,
            "coords": None,
            "subArea": None,
        }

        if tid in SKIP_TELEPORTER_IDS:
            entry["source"] = "skipped-teleporter"
            counts["skipped-teleporter"] += 1
            print("  -> skipped (teleporter)")
            results.append(entry)
            continue

        # 1. Doflex
        key = normalize(name)
        candidates = doflex_index.get(key, [])
        doflex_hit = False
        for nid, url, raw_name in candidates:
            positions = fetch_doflex_positions(url)
            if not positions:
                print(f"  -> doflex {raw_name!r} id={nid} : no positions found")
                continue
            # Use first position
            sa_label, parent_label, x, y = positions[0]
            print(f"  -> doflex {raw_name!r} id={nid} : {sa_label} [{x},{y}] ({parent_label})")
            entry["url"] = url
            entry["coords"] = f"{x},{y}"
            entry["subArea"] = sa_label
            map_id, sa_resolved = resolve_mapid(x, y, sa_label, maps_by_coord)
            if map_id is None:
                # Coords don't match any Incarnam map - still mark doflex source but note off-map
                print(f"     ! ({x},{y}) not in Incarnam maps. Keeping coords, fallback mapId.")
                counts["coords-off-map"] += 1
                # Fall through to wiki try? No — Doflex provided data, but it's outside Incarnam.
                # Per user spec: fallback explicitly.
            else:
                entry["mapId"] = map_id
                entry["source"] = "doflex"
                counts["doflex"] += 1
                doflex_hit = True
            break  # first matching Doflex candidate wins

        if doflex_hit:
            results.append(entry)
            continue

        # 2. wiki-dofus.eu
        wiki = fetch_wiki_dofus_eu(name)
        if wiki:
            url, x, y, sa_label = wiki
            print(f"  -> wiki-dofus.eu : [{x},{y}]")
            entry["url"] = url
            entry["coords"] = f"{x},{y}"
            map_id, sa_resolved = resolve_mapid(x, y, None, maps_by_coord)
            if map_id is not None:
                entry["mapId"] = map_id
                entry["subArea"] = sa_resolved
                entry["source"] = "wiki-dofus-eu"
                counts["wiki-dofus-eu"] += 1
                results.append(entry)
                continue
            else:
                print(f"     ! ({x},{y}) not in Incarnam maps.")

        # 3. Fallback
        if entry["source"] == "fallback-spawn-map":
            counts["fallback-spawn-map"] += 1
            print(f"  -> fallback spawn map {SPAWN_MAP_ID}")
        results.append(entry)

    OUTPUT_FILE.write_text(json.dumps(results, indent=2, ensure_ascii=False), encoding="utf-8")

    print()
    print("=== Summary ===")
    print(f"Total NPCs       : {len(npcs)}")
    print(f"Doflex           : {counts['doflex']}")
    print(f"Wiki-Dofus-EU    : {counts['wiki-dofus-eu']}")
    print(f"Skipped (telep.) : {counts['skipped-teleporter']}")
    print(f"Off-map coord    : {counts['coords-off-map']}")
    print(f"Fallback         : {counts['fallback-spawn-map']}")
    resolved = counts["doflex"] + counts["wiki-dofus-eu"]
    print(f"Coverage         : {resolved}/{len(npcs)} = {100*resolved/len(npcs):.1f}%")
    print(f"Output           : {OUTPUT_FILE}")


if __name__ == "__main__":
    main()
