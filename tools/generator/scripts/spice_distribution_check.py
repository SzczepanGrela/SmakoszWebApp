"""Sanity check for spice tag distribution after regen.

Run after a full generator pass to verify thresholds in phase3_dishes:_get_tags_for_dish
produce an acceptable distribution. Acceptance:
    Łagodne        40-60 percent
    Średnio ostre  20-35 percent
    Ostre          10-20 percent
    Bardzo ostre   under 5 percent
"""

import os
import sys
from collections import Counter

import psycopg2

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from config import get_connection_params


SPICE_TAGS = ["Łagodne", "Średnio ostre", "Ostre", "Bardzo ostre"]

ACCEPTANCE = {
    "Łagodne": (40.0, 60.0),
    "Średnio ostre": (20.0, 35.0),
    "Ostre": (10.0, 20.0),
    "Bardzo ostre": (0.0, 5.0),
}


def main() -> int:
    params = get_connection_params()
    conn = psycopg2.connect(**params)
    cur = conn.cursor()

    cur.execute(
        """
        SELECT t.tag_name, COUNT(*)
        FROM tags t
        JOIN dish_tags dt ON dt.tag_id = t.tag_id
        WHERE t.tag_name = ANY(%s)
        GROUP BY t.tag_name
        """,
        (SPICE_TAGS,),
    )
    counts = dict(cur.fetchall())
    total = sum(counts.values())

    if total == 0:
        print("No spice tags found in dish_tags. Did you run the generator?")
        cur.close()
        conn.close()
        return 1

    print("Spice tag distribution:")
    print(f"  Total assignments: {total}")
    print()

    all_pass = True
    for tag in SPICE_TAGS:
        n = counts.get(tag, 0)
        pct = n / total * 100.0 if total else 0.0
        lo, hi = ACCEPTANCE[tag]
        ok = lo <= pct <= hi
        status = "OK" if ok else "FAIL"
        print(f"  {tag:14s} {n:>6d}  {pct:5.1f}%   target {lo:.0f}-{hi:.0f}%   [{status}]")
        if not ok:
            all_pass = False

    cur.close()
    conn.close()

    if not all_pass:
        print()
        print("Distribution outside acceptable range. Adjust thresholds in phase3_dishes.py:_get_tags_for_dish.")
        return 2

    print()
    print("Spice distribution within acceptable ranges.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
