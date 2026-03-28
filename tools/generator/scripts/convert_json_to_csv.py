import csv
import json
import logging
import sys
from pathlib import Path

ROOT = Path(__file__).parent.parent
sys.path.insert(0, str(ROOT))

from generators.constants import (
    DAIRY_KEYWORDS,
    EGG_KEYWORDS,
    GLUTEN_KEYWORDS,
    MEAT_KEYWORDS,
    MENU_BLUEPRINTS,
    THEME_TO_MENU_BLUEPRINT,
)

logging.basicConfig(level=logging.INFO, format="%(message)s")
log = logging.getLogger(__name__)

BLUEPRINTS = ROOT / "blueprints"
DATA = BLUEPRINTS / "data"

CUISINE_DISPLAY_NAMES = {
    "Pizzeria": "Wloska",
    "Kebab": "Turecka",
    "Burgerownia": "Amerykanska",
    "Kuchnia Polska": "Polska",
    "Sushi Bar": "Japonska",
    "Kuchnia Indyjska": "Indyjska",
    "Bar Meksykanski": "Meksykanska",
    "Francuskie Bistro": "Francuska",
    "Kuchnia Wloska": "Wloska",
    "Ramen Bar": "Japonska",
    "Kawiarnia": "Kawiarnia",
    "Steakhouse": "Amerykanska",
    "Kuchnia Azjatycka": "Azjatycka",
    "Kuchnia Wietnamska": "Wietnamska",
    "Grecka Taverna": "Grecka",
    "Korean BBQ": "Koreanska",
    "Bar Tapas": "Hiszpanska",
    "Amerykanski Diner": "Amerykanska",
    "Restauracja z Owocami Morza": "Srodziemnomorska",
    "Piekarnia z Kawiarnia": "Piekarnia",
    "Wedzarnia BBQ": "BBQ",
    "Kuchnia Bliskowschodnia": "Bliskowschodnia",
    "Kuchnia Turecka": "Turecka",
    "Niemiecki Pub": "Niemiecka",
    "Lodziarnia": "Desery",
    "Wykwintna Restauracja": "Fine Dining",
    "Kanapkownia": "Kanapki",
}

CUISINE_ICONS = {
    "Amerykanski Diner": "\U0001f32d",
    "Bar Meksykanski": "\U0001f32e",
    "Bar Tapas": "\U0001f372",
    "Burgerownia": "\U0001f354",
    "Francuskie Bistro": "\U0001f950",
    "Grecka Taverna": "\U0001f957",
    "Kanapkownia": "\U0001f96a",
    "Kawiarnia": "\u2615",
    "Kebab": "\U0001f959",
    "Korean BBQ": "\U0001f969",
    "Kuchnia Azjatycka": "\U0001f961",
    "Kuchnia Bliskowschodnia": "\U0001f9c6",
    "Kuchnia Indyjska": "\U0001f35b",
    "Kuchnia Polska": "\U0001f95f",
    "Kuchnia Turecka": "\U0001f959",
    "Kuchnia Wietnamska": "\U0001f35c",
    "Kuchnia Wloska": "\U0001f35d",
    "Lodziarnia": "\U0001f366",
    "Niemiecki Pub": "\U0001f37a",
    "Piekarnia z Kawiarnia": "\U0001f35e",
    "Ramen Bar": "\U0001f35c",
    "Restauracja z Owocami Morza": "\U0001f99e",
    "Steakhouse": "\U0001f969",
    "Sushi Bar": "\U0001f363",
    "Wykwintna Restauracja": "\U0001f377",
    "Wedzarnia BBQ": "\U0001f525",
}

ARCHETYPE_CUISINE_TAGS = {
    "Pizza": "Wloska",
    "Makaron": "Wloska",
    "Risotto": "Wloska",
    "Burger": "Amerykanska",
    "Stek": "Amerykanska",
    "Dania BBQ": "Amerykanska",
    "Sushi": "Japonska",
    "Ramen": "Japonska",
    "Pho": "Wietnamska",
    "Danie Azjatyckie": "Azjatycka",
    "Danie Koreanskie": "Koreanska",
    "Curry": "Indyjska",
    "Naan": "Indyjska",
    "Taco": "Meksykanska",
    "Mexican": "Meksykanska",
    "Kebab": "Bliskowschodnia",
    "Danie Bliskowschodnie": "Bliskowschodnia",
    "Salatka": "Srodziemnomorska",
    "Ryby i Owoce Morza": "Srodziemnomorska",
    "Danie Greckie": "Grecka",
    "Tapas": "Hiszpanska",
    "Danie Francuskie": "Francuska",
    "Danie Niemieckie": "Niemiecka",
    "Danie Polskie": "Polska",
    "Pierogi": "Polska",
    "Zupa": "Polska",
}


def load_json(name):
    with open(BLUEPRINTS / name, encoding="utf-8") as f:
        return json.load(f)


def write_csv(name, rows, fieldnames):
    path = DATA / name
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fieldnames)
        w.writeheader()
        w.writerows(rows)
    log.info(f"  {name}: {len(rows)} rows")


def classify_ingredient(name, meat_kw, dairy_kw, egg_kw, gluten_kw):
    lower = name.lower()
    is_meat = int(any(kw in lower for kw in meat_kw))
    is_dairy = int(any(kw in lower for kw in dairy_kw))
    is_egg = int(any(kw in lower for kw in egg_kw))
    is_gluten = int(any(kw in lower for kw in gluten_kw))

    if "tofu" in lower:
        is_meat = 0
    if "miod" in lower or "miód" in lower:
        is_egg = 1

    return is_meat, is_dairy, is_egg, is_gluten


def main():
    DATA.mkdir(parents=True, exist_ok=True)

    dishes = load_json("dishes.json")
    restaurant_types = load_json("restaurant_types.json")["RESTAURANT_THEMES"]
    global_config = load_json("global_config.json")
    pixabay_map = load_json("ingredients_pixabay.json")

    dietary_kw = global_config.get("DIETARY_KEYWORDS", {})
    all_meat_kw = list(set(MEAT_KEYWORDS + dietary_kw.get("meat", [])))
    all_dairy_kw = list(set(DAIRY_KEYWORDS + dietary_kw.get("dairy", [])))
    all_egg_kw = list(set(EGG_KEYWORDS + dietary_kw.get("eggs", [])))
    all_gluten_kw = list(set(GLUTEN_KEYWORDS + dietary_kw.get("gluten", [])))

    dish_section_mapping = global_config.get("DISH_SECTION_MAPPING", {})
    tier_probs = global_config.get("RESTAURANT_TIER_PROBABILITIES", {})

    log.info("Generating CSV files...")

    archetype_rows = []
    variant_rows = []
    all_ingredients = set()
    variant_ingredient_links = []
    archetype_id_map = {}
    variant_id_map = {}

    arch_id = 1
    var_id = 1

    for arch_name, arch_data in dishes.items():
        if not isinstance(arch_data, dict):
            continue

        bp = arch_data.get("base_price", {})
        base = arch_data.get("archetype_base", {})
        base_chars = base.get("characteristics", {})
        base_weights = base.get("default_weights", None)

        archetype_rows.append({
            "id": arch_id,
            "name": arch_name,
            "base_price_mean": bp.get("mean", 35.0),
            "base_price_stdev": bp.get("stdev", 5.0),
            "pixabay_term": arch_data.get("pixabay_term", ""),
            "cuisine_tag": ARCHETYPE_CUISINE_TAGS.get(arch_name, ""),
        })
        archetype_id_map[arch_name] = arch_id

        for v_name, v_data in arch_data.get("variants", {}).items():
            if not isinstance(v_data, dict):
                continue

            pm = v_data.get("price_multiplier", {})
            v_chars = v_data.get("characteristics", {})
            v_weights = v_data.get("weights", None)

            merged_chars = {**base_chars, **v_chars}
            resolved_weights = v_weights if v_weights is not None else base_weights

            variant_rows.append({
                "id": var_id,
                "archetype_id": arch_id,
                "name": v_name,
                "price_multiplier_mean": pm.get("mean", 1.0),
                "price_multiplier_stdev": pm.get("stdev", 0.1),
                "pixabay_term": v_data.get("pixabay_term", ""),
                "characteristics": json.dumps(merged_chars, ensure_ascii=False),
                "weights": json.dumps(resolved_weights, ensure_ascii=False) if resolved_weights else "",
            })
            variant_id_map[(arch_name, v_name)] = var_id

            for ing in v_data.get("ingredients", []):
                all_ingredients.add(ing)
                variant_ingredient_links.append((var_id, ing))

            var_id += 1
        arch_id += 1

    write_csv("archetypes.csv", archetype_rows,
              ["id", "name", "base_price_mean", "base_price_stdev", "pixabay_term", "cuisine_tag"])
    write_csv("variants.csv", variant_rows,
              ["id", "archetype_id", "name", "price_multiplier_mean", "price_multiplier_stdev",
               "pixabay_term", "characteristics", "weights"])

    ingredient_id_map = {}
    ingredient_rows = []
    ing_id = 1
    for ing_name in sorted(all_ingredients):
        is_meat, is_dairy, is_egg, is_gluten = classify_ingredient(
            ing_name, all_meat_kw, all_dairy_kw, all_egg_kw, all_gluten_kw
        )
        ingredient_rows.append({
            "id": ing_id,
            "name": ing_name,
            "pixabay_term": pixabay_map.get(ing_name, ""),
            "is_meat": is_meat,
            "is_dairy": is_dairy,
            "is_egg": is_egg,
            "is_gluten": is_gluten,
        })
        ingredient_id_map[ing_name] = ing_id
        ing_id += 1

    write_csv("ingredients.csv", ingredient_rows,
              ["id", "name", "pixabay_term", "is_meat", "is_dairy", "is_egg", "is_gluten"])

    vi_rows = []
    for var_id_val, ing_name in variant_ingredient_links:
        ing_id_val = ingredient_id_map.get(ing_name)
        if ing_id_val:
            vi_rows.append({"variant_id": var_id_val, "ingredient_id": ing_id_val})
    write_csv("variant_ingredients.csv", vi_rows, ["variant_id", "ingredient_id"])

    section_id_map = {}
    section_rows = []
    sec_id = 1
    all_section_names = set()
    for theme_data in restaurant_types.values():
        for s in theme_data.get("menu_config", {}).get("sections", []):
            all_section_names.add(s["name"])
    for sec_name in sorted(all_section_names):
        section_rows.append({"id": sec_id, "name": sec_name})
        section_id_map[sec_name] = sec_id
        sec_id += 1
    write_csv("sections.csv", section_rows, ["id", "name"])

    theme_id_map = {}
    theme_rows = []
    theme_name_part_rows = []
    theme_section_rows = []
    t_id = 1
    tnp_id = 1

    for theme_name, theme_data in restaurant_types.items():
        blueprint_key = THEME_TO_MENU_BLUEPRINT.get(theme_name, "General")
        bp_config = MENU_BLUEPRINTS.get(blueprint_key, MENU_BLUEPRINTS["General"])

        tp = tier_probs.get(theme_name, tier_probs.get("__default__", {}))

        display_name = CUISINE_DISPLAY_NAMES.get(theme_name, theme_name)
        icon = CUISINE_ICONS.get(theme_name, "")

        theme_rows.append({
            "id": t_id,
            "name": theme_name,
            "distribution_chance": theme_data.get("distribution_chance", 0.01),
            "pixabay_term": theme_data.get("pixabay_term", ""),
            "dish_count_mean": bp_config.get("mean", 20),
            "dish_count_sigma": bp_config.get("sigma", 5),
            "budget_prob": tp.get("Budget", 0.2),
            "casual_prob": tp.get("Casual", 0.7),
            "fine_dining_prob": tp.get("Fine Dining", 0.1),
            "display_name": display_name,
            "icon": icon,
        })
        theme_id_map[theme_name] = t_id

        nt = theme_data.get("name_templates", {})
        for part_num, part_key in [(1, "part1"), (2, "part2")]:
            for entry in nt.get(part_key, []):
                theme_name_part_rows.append({
                    "id": tnp_id,
                    "theme_id": t_id,
                    "part": part_num,
                    "name": entry["name"],
                    "chance": entry["chance"],
                })
                tnp_id += 1

        for sec_def in theme_data.get("menu_config", {}).get("sections", []):
            sec_id_val = section_id_map.get(sec_def["name"])
            if sec_id_val:
                lim = sec_def.get("limit", [1, 10])
                theme_section_rows.append({
                    "theme_id": t_id,
                    "section_id": sec_id_val,
                    "chance": sec_def.get("chance", 1.0),
                    "limit_min": lim[0] if isinstance(lim, list) else 1,
                    "limit_max": lim[1] if isinstance(lim, list) and len(lim) > 1 else 10,
                })

        t_id += 1

    write_csv("themes.csv", theme_rows,
              ["id", "name", "distribution_chance", "pixabay_term",
               "dish_count_mean", "dish_count_sigma",
               "budget_prob", "casual_prob", "fine_dining_prob",
               "display_name", "icon"])
    write_csv("theme_name_parts.csv", theme_name_part_rows,
              ["id", "theme_id", "part", "name", "chance"])
    write_csv("theme_sections.csv", theme_section_rows,
              ["theme_id", "section_id", "chance", "limit_min", "limit_max"])

    tas_rows = []
    warnings = []
    for theme_name, t_id_val in theme_id_map.items():
        blueprint_key = THEME_TO_MENU_BLUEPRINT.get(theme_name, "General")
        bp_config = MENU_BLUEPRINTS.get(blueprint_key, MENU_BLUEPRINTS["General"])
        archetypes = bp_config["archetypes"]

        theme_section_names = set()
        for sec_def in restaurant_types[theme_name].get("menu_config", {}).get("sections", []):
            theme_section_names.add(sec_def["name"])

        for arch_name in archetypes:
            arch_id_val = archetype_id_map.get(arch_name)
            if not arch_id_val:
                warnings.append(f"  Archetype '{arch_name}' in blueprint '{blueprint_key}' not found in dishes.json")
                continue

            preferred_sections = dish_section_mapping.get(arch_name, ["Dania Glowne"])
            matched = False

            for pref_sec in preferred_sections:
                for theme_sec in theme_section_names:
                    if pref_sec.lower() in theme_sec.lower():
                        sec_id_val = section_id_map.get(theme_sec)
                        if sec_id_val:
                            tas_rows.append({
                                "theme_id": t_id_val,
                                "archetype_id": arch_id_val,
                                "section_id": sec_id_val,
                            })
                            matched = True

            if not matched:
                warnings.append(
                    f"  No section match: theme='{theme_name}', archetype='{arch_name}', "
                    f"preferred={preferred_sections}, available={sorted(theme_section_names)}"
                )

    seen = set()
    deduped = []
    for row in tas_rows:
        key = (row["theme_id"], row["archetype_id"], row["section_id"])
        if key not in seen:
            seen.add(key)
            deduped.append(row)

    write_csv("theme_archetype_section.csv", deduped,
              ["theme_id", "archetype_id", "section_id"])

    dk_rows = []
    dk_id = 1
    seen_kw = set()
    all_keywords = {
        "meat": all_meat_kw,
        "dairy": all_dairy_kw,
        "eggs": all_egg_kw,
        "gluten": all_gluten_kw,
    }
    for cat, keywords in all_keywords.items():
        for kw in sorted(set(keywords)):
            key = (cat, kw)
            if key not in seen_kw:
                seen_kw.add(key)
                dk_rows.append({"id": dk_id, "category": cat, "keyword": kw})
                dk_id += 1

    write_csv("dietary_keywords.csv", dk_rows, ["id", "category", "keyword"])

    log.info("")
    if warnings:
        log.warning(f"WARNINGS ({len(warnings)}):")
        for w in warnings:
            log.warning(w)
    else:
        log.info("No warnings. All theme-archetype pairs have section matches.")

    log.info(f"\nDone. {len(archetype_rows)} archetypes, {len(variant_rows)} variants, "
             f"{len(ingredient_rows)} ingredients, {len(deduped)} routing rows.")


if __name__ == "__main__":
    main()
