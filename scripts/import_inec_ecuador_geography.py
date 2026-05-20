#!/usr/bin/env python3
"""
Descarga la División Político Administrativa (DPA) de Ecuador desde los
FeatureServer de ArcGIS publicados como información oficial INEC
(vía Ecuador en Cifras / geoportal).

Genera SQL compatible con sri_country (países), geo_provinces,
geo_cantons y geo_parishes del ERP (PostgreSQL).

Uso:
  python import_inec_ecuador_geography.py -o inec_ecuador_geography.sql
  psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f inec_ecuador_geography.sql

  Para envolver en BEGIN/COMMIT (solo si ejecutas el SQL fuera de una
  transacción existente): --wrap-transaction

Requisitos: Python 3.10+ (solo stdlib). Conexión a Internet.

Nota: Si ya existen sucursales (branches) apuntando a códigos de parroquia
que cambien de nombre, no hay problema; si eliminaras filas geo_* a mano,
respeta las FK de branches.
"""

from __future__ import annotations

import argparse
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from typing import Any, Iterable

# Fuentes: metadatos indican origen INEC / Ecuador en Cifras.
BASE = "https://services7.arcgis.com/iFGeGXTAJXnjq0YN/arcgis/rest/services"
LAYERS = {
    "provinces": f"{BASE}/Provincias_del_Ecuador/FeatureServer/0/query",
    "cantons": f"{BASE}/Cantones_del_Ecuador/FeatureServer/0/query",
    "parishes": f"{BASE}/Parroquias_del_Ecuador/FeatureServer/0/query",
}


def sql_literal(s: str) -> str:
    return "'" + (s or "").replace("'", "''") + "'"


def clip(s: str, n: int) -> str:
    t = (s or "").strip()
    return t[:n] if len(t) > n else t


def fetch_layer(
    url: str,
    out_fields: str,
    order_by: str,
    batch: int = 2000,
) -> Iterable[dict[str, Any]]:
    offset = 0
    while True:
        params = {
            "f": "json",
            "where": "1=1",
            "outFields": out_fields,
            "returnGeometry": "false",
            "orderByFields": order_by,
            "resultRecordCount": str(batch),
            "resultOffset": str(offset),
        }
        qs = urllib.parse.urlencode(params)
        full = f"{url}?{qs}"
        req = urllib.request.Request(full, headers={"User-Agent": "ERP-INEC-Import/1.0"})
        try:
            with urllib.request.urlopen(req, timeout=120) as resp:
                raw = resp.read()
        except urllib.error.HTTPError as e:
            raise SystemExit(f"HTTP {e.code} al consultar ArcGIS: {full}") from e
        except urllib.error.URLError as e:
            raise SystemExit(f"Error de red: {e}") from e

        data = json.loads(raw.decode("utf-8"))
        if "error" in data:
            raise SystemExit(f"Error API ArcGIS: {data['error']}")

        feats = data.get("features") or []
        for f in feats:
            attrs = f.get("attributes") or {}
            yield attrs

        if len(feats) < batch:
            break
        offset += batch


def emit_inserts(out, *, wrap_transaction: bool = False) -> None:
    out.write("-- INEC / DPA Ecuador (capas ArcGIS oficiales INEC)\n")
    if wrap_transaction:
        out.write("BEGIN;\n\n")

    out.write(
        "INSERT INTO sri_country (code, iso2, name, phone_code, is_active)\n"
        "VALUES ('ECU', 'EC', 'Ecuador', '+593', TRUE)\n"
        "ON CONFLICT (code) DO UPDATE SET iso2 = EXCLUDED.iso2, name = EXCLUDED.name, "
        "phone_code = EXCLUDED.phone_code, is_active = EXCLUDED.is_active;\n\n"
    )

    provinces: list[tuple[str, str]] = []
    for a in fetch_layer(LAYERS["provinces"], "DPA_PROVIN,DPA_DESPRO", "DPA_PROVIN"):
        pid = clip(str(a.get("DPA_PROVIN") or ""), 10)
        name = clip(str(a.get("DPA_DESPRO") or ""), 120)
        if not pid or not name:
            continue
        provinces.append((pid, name))

    provinces.sort(key=lambda x: x[0])
    for pid, name in provinces:
        out.write(
            f"INSERT INTO geo_provinces (id, country_id, name) VALUES ({sql_literal(pid)}, 'EC', {sql_literal(name)})\n"
            f"ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;\n"
        )
    out.write("\n")

    cantons: list[tuple[str, str, str]] = []
    for a in fetch_layer(LAYERS["cantons"], "DPA_CANTON,DPA_DESCAN,DPA_PROVIN", "DPA_CANTON"):
        cid = clip(str(a.get("DPA_CANTON") or ""), 10)
        name = clip(str(a.get("DPA_DESCAN") or ""), 120)
        pid = clip(str(a.get("DPA_PROVIN") or ""), 10)
        if not cid or not name or not pid:
            continue
        cantons.append((cid, pid, name))

    cantons.sort(key=lambda x: x[0])
    for cid, pid, name in cantons:
        out.write(
            f"INSERT INTO geo_cantons (id, province_id, name) VALUES ({sql_literal(cid)}, {sql_literal(pid)}, {sql_literal(name)})\n"
            f"ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;\n"
        )
    out.write("\n")

    parishes: list[tuple[str, str, str]] = []
    for a in fetch_layer(LAYERS["parishes"], "DPA_PARROQ,DPA_DESPAR,DPA_CANTON", "DPA_PARROQ"):
        rid = clip(str(a.get("DPA_PARROQ") or ""), 10)
        name = clip(str(a.get("DPA_DESPAR") or ""), 120)
        cid = clip(str(a.get("DPA_CANTON") or ""), 10)
        if not rid or not name or not cid:
            continue
        parishes.append((rid, cid, name))

    parishes.sort(key=lambda x: x[0])
    for rid, cid, name in parishes:
        out.write(
            f"INSERT INTO geo_parishes (id, canton_id, name) VALUES ({sql_literal(rid)}, {sql_literal(cid)}, {sql_literal(name)})\n"
            f"ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;\n"
        )

    if wrap_transaction:
        out.write("\nCOMMIT;\n")


def main() -> None:
    p = argparse.ArgumentParser(description="Genera SQL con DPA Ecuador (INEC / ArcGIS).")
    p.add_argument(
        "-o",
        "--output",
        default="-",
        help="Archivo de salida (por defecto: stdout).",
    )
    p.add_argument(
        "--wrap-transaction",
        action="store_true",
        help="Incluir BEGIN/COMMIT (no usar dentro de migraciones EF).",
    )
    args = p.parse_args()

    if args.output == "-":
        emit_inserts(sys.stdout, wrap_transaction=args.wrap_transaction)
    else:
        with open(args.output, "w", encoding="utf-8", newline="\n") as f:
            emit_inserts(f, wrap_transaction=args.wrap_transaction)
        print(f"Escrito: {args.output}", file=sys.stderr)


if __name__ == "__main__":
    main()
