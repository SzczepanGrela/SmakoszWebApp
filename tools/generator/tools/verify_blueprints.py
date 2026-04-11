import logging
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from utils.blueprint_db import BlueprintDB

logging.basicConfig(level=logging.INFO, format="%(message)s")
logger = logging.getLogger("BlueprintVerifier")


def verify_blueprints():
    logger.info("Starting Blueprint Verification (SQLite)...\n")

    bdb = BlueprintDB()

    logger.info("--- 1. Theme Analysis ---")
    themes = bdb.get_themes()
    logger.info(f"Loaded {len(themes)} themes.")

    for theme in themes:
        name = theme["name"]
        sections = bdb.get_theme_sections(name)
        archetypes = bdb.get_theme_archetypes(name)

        if not sections:
            logger.error(f"ERROR: {name}: No sections defined!")
        if not archetypes:
            logger.error(f"ERROR: {name}: No archetypes mapped!")

        if not theme["display_name"]:
            logger.warning(f"WARNING: {name}: Missing display_name")
        if not theme["icon"]:
            logger.warning(f"WARNING: {name}: Missing icon")

    logger.info("\n--- 2. Archetype Reachability ---")
    all_archetype_names = set(bdb.get_archetype_names())
    reachable = set()
    for theme in themes:
        for a in bdb.get_theme_archetypes(theme["name"]):
            reachable.add(a)

    orphans = all_archetype_names - reachable
    if orphans:
        logger.error(f"ERROR: {len(orphans)} orphaned archetypes (not reachable by any theme):")
        for o in sorted(orphans):
            logger.error(f"   - {o}")
    else:
        logger.info("OK: All archetypes are reachable by at least one theme.")

    logger.info("\n--- 3. Variant Integrity ---")
    variants = bdb.get_all_variants_with_details()
    logger.info(f"Loaded {len(variants)} variants across {len(all_archetype_names)} archetypes.")

    missing_chars = []
    for v in variants:
        if not v["characteristics"]:
            missing_chars.append(f"{v['archetype_name']}/{v['name']}")

        ings = bdb.get_variant_ingredients(v["id"])
        if not ings:
            logger.warning(f"WARNING: {v['archetype_name']}/{v['name']}: No ingredients")

    if missing_chars:
        logger.error(f"ERROR: {len(missing_chars)} variants with empty characteristics:")
        for m in missing_chars[:10]:
            logger.error(f"   - {m}")
    else:
        logger.info("OK: All variants have characteristics vectors.")

    logger.info("\n--- 4. Ingredient Dietary Flags ---")
    ingredients = bdb.get_all_ingredients()
    logger.info(f"Loaded {len(ingredients)} ingredients.")

    meat_count = sum(1 for i in ingredients if i["is_meat"])
    dairy_count = sum(1 for i in ingredients if i["is_dairy"])
    egg_count = sum(1 for i in ingredients if i["is_egg"])
    gluten_count = sum(1 for i in ingredients if i["is_gluten"])
    logger.info(f"  Meat: {meat_count}, Dairy: {dairy_count}, Egg: {egg_count}, Gluten: {gluten_count}")

    logger.info("\n--- 5. Section Routing Completeness ---")
    missing_routes = []
    for theme in themes:
        t_name = theme["name"]
        t_archetypes = bdb.get_theme_archetypes(t_name)
        for arch in t_archetypes:
            sections = bdb.get_sections_for_dish(t_name, arch)
            if not sections:
                missing_routes.append(f"{t_name} -> {arch}")

    if missing_routes:
        logger.error(f"ERROR: {len(missing_routes)} theme-archetype pairs with no section route:")
        for m in missing_routes[:10]:
            logger.error(f"   - {m}")
    else:
        logger.info("OK: All theme-archetype pairs have section routes.")

    bdb.close()
    logger.info("\n--- Verification Complete ---")


if __name__ == "__main__":
    verify_blueprints()
